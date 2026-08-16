using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Recreates missing reference weapon assets from the Tools menu.
/// Existing definition tuning is preserved; reference prefabs receive the canonical grip pose.
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
            // makes the blade extend along the controller-hand's +Z grip direction.
            attach.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(-90f, 0f, 0f));
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
            serializedMelee.FindProperty("m_AudioSource").objectReferenceValue = audioSource;
            serializedMelee.ApplyModifiedPropertiesWithoutUndo();

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

            // Controller-mode hands use the tracked grip frame directly. The old 180-degree
            // controller-model correction made the revolver face backward in the new hand mesh.
            attach.SetLocalPositionAndRotation(
                new Vector3(0f, -0.029f, -0.028f),
                Quaternion.identity);
            grab.attachTransform = attach;
            ConfigureHeldPhysics(root, grab);

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
