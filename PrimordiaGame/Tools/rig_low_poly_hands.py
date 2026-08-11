"""Rig the third-party low-poly hand against Unity XR Hands sample skeletons.

Run with Blender 5.x from the repository root:

    blender --background --factory-startup --python \
        PrimordiaGame/Tools/rig_low_poly_hands.py -- \
        --source PrimordiaGame/Tools/SourceAssets/hand_Lowpoly.blend \
        --left-reference "PrimordiaGame/Assets/Samples/XR Hands/1.7.3/HandVisualizer/Models/LeftHand.fbx" \
        --right-reference "PrimordiaGame/Assets/Samples/XR Hands/1.7.3/HandVisualizer/Models/RightHand.fbx" \
        --output PrimordiaGame/Assets/ThirdParty/LowPolyHand/Models \
        --working-output PrimordiaGame/Tools/Generated/LowPolyHand

The source contains four transformed copies of one unrigged mesh. This script uses
one copy, fits it to each canonical XR Hands bind pose, transfers skin weights,
copies the canonical joint hierarchy, and exports deterministic left/right FBX
assets. It intentionally keeps the canonical object and bone names so the model
can be wired into XRHandSkeletonDriver prefabs.
"""

from __future__ import annotations

import argparse
import itertools
import json
import math
import os
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector


