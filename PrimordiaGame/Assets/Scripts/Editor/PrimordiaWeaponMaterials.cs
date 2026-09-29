using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds URP materials from each weapon model's texture set. Models whose FBX materials import
/// are remapped at the importer, like the Knife; models without usable FBX materials get their
/// materials assigned per renderer on the weapon rig.
/// </summary>
public static class PrimordiaWeaponMaterials
{
    const string k_WeaponPrefabFolder = "Assets/Prefabs/Weapons";
    const string k_Shader = "Universal Render Pipeline/Lit";
    const float k_DefaultSmoothness = 0.35f;

    // One material built from the textures named Prefix* in the model's textures folder.
    // Slots are FBX material names, or renderer names ("*" for all) when the FBX has none.
    // A null prefix makes an untextured material in the fallback colour.
    sealed class MaterialSet
    {
        public string Name { get; }
        public string Prefix { get; }
        public string[] Slots { get; }

        public MaterialSet(string name, string prefix, params string[] slots)
        {
            Name = name;
            Prefix = prefix;
            Slots = slots;
        }
    }

    sealed class ModelMaterials
    {
        public string ModelPath { get; }
        public bool ByRenderer { get; }
        public MaterialSet[] Sets { get; }

        public ModelMaterials(string modelPath, bool byRenderer, params MaterialSet[] sets)
        {
            ModelPath = modelPath;
            ByRenderer = byRenderer;
            Sets = sets;
        }

        public string Root
        {
            get
            {
                var folder = Path.GetDirectoryName(ModelPath)?.Replace('\\', '/');
                return Path.GetFileName(folder) == "source" ? Path.GetDirectoryName(folder)?.Replace('\\', '/') : folder;
            }
        }
    }

    static readonly ModelMaterials[] k_Models =
    {
        Remap("Assets/Models/guns/m16-assault-rifle/source/m16a3.fbx",
            new MaterialSet("M16A3", "m16_M16A3_", "M16A3")),
        Remap("Assets/Models/guns/rpg-7/source/FBX RICKY.fbx",
            new MaterialSet("RPG", "RPG_high_DefaultMaterial_", "VR_DefaultMaterial")),
        Remap("Assets/Models/guns/stylized-tri-revolver/source/SM_Tri-Revolver.fbx",
            new MaterialSet("Tri-Revolver", "T_Tri-Revolver_", "M_Tri-Revolver")),
        Remap("Assets/Models/guns/hunting-rifle-fortnite-pbr/source/huntingrifle.fbx",
            new MaterialSet("Old Hunting Rifle", "T_Old_Hunting_Rifle_", "MI_Old_Hunting_Rifle")),
        // Links read from the FBX; the textures' alpha is gloss, not transparency.
        Remap("Assets/Models/guns/auto-shotgun/source/011.fbx",
            new MaterialSet("Auto Shotgun 41e08f40", "41e08f40", "Material #130"),
            new MaterialSet("Auto Shotgun 416e74c0", "416e74c0", "Material #144", "Material #145"),
            new MaterialSet("Auto Shotgun 41398f00", "41398f00", "Material #153")),
        Remap("Assets/Models/melee/mace-low/source/MaceLow.fbx",
            new MaterialSet("Mace", "Mace_", "Mace")),
        Remap("Assets/Models/melee/medieval-spear/source/Spear.fbx",
            new MaterialSet("Spear", "Spear_", "Spear")),
        Remap("Assets/Models/melee/skull-axe/source/skull axe.fbx",
            new MaterialSet("Skull Axe", "lambert1_", "lambert1")),
        Remap("Assets/Models/melee/sword/source/sword.fbx",
            new MaterialSet("Sword lambert2", "sword_lambert2SG_", "lambert2"),
            new MaterialSet("Sword lambert3", "sword_lambert3SG_", "lambert3"),
            new MaterialSet("Sword lambert4", "sword_lambert4SG_", "lambert4")),
        // Every material in this FBX references the single lambert1 texture set.
        Remap("Assets/Models/melee/sword-weekly/source/Sword_Weapon.fbx",
            new MaterialSet("Sword Weapon", "lambert1_", "Blade", "Buttons", "Ornaments", "Rope")),
        Remap("Assets/Models/melee/bludgeon-bandit-low-poly/source/Версия с обновлённой развёрткой_LP.fbx",
            new MaterialSet("Cudgel", "2_LP_Сcudgel_", "Сcudgel")),
        Remap("Assets/Models/melee/dragon-slayer-30-berserk/source/DragonSlayer3_0.fbx",
            new MaterialSet("Dragon Slayer Metal", "Metal_", "Metal"),
            new MaterialSet("Dragon Slayer BladeBlack", "BladeBlack_", "BladeBlack"),
            new MaterialSet("Dragon Slayer BladeWhite", "BladeWhite_", "BladeWhite"),
            new MaterialSet("Dragon Slayer Handle", "handle_", "handle"),
            new MaterialSet("Dragon Slayer Cloth", "Cloth_", "Cloth")),

        ByRenderer("Assets/Models/guns/handmade-pistol-prison-pistol/source/Pistol.fbx",
            new MaterialSet("Pistol", "Pistol_DefaultMaterial_", "*")),
        ByRenderer("Assets/Models/melee/ancient-knife/source/Ancient_knife.fbx",
            new MaterialSet("Ancient Knife", "Ancient_knife_", "*")),
        // The FBX names these materials, but they aren't bound to the meshes, so Unity skips them.
        ByRenderer("Assets/Models/guns/hunting-shotgun/source/gunful.fbx",
            new MaterialSet("Shotgun Stock", "GUN_test_приклад_", "priclad.003"),
            new MaterialSet("Shotgun Barrel", "GUN_test_дуло_", "dylo.002"),
            new MaterialSet("Shotgun Cover", "GUN_test_крышка_", "mini_Ruk.002"),
            new MaterialSet("Shotgun Pump", "GUN_test_перезарядник_", "reloader.002"),
            new MaterialSet("Shotgun Tube", "GUN_test_трубка_", "celinder.002"),
            new MaterialSet("Shotgun Receiver", "GUN_test_основа.001_", "CDDS.002"),
            new MaterialSet("Shotgun Lever", "GUN_test_РЫЧАГ_", "Cube.004", "Cube.005")),
        // No colour texture ships with this model, so it gets a plain gunmetal finish.
        ByRenderer("Assets/Models/guns/barret-50cal/source/barret.fbx",
            new MaterialSet("Barrett Gunmetal", null, "*")),
    };

