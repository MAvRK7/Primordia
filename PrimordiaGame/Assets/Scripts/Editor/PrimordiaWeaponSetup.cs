using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Builds the complete weapon-model catalog as designer-editable definitions and
/// XR-ready pickup prefabs. Existing definition tuning is preserved; generated rigs
/// receive the canonical grip, physics, hit detection, and feedback setup.
/// </summary>
public static class PrimordiaWeaponSetup
{
    const string k_InputActionsPath =
        "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/XRI Default Input Actions.inputactions";
    const string k_RevolverModelPath =
        "Assets/Models/guns/stylized-tri-revolver/source/SM_Tri-Revolver.fbx";
    const string k_KnifePrefabPath = "Assets/Prefabs/Knife.prefab";
    const string k_WeaponDataFolder = "Assets/Data/Weapons";
    const string k_WeaponPrefabFolder = "Assets/Prefabs/Weapons";
    const string k_ScrapRevolverDataPath = k_WeaponDataFolder + "/Scrap Revolver.asset";
    const string k_KnifeDataPath = k_WeaponDataFolder + "/Knife.asset";
    const string k_ScrapRevolverPrefabPath = k_WeaponPrefabFolder + "/Scrap Revolver.prefab";
    const string k_TracerMaterialPath = k_WeaponDataFolder + "/Weapon Tracer.mat";
    const string k_ParticleMaterialPath = k_WeaponDataFolder + "/Weapon Particles.mat";
    const string k_FallbackModelMaterialPath = k_WeaponDataFolder + "/Weapon Model Fallback.mat";

    sealed class WeaponSpec
    {
        public string ModelPath { get; }
        public string DefinitionPath { get; }
        public string PrefabPath { get; }
        public string WeaponId { get; }
        public string DisplayName { get; }
        public WeaponType Type { get; }
        public int Tier { get; }
        public float Damage { get; }
        public float Range { get; }
        public float AttacksPerSecond { get; }
        public float NoiseRange { get; }
        public AmmoType AmmoType { get; }
        public int MagazineCapacity { get; }
        public bool Automatic { get; }
        public int ReserveRounds { get; }
        public float TargetLength { get; }
        public float Mass { get; }
        public float GripHeight { get; }
        public float GripForward { get; }
        public float MuzzleHeight { get; }
        public float StrikeStart { get; }
        public bool FlipForward { get; }
        public bool IsReference { get; }

        public WeaponSpec(
            string assetName,
            string modelPath,
            string prefabPath,
            string weaponId,
            string displayName,
            WeaponType type,
            int tier,
            float damage,
            float range,
            float attacksPerSecond,
            float noiseRange,
            AmmoType ammoType,
            int magazineCapacity,
            bool automatic,
            int reserveRounds,
            float targetLength,
            float mass,
            float gripHeight,
            float gripForward,
            float muzzleHeight,
            float strikeStart,
            bool flipForward,
            bool isReference = false)
        {
            ModelPath = modelPath;
            DefinitionPath = k_WeaponDataFolder + "/" + assetName + ".asset";
            PrefabPath = prefabPath;
            WeaponId = weaponId;
            DisplayName = displayName;
            Type = type;
            Tier = tier;
            Damage = damage;
            Range = range;
            AttacksPerSecond = attacksPerSecond;
            NoiseRange = noiseRange;
            AmmoType = ammoType;
            MagazineCapacity = magazineCapacity;
            Automatic = automatic;
            ReserveRounds = reserveRounds;
            TargetLength = targetLength;
            Mass = mass;
            GripHeight = gripHeight;
            GripForward = gripForward;
            MuzzleHeight = muzzleHeight;
            StrikeStart = strikeStart;
            FlipForward = flipForward;
            IsReference = isReference;
        }
    }