SOURCE_OBJECT_NAME = "Plane005"
ARMATURE_NAME = "Armature"


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True)
    parser.add_argument("--left-reference", required=True)
    parser.add_argument("--right-reference", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--working-output", required=True)
    parser.add_argument("--preview", action="store_true")
    return parser.parse_args(argv)


def absolute_path(value: str) -> Path:
    return Path(value).expanduser().resolve()


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (
        bpy.data.meshes,
        bpy.data.armatures,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for block in list(collection):
            if block.users == 0:
                collection.remove(block)


def load_source_mesh(source_path: Path):
    with bpy.data.libraries.load(str(source_path), link=False) as (available, loaded):
        if SOURCE_OBJECT_NAME not in available.objects:
            raise RuntimeError(
                f"{SOURCE_OBJECT_NAME!r} was not found in {source_path}. "
                f"Available objects: {available.objects}"
            )
        loaded.objects = [SOURCE_OBJECT_NAME]

    source = loaded.objects[0]
    bpy.context.collection.objects.link(source)
    source.name = "SourceHand"
    source.hide_render = True
    return source


def import_reference(reference_path: Path, mesh_name: str):
    result = bpy.ops.import_scene.fbx(filepath=str(reference_path))
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender could not import {reference_path}")

    armature = next(
        (obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"), None
    )
    mesh = bpy.data.objects.get(mesh_name)
    if armature is None or mesh is None or mesh.type != "MESH":
        raise RuntimeError(
            f"Reference {reference_path} did not contain the expected armature/{mesh_name}"
        )
    return armature, mesh


def world_vertices(obj) -> np.ndarray:
    matrix = np.asarray(obj.matrix_world, dtype=np.float64)
    points = np.asarray([vertex.co[:] for vertex in obj.data.vertices], dtype=np.float64)
    homogeneous = np.column_stack((points, np.ones(len(points))))
    return (homogeneous @ matrix.T)[:, :3]


def nearest_indices(points: np.ndarray, surface: np.ndarray):
    differences = points[:, None, :] - surface[None, :, :]
    squared = np.einsum("ijk,ijk->ij", differences, differences)
    indices = np.argmin(squared, axis=1)
    distances = squared[np.arange(len(points)), indices]
    return indices, distances


def kabsch(source: np.ndarray, target: np.ndarray):
    source_center = source.mean(axis=0)
    target_center = target.mean(axis=0)
    covariance = (source - source_center).T @ (target - target_center)
    u, _, vt = np.linalg.svd(covariance)
    rotation = vt.T @ u.T
    if np.linalg.det(rotation) < 0:
        vt[-1, :] *= -1
        rotation = vt.T @ u.T
    translation = target_center - rotation @ source_center
    return rotation, translation


def principal_axes(points: np.ndarray):
    centered = points - points.mean(axis=0)
    eigenvalues, eigenvectors = np.linalg.eigh(np.cov(centered.T))
    order = np.argsort(eigenvalues)[::-1]
    return eigenvalues[order], eigenvectors[:, order]


def fit_similarity(source: np.ndarray, target: np.ndarray):
    """Fit source to target with PCA hypotheses followed by trimmed rigid ICP."""

    source_center = source.mean(axis=0)
    target_center = target.mean(axis=0)
    source_values, source_axes = principal_axes(source)
    target_values, target_axes = principal_axes(target)
    scale = math.sqrt(target_values.sum() / source_values.sum())

    best = None
    for permutation in itertools.permutations(range(3)):
        for signs in itertools.product((-1.0, 1.0), repeat=3):
            mapping = np.zeros((3, 3), dtype=np.float64)
            for source_axis, target_axis in enumerate(permutation):
                mapping[target_axis, source_axis] = signs[source_axis]
            rotation = target_axes @ mapping @ source_axes.T
            transformed = (source - source_center) @ rotation.T * scale + target_center

            accumulated_rotation = rotation.copy()
            accumulated_translation = target_center - (
                scale * rotation @ source_center
            )

            for _ in range(18):
                indices, distances = nearest_indices(transformed, target)
                cutoff = np.quantile(distances, 0.88)
                keep = distances <= cutoff
                incremental_rotation, incremental_translation = kabsch(
                    transformed[keep], target[indices[keep]]
                )
                transformed = (
                    transformed @ incremental_rotation.T + incremental_translation
                )
                accumulated_translation = (
                    incremental_rotation @ accumulated_translation
                    + incremental_translation
                )
                accumulated_rotation = incremental_rotation @ accumulated_rotation

            _, forward = nearest_indices(transformed, target)
            _, reverse = nearest_indices(target, transformed)
            score = float(0.5 * (forward.mean() + reverse.mean()))
            candidate = {
                "score": score,
                "scale": scale,
                "rotation": accumulated_rotation,
                "translation": accumulated_translation,
                "points": transformed,
                "reflected": bool(np.linalg.det(accumulated_rotation) < 0),
            }
            if best is None or score < best["score"]:
                best = candidate

    if best is None:
        raise RuntimeError("No alignment candidate was generated")
    return best


def create_normalized_armature(reference_armature, handed_prefix: str):
    armature_data = bpy.data.armatures.new(ARMATURE_NAME)
    armature = bpy.data.objects.new(ARMATURE_NAME, armature_data)
    bpy.context.collection.objects.link(armature)
    armature.show_in_front = True
    armature.data.display_type = "OCTAHEDRAL"

    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    created = {}
    reference_world = reference_armature.matrix_world
    reference_rotation = reference_world.to_3x3()
    expected = [
        bone for bone in reference_armature.data.bones if bone.name.startswith(handed_prefix)
    ]
    if len(expected) != 26:
        raise RuntimeError(
            f"Expected 26 {handed_prefix} XR hand bones, found {len(expected)}"
        )

    for reference_bone in expected:
        edit_bone = armature_data.edit_bones.new(reference_bone.name)
        edit_bone.head = reference_world @ reference_bone.head_local
        edit_bone.tail = reference_world @ reference_bone.tail_local
        roll_axis = reference_rotation @ reference_bone.z_axis
        if roll_axis.length_squared > 0:
            edit_bone.align_roll(roll_axis.normalized())
        edit_bone.use_deform = reference_bone.use_deform
        edit_bone.use_connect = False
        created[reference_bone.name] = edit_bone

    for reference_bone in expected:
        if reference_bone.parent and reference_bone.parent.name in created:
            created[reference_bone.name].parent = created[reference_bone.parent.name]

    bpy.ops.object.mode_set(mode="OBJECT")
    armature.select_set(False)
    return armature


def copy_source_mesh(source, fitted_points: np.ndarray, mesh_name: str):
    mesh_data = source.data.copy()
    mesh_data.name = mesh_name
    hand = bpy.data.objects.new(mesh_name, mesh_data)
    bpy.context.collection.objects.link(hand)
    for vertex, coordinate in zip(mesh_data.vertices, fitted_points):
        vertex.co = coordinate

    for polygon in mesh_data.polygons:
        polygon.use_smooth = True
    hand.color = (0.56, 0.25, 0.12, 1.0)
    return hand


def transfer_weights(
    reference_mesh, hand, armature, configure_armature_modifier: bool = True
) -> dict:
    reference_points = world_vertices(reference_mesh)
    hand_points = world_vertices(hand)
    group_names = [group.name for group in reference_mesh.vertex_groups]
    group_index = {group.index: column for column, group in enumerate(reference_mesh.vertex_groups)}

    reference_weights = np.zeros(
        (len(reference_mesh.data.vertices), len(group_names)), dtype=np.float64
    )
    for vertex in reference_mesh.data.vertices:
        for membership in vertex.groups:
            column = group_index.get(membership.group)
            if column is not None:
                reference_weights[vertex.index, column] = membership.weight

    differences = hand_points[:, None, :] - reference_points[None, :, :]
    squared = np.einsum("ijk,ijk->ij", differences, differences)
    nearest = np.argpartition(squared, kth=7, axis=1)[:, :8]
    nearest_squared = np.take_along_axis(squared, nearest, axis=1)
    local_scale = np.maximum(np.median(nearest_squared, axis=1, keepdims=True), 1e-10)
    proximity = np.exp(-nearest_squared / (2.0 * local_scale))
    proximity /= proximity.sum(axis=1, keepdims=True)
    weights = np.einsum("ij,ijk->ik", proximity, reference_weights[nearest])

    # Quest skinning is limited to four influences per vertex. Keep the strongest
    # four now so Blender and Unity deform the same way.
    if weights.shape[1] > 4:
        strongest = np.argpartition(weights, -4, axis=1)[:, -4:]
        mask = np.zeros_like(weights, dtype=bool)
        mask[np.arange(len(weights))[:, None], strongest] = True
        weights = np.where(mask, weights, 0.0)
    totals = weights.sum(axis=1, keepdims=True)
    zero_rows = totals[:, 0] <= 1e-8
    if np.any(zero_rows):
        wrist_column = next(
            index for index, name in enumerate(group_names) if name.endswith("_Wrist")
        )
        weights[zero_rows, wrist_column] = 1.0
        totals = weights.sum(axis=1, keepdims=True)
    weights /= totals

    for column, name in enumerate(group_names):
        group = hand.vertex_groups.new(name=name)
        for vertex_index in np.flatnonzero(weights[:, column] > 1e-5):
            group.add(
                [int(vertex_index)], float(weights[vertex_index, column]), "REPLACE"
            )

    if configure_armature_modifier:
        hand.parent = armature
        hand.matrix_parent_inverse = Matrix.Identity(4)
        modifier = hand.modifiers.new(name="XR Hands Armature", type="ARMATURE")
        modifier.object = armature
        modifier.use_deform_preserve_volume = True

    used_groups = int(np.count_nonzero(weights.max(axis=0) > 1e-5))
    return {
        "vertices": len(hand.data.vertices),
        "groups": len(group_names),
        "used_groups": used_groups,
        "minimum_total_weight": float(weights.sum(axis=1).min()),
        "maximum_influences": int(np.max(np.count_nonzero(weights > 1e-5, axis=1))),
    }


def replace_reference_mesh(
    source, fitted_points: np.ndarray, reference_mesh, reference_armature, mesh_name: str
):
    """Replace mesh data while preserving the canonical FBX transforms/bind space."""

    weight_reference = reference_mesh.copy()
    weight_reference.data = reference_mesh.data.copy()
    weight_reference.name = f"{mesh_name}_WeightReference"
    bpy.context.collection.objects.link(weight_reference)
    weight_reference.hide_render = True

    old_mesh_data = reference_mesh.data
    custom_mesh_data = source.data.copy()
    custom_mesh_data.name = mesh_name
    inverse_world = reference_mesh.matrix_world.inverted()
    for vertex, world_coordinate in zip(custom_mesh_data.vertices, fitted_points):
        vertex.co = inverse_world @ Vector(world_coordinate)
    for polygon in custom_mesh_data.polygons:
        polygon.use_smooth = True

    reference_mesh.data = custom_mesh_data
    reference_mesh.name = mesh_name
    reference_mesh.color = (0.56, 0.25, 0.12, 1.0)
    for group in list(reference_mesh.vertex_groups):
        reference_mesh.vertex_groups.remove(group)

    weight_report = transfer_weights(
        weight_reference,
        reference_mesh,
        reference_armature,
        configure_armature_modifier=False,
    )

    weight_reference_data = weight_reference.data
    bpy.data.objects.remove(weight_reference, do_unlink=True)
    if weight_reference_data.users == 0:
        bpy.data.meshes.remove(weight_reference_data)
    if old_mesh_data.users == 0:
        bpy.data.meshes.remove(old_mesh_data)
    return reference_mesh, weight_report


def remove_reference_rig(reference_armature, reference_mesh) -> None:
    bpy.data.objects.remove(reference_mesh, do_unlink=True)
    bpy.data.objects.remove(reference_armature, do_unlink=True)


def render_preview(hand, armature, output_path: Path, suffix: str) -> None:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 1100
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = True
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "OBJECT"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "BOTH"

    points = world_vertices(hand)
    minimum = points.min(axis=0)
    maximum = points.max(axis=0)
    center = 0.5 * (minimum + maximum)
    extent = maximum - minimum

    camera_data = bpy.data.cameras.new(f"PreviewCamera{suffix}")
    camera = bpy.data.objects.new(f"PreviewCamera{suffix}", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = Vector(center) + Vector((0.0, 0.0, -0.55))
    direction = Vector(center) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(extent[0] * 1.4, extent[1] * 1.2)
    scene.camera = camera

    source = bpy.data.objects.get("SourceHand")
    if source:
        source.hide_render = True
    armature.hide_render = True
    for obj in scene.objects:
        if obj.name in {"Cube", "Camera", "Light"}:
            obj.hide_render = True
    hand.hide_render = False
    scene.render.filepath = str(output_path)
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)


def set_test_curl(armature, axis: str, direction: float = 1.0) -> None:
    angles = {
        "Proximal": 35.0,
        "Intermediate": 55.0,
        "Distal": 40.0,
    }
    for pose_bone in armature.pose.bones:
        pose_bone.rotation_mode = "XYZ"
        pose_bone.rotation_euler = (0.0, 0.0, 0.0)
        if "Thumb" in pose_bone.name:
            continue
        for segment, angle in angles.items():
            if pose_bone.name.endswith(segment):
                setattr(
                    pose_bone.rotation_euler,
                    axis,
                    math.radians(angle) * direction,
                )
                break
    bpy.context.view_layer.update()


def clear_pose(armature) -> None:
    for pose_bone in armature.pose.bones:
        pose_bone.rotation_mode = "QUATERNION"
        pose_bone.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pose_bone.location = (0.0, 0.0, 0.0)
        pose_bone.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()


def save_and_export(hand, armature, mesh_name: str, output_dir: Path, working_dir: Path):
    output_dir.mkdir(parents=True, exist_ok=True)
    working_dir.mkdir(parents=True, exist_ok=True)
    blend_path = working_dir / f"{mesh_name}_Rigged.blend"
    fbx_path = output_dir / f"{mesh_name}_Rigged.fbx"

    source = bpy.data.objects.get("SourceHand")
    if source:
        bpy.data.objects.remove(source, do_unlink=True)

    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), compress=True)

    bpy.ops.object.select_all(action="DESELECT")
    export_objects = [hand, armature]
    export_objects.extend(
        obj for obj in bpy.context.scene.objects if obj.name in {"Cube", "Camera", "Light"}
    )
    for obj in export_objects:
        obj.hide_render = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature

    result = bpy.ops.export_scene.fbx(
        filepath=str(fbx_path),
        use_selection=True,
        object_types={"ARMATURE", "MESH", "CAMERA", "LIGHT"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        use_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=False,
        add_leaf_bones=False,
        use_armature_deform_only=True,
        bake_anim=False,
        path_mode="AUTO",
        embed_textures=False,
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender failed to export {fbx_path}")
    return blend_path, fbx_path


def rig_hand(
    source_path: Path,
    reference_path: Path,
    output_dir: Path,
    working_dir: Path,
    mesh_name: str,
    prefix: str,
    preview: bool,
) -> dict:
    clear_scene()
    source = load_source_mesh(source_path)
    source_points = np.asarray(
        [vertex.co[:] for vertex in source.data.vertices], dtype=np.float64
    )
    reference_armature, reference_mesh = import_reference(reference_path, mesh_name)
    target_points = world_vertices(reference_mesh)
    fit = fit_similarity(source_points, target_points)

    hand, weight_report = replace_reference_mesh(
        source, fit["points"], reference_mesh, reference_armature, mesh_name
    )
    armature = reference_armature

    preview_path = working_dir / f"{mesh_name}_Rigged.png"
    if preview:
        working_dir.mkdir(parents=True, exist_ok=True)
        render_preview(hand, armature, preview_path, mesh_name)
        for axis in ("x", "z"):
            set_test_curl(armature, axis)
            render_preview(
                hand,
                armature,
                working_dir / f"{mesh_name}_Curl{axis.upper()}.png",
                f"{mesh_name}Curl{axis.upper()}",
            )
            clear_pose(armature)

    blend_path, fbx_path = save_and_export(
        hand, armature, mesh_name, output_dir, working_dir
    )
    return {
        "hand": mesh_name,
        "alignment_rms_metres": math.sqrt(fit["score"]),
        "alignment_reflected": fit["reflected"],
        "weights": weight_report,
        "blend": str(blend_path),
        "fbx": str(fbx_path),
        "preview": str(preview_path) if preview else None,
    }


def main() -> None:
    args = parse_args()
    source = absolute_path(args.source)
    left_reference = absolute_path(args.left_reference)
    right_reference = absolute_path(args.right_reference)
    output = absolute_path(args.output)
    working_output = absolute_path(args.working_output)

    for path in (source, left_reference, right_reference):
        if not path.is_file():
            raise FileNotFoundError(path)

    reports = [
        rig_hand(
            source,
            left_reference,
            output,
            working_output,
            "LeftHand",
            "L_",
            args.preview,
        ),
        rig_hand(
            source,
            right_reference,
            output,
            working_output,
            "RightHand",
            "R_",
            args.preview,
        ),
    ]
    print("PRIMORDIA_RIG_REPORT_BEGIN")
    print(json.dumps(reports, indent=2))
    print("PRIMORDIA_RIG_REPORT_END")


if __name__ == "__main__":
    main()
