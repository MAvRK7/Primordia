using UnityEngine;
using UnityEditor;

public static class DinoCreator
{
    // Spawns a normal dino prefab into the current scene at the Scene view's focus point.
    static void Spawn(string prefabName)
    {
        string path = $"Assets/Prefabs/Dinos/{prefabName}.prefab";
        SpawnFromPath(path, prefabName);
    }

    // Spawns an alpha dino prefab from the "Alpha dinos" subfolder.
    static void SpawnAlpha(string prefabName)
    {
        string path = $"Assets/Prefabs/Dinos/Alpha dinos/{prefabName}.prefab";
        SpawnFromPath(path, prefabName);
    }

    // Shared spawn logic.
    static void SpawnFromPath(string path, string prefabName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"Dino prefab not found at {path}. Check the name/folder.");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        if (SceneView.lastActiveSceneView != null)
            instance.transform.position = SceneView.lastActiveSceneView.pivot;
        else
            instance.transform.position = Vector3.zero;

        Undo.RegisterCreatedObjectUndo(instance, $"Create {prefabName}");
        Selection.activeGameObject = instance;
        EditorGUIUtility.PingObject(instance);
    }

    // ---- Normal dinos ----
    [MenuItem("GameObject/Dino/Velociraptor", false, 10)]
    static void CreateVelociraptor() => Spawn("Velociraptor");

    [MenuItem("GameObject/Dino/Triceratops", false, 10)]
    static void CreateTriceratops() => Spawn("Triceratops");

    [MenuItem("GameObject/Dino/Apatosaurus", false, 10)]
    static void CreateApatosaurus() => Spawn("Apatosaurus");

    [MenuItem("GameObject/Dino/Parasaurolophus", false, 10)]
    static void CreateParasaurolophus() => Spawn("Parasaurolophus");

    [MenuItem("GameObject/Dino/Stegosaurus", false, 10)]
    static void CreateStegosaurus() => Spawn("Stegosaurus");

    [MenuItem("GameObject/Dino/TRex", false, 10)]
    static void CreateTRex() => Spawn("TRex");

    // ---- Alpha dinos ----
    [MenuItem("GameObject/Dino/Alpha/Alpha Velociraptor", false, 10)]
    static void CreateAlphaVelociraptor() => SpawnAlpha("Alpha Velociraptor");

    [MenuItem("GameObject/Dino/Alpha/Alpha Triceratops", false, 10)]
    static void CreateAlphaTriceratops() => SpawnAlpha("Alpha Triceratops");

    [MenuItem("GameObject/Dino/Alpha/Alpha Apatosaurus", false, 10)]
    static void CreateAlphaApatosaurus() => SpawnAlpha("Alpha Apatosaurus");

    [MenuItem("GameObject/Dino/Alpha/Alpha Parasaurolophus", false, 10)]
    static void CreateAlphaParasaurolophus() => SpawnAlpha("Alpha Parasaurolophus");

    [MenuItem("GameObject/Dino/Alpha/Alpha Stegosaurus", false, 10)]
    static void CreateAlphaStegosaurus() => SpawnAlpha("Alpha Stegosaurus");

    [MenuItem("GameObject/Dino/Alpha/Alpha TRex", false, 10)]
    static void CreateAlphaTRex() => SpawnAlpha("Alpha TRex");
}