    // The first 16 entries implement the 8+8 progression roster in the master
    // development specification. Forged Longsword gives the additional imported
    // sword model a complete profile/rig without displacing a specified tier.
    static readonly WeaponSpec[] k_WeaponSpecs =
    {
        Gun(
            "Scrap Revolver",
            k_RevolverModelPath,
            k_ScrapRevolverPrefabPath,
            "scrap_revolver",
            "Scrap Revolver",
            tier: 1,
            damage: 22f,
            range: 35f,
            attacksPerSecond: 1.5f,
            ammoType: AmmoType.Light,
            magazineCapacity: 6,
            automatic: false,
            targetLength: 0.28f,
            mass: 1.1f,
            gripHeight: 0.25f,
            gripForward: 0.34f,
            muzzleHeight: 0.6f,
            isReference: true),
        Gun(
            "Bone Sidearm",
            "Assets/Models/guns/handmade-pistol-prison-pistol/source/Pistol.fbx",
            k_WeaponPrefabFolder + "/Bone Sidearm.prefab",
            "bone_sidearm",
            "Bone Sidearm",
            tier: 2,
            damage: 30f,
            range: 40f,
            attacksPerSecond: 2.2f,
            ammoType: AmmoType.Light,
            magazineCapacity: 10,
            automatic: false,
            targetLength: 0.26f,
            mass: 0.9f,
            gripHeight: 0.22f,
            gripForward: 0.34f,
            muzzleHeight: 0.68f,
            flipForward: true),
        Gun(
            "Pump Carbine",
            "Assets/Models/guns/auto-shotgun/source/011.fbx",
            k_WeaponPrefabFolder + "/Pump Carbine.prefab",
            "pump_carbine",
            "Pump Carbine",
            tier: 3,
            damage: 40f,
            range: 50f,
            attacksPerSecond: 1.6f,
            ammoType: AmmoType.Light,
            magazineCapacity: 8,
            automatic: false,
            targetLength: 0.78f,
            mass: 3.2f,
            gripHeight: 0.28f,
            gripForward: 0.48f,
            muzzleHeight: 0.67f,
            flipForward: true),
        Gun(
            "Hunting Rifle",
            "Assets/Models/guns/hunting-rifle-fortnite-pbr/source/huntingrifle.fbx",
            k_WeaponPrefabFolder + "/Hunting Rifle.prefab",
            "hunting_rifle",
            "Hunting Rifle",
            tier: 4,
            damage: 52f,
            range: 70f,
            attacksPerSecond: 1.2f,
            ammoType: AmmoType.Heavy,
            magazineCapacity: 5,
            automatic: false,
            targetLength: 1f,
            mass: 3.6f,
            gripHeight: 0.3f,
            gripForward: 0.48f,
            muzzleHeight: 0.68f),
        Gun(
            "Auto Carbine",
            "Assets/Models/guns/m16-assault-rifle/source/m16a3.fbx",
            k_WeaponPrefabFolder + "/Auto Carbine.prefab",
            "auto_carbine",
            "Auto Carbine",
            tier: 5,
            damage: 44f,
            range: 55f,
            attacksPerSecond: 5f,
            ammoType: AmmoType.Heavy,
            magazineCapacity: 24,
            automatic: true,
            targetLength: 0.84f,
            mass: 3.3f,
            gripHeight: 0.28f,
            gripForward: 0.5f,
            muzzleHeight: 0.66f,
            flipForward: true),
        Gun(
            "Plated Shotgun",
            "Assets/Models/guns/hunting-shotgun/source/gunful.fbx",
            k_WeaponPrefabFolder + "/Plated Shotgun.prefab",
            "plated_shotgun",
            "Plated Shotgun",
            tier: 6,
            damage: 72f,
            range: 18f,
            attacksPerSecond: 1f,
            ammoType: AmmoType.Shell,
            magazineCapacity: 6,
            automatic: false,
            targetLength: 0.95f,
            mass: 3.8f,
            gripHeight: 0.27f,
            gripForward: 0.48f,
            muzzleHeight: 0.68f,
            flipForward: true),
        Gun(
            "Marksman Rifle",
            "Assets/Models/guns/barret-50cal/source/barret.fbx",
            k_WeaponPrefabFolder + "/Marksman Rifle.prefab",
            "marksman_rifle",
            "Marksman Rifle",
            tier: 7,
            damage: 80f,
            range: 85f,
            attacksPerSecond: 1.4f,
            ammoType: AmmoType.Heavy,
            magazineCapacity: 8,
            automatic: false,
            targetLength: 1.18f,
            mass: 6.5f,
            gripHeight: 0.3f,
            gripForward: 0.48f,
            muzzleHeight: 0.67f),
        Gun(
            "Apex Rifle",
            "Assets/Models/guns/rpg-7/source/FBX RICKY.fbx",
            k_WeaponPrefabFolder + "/Apex Rifle.prefab",
            "apex_rifle",
            "Apex Rifle",
            tier: 8,
            damage: 98f,
            range: 90f,
            attacksPerSecond: 2.2f,
            ammoType: AmmoType.Heavy,
            magazineCapacity: 12,
            automatic: false,
            targetLength: 0.98f,
            mass: 5.8f,
            gripHeight: 0.42f,
            gripForward: 0.5f,
            muzzleHeight: 0.55f,
            flipForward: true),
        MeleeWeapon(
            "Knife",
            "Assets/Models/melee/military-knife/Knife_LP.fbx",
            k_KnifePrefabPath,
            "knife",
            "Knife",
            tier: 1,
            damage: 12f,
            range: 1.2f,
            attacksPerSecond: 2f,
            targetLength: 0.3f,
            mass: 1f,
            gripForward: 0.15f,
            strikeStart: 0.3f,
            noiseRange: 3f,
            isReference: true),
        MeleeWeapon(
            "Wooden Spear",
            "Assets/Models/melee/medieval-spear/source/Spear.fbx",
            k_WeaponPrefabFolder + "/Wooden Spear.prefab",
            "wooden_spear",
            "Wooden Spear",
            tier: 2,
            damage: 18f,
            range: 2f,
            attacksPerSecond: 1.4f,
            targetLength: 2f,
            mass: 1.6f,
            gripForward: 0.3f,
            strikeStart: 0.78f),
        MeleeWeapon(
            "Bone Club",
            "Assets/Models/melee/bludgeon-bandit-low-poly/source/Версия с обновлённой развёрткой_LP.fbx",
            k_WeaponPrefabFolder + "/Bone Club.prefab",
            "bone_club",
            "Bone Club",
            tier: 3,
            damage: 26f,
            range: 1.5f,
            attacksPerSecond: 1.6f,
            targetLength: 0.68f,
            mass: 1.8f,
            gripForward: 0.16f,
            strikeStart: 0.48f),
        MeleeWeapon(
            "Claw Blade",
            "Assets/Models/melee/ancient-knife/source/Ancient_knife.fbx",
            k_WeaponPrefabFolder + "/Claw Blade.prefab",
            "claw_blade",
            "Claw Blade",
            tier: 4,
            damage: 34f,
            range: 1.4f,
            attacksPerSecond: 1.8f,
            targetLength: 0.46f,
            mass: 0.65f,
            gripForward: 0.14f,
            strikeStart: 0.3f),
        MeleeWeapon(
            "Horn Pike",
            "Assets/Models/melee/sword-weekly/source/Sword_Weapon.fbx",
            k_WeaponPrefabFolder + "/Horn Pike.prefab",
            "horn_pike",
            "Horn Pike",
            tier: 5,
            damage: 44f,
            range: 2.2f,
            attacksPerSecond: 1.2f,
            targetLength: 1.08f,
            mass: 1.9f,
            gripForward: 0.12f,
            strikeStart: 0.28f),
        MeleeWeapon(
            "Spike Maul",
            "Assets/Models/melee/mace-low/source/MaceLow.fbx",
            k_WeaponPrefabFolder + "/Spike Maul.prefab",
            "spike_maul",
            "Spike Maul",
            tier: 6,
            damage: 56f,
            range: 1.6f,
            attacksPerSecond: 1f,
            targetLength: 0.72f,
            mass: 3.4f,
            gripForward: 0.14f,
            strikeStart: 0.68f),
        MeleeWeapon(
            "Plated Cleaver",
            "Assets/Models/melee/skull-axe/source/skull axe.fbx",
            k_WeaponPrefabFolder + "/Plated Cleaver.prefab",
            "plated_cleaver",
            "Plated Cleaver",
            tier: 7,
            damage: 68f,
            range: 1.7f,
            attacksPerSecond: 1.1f,
            targetLength: 0.82f,
            mass: 3f,
            gripForward: 0.14f,
            strikeStart: 0.66f),
        MeleeWeapon(
            "Rex-Fang Greatblade",
            "Assets/Models/melee/dragon-slayer-30-berserk/source/DragonSlayer3_0.fbx",
            k_WeaponPrefabFolder + "/Rex-Fang Greatblade.prefab",
            "rex_fang_greatblade",
            "Rex-Fang Greatblade",
            tier: 8,
            damage: 84f,
            range: 1.9f,
            attacksPerSecond: 1f,
            targetLength: 1.55f,
            mass: 5.5f,
            gripForward: 0.12f,
            strikeStart: 0.24f,
            flipForward: true),
        MeleeWeapon(
            "Forged Longsword",
            "Assets/Models/melee/sword/source/sword.fbx",
            k_WeaponPrefabFolder + "/Forged Longsword.prefab",
            "forged_longsword",
            "Forged Longsword",
            tier: 5,
            damage: 44f,
            range: 1.8f,
            attacksPerSecond: 1.3f,
            targetLength: 1f,
            mass: 1.8f,
            gripForward: 0.12f,
            strikeStart: 0.28f),
    };