    static ModelMaterials Remap(string modelPath, params MaterialSet[] sets) => new ModelMaterials(modelPath, false, sets);
    static ModelMaterials ByRenderer(string modelPath, params MaterialSet[] sets) => new ModelMaterials(modelPath, true, sets);

    [MenuItem("Tools/Primordia/Weapons/Apply Weapon Textures")]
    public static void ApplyAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EnsureModelMaterials();
        var updated = ApplyToWeaponPrefabs();
        AssetDatabase.SaveAssets();
        Debug.Log($"Primordia weapon textures are applied: {k_Models.Length} models, {updated} rigs updated.");
    }

    /// <summary>Creates the materials and remaps the FBX material slots that Unity imports.</summary>
    public static void EnsureModelMaterials()
    {
        foreach (var model in k_Models)
        {
            var materials = model.Sets.ToDictionary(set => set, set => EnsureMaterial(model, set));
            if (model.ByRenderer)
                continue;

            var importer = (ModelImporter)AssetImporter.GetAtPath(model.ModelPath);
            if (importer == null)
                throw new InvalidOperationException($"Weapon model is missing at {model.ModelPath}.");

            var existing = importer.GetExternalObjectMap();
            var changed = false;
            foreach (var set in model.Sets)
            foreach (var slot in set.Slots)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), slot);
                if (existing.TryGetValue(id, out var current) && current == materials[set])
                    continue;

                importer.AddRemap(id, materials[set]);
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }
    }

    /// <summary>Assigns materials to a freshly built weapon visual whose FBX has no usable materials.</summary>
    public static void ApplyToVisual(string modelPath, GameObject visual)
    {
        var model = k_Models.FirstOrDefault(candidate => candidate.ByRenderer && candidate.ModelPath == modelPath);
        if (model == null)
            return;

        foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            var set = model.Sets.FirstOrDefault(candidate => candidate.Slots.Contains(renderer.name)) ??
                model.Sets.FirstOrDefault(candidate => candidate.Slots.Contains("*"));
            if (set == null)
                continue;

            var material = EnsureMaterial(model, set);
            renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
        }
    }

    static int ApplyToWeaponPrefabs()
    {
        var updated = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { k_WeaponPrefabFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = root.transform.Find("Visual");
                var source = visual != null ? PrefabUtility.GetCorrespondingObjectFromOriginalSource(visual.gameObject) : null;
                var modelPath = source != null ? AssetDatabase.GetAssetPath(source) : null;
                if (!k_Models.Any(model => model.ByRenderer && model.ModelPath == modelPath))
                    continue;

                ApplyToVisual(modelPath, visual.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                updated++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        return updated;
    }

    static Material EnsureMaterial(ModelMaterials model, MaterialSet set)
    {
        var folder = model.Root + "/Materials";
        EnsureFolder(folder);
        var path = $"{folder}/{set.Name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(k_Shader)) { name = set.Name };
            AssetDatabase.CreateAsset(material, path);
        }

        if (set.Prefix == null)
        {
            material.SetColor("_BaseColor", new Color(0.16f, 0.17f, 0.18f, 1f));
            material.SetFloat("_Metallic", 0.8f);
            material.SetFloat("_Smoothness", 0.45f);
            EditorUtility.SetDirty(material);
            return material;
        }

        var textures = FindTextures(model.Root + "/textures", set.Prefix);
        if (!textures.TryGetValue("base", out var baseMap))
            throw new InvalidOperationException($"{set.Name} has no colour texture starting with {set.Prefix} in {model.Root}/textures.");

        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", LoadTexture(baseMap, srgb: true));
        SetMap(material, "_BumpMap", "_NORMALMAP", textures.TryGetValue("normal", out var normal) ? LoadNormalMap(normal) : null);
        SetMap(material, "_MetallicGlossMap", "_METALLICSPECGLOSSMAP", textures.TryGetValue("metallic", out var metallic) ? LoadTexture(metallic, srgb: false) : null);
        SetMap(material, "_OcclusionMap", "_OCCLUSIONMAP", textures.TryGetValue("occlusion", out var occlusion) ? LoadTexture(occlusion, srgb: false) : null);

        // URP Lit has no roughness input, so use the texture's average as the smoothness.
        var smoothness = k_DefaultSmoothness;
        if (textures.TryGetValue("roughness", out var roughness))
            smoothness = 1f - AverageValue(roughness);
        else if (textures.TryGetValue("glossiness", out var glossiness))
            smoothness = AverageValue(glossiness);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", 0f);

        EditorUtility.SetDirty(material);
        return material;
    }

    static Dictionary<string, string> FindTextures(string folder, string prefix)
    {
        var maps = new Dictionary<string, string>();
        prefix = prefix.Normalize();
        foreach (var path in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var name = Path.GetFileNameWithoutExtension(path).Normalize();
            if (!name.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var kind = name.Substring(prefix.Length).Replace("_", "").Replace(" ", "").ToLowerInvariant() switch
            {
                "" or "basecolor" or "albedo" or "albedotransparency" or "d" or "diffuse" => "base",
                "normal" or "normalopengl" => "normal",
                "metallic" => "metallic",
                "roughness" => "roughness",
                "glossiness" or "gloss" => "glossiness",
                "ao" or "mixedao" or "occlusion" => "occlusion",
                _ => null,
            };
            if (kind != null)
                maps[kind] = path;
        }
        return maps;
    }

    static void SetMap(Material material, string property, string keyword, Texture texture)
    {
        material.SetTexture(property, texture);
        if (texture != null)
            material.EnableKeyword(keyword);
        else
            material.DisableKeyword(keyword);
    }

    static Texture2D LoadTexture(string path, bool srgb)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType != TextureImporterType.Default || importer.sRGBTexture != srgb)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Texture2D LoadNormalMap(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Reads the source file directly so the texture doesn't need to be import-readable.
    static float AverageValue(string path)
    {
        var texture = new Texture2D(2, 2);
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(path)))
                return k_DefaultSmoothness;

            var pixels = texture.GetPixels32();
            var step = Mathf.Max(1, pixels.Length / 65536);
            double sum = 0;
            var count = 0;
            for (var index = 0; index < pixels.Length; index += step)
            {
                sum += pixels[index].r / 255.0;
                count++;
            }
            return Mathf.Clamp01((float)(sum / Math.Max(1, count)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
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
