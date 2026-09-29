using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Turns the loot models into drop prefabs, lists them in the loot catalog and points
/// every dinosaur's DinoLoot at that catalog. Re-run after adding or replacing models.
/// </summary>
public static class PrimordiaLootSetup
{
    const string k_ModelFolder = "Assets/Models/Loot";
    const string k_GlowMaterialFolder = "Assets/Models/Loot/Materials";
    const string k_PrefabFolder = "Assets/Prefabs/Loot";
    const string k_CatalogPath = "Assets/Data/Loot Catalog.asset";
    const string k_DinoPrefabFolder = "Assets/Prefabs/Dinos";

    // Drop name as written in the DinoProfiles, source model, and longest side in metres.
    static readonly (string PartName, string Model, float Size)[] k_Loot =
    {
        ("Bone", "Big_bone", 0.4f),
        ("Giant Bone", "Big_bone", 0.9f),
        ("Ancient Bone", "rare_bone", 0.6f),
        ("Hide", "Hide_animal", 0.5f),
        ("Alpha Pelt", "Hide_animal", 0.6f),
        ("Sickle Claw", "claw", 0.3f),
        ("Horn", "Normal_horn", 0.45f),
        ("Flawless Horn", "Flawless_horn", 0.5f),
        ("Crest", "crest", 0.5f),
        ("Pristine Crest", "Pristine_crest", 0.55f),
        ("Back-Plate", "backblate", 0.5f),
        ("Twin Tail-Spike", "twin_spike", 0.6f),
        ("Apex Fang", "apex_fang", 0.3f),
        ("Rex Skull", "skull", 0.7f),
    };

    [MenuItem("Tools/Primordia/Loot/Build Loot Catalog")]
    public static void BuildLootCatalog()
    {
        EnsureFolder(k_PrefabFolder);
        EnsureFolder(Path.GetDirectoryName(k_CatalogPath)?.Replace('\\', '/'));
        foreach (var model in k_Loot.Select(loot => loot.Model).Distinct())
            ApplyMtlEmission($"{k_ModelFolder}/{model}.obj");

        var entries = k_Loot
            .Select(loot => new LootCatalog.Entry
            {
                partName = loot.PartName,
                prefab = BuildDropPrefab(loot.PartName, loot.Model, loot.Size),
            })
            .ToList();

        var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>(k_CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<LootCatalog>();
            AssetDatabase.CreateAsset(catalog, k_CatalogPath);
        }

        // Keep hand-added entries for parts this tool doesn't generate.
        entries.AddRange(catalog.entries.Where(entry => entry != null && !k_Loot.Any(loot =>
            string.Equals(loot.PartName, entry.partName, StringComparison.OrdinalIgnoreCase))));
        catalog.entries = entries.ToArray();
        EditorUtility.SetDirty(catalog);

        var dinoCount = AssignCatalogToDinos(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"Primordia loot catalog is ready: {k_Loot.Length} drop prefabs, used by {dinoCount} dinosaur prefabs.");
    }

    static GameObject BuildDropPrefab(string partName, string modelName, float size)
    {
        var modelPath = $"{k_ModelFolder}/{modelName}.obj";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
            throw new InvalidOperationException($"{partName} model is missing at {modelPath}.");

        var root = new GameObject(partName);
        try
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);

            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException($"{partName} model has no renderers.");

            // Normalise the model's size and centre it so the drop spawns and tumbles predictably.
            var bounds = CalculateLocalBounds(root.transform, renderers);
            var longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.0001f);
            visual.transform.localScale *= size / longest;
            bounds = CalculateLocalBounds(root.transform, renderers);
            visual.transform.localPosition -= bounds.center;

            // DinoLoot adds the Rigidbody, grab and pickup components when the drop spawns.
            var collider = root.AddComponent<BoxCollider>();
            collider.size = Vector3.Max(bounds.size, Vector3.one * 0.05f);

            var path = $"{k_PrefabFolder}/{partName}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (prefab == null)
                throw new InvalidOperationException($"Could not save loot prefab at {path}.");
            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    // Unity's OBJ importer ignores .mtl emission (Ke), which the rare parts use for their glow.
    static void ApplyMtlEmission(string modelPath)
    {
        var mtlPath = Path.ChangeExtension(modelPath, ".mtl");
        if (!File.Exists(mtlPath))
            return;

        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        var imported = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().ToArray();
        var remapped = false;
        string materialName = null;
        foreach (var line in File.ReadAllLines(mtlPath))
        {
            var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0] == "newmtl")
                materialName = parts[1];
            if (parts.Length < 4 || parts[0] != "Ke" || materialName == null)
                continue;

            var emission = new Color(
                float.Parse(parts[1], CultureInfo.InvariantCulture),
                float.Parse(parts[2], CultureInfo.InvariantCulture),
                float.Parse(parts[3], CultureInfo.InvariantCulture));
            if (emission.maxColorComponent <= 0f)
                continue;

            var path = $"{k_GlowMaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var source = imported.FirstOrDefault(candidate => candidate.name == materialName);
                if (source == null)
                    continue;

                EnsureFolder(k_GlowMaterialFolder);
                material = new Material(source) { name = materialName };
                AssetDatabase.CreateAsset(material, path);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), materialName), material);
                remapped = true;
            }

            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
        }

        if (remapped)
            importer.SaveAndReimport();
    }

    static int AssignCatalogToDinos(LootCatalog catalog)
    {
        var count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { k_DinoPrefabFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var loots = root.GetComponentsInChildren<DinoLoot>(true);
                if (loots.Length == 0 || loots.All(loot => loot.catalog == catalog))
                {
                    count += loots.Length > 0 ? 1 : 0;
                    continue;
                }

                foreach (var loot in loots)
                    loot.catalog = catalog;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                count++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        return count;
    }

    static Bounds CalculateLocalBounds(Transform root, IEnumerable<Renderer> renderers)
    {
        var hasPoint = false;
        var localBounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (var renderer in renderers)
        {
            var bounds = renderer.bounds;
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var localPoint = root.InverseTransformPoint(
                    bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                if (!hasPoint)
                {
                    localBounds = new Bounds(localPoint, Vector3.zero);
                    hasPoint = true;
                }
                else
                {
                    localBounds.Encapsulate(localPoint);
                }
            }
        }

        return localBounds;
    }

    static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
            return;

        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
