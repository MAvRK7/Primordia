using UnityEngine;
using UnityEditor;

public static class DinoCreator
{
    // Spawns a dino prefab into the current scene at the Scene view's focus point.
    static void Spawn(string prefabName)
    {
        string path = $"Assets/Prefabs/Dinos/{prefabName}.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"Dino prefab not found at {path}. Check the name/folder.");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        // Place it where the Scene view is looking (or origin if no Scene view open)
        if (SceneView.lastActiveSceneView != null)
            instance.transform.position = SceneView.lastActiveSceneView.pivot;
        else
            instance.transform.position = Vector3.zero;

        Undo.RegisterCreatedObjectUndo(instance, $"Create {prefabName}");
        Selection.activeGameObject = instance;
        EditorGUIUtility.PingObject(instance);
    }

    [MenuItem("GameObject/Dino/Velociraptor", false, 10)]
    static void CreateVelociraptor() => Spawn("Velociraptor");

    [MenuItem("GameObject/Dino/Triceratops", false, 10)]
    static void CreateTriceratops() => Spawn("Triceratops");

    // Add these as you make each prefab:
    [MenuItem("GameObject/Dino/Apatosaurus", false, 10)]
    static void CreateApatosaurus() => Spawn("Apatosaurus");

    [MenuItem("GameObject/Dino/Parasaurolophus", false, 10)]
    static void CreateParasaurolophus() => Spawn("Parasaurolophus");

    [MenuItem("GameObject/Dino/Stegosaurus", false, 10)]
    static void CreateStegosaurus() => Spawn("Stegosaurus");

    [MenuItem("GameObject/Dino/TRex", false, 10)]
    static void CreateTRex() => Spawn("TRex");
}