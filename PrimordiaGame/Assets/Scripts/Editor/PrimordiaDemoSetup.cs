using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Authors the demo's controller punches and missing dinosaur hit colliders.</summary>
public static class PrimordiaDemoSetup
{
    [MenuItem("Tools/Primordia/Set Up Demo Combat")]
    public static void SetUpDemoCombat()
    {
        const string playerPath = "Assets/Prefabs/Primordia Player.prefab";
        var player = PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            var references = AssetDatabase.LoadAllAssetsAtPath(
                "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/XRI Default Input Actions.inputactions")
                .OfType<InputActionReference>().ToArray();
            foreach (var side in new[] { "Left", "Right" })
            {
                var controller = player.GetComponentsInChildren<Transform>(true)
                    .Single(t => t.name == side + " Controller");
                var punch = controller.GetComponent<PlayerPunch>();
                if (punch == null) punch = controller.gameObject.AddComponent<PlayerPunch>();
                var serialized = new SerializedObject(punch);
                Bind("grip", "XRI " + side + " Interaction", "Select Value");
                Bind("trigger", "XRI " + side + " Interaction", "Activate Value");
                Bind("isTracked", "XRI " + side, "Is Tracked");
                serialized.ApplyModifiedPropertiesWithoutUndo();

                void Bind(string field, string map, string action)
                {
                    // XRI imports both current and legacy references to the same action.
                    serialized.FindProperty(field).objectReferenceValue = references.First(r =>
                        r.action != null && r.action.actionMap.name == map && r.action.name == action);
                }
            }
            PrefabUtility.SaveAsPrefabAsset(player, playerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }

        foreach (var species in new[] { "Velociraptor", "Trex", "Apatosaurus", "Stegosaurus" })
        {
            FitCollider($"Assets/Prefabs/Dinos/Regular dinos/{species}.prefab");
            FitCollider($"Assets/Prefabs/Dinos/Alpha dinos/Alpha {species}.prefab");
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Demo combat configured: two controller fists and four species, including alpha variants.");
    }

    static void FitCollider(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // Renderer bounds account for the imported armature's separate scale.
            // Convert all corners to the prefab root so alpha scaling is applied only once.
            Bounds? bounds = null;
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var worldBounds = renderer.bounds;
                for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                for (var z = -1; z <= 1; z += 2)
                {
                    var point = root.transform.InverseTransformPoint(worldBounds.center +
                        Vector3.Scale(worldBounds.extents, new Vector3(x, y, z)));
                    var combined = bounds ?? new Bounds(point, Vector3.zero);
                    combined.Encapsulate(point);
                    bounds = combined;
                }
            }
            if (!bounds.HasValue || bounds.Value.size.sqrMagnitude < 0.01f)
                throw new InvalidOperationException("No visible dinosaur mesh to fit: " + path);

            var collider = root.GetComponent<BoxCollider>();
            if (collider == null) collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.Value.center;
            collider.size = bounds.Value.size;
            collider.isTrigger = false;
            collider.enabled = true;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"Fitted {root.name} hitbox: center {collider.center}, size {collider.size}.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
