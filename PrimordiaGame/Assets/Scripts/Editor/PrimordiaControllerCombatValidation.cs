using System;
using System.Linq;
using System.Reflection;
using Primordia;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

/// <summary>Checks controller palms and weapon grips using isolated prefab contents.</summary>
public static class PrimordiaControllerCombatValidation
{
    const string PlayerPrefabPath = "Assets/Prefabs/Primordia Player.prefab";
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Primordia/Player/Validate Controller Combat Poses")]
    public static void Validate()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,
            "Controller combat validation must run outside Play Mode.");
        ValidatePlayer();
        var gunPaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Weapons" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Ranged>() != null)
            .ToArray();
        Require(gunPaths.Length == 8, "Expected all eight firearm prefabs in the weapon catalog.");
        ValidateWeaponAttachments(gunPaths);
        Debug.Log("Controller combat validation passed: both controller palm frames and all 17 " +
            "weapon grab poses, including measured handles, muzzle directions and recoil pivots.");
    }

    static void ValidatePlayer()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            foreach (var side in new[] { "Left", "Right" })
            {
                var controller = root.GetComponentsInChildren<Transform>(true)
                    .Single(t => t.name == side + " Controller");
                var aim = controller.parent.Find(side + " Controller Aim Pose");
                var itemPose = controller.parent.Find(side + " Controller Item Pose");
                Require(aim != null && itemPose != null,
                    $"{side} controller needs separate aim and item poses in tracking space.");
                var gripDriver = controller.GetComponent<TrackedPoseDriver>();
                var aimDriver = aim.GetComponent<TrackedPoseDriver>();
                var itemDriver = itemPose.GetComponent<TrackedPoseDriver>();
                Require(gripDriver != null && aimDriver != null && itemDriver != null,
                    $"{side} controller poses need tracked pose drivers.");
                Require(HasBinding(itemDriver.positionInput.action, $"<XRController>{{{side}Hand}}/devicePosition"),
                    $"{side} held items are not positioned at the controller grip.");
                Require(SameBindings(itemDriver.rotationInput.action, aimDriver.rotationInput.action) &&
                    HasBinding(itemDriver.rotationInput.action, $"<XRController>{{{side}Hand}}/pointerRotation"),
                    $"{side} held items do not use the controller aim rotation.");
                Require(itemDriver.updateType == TrackedPoseDriver.UpdateType.UpdateAndBeforeRender,
                    $"{side} item pose does not update before rendering.");
                var palmGrip = itemPose.Find("Palm Grip Anchor");
                Require(palmGrip != null && Vector3.Distance(palmGrip.localPosition,
                    ControllerHandVisual.PalmGripOffset(side == "Left")) < 0.0001f,
                    $"{side} held items need a mirrored anchor in the curled-finger channel.");
                var attachments = controller.GetComponentsInChildren<InteractionAttachController>(true);
                Require(attachments.Length > 0 && attachments.All(a => a.transformToFollow == palmGrip),
                    $"{side} item attachments do not follow the palm-channel anchor.");
                Require(controller.GetComponentsInChildren<CurveInteractionCaster>(true)
                        .All(caster => caster.castOrigin == aim),
                    $"{side} pointing rays do not follow the aim pose.");

                var hand = controller.GetComponentInChildren<ControllerHandVisual>(true);
                Require(hand != null, $"{side} controller has no hand visual.");
                Require(new SerializedObject(hand).FindProperty("m_AimRotationSource").objectReferenceValue == aim,
                    $"{side} hand does not follow the aim rotation.");
                Require(new SerializedObject(hand).FindProperty("m_PalmGripAnchor").objectReferenceValue == palmGrip,
                    $"{side} hand cannot adjust the grip channel for different handle widths.");
                Require(controller.GetComponent<PlayerPunch>().PalmGripOrigin == palmGrip,
                    $"{side} temporary butcher knife does not share the pickup knife's palm channel.");
                Invoke(hand, "Awake");
                Require(hand.enabled, $"{side} hand failed its bone initialization.");
                var renderer = hand.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var prefix = side == "Left" ? "L_" : "R_";
                var palm = renderer.bones.Single(bone => bone.name == prefix + "Palm");
                var index = renderer.bones.Single(bone => bone.name == prefix + "IndexMetacarpal");
                var little = renderer.bones.Single(bone => bone.name == prefix + "LittleMetacarpal");

                foreach (var aimRotation in new[] { Quaternion.identity, Quaternion.Euler(25f, 43f, -31f) })
                {
                    controller.SetPositionAndRotation(new Vector3(side == "Left" ? -0.25f : 0.25f, 1.3f, 0.4f),
                        Quaternion.Euler(-15f, 28f, 9f));
                    aim.SetPositionAndRotation(controller.position + new Vector3(0.02f, 0.01f, 0.05f), aimRotation);
                    Invoke(hand, "AlignHand");
                    var inward = side == "Left" ? aim.right : -aim.right;
                    Require(Vector3.Dot(-palm.up, inward) > 0.999f,
                        $"{side} palm does not face inward relative to the controller aim pose.");
                    Require(Vector3.Dot(palm.forward, aim.forward) > 0.999f,
                        $"{side} palm frame does not point along controller aim.");
                    var thumbSide = side == "Left" ? index.position - little.position : little.position - index.position;
                    thumbSide = Vector3.ProjectOnPlane(thumbSide, aim.forward).normalized;
                    Require(Vector3.Dot(thumbSide, aim.up) > 0.99f,
                        $"{side} hand's thumb side does not face upward.");
                    Require(Vector3.Distance(palm.position, controller.position) < 0.0001f,
                        $"{side} hand palm is offset from the controller grip position.");
                }
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ValidateWeaponAttachments(string[] gunPaths)
    {
        var meleePaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Weapons" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Melee>() != null)
            .Append("Assets/Prefabs/Knife.prefab")
            .ToArray();
        Require(meleePaths.Length == 9, "Expected all nine melee weapon prefabs, including the knife.");
        var player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            foreach (var side in new[] { "Left", "Right" })
            {
                var controller = player.GetComponentsInChildren<Transform>(true)
                    .Single(t => t.name == side + " Controller");
                var itemPose = controller.parent.Find(side + " Controller Item Pose");
                var interactor = controller.GetComponentInChildren<NearFarInteractor>(true);
                Require(interactor != null && interactor.interactionAttachController != null,
                    $"{side} controller has no near-far item attachment.");
                foreach (var path in gunPaths)
                    ValidateWeapon(path, itemPose, interactor, isMelee: false, isLeft: side == "Left");
                foreach (var path in meleePaths)
                    ValidateWeapon(path, itemPose, interactor, isMelee: true, isLeft: side == "Left");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
    }

    static void ValidateWeapon(string path, Transform itemPose, NearFarInteractor interactor, bool isMelee, bool isLeft)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var grab = root.GetComponent<XRGrabInteractable>();
            Require(grab != null && grab.attachTransform != null,
                $"{root.name} needs an authored grip pose.");
            Require(!grab.useDynamicAttach, $"{root.name} must use its authored hand grip.");
            Require(grab.farAttachMode == InteractableFarAttachMode.Near,
                $"{root.name} must pull to the palm when selected with a distant ray.");
            var weaponName = System.IO.Path.GetFileNameWithoutExtension(path);
            var measured = PrimordiaWeaponGripCalibration.Get(weaponName);
            Require(Vector3.Distance(grab.attachTransform.localPosition, measured.HandlePosition) < 0.0001f,
                $"{weaponName} no longer uses its measured handle centre.");
            var handGrip = root.GetComponent<WeaponPalmGrip>();
            Require(handGrip != null && Mathf.Abs(handGrip.FingerCurl - measured.FingerCurl) < 0.0001f &&
                Mathf.Abs(handGrip.RingCurlScale - measured.RingCurlScale) < 0.0001f &&
                Mathf.Abs(handGrip.LittleCurlScale - measured.LittleCurlScale) < 0.0001f &&
                Mathf.Abs(handGrip.ThumbCurlScale - measured.ThumbCurlScale) < 0.0001f &&
                Mathf.Abs(handGrip.HandPitch - measured.HandPitch) < 0.0001f &&
                Vector3.Distance(handGrip.GripFingerAngles, measured.GripFingerAngles) < 0.01f &&
                Mathf.Abs(handGrip.IndexCurlScale - measured.IndexCurlScale) < 0.0001f &&
                Vector2.Distance(handGrip.ThumbOpposition, measured.ThumbOpposition) < 0.01f,
                $"{weaponName} has no matching hand-fit profile.");
            Require(Vector3.Distance(handGrip.GetPalmOffset(isLeft), new Vector3(
                    isLeft ? measured.InwardOffset : -measured.InwardOffset,
                    measured.HeightOffset, measured.ForwardOffset)) < 0.0001f,
                $"{weaponName} palm offset does not match its calibrated hand-fit profile.");
            if (weaponName != "Knife")
                Require(Quaternion.Angle(root.transform.Find("Visual").localRotation, measured.ModelRotation) < 0.01f,
                    $"{weaponName} visible model no longer uses its measured barrel/blade orientation.");
            Require(HandleEnclosedByMesh(root.transform, measured.HandlePosition),
                $"{weaponName} grip lies outside its physical handle cross-section.");
            var palmGrip = itemPose.Find("Palm Grip Anchor");
            palmGrip.localPosition = handGrip.GetPalmOffset(isLeft);
            var attachment = interactor.interactionAttachController;
            interactor.attachTransform = attachment.GetOrCreateAnchorTransform(updateTransform: true);
            // Exercise the package's OnGrab and Process implementation, including
            // the actual near-far anchor, rather than duplicating its pose formula.
            grab.interactorsSelecting.Add(interactor);
            var transformer = root.AddComponent<XRGeneralGrabTransformer>();
            transformer.OnLink(grab);
            foreach (var aimRotation in new[] { Quaternion.identity, Quaternion.Euler(-35f, 64f, 18f) })
            {
                itemPose.SetPositionAndRotation(new Vector3(0.23f, 1.27f, 0.41f), aimRotation);
                var palmPosition = palmGrip.position;
                attachment.ResetOffset();
                root.transform.SetPositionAndRotation(new Vector3(-0.4f, 0.8f, 1.2f),
                    Quaternion.Euler(14f, 170f, -28f));
                var targetPose = new Pose(root.transform.position, root.transform.rotation);
                var localScale = root.transform.localScale;
                transformer.OnGrab(grab);
                transformer.OnGrabCountChanged(grab, targetPose, localScale);
                transformer.Process(grab, XRInteractionUpdateOrder.UpdatePhase.Dynamic, ref targetPose, ref localScale);
                root.transform.SetPositionAndRotation(targetPose.position, targetPose.rotation);
                Require(Vector3.Distance(grab.attachTransform.position, palmPosition) < 0.0001f,
                    $"{root.name} handle does not sit at the hand's palm.");
                Require(Quaternion.Angle(grab.attachTransform.rotation, aimRotation) < 0.01f,
                    $"{root.name} authored grip does not match the shared item pose.");
                if (isMelee)
                {
                    var handUp = aimRotation * Vector3.up;
                    if (path == "Assets/Prefabs/Knife.prefab")
                    {
                        var blade = root.GetComponentsInChildren<MeshFilter>(true)
                            .Single(filter => filter.name.IndexOf("blade", StringComparison.OrdinalIgnoreCase) >= 0);
                        var bladeDirection = blade.transform.TransformVector(Vector3.forward).normalized;
                        Require(Vector3.Dot(bladeDirection, handUp) > 0.999f,
                            "The held knife blade must rise above the hand, perpendicular to controller aim.");
                        var bladeBase = blade.transform.TransformPoint(blade.sharedMesh.bounds.center -
                            Vector3.forward * blade.sharedMesh.bounds.extents.z);
                        Require(Vector3.Dot(bladeBase - palmPosition, handUp) > 0.02f,
                            "The held knife blade begins inside the palm.");
                    }
                    else
                    {
                        Require(Vector3.Dot(root.transform.forward, handUp) > 0.999f,
                            $"{root.name} handle must sit upright across the fist, perpendicular to aim.");
                    }
                }
                else
                {
                    var ranged = root.GetComponent<Ranged>();
                    var muzzle = new SerializedObject(ranged).FindProperty("m_FirePoint").objectReferenceValue as Transform;
                    Require(muzzle != null, $"{root.name} has no muzzle transform.");
                    var forward = aimRotation * Vector3.forward;
                    Require(Vector3.Dot(muzzle.forward, forward) > 0.999f,
                        $"{root.name} fires away from controller aim when attached to the hand.");
                    Require(Vector3.Dot(muzzle.position - palmPosition, forward) > 0.03f,
                        $"{root.name} muzzle sits behind or inside the hand grip.");
                    ValidateRecoil(root.transform, grab.attachTransform, muzzle);
                }
            }
            grab.interactorsSelecting.Clear();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ValidateRecoil(Transform root, Transform grip, Transform muzzle)
    {
        var visual = root.Find("Visual");
        Require(visual != null, $"{root.name} has no recoil visual.");
        var position = visual.localPosition;
        var rotation = visual.localRotation;
        var handleInVisual = visual.InverseTransformPoint(grip.position);
        var muzzleInVisual = visual.InverseTransformPoint(muzzle.position);
        var pivot = visual.parent.InverseTransformPoint(grip.position);
        try
        {
            foreach (var amount in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            {
                var pose = WeaponPalmGrip.RecoilVisualPose(position, rotation, pivot, -8f * amount);
                visual.SetLocalPositionAndRotation(pose.position, pose.rotation);
                Require(Vector3.Distance(visual.TransformPoint(handleInVisual), grip.position) < 0.00001f,
                    $"{root.name} recoil moves the handle out of the palm.");
                var muzzlePose = WeaponPalmGrip.RecoilVisualPose(
                    muzzle.localPosition, muzzle.localRotation, pivot, -8f * amount);
                Require(Vector3.Distance(visual.TransformPoint(muzzleInVisual),
                    muzzle.parent.TransformPoint(muzzlePose.position)) < 0.00001f,
                    $"{root.name} recoil separates the muzzle from its visible barrel.");
            }
        }
        finally { visual.SetLocalPositionAndRotation(position, rotation); }
    }

    static bool HandleEnclosedByMesh(Transform root, Vector3 handle)
    {
        // Independently intersect actual triangles on both sides of the grip.
        // A near-zero bounds centre can sit beside an offset shaft or receiver.
        float negative = float.PositiveInfinity, positive = float.PositiveInfinity;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null) continue;
            var points = filter.sharedMesh.vertices.Select(v => root.InverseTransformPoint(filter.transform.TransformPoint(v))).ToArray();
            var triangles = filter.sharedMesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = points[triangles[i]];
                var ab = points[triangles[i + 1]] - a;
                var ac = points[triangles[i + 2]] - a;
                var cross = Vector3.Cross(Vector3.right, ac);
                var determinant = Vector3.Dot(ab, cross);
                if (Mathf.Abs(determinant) < 0.00000001f) continue;
                var delta = handle - a;
                var u = Vector3.Dot(delta, cross) / determinant;
                var q = Vector3.Cross(delta, ab);
                var v = Vector3.Dot(Vector3.right, q) / determinant;
                if (u < 0f || v < 0f || u + v > 1f) continue;
                var distance = Vector3.Dot(ac, q) / determinant;
                if (distance >= 0f) positive = Mathf.Min(positive, distance);
                else negative = Mathf.Min(negative, -distance);
            }
        }
        return positive > 0.001f && negative > 0.001f && positive < 0.04f && negative < 0.04f;
    }

    static bool HasBinding(InputAction action, string path) => action != null &&
        action.bindings.Any(binding => string.Equals(binding.path, path, StringComparison.OrdinalIgnoreCase));

    static bool SameBindings(InputAction first, InputAction second) => first != null && second != null &&
        first.bindings.Select(binding => binding.path).SequenceEqual(second.bindings.Select(binding => binding.path));

    static object Invoke(object target, string name, params object[] arguments)
    {
        var method = target.GetType().GetMethod(name, PrivateInstance);
        Require(method != null, $"Missing validation hook {target.GetType().Name}.{name}.");
        try { return method.Invoke(target, arguments); }
        catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
