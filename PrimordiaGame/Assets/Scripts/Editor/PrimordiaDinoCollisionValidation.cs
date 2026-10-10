using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PrimordiaDinoCollisionValidation
{
    [MenuItem("Tools/Primordia/Dinosaurs/Validate Live Hitboxes")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before validating dinosaur prefabs.");

        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Dinos" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
        Require(paths.Length == 12, "Expected the six regular and six alpha dinosaur prefabs.");
        int rayChecks = 0;

        foreach (var path in paths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var health = root.GetComponent<DinoHealth>();
                Require(health != null && health.enabled && health.profile != null,
                    $"{path}: missing enabled dinosaur health/profile.");
                var colliders = root.GetComponentsInChildren<Collider>()
                    .Where(hitbox => hitbox.enabled && !hitbox.isTrigger &&
                        hitbox.GetComponentInParent<DinoHealth>() == health &&
                        (Physics.DefaultRaycastLayers & (1 << hitbox.gameObject.layer)) != 0).ToArray();
                Require(colliders.Length > 0, $"{path}: no solid, ray-visible live hitbox.");

                Physics.SyncTransforms();
                var renderers = root.GetComponentsInChildren<Renderer>();
                Require(renderers.Length > 0, $"{path}: no visible dinosaur mesh.");
                var visible = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) visible.Encapsulate(renderer.bounds);
                var collisionBounds = colliders[0].bounds;
                foreach (var hitbox in colliders.Skip(1)) collisionBounds.Encapsulate(hitbox.bounds);
                collisionBounds.Expand(0.002f);
                Require(collisionBounds.Contains(visible.min) && collisionBounds.Contains(visible.max),
                    $"{path}: live hitboxes do not cover the visible body ({visible}) with {collisionBounds}.");

                float distance = visible.extents.magnitude + 1f;
                foreach (var direction in new[] { Vector3.right, Vector3.left, Vector3.up,
                    Vector3.down, Vector3.forward, Vector3.back })
                {
                    CheckRay(colliders, visible.center, direction, distance, path);
                    rayChecks++;
                }

                // Default capsules at the feet, or a sphere around the torso, miss
                // these visible upper-body/head/tail regions despite centre shots working.
                foreach (float height in new[] { -0.8f, 0.8f })
                foreach (float length in new[] { -0.8f, 0.8f })
                {
                    var point = visible.center + new Vector3(0f,
                        visible.extents.y * height, visible.extents.z * length);
                    CheckRay(colliders, point, Vector3.right, distance, path);
                    rayChecks++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Debug.Log($"Dinosaur live hitboxes passed: {paths.Length} prefabs, {rayChecks} body/head/tail rays.");
    }

    static void CheckRay(Collider[] colliders, Vector3 point, Vector3 direction, float distance, string path)
    {
        var ray = new Ray(point + direction * distance, -direction);
        Require(colliders.Any(hitbox => hitbox.Raycast(ray, out _, distance * 2f)),
            $"{path}: a shot at visible region {point} passes through every live hitbox.");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
