using System;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Builds the small, repeatable weapon test range used by SampleScene.
/// Only objects below the owned "Weapon Test Range" root are replaced.
/// </summary>
public static class PrimordiaSampleSceneSetup
{
    const string k_ScenePath = "Assets/Scenes/SampleScene.unity";
    const string k_RevolverPrefabPath = "Assets/Prefabs/Weapons/Scrap Revolver.prefab";
    const string k_KnifePrefabPath = "Assets/Prefabs/Knife.prefab";
    const string k_RaptorModelPath = "Assets/DinoModels/Velociraptor/Velociraptor.fbx";
    const string k_RaptorControllerPath = "Assets/Animation/RaptorAnimator.controller";
    const string k_TestProfilePath = "Assets/Data/Dinosaurs/Training Raptor.asset";
    const string k_TestRaptorPrefabPath = "Assets/Prefabs/Dinosaurs/Training Raptor.prefab";
    const string k_TableMaterialPath = "Assets/Data/Weapons/Weapon Test Table.mat";
    const string k_BackstopMaterialPath = "Assets/Data/Weapons/Range Backstop.mat";
    const string k_TestRootName = "Weapon Test Range";

    [MenuItem("Tools/Primordia/Weapons/Build Sample Scene Test Range")]
    public static void BuildSampleSceneTestRange()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var revolverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_RevolverPrefabPath);
        var knifePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_KnifePrefabPath);
        if (revolverPrefab == null || knifePrefab == null)
            throw new InvalidOperationException("Create the reference weapons before building the test range.");

        var profile = EnsureTrainingRaptorProfile();
        var raptorPrefab = EnsureTrainingRaptorPrefab(profile);
        if (raptorPrefab == null)
            throw new InvalidOperationException("Could not create the Training Raptor prefab.");

        var scene = EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);
        var player = FindPlayerRoot(scene);
        if (player == null)
            throw new InvalidOperationException("SampleScene has no Player root.");

        EnsurePlayerCombatComponents(player);
        EnsureBodyInventoryRig(player);

        var oldRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == k_TestRootName);
        if (oldRoot != null)
            UnityEngine.Object.DestroyImmediate(oldRoot);

        var testRoot = new GameObject(k_TestRootName);
        SceneManager.MoveGameObjectToScene(testRoot, scene);

        var tableMaterial = EnsureMaterial(k_TableMaterialPath, new Color(0.18f, 0.12f, 0.07f));
        var backstopMaterial = EnsureMaterial(k_BackstopMaterialPath, new Color(0.14f, 0.16f, 0.18f));

        var flatForward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        if (flatForward.sqrMagnitude < 0.5f)
            flatForward = Vector3.forward;
        var right = Vector3.Cross(Vector3.up, flatForward).normalized;

        var tablePosition = player.transform.position + flatForward * 1.45f;
        CreateBlock(
            testRoot.transform,
            "Weapon Table",
            tablePosition + Vector3.up * 0.38f,
            Quaternion.LookRotation(flatForward),
            new Vector3(1.6f, 0.76f, 0.7f),
            tableMaterial);

        var existingKnife = UnityEngine.Object.FindObjectsByType<Melee>(FindObjectsSortMode.None)
            .Select(melee => melee.gameObject)
            .FirstOrDefault();
        if (existingKnife == null)
            existingKnife = InstantiatePrefab(knifePrefab, scene);
        existingKnife.name = "Test Knife";
        existingKnife.transform.SetParent(testRoot.transform, true);
        PlacePickup(
            existingKnife.transform,
            tablePosition - right * 0.38f + Vector3.up * 0.86f,
            Quaternion.LookRotation(flatForward) * Quaternion.Euler(0f, 0f, 90f));

        var revolver = InstantiatePrefab(revolverPrefab, scene);
        revolver.name = "Test Scrap Revolver";
        revolver.transform.SetParent(testRoot.transform, true);
        PlacePickup(
            revolver.transform,
            tablePosition + right * 0.38f + Vector3.up * 0.88f,
            Quaternion.LookRotation(flatForward));

        var rangeDirection = flatForward;
        var raptorPosition = player.transform.position + rangeDirection * 6.5f;
        var raptor = InstantiatePrefab(raptorPrefab, scene);
        raptor.name = "Training Raptor";
        raptor.transform.SetParent(testRoot.transform, true);
        raptor.transform.SetPositionAndRotation(
            raptorPosition + Vector3.up * 0.02f,
            Quaternion.LookRotation(-rangeDirection));

        var playerTarget = Camera.main != null ? Camera.main.transform : player.transform;
        var raptorAi = raptor.GetComponent<DinoAI>();
        raptorAi.player = playerTarget;

        CreateBlock(
            testRoot.transform,
            "Range Backstop",
            player.transform.position + rangeDirection * 10f + Vector3.up * 1.5f,
            Quaternion.LookRotation(rangeDirection),
            new Vector3(5f, 3f, 0.3f),
            backstopMaterial);

        var navMeshSurface = EnsureNavigationSurface(scene);
        navMeshSurface.BuildNavMesh();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("SampleScene weapon range is ready: revolver, knife, and Training Raptor.");
    }

    [MenuItem("Tools/Primordia/Weapons/Validate Sample Scene Test Range")]
    public static void ValidateSampleSceneTestRange()
    {
        var scene = EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);
        var player = FindPlayerRoot(scene);
        Require(player != null, "SampleScene has no Player root.");
        Require(player.GetComponent<PlayerHealth>() != null, "PlayerHealth is missing from the Player root.");
        Require(player.GetComponent<PlayerNoise>() != null, "PlayerNoise is missing from the Player root.");
        var bodyInventory = player.GetComponentInChildren<BodyInventoryRig>(true);
        Require(bodyInventory != null,
            "The body inventory is not using head-relative tracking.");
        ValidateBodyInventory(bodyInventory, player.transform);

        var testRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == k_TestRootName);
        Require(testRoot != null, "The Weapon Test Range root is missing.");

        var objects = testRoot.GetComponentsInChildren<Transform>(true)
            .Select(item => item.gameObject)
            .ToArray();
        var knife = objects.FirstOrDefault(item => item.name == "Test Knife");
        var revolver = objects.FirstOrDefault(item => item.name == "Test Scrap Revolver");
        var raptor = objects.FirstOrDefault(item => item.name == "Training Raptor");
        Require(objects.Any(item => item.name == "Weapon Table"), "The weapon table is missing.");
        Require(objects.Any(item => item.name == "Range Backstop"), "The range backstop is missing.");
        Require(knife != null && knife.GetComponent<Melee>() != null, "The test knife is missing or not configured.");
        Require(revolver != null && revolver.GetComponent<Ranged>() != null,
            "The test revolver is missing or not configured.");
        ValidateHeldWeapon(knife, expectBladeAlongGrip: true);
        ValidateHeldWeapon(revolver, expectBladeAlongGrip: false);
        Require(raptor != null, "The Training Raptor is missing.");

        var raptorAi = raptor.GetComponent<DinoAI>();
        var raptorAgent = raptor.GetComponent<NavMeshAgent>();
        Require(raptor.GetComponent<DinoHealth>() != null, "The Training Raptor has no health component.");
        Require(raptorAgent != null, "The Training Raptor has no navigation agent.");
        Require(raptorAi != null && raptorAi.profile != null && raptorAi.player != null,
            "The Training Raptor AI is not fully configured.");
        Require(raptorAi.player == player.transform || raptorAi.player.IsChildOf(player.transform),
            "The Training Raptor is not targeting the Player hierarchy.");
        ValidateRaptorClearance(raptor, raptorAi, raptorAgent);

        var surface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
        Require(surface != null && surface.navMeshData != null, "SampleScene has no baked navigation mesh.");
        Require(NavMesh.CalculateTriangulation().vertices.Length > 0, "The baked navigation mesh is empty.");

        Debug.Log("SampleScene weapon range validation passed.");
    }

    static DinoProfile EnsureTrainingRaptorProfile()
    {
        EnsureFolder("Assets/Data");
        EnsureFolder("Assets/Data/Dinosaurs");

        var profile = AssetDatabase.LoadAssetAtPath<DinoProfile>(k_TestProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<DinoProfile>();
            AssetDatabase.CreateAsset(profile, k_TestProfilePath);
        }

        profile.behaviour = DinoBehaviour.PredatorHuntsPlayer;
        profile.primaryTarget = DinoProfile.TargetType.Player;
        profile.secondaryTarget = DinoProfile.TargetType.None;
        profile.sightRange = 18f;
        profile.sightAngle = 80f;
        profile.hearingRadius = 30f;
        profile.noiseMemory = 5f;
        profile.giveUpTimer = 7f;
        profile.leashRange = 14f;
        // The model is roughly 4.4 metres nose-to-tail. Combined with the agent and
        // player radii this keeps its head outside the player's body during attacks.
        profile.attackRange = 1.8f;
        profile.attackCooldown = 1.8f;
        profile.attackDamage = 10f;
        profile.moveSpeed = 3.25f;
        profile.health = 100f;
        profile.xpReward = 0;
        EditorUtility.SetDirty(profile);
        return profile;
    }

    static GameObject EnsureTrainingRaptorPrefab(DinoProfile profile)
    {
        EnsureFolder("Assets/Prefabs/Dinosaurs");

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(k_TestRaptorPrefabPath);
        if (existing != null)
            return existing;

        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(k_RaptorModelPath);
        if (modelAsset == null)
            return null;

        var root = new GameObject("Training Raptor");
        try
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            NormalizeVisual(root.transform, visual.transform, 1.8f);

            var animator = visual.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(k_RaptorControllerPath);
                animator.applyRootMotion = false;
            }

            var bounds = CalculateLocalBounds(root.transform, visual.GetComponentsInChildren<Renderer>());
            var collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;

            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = Mathf.Clamp(Mathf.Min(bounds.extents.x, bounds.extents.z), 0.3f, 0.55f);
            agent.height = Mathf.Max(1.4f, bounds.size.y);
            agent.baseOffset = 0f;
            agent.speed = profile.moveSpeed;
            agent.angularSpeed = 240f;
            agent.acceleration = 12f;
            agent.stoppingDistance = profile.attackRange;

            var health = root.AddComponent<DinoHealth>();
            health.profile = profile;

            var ai = root.AddComponent<DinoAI>();
            ai.profile = profile;
            ai.wanderRadius = 4f;
            ai.wanderPause = 2.5f;
            ai.wanderSpeed = 1.5f;

            return PrefabUtility.SaveAsPrefabAsset(root, k_TestRaptorPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void NormalizeVisual(Transform root, Transform visual, float targetHeight)
    {
        var bounds = CalculateLocalBounds(root, visual.GetComponentsInChildren<Renderer>());
        visual.localScale *= targetHeight / Mathf.Max(bounds.size.y, 0.001f);
        bounds = CalculateLocalBounds(root, visual.GetComponentsInChildren<Renderer>());
        visual.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
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
                var point = root.InverseTransformPoint(
                    bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                if (!hasPoint)
                {
                    localBounds = new Bounds(point, Vector3.zero);
                    hasPoint = true;
                }
                else
                {
                    localBounds.Encapsulate(point);
                }
            }
        }

        return localBounds;
    }

    static void EnsurePlayerCombatComponents(GameObject player)
    {
        if (player.GetComponent<PlayerHealth>() == null)
            player.AddComponent<PlayerHealth>();
        if (player.GetComponent<PlayerNoise>() == null)
            player.AddComponent<PlayerNoise>();
    }

    static void EnsureBodyInventoryRig(GameObject player)
    {
        var inventory = player.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(item => item.name == "InventoryRig");
        if (inventory == null)
            throw new InvalidOperationException("SampleScene has no InventoryRig below the Player.");

        var bodyRig = inventory.GetComponent<BodyInventoryRig>();
        if (bodyRig == null)
            bodyRig = inventory.gameObject.AddComponent<BodyInventoryRig>();

        var playerCamera = player.GetComponentInChildren<Camera>(true);
        if (playerCamera == null)
            throw new InvalidOperationException("SampleScene has no player camera for body inventory tracking.");
        bodyRig.ConfigureFromHierarchy(playerCamera.transform);
        EditorUtility.SetDirty(bodyRig);
    }

    static NavMeshSurface EnsureNavigationSurface(Scene scene)
    {
        var surface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
        if (surface != null)
            return surface;

        var ground = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Plane");
        if (ground == null)
            throw new InvalidOperationException("SampleScene has no Plane for navigation.");

        surface = ground.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.layerMask = ~0;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.ignoreNavMeshAgent = true;
        surface.ignoreNavMeshObstacle = true;
        return surface;
    }

    static GameObject FindPlayerRoot(Scene scene)
    {
        return scene.GetRootGameObjects()
            .FirstOrDefault(root => root.name == "Player");
    }

    static GameObject InstantiatePrefab(GameObject prefab, Scene scene)
    {
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
    }

    static void PlacePickup(Transform pickup, Vector3 position, Quaternion rotation)
    {
        pickup.SetPositionAndRotation(position, rotation);
        var body = pickup.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    static GameObject CreateBlock(
        Transform parent,
        string name,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        Material material)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, true);
        block.transform.SetPositionAndRotation(position, rotation);
        block.transform.localScale = scale;

        block.GetComponent<Renderer>().sharedMaterial = material;
        return block;
    }

    static Material EnsureMaterial(string path, Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader)
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path),
            };
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
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

    static void ValidateHeldWeapon(GameObject weapon, bool expectBladeAlongGrip)
    {
        var grab = weapon.GetComponent<XRGrabInteractable>();
        var body = weapon.GetComponent<Rigidbody>();
        Require(grab != null && grab.attachTransform != null,
            $"{weapon.name} has no configured hand attach pose.");
        Require(grab.movementType == XRBaseInteractable.MovementType.Kinematic,
            $"{weapon.name} is not using smooth kinematic hand tracking.");
        Require(body != null && body.interpolation == RigidbodyInterpolation.Interpolate,
            $"{weapon.name} does not interpolate its held physics pose.");
        Require(body.collisionDetectionMode == CollisionDetectionMode.ContinuousSpeculative,
            $"{weapon.name} is not using kinematic-safe continuous collision detection.");

        if (expectBladeAlongGrip)
        {
            Require(Vector3.Angle(grab.attachTransform.forward, weapon.transform.up) < 1f,
                "The knife blade is not aligned with the hand's forward grip direction.");
        }
        else
        {
            Require(Quaternion.Angle(grab.attachTransform.localRotation, Quaternion.identity) < 1f,
                "The revolver still contains the old controller-model rotation offset.");
        }
    }

    static void ValidateBodyInventory(BodyInventoryRig bodyInventory, Transform player)
    {
        Require(bodyInventory.Head != null && bodyInventory.Head.IsChildOf(player),
            "The body inventory is not tracking the player camera.");

        var sockets = bodyInventory.GetComponentsInChildren<Transform>(true);
        var backLeft = sockets.FirstOrDefault(item => item.name == "BackLeft");
        var backRight = sockets.FirstOrDefault(item => item.name == "BackRight");
        var hipLeft = sockets.FirstOrDefault(item => item.name == "HipLeft");
        var hipRight = sockets.FirstOrDefault(item => item.name == "HipRight");
        Require(backLeft != null && backRight != null && hipLeft != null && hipRight != null,
            "One or more body inventory sockets are missing.");
        Require(backLeft.GetComponent<XRSocketInteractor>() != null &&
            backRight.GetComponent<XRSocketInteractor>() != null &&
            hipLeft.GetComponent<XRSocketInteractor>() != null &&
            hipRight.GetComponent<XRSocketInteractor>() != null,
            "One or more body inventory anchors are not socket interactors.");
        Require(backLeft.localPosition.x < 0f && backRight.localPosition.x > 0f &&
            hipLeft.localPosition.x < 0f && hipRight.localPosition.x > 0f,
            "The body inventory left/right anchors are crossed.");
        Require(backLeft.localPosition.y > hipLeft.localPosition.y &&
            backRight.localPosition.y > hipRight.localPosition.y,
            "The shoulder and hip inventory rows are vertically misaligned.");
        Require(Vector3.Angle(backLeft.forward, Vector3.down) < 1f &&
            Vector3.Angle(hipRight.forward, Vector3.down) < 1f,
            "Holstered items are not oriented down along the player's body.");
    }

    static void ValidateRaptorClearance(GameObject raptor, DinoAI ai, NavMeshAgent agent)
    {
        var playerController = ai.player.GetComponentInParent<CharacterController>();
        var raptorCollider = raptor.GetComponent<Collider>();
        Require(playerController != null, "The Training Raptor target has no player collision radius.");
        Require(raptorCollider != null, "The Training Raptor has no collision volume.");

        var forward = raptor.transform.forward;
        float colliderForwardExtent;
        if (raptorCollider is BoxCollider box)
        {
            var halfSize = box.size * 0.5f;
            colliderForwardExtent =
                Mathf.Abs(Vector3.Dot(forward,
                    box.transform.TransformVector(Vector3.right * halfSize.x))) +
                Mathf.Abs(Vector3.Dot(forward,
                    box.transform.TransformVector(Vector3.up * halfSize.y))) +
                Mathf.Abs(Vector3.Dot(forward,
                    box.transform.TransformVector(Vector3.forward * halfSize.z)));
        }
        else
        {
            colliderForwardExtent = raptorCollider.bounds.extents.magnitude;
        }
        var attackCentreDistance =
            ai.profile.attackRange + agent.radius + playerController.radius;
        Require(attackCentreDistance >= colliderForwardExtent + playerController.radius + 0.05f,
            $"The Training Raptor attack spacing still overlaps the player's collision volume " +
            $"({attackCentreDistance:0.00}m stop, {colliderForwardExtent + playerController.radius:0.00}m needed).");
    }

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
