using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DinoSpawnPointConversionTool
{
    private const string MenuPath = "Tools/Dino/Convert Placed Dinos To Spawn Points";

    [MenuItem(MenuPath)]
    private static void ConvertSelection()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("Select one or more placed dino prefab instances before running this tool.");
            return;
        }

        List<GameObject> convertedPoints = new List<GameObject>();
        List<string> skippedObjects = new List<string>();

        foreach (GameObject selectedObject in selectedObjects)
        {
            if (!TryGetSelectedPrefabRoot(selectedObject, out GameObject prefabRoot, out GameObject prefabAsset))
            {
                skippedObjects.Add($"{selectedObject.name}: select the root of a dino prefab instance.");
                continue;
            }

            if (!IsDinoPrefab(prefabAsset))
            {
                skippedObjects.Add($"{prefabRoot.name}: its source prefab is not a dino prefab.");
                continue;
            }

            Transform originalParent = prefabRoot.transform.parent;
            int originalSiblingIndex = prefabRoot.transform.GetSiblingIndex();
            GameObject spawnPointObject = new GameObject($"Spawn Point - {prefabRoot.name}");
            Undo.RegisterCreatedObjectUndo(spawnPointObject, "Create Dino Spawn Point");
            spawnPointObject.transform.SetParent(originalParent, false);
            spawnPointObject.transform.SetSiblingIndex(originalSiblingIndex);
            spawnPointObject.transform.SetPositionAndRotation(prefabRoot.transform.position, prefabRoot.transform.rotation);

            DinoSpawnPoint spawnPoint = spawnPointObject.AddComponent<DinoSpawnPoint>();
            spawnPoint.dinoPrefab = prefabAsset;
            spawnPoint.ApplyPrefabDefaults();

            Undo.DestroyObjectImmediate(prefabRoot);
            convertedPoints.Add(spawnPointObject);
        }

        if (convertedPoints.Count > 0)
        {
            Selection.objects = convertedPoints.ToArray();
            foreach (GameObject spawnPointObject in convertedPoints)
                EditorSceneManager.MarkSceneDirty(spawnPointObject.scene);
        }

        string summary = $"Converted {convertedPoints.Count} placed dino(s) into DinoSpawnPoint object(s).";
        if (skippedObjects.Count > 0)
            summary += $" Skipped {skippedObjects.Count}:\n- {string.Join("\n- ", skippedObjects)}";

        Debug.Log(summary);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateConvertSelection()
    {
        return Selection.gameObjects.Length > 0;
    }

    private static bool TryGetSelectedPrefabRoot(GameObject selectedObject, out GameObject prefabRoot, out GameObject prefabAsset)
    {
        prefabRoot = null;
        prefabAsset = null;

        if (selectedObject == null || !PrefabUtility.IsPartOfPrefabInstance(selectedObject))
            return false;

        prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(selectedObject);
        if (prefabRoot != selectedObject)
            return false;

        prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(prefabRoot);
        return prefabAsset != null;
    }

    private static bool IsDinoPrefab(GameObject prefab)
    {
        return prefab.GetComponent<DinoAI>() != null || prefab.GetComponent<DinoHealth>() != null;
    }
}