    [MenuItem("Tools/Primordia/Weapons/Ensure Reference Weapon Setup")]
    public static void EnsureReferenceWeapons()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EnsureFolder("Assets/Data");
        EnsureFolder(k_WeaponDataFolder);
        EnsureFolder(k_WeaponPrefabFolder);

        AssetDatabase.ImportAsset(k_InputActionsPath, ImportAssetOptions.ForceSynchronousImport);

        var revolverDefinition = EnsureDefinition(
            k_ScrapRevolverDataPath,
            "scrap_revolver",
            "Scrap Revolver",
            WeaponType.Gun,
            tier: 1,
            damage: 22f,
            range: 35f,
            attacksPerSecond: 1.5f,
            noiseRange: 40f,
            ammoType: AmmoType.Light,
            magazineCapacity: 6,
            automatic: false);

        var knifeDefinition = EnsureDefinition(
            k_KnifeDataPath,
            "knife",
            "Knife",
            WeaponType.Melee,
            tier: 1,
            damage: 12f,
            range: 1.2f,
            attacksPerSecond: 2f,
            noiseRange: 3f,
            ammoType: AmmoType.None,
            magazineCapacity: 0,
            automatic: false);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(k_ScrapRevolverPrefabPath) == null)
            CreateScrapRevolverPrefab(revolverDefinition);

        TuneScrapRevolverPrefab();
        EnsureKnifePrefab(knifeDefinition);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Primordia reference weapons are ready: Scrap Revolver and Knife.");
    }

    [MenuItem("Tools/Primordia/Weapons/Ensure Complete Weapon Catalog")]
    public static void EnsureAllWeapons()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EnsureReferenceWeapons();
        EnsureFolder("Assets/Data");
        EnsureFolder(k_WeaponDataFolder);
        EnsureFolder(k_WeaponPrefabFolder);

        AssetDatabase.ImportAsset(k_InputActionsPath, ImportAssetOptions.ForceSynchronousImport);

        foreach (var spec in k_WeaponSpecs)
        {
            AssetDatabase.ImportAsset(spec.ModelPath, ImportAssetOptions.ForceSynchronousImport);
            if (spec.IsReference)
                continue;

            var definition = EnsureDefinition(
                spec.DefinitionPath,
                spec.WeaponId,
                spec.DisplayName,
                spec.Type,
                spec.Tier,
                spec.Damage,
                spec.Range,
                spec.AttacksPerSecond,
                spec.NoiseRange,
                spec.AmmoType,
                spec.MagazineCapacity,
                spec.Automatic);

            CreateWeaponRig(spec, definition);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateAllWeapons();
        Debug.Log($"Primordia weapon catalog is ready: {k_WeaponSpecs.Length} profiles and {k_WeaponSpecs.Length} XR rigs.");
    }

    static WeaponSpec Gun(
        string assetName,
        string modelPath,
        string prefabPath,
        string weaponId,
        string displayName,
        int tier,
        float damage,
        float range,
        float attacksPerSecond,
        AmmoType ammoType,
        int magazineCapacity,
        bool automatic,
        float targetLength,
        float mass,
        float gripHeight,
        float gripForward,
        float muzzleHeight,
        bool flipForward = false,
        bool isReference = false)
    {
        return new WeaponSpec(
            assetName,
            modelPath,
            prefabPath,
            weaponId,
            displayName,
            WeaponType.Gun,
            tier,
            damage,
            range,
            attacksPerSecond,
            noiseRange: 40f,
            ammoType,
            magazineCapacity,
            automatic,
            reserveRounds: magazineCapacity * 3,
            targetLength,
            mass,
            gripHeight,
            gripForward,
            muzzleHeight,
            strikeStart: 0f,
            flipForward,
            isReference);
    }

    static WeaponSpec MeleeWeapon(
        string assetName,
        string modelPath,
        string prefabPath,
        string weaponId,
        string displayName,
        int tier,
        float damage,
        float range,
        float attacksPerSecond,
        float targetLength,
        float mass,
        float gripForward,
        float strikeStart,
        float noiseRange = 5f,
        bool flipForward = false,
        bool isReference = false)
    {
        return new WeaponSpec(
            assetName,
            modelPath,
            prefabPath,
            weaponId,
            displayName,
            WeaponType.Melee,
            tier,
            damage,
            range,
            attacksPerSecond,
            noiseRange,
            AmmoType.None,
            magazineCapacity: 0,
            automatic: false,
            reserveRounds: 0,
            targetLength,
            mass,
            gripHeight: 0.5f,
            gripForward,
            muzzleHeight: 0.5f,
            strikeStart,
            flipForward,
            isReference);
    }

    static void CreateWeaponRig(WeaponSpec spec, WeaponDefinition definition)
    {
        if (spec.Type == WeaponType.Gun)
            CreateGunRig(spec, definition);
        else
            CreateMeleeRig(spec, definition);
    }

    static void CreateGunRig(WeaponSpec spec, WeaponDefinition definition)
    {
        var root = new GameObject(spec.DisplayName);
        try
        {
            var body = AddWeaponBody(root, spec.Mass);
            var visual = AddNormalizedVisual(root, spec);
            var visualBounds = CalculateLocalBounds(
                root.transform,
                visual.GetComponentsInChildren<Renderer>(true));
            AddPhysicalCollider(root, visualBounds);
            body.centerOfMass = visualBounds.center;

            var attach = CreateChild(root.transform, "Attach");
            attach.SetLocalPositionAndRotation(
                new Vector3(
                    visualBounds.center.x,
                    Mathf.Lerp(visualBounds.min.y, visualBounds.max.y, spec.GripHeight),
                    Mathf.Lerp(visualBounds.min.z, visualBounds.max.z, spec.GripForward)),
                Quaternion.Euler(0f, 180f, 0f));

            var muzzle = CreateChild(root.transform, "Muzzle");
            muzzle.SetLocalPositionAndRotation(
                new Vector3(
                    visualBounds.center.x,
                    Mathf.Lerp(visualBounds.min.y, visualBounds.max.y, spec.MuzzleHeight),
                    visualBounds.max.z + 0.012f),
                Quaternion.identity);

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.attachTransform = attach;
            ConfigureHeldPhysics(root, grab);

            var reserveAmmo = root.AddComponent<Ammo>();
            reserveAmmo.Configure(spec.AmmoType, spec.ReserveRounds);

            var audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;

            var tracerMaterial = EnsureMaterial(
                k_TracerMaterialPath,
                "Universal Render Pipeline/Unlit",
                new Color(1f, 0.78f, 0.2f, 1f));
            var particleMaterial = EnsureMaterial(
                k_ParticleMaterialPath,
                "Universal Render Pipeline/Particles/Unlit",
                new Color(1f, 0.65f, 0.12f, 1f));

            var tracerObject = CreateChild(root.transform, "Tracer").gameObject;
            var tracer = tracerObject.AddComponent<LineRenderer>();
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.startWidth = 0.008f;
            tracer.endWidth = 0.002f;
            tracer.sharedMaterial = tracerMaterial;
            tracer.enabled = false;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.receiveShadows = false;

            var muzzleFlash = CreateBurstParticle(
                muzzle,
                "Muzzle Flash",
                particleMaterial,
                startSize: 0.045f,
                lifetime: 0.045f,
                particleCount: 4);
            var impactEffect = CreateBurstParticle(
                root.transform,
                "Impact Effect",
                particleMaterial,
                startSize: 0.025f,
                lifetime: 0.12f,
                particleCount: 6);

            var ranged = root.AddComponent<Ranged>();
            var serializedRanged = new SerializedObject(ranged);
            serializedRanged.FindProperty("m_Definition").objectReferenceValue = definition;
            serializedRanged.FindProperty("m_ReserveAmmo").objectReferenceValue = reserveAmmo;
            serializedRanged.FindProperty("m_GrabInteractable").objectReferenceValue = grab;
            serializedRanged.FindProperty("m_ReloadAction").objectReferenceValue = FindReloadActionReference();
            serializedRanged.FindProperty("m_FirePoint").objectReferenceValue = muzzle;
            serializedRanged.FindProperty("m_VisualRoot").objectReferenceValue = visual.transform;
            serializedRanged.FindProperty("m_MuzzleFlash").objectReferenceValue = muzzleFlash;
            serializedRanged.FindProperty("m_ImpactEffect").objectReferenceValue = impactEffect;
            serializedRanged.FindProperty("m_Tracer").objectReferenceValue = tracer;
            serializedRanged.FindProperty("m_AudioSource").objectReferenceValue = audioSource;
            serializedRanged.ApplyModifiedPropertiesWithoutUndo();

            SavePrefabOrThrow(root, spec.PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void CreateMeleeRig(WeaponSpec spec, WeaponDefinition definition)
    {
        var root = new GameObject(spec.DisplayName);
        try
        {
            var body = AddWeaponBody(root, spec.Mass);
            var visual = AddNormalizedVisual(root, spec);
            var visualBounds = CalculateLocalBounds(
                root.transform,
                visual.GetComponentsInChildren<Renderer>(true));
            AddPhysicalCollider(root, visualBounds);
            body.centerOfMass = visualBounds.center;

            var attach = CreateChild(root.transform, "Attach");
            attach.SetLocalPositionAndRotation(
                new Vector3(
                    visualBounds.center.x,
                    visualBounds.center.y,
                    Mathf.Lerp(visualBounds.min.z, visualBounds.max.z, spec.GripForward)),
                Quaternion.identity);

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.attachTransform = attach;
            ConfigureHeldPhysics(root, grab);

            var audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;

            var melee = root.AddComponent<Melee>();
            var hitboxTransform = CreateChild(root.transform, "Damage Hitbox");
            var hitCollider = hitboxTransform.gameObject.AddComponent<BoxCollider>();
            var strikeMin = Mathf.Lerp(visualBounds.min.z, visualBounds.max.z, spec.StrikeStart);
            hitCollider.center = new Vector3(
                visualBounds.center.x,
                visualBounds.center.y,
                (strikeMin + visualBounds.max.z) * 0.5f);
            hitCollider.size = new Vector3(
                Mathf.Max(visualBounds.size.x * 1.02f, 0.015f),
                Mathf.Max(visualBounds.size.y * 1.02f, 0.015f),
                Mathf.Max(visualBounds.max.z - strikeMin, 0.03f));
            hitCollider.isTrigger = true;
            hitCollider.enabled = false;

            var hitbox = hitboxTransform.gameObject.AddComponent<MeleeHitbox>();
            var serializedHitbox = new SerializedObject(hitbox);
            serializedHitbox.FindProperty("m_Owner").objectReferenceValue = melee;
            serializedHitbox.FindProperty("m_HitCollider").objectReferenceValue = hitCollider;
            serializedHitbox.ApplyModifiedPropertiesWithoutUndo();

            var serializedMelee = new SerializedObject(melee);
            serializedMelee.FindProperty("m_Definition").objectReferenceValue = definition;
            serializedMelee.FindProperty("m_GrabInteractable").objectReferenceValue = grab;
            serializedMelee.FindProperty("m_Hitbox").objectReferenceValue = hitbox;
            serializedMelee.FindProperty("m_MinimumHitSpeed").floatValue = 1f;
            serializedMelee.FindProperty("m_AudioSource").objectReferenceValue = audioSource;
            serializedMelee.ApplyModifiedPropertiesWithoutUndo();

            SavePrefabOrThrow(root, spec.PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static Rigidbody AddWeaponBody(GameObject root, float mass)
    {
        var body = root.AddComponent<Rigidbody>();
        body.mass = mass;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        return body;
    }

    static GameObject AddNormalizedVisual(GameObject root, WeaponSpec spec)
    {
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ModelPath);
        if (modelAsset == null)
            throw new InvalidOperationException($"{spec.DisplayName} model is missing at {spec.ModelPath}.");

        var visual = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
        if (visual == null)
            throw new InvalidOperationException($"Could not instantiate the {spec.DisplayName} model.");

        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        visual.transform.localScale = Vector3.one;
        OrientAndScaleAlongForward(root.transform, visual.transform, spec.TargetLength);
        if (spec.FlipForward)
            visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * visual.transform.localRotation;

        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException($"{spec.DisplayName} model has no renderers.");

        var visualBounds = CalculateLocalBounds(root.transform, renderers);
        visual.transform.localPosition -= visualBounds.center;

        foreach (var camera in visual.GetComponentsInChildren<Camera>(true))
            camera.enabled = false;
        foreach (var light in visual.GetComponentsInChildren<Light>(true))
            light.enabled = false;

        EnsureNonNullModelMaterials(renderers);
        return visual;
    }

    static void EnsureNonNullModelMaterials(Renderer[] renderers)
    {
        var fallback = EnsureMaterial(
            k_FallbackModelMaterialPath,
            "Universal Render Pipeline/Lit",
            new Color(0.35f, 0.38f, 0.4f, 1f));

        foreach (var renderer in renderers)
        {
            var materials = renderer.sharedMaterials;
            if (materials.Length == 0)
            {
                renderer.sharedMaterial = fallback;
                continue;
            }

            var changed = false;
            for (var index = 0; index < materials.Length; index++)
            {
                if (materials[index] != null)
                    continue;

                materials[index] = fallback;
                changed = true;
            }

            if (changed)
                renderer.sharedMaterials = materials;
        }
    }

    static void AddPhysicalCollider(GameObject root, Bounds bounds)
    {
        var collider = root.AddComponent<BoxCollider>();
        collider.center = bounds.center;
        collider.size = new Vector3(
            Mathf.Max(bounds.size.x, 0.015f),
            Mathf.Max(bounds.size.y, 0.015f),
            Mathf.Max(bounds.size.z, 0.03f));
    }

    static void SavePrefabOrThrow(GameObject root, string path)
    {
        if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
            throw new InvalidOperationException($"Could not save weapon rig at {path}.");
    }

    [MenuItem("Tools/Primordia/Weapons/Validate Complete Weapon Catalog")]
    public static void ValidateAllWeapons()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var errors = new List<string>();
        var expectedModels = new HashSet<string>(
            k_WeaponSpecs.Select(spec => spec.ModelPath),
            StringComparer.Ordinal);
        var importedModels = new HashSet<string>(
            AssetDatabase.FindAssets(
                    "t:Model",
                    new[] { "Assets/Models/guns", "Assets/Models/melee" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)),
            StringComparer.Ordinal);

        foreach (var missing in expectedModels.Except(importedModels).OrderBy(path => path))
            errors.Add("Model is not imported: " + missing);
        foreach (var uncovered in importedModels.Except(expectedModels).OrderBy(path => path))
            errors.Add("Imported weapon model has no catalog entry: " + uncovered);

        var weaponIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spec in k_WeaponSpecs)
        {
            var definition = ValidateDefinition(spec, weaponIds, errors);
            ValidateRig(spec, definition, errors);
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Weapon catalog validation failed:\n - " + string.Join("\n - ", errors));
        }

        Debug.Log($"Weapon catalog validation passed: {k_WeaponSpecs.Length} models, profiles, and XR rigs.");
    }

    static WeaponDefinition ValidateDefinition(
        WeaponSpec spec,
        HashSet<string> weaponIds,
        List<string> errors)
    {
        var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(spec.DefinitionPath);
        if (definition == null)
        {
            errors.Add($"{spec.DisplayName}: profile is missing at {spec.DefinitionPath}.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(definition.WeaponId))
            errors.Add($"{spec.DisplayName}: profile has no stable weapon id.");
        else if (!weaponIds.Add(definition.WeaponId))
            errors.Add($"{spec.DisplayName}: duplicate weapon id '{definition.WeaponId}'.");

        if (definition.Type != spec.Type)
            errors.Add($"{spec.DisplayName}: profile type is {definition.Type}, expected {spec.Type}.");
        if (definition.Tier < 1)
            errors.Add($"{spec.DisplayName}: tier must be at least 1.");
        if (!IsFinitePositive(definition.Damage) ||
            !IsFinitePositive(definition.Range) ||
            !IsFinitePositive(definition.AttacksPerSecond))
        {
            errors.Add($"{spec.DisplayName}: profile combat values must be finite and positive.");
        }

        if (definition.Type == WeaponType.Gun)
        {
            if (definition.AmmoType == AmmoType.None || definition.MagazineCapacity < 1)
                errors.Add($"{spec.DisplayName}: gun profile has invalid ammo configuration.");
        }
        else if (definition.AmmoType != AmmoType.None ||
            definition.MagazineCapacity != 0 ||
            definition.Automatic)
        {
            errors.Add($"{spec.DisplayName}: melee profile contains gun-only settings.");
        }

        return definition;
    }

    static void ValidateRig(
        WeaponSpec spec,
        WeaponDefinition definition,
        List<string> errors)
    {
        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
        if (prefabAsset == null)
        {
            errors.Add($"{spec.DisplayName}: rig is missing at {spec.PrefabPath}.");
            return;
        }

        if (!AssetDatabase.GetDependencies(spec.PrefabPath, true).Contains(spec.ModelPath))
            errors.Add($"{spec.DisplayName}: rig does not depend on its model {spec.ModelPath}.");

        var root = PrefabUtility.LoadPrefabContents(spec.PrefabPath);
        try
        {
            var missingScriptCount = root.GetComponentsInChildren<Transform>(true)
                .Sum(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject));
            if (missingScriptCount != 0)
                errors.Add($"{spec.DisplayName}: rig contains {missingScriptCount} missing scripts.");

            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer is not ParticleSystemRenderer && renderer is not LineRenderer)
                .ToArray();
            if (renderers.Length == 0)
            {
                errors.Add($"{spec.DisplayName}: rig has no model renderers.");
            }
            else
            {
                if (renderers.Any(renderer =>
                        renderer.sharedMaterials.Length == 0 ||
                        renderer.sharedMaterials.Any(material => material == null)))
                {
                    errors.Add($"{spec.DisplayName}: model has an unassigned material slot.");
                }

                var bounds = CalculateLocalBounds(root.transform, renderers);
                if (!IsUsableBounds(bounds))
                    errors.Add($"{spec.DisplayName}: model bounds are empty or invalid.");
                else if (!spec.IsReference &&
                    (bounds.size.z < bounds.size.x ||
                     bounds.size.z < bounds.size.y ||
                     Mathf.Abs(bounds.size.z - spec.TargetLength) > 0.025f))
                {
                    errors.Add($"{spec.DisplayName}: generated model is not normalized along weapon-forward (+Z).");
                }
            }

            var body = root.GetComponent<Rigidbody>();
            if (body == null)
                errors.Add($"{spec.DisplayName}: rig has no Rigidbody.");
            else
            {
                if (body.interpolation != RigidbodyInterpolation.Interpolate)
                    errors.Add($"{spec.DisplayName}: Rigidbody interpolation must be Interpolate.");
                if (body.collisionDetectionMode != CollisionDetectionMode.ContinuousSpeculative)
                    errors.Add($"{spec.DisplayName}: Rigidbody collision mode must be Continuous Speculative.");
            }

            if (!root.GetComponentsInChildren<Collider>(true).Any(collider => !collider.isTrigger))
                errors.Add($"{spec.DisplayName}: rig has no physical collider.");

            var grab = root.GetComponent<XRGrabInteractable>();
            if (grab == null)
                errors.Add($"{spec.DisplayName}: rig has no XRGrabInteractable.");
            else
            {
                if (grab.attachTransform == null || !grab.attachTransform.IsChildOf(root.transform))
                    errors.Add($"{spec.DisplayName}: grab attach is missing or outside the rig.");
                if (grab.useDynamicAttach)
                    errors.Add($"{spec.DisplayName}: grab must use its authored attach transform.");
                if (grab.movementType != XRBaseInteractable.MovementType.Kinematic)
                    errors.Add($"{spec.DisplayName}: grab movement must be Kinematic.");
            }

            var audioSource = root.GetComponent<AudioSource>();
            if (audioSource == null || audioSource.playOnAwake || audioSource.spatialBlend < 0.99f)
                errors.Add($"{spec.DisplayName}: rig requires a non-autoplaying 3D AudioSource.");

            if (spec.Type == WeaponType.Gun)
                ValidateGunRig(spec, root, definition, grab, errors);
            else
                ValidateMeleeRig(spec, root, definition, grab, errors);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ValidateGunRig(
        WeaponSpec spec,
        GameObject root,
        WeaponDefinition definition,
        XRGrabInteractable grab,
        List<string> errors)
    {
        var ranged = root.GetComponent<Ranged>();
        if (ranged == null)
        {
            errors.Add($"{spec.DisplayName}: gun rig has no Ranged component.");
            return;
        }

        var serializedRanged = new SerializedObject(ranged);
        var reserveAmmo = GetObjectReference<Ammo>(serializedRanged, "m_ReserveAmmo");
        var firePoint = GetObjectReference<Transform>(serializedRanged, "m_FirePoint");
        var visualRoot = GetObjectReference<Transform>(serializedRanged, "m_VisualRoot");
        var reloadAction = serializedRanged.FindProperty("m_ReloadAction").objectReferenceValue;
        var muzzleFlash = GetObjectReference<ParticleSystem>(serializedRanged, "m_MuzzleFlash");
        var impactEffect = GetObjectReference<ParticleSystem>(serializedRanged, "m_ImpactEffect");
        var tracer = GetObjectReference<LineRenderer>(serializedRanged, "m_Tracer");
        var audioSource = GetObjectReference<AudioSource>(serializedRanged, "m_AudioSource");

        if (ranged.Definition != definition)
            errors.Add($"{spec.DisplayName}: Ranged component references the wrong profile.");
        if (reserveAmmo == null ||
            (definition != null && reserveAmmo.Type != definition.AmmoType))
        {
            errors.Add($"{spec.DisplayName}: reserve ammo is missing or has the wrong type.");
        }
        if (reloadAction == null)
            errors.Add($"{spec.DisplayName}: reload input action is not assigned.");
        if (firePoint == null || !firePoint.IsChildOf(root.transform))
            errors.Add($"{spec.DisplayName}: muzzle is missing or outside the rig.");
        else
        {
            if (Vector3.Angle(firePoint.forward, root.transform.forward) > 1f)
                errors.Add($"{spec.DisplayName}: muzzle does not point along weapon-forward (+Z).");
            if (grab != null && grab.attachTransform != null &&
                root.transform.InverseTransformPoint(firePoint.position).z <=
                root.transform.InverseTransformPoint(grab.attachTransform.position).z)
            {
                errors.Add($"{spec.DisplayName}: muzzle is not in front of the grip.");
            }
        }
        if (visualRoot == null || !visualRoot.IsChildOf(root.transform))
            errors.Add($"{spec.DisplayName}: recoil visual root is missing or outside the rig.");
        if (muzzleFlash == null || muzzleFlash.main.playOnAwake)
            errors.Add($"{spec.DisplayName}: muzzle flash is missing or plays on awake.");
        if (impactEffect == null || impactEffect.main.playOnAwake)
            errors.Add($"{spec.DisplayName}: impact effect is missing or plays on awake.");
        if (tracer == null || tracer.enabled || tracer.sharedMaterial == null)
            errors.Add($"{spec.DisplayName}: tracer is missing, active by default, or unmaterialed.");
        if (audioSource == null)
            errors.Add($"{spec.DisplayName}: Ranged feedback has no AudioSource reference.");

        if (!spec.IsReference && grab != null && grab.attachTransform != null &&
            Quaternion.Angle(grab.attachTransform.localRotation, Quaternion.Euler(0f, 180f, 0f)) > 1f)
        {
            errors.Add($"{spec.DisplayName}: gun grip frame does not match the controller-hand pose.");
        }
    }

    static void ValidateMeleeRig(
        WeaponSpec spec,
        GameObject root,
        WeaponDefinition definition,
        XRGrabInteractable grab,
        List<string> errors)
    {
        var melee = root.GetComponent<Melee>();
        if (melee == null)
        {
            errors.Add($"{spec.DisplayName}: melee rig has no Melee component.");
            return;
        }

        var serializedMelee = new SerializedObject(melee);
        var hitbox = GetObjectReference<MeleeHitbox>(serializedMelee, "m_Hitbox");
        var audioSource = GetObjectReference<AudioSource>(serializedMelee, "m_AudioSource");
        if (melee.Definition != definition)
            errors.Add($"{spec.DisplayName}: Melee component references the wrong profile.");
        if (hitbox == null || !hitbox.transform.IsChildOf(root.transform))
        {
            errors.Add($"{spec.DisplayName}: damaging hitbox is missing or outside the rig.");
        }
        else
        {
            var hitCollider = hitbox.HitCollider;
            if (hitCollider == null || !hitCollider.isTrigger || hitCollider.enabled)
                errors.Add($"{spec.DisplayName}: damage collider must be a disabled-by-default trigger.");

            var serializedHitbox = new SerializedObject(hitbox);
            if (GetObjectReference<Melee>(serializedHitbox, "m_Owner") != melee)
                errors.Add($"{spec.DisplayName}: MeleeHitbox references the wrong owner.");

            if (!spec.IsReference &&
                grab != null &&
                grab.attachTransform != null &&
                hitCollider != null &&
                root.transform.InverseTransformPoint(GetColliderCenter(hitCollider)).z <=
                root.transform.InverseTransformPoint(grab.attachTransform.position).z)
            {
                errors.Add($"{spec.DisplayName}: damage zone is not in front of the grip.");
            }
        }

        if (audioSource == null)
            errors.Add($"{spec.DisplayName}: Melee feedback has no AudioSource reference.");
        if (serializedMelee.FindProperty("m_MinimumHitSpeed").floatValue <= 0f)
            errors.Add($"{spec.DisplayName}: minimum physical hit speed must be positive.");
        if (!spec.IsReference && grab != null && grab.attachTransform != null &&
            Quaternion.Angle(grab.attachTransform.localRotation, Quaternion.identity) > 1f)
        {
            errors.Add($"{spec.DisplayName}: melee grip frame does not align its strike axis with the hand.");
        }
    }

    static T GetObjectReference<T>(SerializedObject serializedObject, string propertyName)
        where T : UnityEngine.Object
    {
        return serializedObject.FindProperty(propertyName).objectReferenceValue as T;
    }

    static Vector3 GetColliderCenter(Collider collider)
    {
        if (collider is BoxCollider box)
            return box.transform.TransformPoint(box.center);
        if (collider is SphereCollider sphere)
            return sphere.transform.TransformPoint(sphere.center);
        if (collider is CapsuleCollider capsule)
            return capsule.transform.TransformPoint(capsule.center);
        return collider.transform.position;
    }

    static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    static bool IsUsableBounds(Bounds bounds)
    {
        return IsFinitePositive(bounds.size.x) &&
            IsFinitePositive(bounds.size.y) &&
            IsFinitePositive(bounds.size.z);
    }

    static WeaponDefinition EnsureDefinition(
        string path,
        string weaponId,
        string displayName,
        WeaponType weaponType,
        int tier,
        float damage,
        float range,
        float attacksPerSecond,
        float noiseRange,
        AmmoType ammoType,
        int magazineCapacity,
        bool automatic)
    {
        var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
        if (definition != null)
            return definition;

        definition = ScriptableObject.CreateInstance<WeaponDefinition>();
        AssetDatabase.CreateAsset(definition, path);

        var serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("m_WeaponId").stringValue = weaponId;
        serializedDefinition.FindProperty("m_DisplayName").stringValue = displayName;
        serializedDefinition.FindProperty("m_WeaponType").enumValueIndex = (int)weaponType;
        serializedDefinition.FindProperty("m_Tier").intValue = tier;
        serializedDefinition.FindProperty("m_Damage").floatValue = damage;
        serializedDefinition.FindProperty("m_Range").floatValue = range;
        serializedDefinition.FindProperty("m_AttacksPerSecond").floatValue = attacksPerSecond;
        serializedDefinition.FindProperty("m_NoiseRange").floatValue = noiseRange;
        serializedDefinition.FindProperty("m_AmmoType").enumValueIndex = (int)ammoType;
        serializedDefinition.FindProperty("m_MagazineCapacity").intValue = magazineCapacity;
        serializedDefinition.FindProperty("m_Automatic").boolValue = automatic;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        return definition;
    }

    static void CreateScrapRevolverPrefab(WeaponDefinition definition)
    {
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(k_RevolverModelPath);
        if (modelAsset == null)
        {
            Debug.LogError("Could not create the Scrap Revolver: model asset is missing at " + k_RevolverModelPath);
            return;
        }

        var root = new GameObject("Scrap Revolver");
        try
        {
            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = 1.1f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "Visual";
            model.transform.SetParent(root.transform, false);
            OrientAndScaleAlongForward(root.transform, model.transform, 0.28f);

            var visualBounds = CalculateLocalBounds(root.transform, model.GetComponentsInChildren<Renderer>());
            model.transform.localPosition -= visualBounds.center;
            visualBounds = CalculateLocalBounds(root.transform, model.GetComponentsInChildren<Renderer>());

            var physicalCollider = root.AddComponent<BoxCollider>();
            physicalCollider.center = visualBounds.center;
            physicalCollider.size = visualBounds.size;

            var attach = CreateChild(root.transform, "Attach");
            attach.localPosition = new Vector3(
                0f,
                -visualBounds.extents.y * 0.3f,
                -visualBounds.extents.z * 0.2f);
            attach.localRotation = Quaternion.identity;

            var firePoint = CreateChild(root.transform, "Muzzle");
            firePoint.localPosition = new Vector3(
                0f,
                visualBounds.center.y + visualBounds.extents.y * 0.2f,
                visualBounds.max.z + 0.012f);

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.attachTransform = attach;
            grab.useDynamicAttach = false;
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;

            var reserveAmmo = root.AddComponent<Ammo>();
            reserveAmmo.Configure(AmmoType.Light, 18);

            var audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;

            var tracerMaterial = EnsureMaterial(
                k_TracerMaterialPath,
                "Universal Render Pipeline/Unlit",
                new Color(1f, 0.78f, 0.2f, 1f));
            var particleMaterial = EnsureMaterial(
                k_ParticleMaterialPath,
                "Universal Render Pipeline/Particles/Unlit",
                new Color(1f, 0.65f, 0.12f, 1f));

            var tracerObject = CreateChild(root.transform, "Tracer").gameObject;
            var tracer = tracerObject.AddComponent<LineRenderer>();
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.startWidth = 0.008f;
            tracer.endWidth = 0.002f;
            tracer.sharedMaterial = tracerMaterial;
            tracer.enabled = false;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.receiveShadows = false;

            var muzzleFlash = CreateBurstParticle(
                firePoint,
                "Muzzle Flash",
                particleMaterial,
                startSize: 0.045f,
                lifetime: 0.045f,
                particleCount: 4);

            var impactEffect = CreateBurstParticle(
                root.transform,
                "Impact Effect",
                particleMaterial,
                startSize: 0.025f,
                lifetime: 0.12f,
                particleCount: 6);

            var ranged = root.AddComponent<Ranged>();
            var serializedRanged = new SerializedObject(ranged);
            serializedRanged.FindProperty("m_Definition").objectReferenceValue = definition;
            serializedRanged.FindProperty("m_ReserveAmmo").objectReferenceValue = reserveAmmo;
            serializedRanged.FindProperty("m_GrabInteractable").objectReferenceValue = grab;
            serializedRanged.FindProperty("m_ReloadAction").objectReferenceValue = FindReloadActionReference();
            serializedRanged.FindProperty("m_FirePoint").objectReferenceValue = firePoint;
            serializedRanged.FindProperty("m_VisualRoot").objectReferenceValue = model.transform;
            serializedRanged.FindProperty("m_MuzzleFlash").objectReferenceValue = muzzleFlash;
            serializedRanged.FindProperty("m_ImpactEffect").objectReferenceValue = impactEffect;
            serializedRanged.FindProperty("m_Tracer").objectReferenceValue = tracer;
            serializedRanged.FindProperty("m_AudioSource").objectReferenceValue = audioSource;
            serializedRanged.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, k_ScrapRevolverPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void EnsureKnifePrefab(WeaponDefinition definition)
    {
        var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_KnifePrefabPath);
        if (sourcePrefab == null)
        {
            Debug.LogWarning("Knife prefab was not found at " + k_KnifePrefabPath);
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(k_KnifePrefabPath);
        try
        {
            var grab = root.GetComponent<XRGrabInteractable>();
            if (grab == null)
                grab = root.AddComponent<XRGrabInteractable>();

            var attach = root.transform.Find("handle_anchor");
            if (attach == null)
                attach = CreateChild(root.transform, "handle_anchor");
            // The knife mesh extends along local +Y. Rotating the attach frame -90 degrees
            // makes the blade extend along the controller-hand's +Z grip direction. The
            // 18 mm offset seats the handle inside the low-poly controller hand's palm.
            attach.SetLocalPositionAndRotation(
                new Vector3(0f, 0.018f, 0f),
                Quaternion.Euler(-90f, 0f, 0f));
            grab.attachTransform = attach;
            ConfigureHeldPhysics(root, grab);

            var audioSource = root.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = root.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
            }

            var melee = root.GetComponent<Melee>();
            if (melee == null)
                melee = root.AddComponent<Melee>();

            var hitbox = root.GetComponentInChildren<MeleeHitbox>(true);
            if (hitbox == null)
            {
                var hitboxTransform = CreateChild(root.transform, "Damage Hitbox");
                var box = hitboxTransform.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;

                var bladeRenderer = root.GetComponentsInChildren<Renderer>()
                    .FirstOrDefault(renderer =>
                        renderer.name.IndexOf("blade", StringComparison.OrdinalIgnoreCase) >= 0);
                var bladeBounds = bladeRenderer != null
                    ? CalculateLocalBounds(root.transform, new[] { bladeRenderer })
                    : CalculateLocalBounds(root.transform, root.GetComponentsInChildren<Renderer>());

                box.center = bladeBounds.center;
                box.size = bladeBounds.size;
                box.enabled = false;

                hitbox = hitboxTransform.gameObject.AddComponent<MeleeHitbox>();
                var serializedHitbox = new SerializedObject(hitbox);
                serializedHitbox.FindProperty("m_Owner").objectReferenceValue = melee;
                serializedHitbox.FindProperty("m_HitCollider").objectReferenceValue = box;
                serializedHitbox.ApplyModifiedPropertiesWithoutUndo();
            }

            var serializedMelee = new SerializedObject(melee);
            serializedMelee.FindProperty("m_Definition").objectReferenceValue = definition;
            serializedMelee.FindProperty("m_GrabInteractable").objectReferenceValue = grab;
            serializedMelee.FindProperty("m_Hitbox").objectReferenceValue = hitbox;
            serializedMelee.FindProperty("m_MinimumHitSpeed").floatValue = 1f;
            serializedMelee.FindProperty("m_AudioSource").objectReferenceValue = audioSource;
            serializedMelee.ApplyModifiedPropertiesWithoutUndo();

            var hitCollider = hitbox.HitCollider != null
                ? hitbox.HitCollider
                : hitbox.GetComponent<Collider>();
            if (hitCollider != null)
            {
                hitCollider.isTrigger = true;
                hitCollider.enabled = false;
            }

            PrefabUtility.SaveAsPrefabAsset(root, k_KnifePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void TuneScrapRevolverPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(k_ScrapRevolverPrefabPath);
        try
        {
            var grab = root.GetComponent<XRGrabInteractable>();
            if (grab == null)
                throw new InvalidOperationException("The Scrap Revolver prefab has no grab interactable.");

            var attach = root.transform.Find("Attach");
            if (attach == null)
                attach = CreateChild(root.transform, "Attach");

            // The controller grip frame points opposite the revolver root's authored forward
            // direction. Rotate the attach frame 180 degrees and move it 18 mm deeper so the
            // handle sits inside the low-poly hand rather than floating in front of the palm.
            attach.SetLocalPositionAndRotation(
                new Vector3(0f, -0.029f, -0.046f),
                Quaternion.Euler(0f, 180f, 0f));
            grab.attachTransform = attach;
            ConfigureHeldPhysics(root, grab);

            var muzzle = root.transform.Find("Muzzle");
            if (muzzle == null)
                throw new InvalidOperationException("The Scrap Revolver prefab has no muzzle transform.");
            muzzle.localRotation = Quaternion.identity;

            PrefabUtility.SaveAsPrefabAsset(root, k_ScrapRevolverPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ConfigureHeldPhysics(GameObject root, XRGrabInteractable grab)
    {
        grab.useDynamicAttach = false;
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.attachEaseInTime = 0.05f;
        grab.smoothPosition = false;
        grab.smoothRotation = false;
        grab.throwOnDetach = true;

        var body = root.GetComponent<Rigidbody>();
        if (body == null)
            body = root.AddComponent<Rigidbody>();
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    static InputActionReference FindReloadActionReference()
    {
        return AssetDatabase.LoadAllAssetsAtPath(k_InputActionsPath)
            .OfType<InputActionReference>()
            .FirstOrDefault(reference =>
                reference.action != null &&
                reference.action.name == "Reload" &&
                reference.action.actionMap != null &&
                reference.action.actionMap.name == "XRI Right Interaction");
    }

    static Material EnsureMaterial(string path, string shaderName, Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        var shader = Shader.Find(shaderName) ??
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Sprites/Default");
        material = new Material(shader)
        {
            color = color,
            name = System.IO.Path.GetFileNameWithoutExtension(path),
        };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static ParticleSystem CreateBurstParticle(
        Transform parent,
        string name,
        Material material,
        float startSize,
        float lifetime,
        short particleCount)
    {
        var particleObject = CreateChild(parent, name).gameObject;
        var particles = particleObject.AddComponent<ParticleSystem>();

        var main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = Mathf.Max(0.01f, lifetime);
        main.startLifetime = lifetime;
        main.startSpeed = 0.75f;
        main.startSize = startSize;
        main.maxParticles = particleCount;

        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, particleCount) });

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 16f;
        shape.radius = 0.002f;

        var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particles;
    }

    static void OrientAndScaleAlongForward(Transform root, Transform visual, float targetLength)
    {
        var bounds = CalculateLocalBounds(root, visual.GetComponentsInChildren<Renderer>());
        var size = bounds.size;

        if (size.x >= size.y && size.x >= size.z)
            visual.localRotation = Quaternion.Euler(0f, -90f, 0f);
        else if (size.y >= size.x && size.y >= size.z)
            visual.localRotation = Quaternion.Euler(90f, 0f, 0f);

        bounds = CalculateLocalBounds(root, visual.GetComponentsInChildren<Renderer>());
        var currentLength = Mathf.Max(bounds.size.z, 0.0001f);
        visual.localScale *= targetLength / currentLength;
    }

    static Bounds CalculateLocalBounds(Transform root, Renderer[] renderers)
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
                var worldPoint = bounds.center + Vector3.Scale(
                    bounds.extents,
                    new Vector3(x, y, z));
                var localPoint = root.InverseTransformPoint(worldPoint);

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

    static Transform CreateChild(Transform parent, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        var folderName = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
