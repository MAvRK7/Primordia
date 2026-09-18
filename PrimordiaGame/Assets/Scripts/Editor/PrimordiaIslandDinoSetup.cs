using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Authors a repeatable dinosaur population on the island's baked navigation mesh.</summary>
public static class PrimordiaIslandDinoSetup
{
    const string ScenePath = "Assets/Scenes/assembly_scene.unity";
    const string RootName = "Island Dinosaurs";
    const string PrefabFolder = "Assets/Prefabs/Dinos/Regular dinos/";
    const float CampRadius = 60f;
    const float PredatorCampRadius = 110f;
    const float Spacing = 35f;

    [MenuItem("Tools/Primordia/Scene/Populate Island Dinosaurs")]
    public static void PopulateIslandDinosaurs()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before populating the island.");

        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        if (scene.isDirty)
            throw new InvalidOperationException("Save your island edits before rebuilding its dinosaur population.");

        var objects = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        var terrain = objects.Select(item => item.GetComponent<Terrain>()).Single(item => item != null);
        var camp = objects.Single(item => item.name == "campfire Site").position;
        var surface = terrain.GetComponent<NavMeshSurface>();
        if (surface == null || surface.navMeshData == null)
            throw new InvalidOperationException("Bake the island's dinosaur navigation mesh first.");
        surface.AddData();

        // Interleave species so the large animals and predators are distributed across the map.
        var species = new[] {
            "Parasaurolophus", "Triceratops", "Stegosaurus", "Apatosaurus", "Velociraptor",
            "Parasaurolophus", "Triceratops", "Stegosaurus", "Apatosaurus", "Velociraptor",
            "Parasaurolophus", "Triceratops", "Stegosaurus", "Apatosaurus", "Velociraptor",
            "Parasaurolophus", "Triceratops", "Stegosaurus", "Apatosaurus", "Velociraptor",
            "Parasaurolophus", "Triceratops", "Stegosaurus", "Velociraptor", "Trex",
            "Parasaurolophus", "Triceratops", "Stegosaurus", "Parasaurolophus", "Parasaurolophus"
        };
        var prefabs = species.Distinct().ToDictionary(name => name,
            name => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab"));
        foreach (var pair in prefabs)
        {
            if (pair.Value == null || pair.Value.GetComponent<DinoAI>()?.profile == null ||
                pair.Value.GetComponent<NavMeshAgent>() == null)
                throw new InvalidOperationException($"{pair.Key} needs a prefab, AI profile, and navigation agent.");
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Populate island dinosaurs");
        var oldRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == RootName);
        var population = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(population, scene);
        Undo.RegisterCreatedObjectUndo(population, "Create island dinosaur population");
        // Keep the previous layout available until the entire replacement can be placed.
        if (oldRoot != null)
        {
            Undo.RecordObject(oldRoot, "Replace previous dinosaur population");
            oldRoot.SetActive(false);
        }

        try
        {
            Physics.SyncTransforms();
            var random = new System.Random(1847);
            var points = new List<Vector3>();
            var radii = new List<float>();
            var filter = new NavMeshQueryFilter { agentTypeID = surface.agentTypeID, areaMask = NavMesh.AllAreas };
            var candidates = CollectCandidates(terrain, camp, filter, random);
            Debug.Log($"Island population: terrain {terrain.terrainData.size}, {candidates.Count} dry walkable candidates.");

            foreach (string name in species)
            {
                var prefab = prefabs[name];
                var agent = prefab.GetComponent<NavMeshAgent>();
                if (agent.agentTypeID != surface.agentTypeID)
                    throw new InvalidOperationException($"{name} does not use the island's navigation agent type.");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(population.transform, false);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                // Use the visible footprint too: several source prefabs have very small agent radii.
                var bounds = new Bounds(instance.transform.position, Vector3.zero);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                    bounds.Encapsulate(renderer.bounds);
                float radius = Mathf.Max(agent.radius, new Vector2(
                    Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)),
                    Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z))).magnitude);
                var rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                instance.transform.rotation = rotation;
                var behaviour = prefab.GetComponent<DinoAI>().profile.behaviour;
                bool predator = behaviour == DinoBehaviour.PredatorHuntsPlayer ||
                    behaviour == DinoBehaviour.PredatorHuntsHerbivores;
                float campBuffer = (predator ? PredatorCampRadius : CampRadius) + radius;

                // The first encounter is just outside camp; subsequent animals fill the island
                // by preferring the point farthest from the existing population.
                var ordered = candidates.OrderByDescending(point => points.Count == 0
                    ? -PlanarDistance(point, camp)
                    : points.Min(other => PlanarDistance(point, other)));
                bool placed = false;
                foreach (var point in ordered)
                {
                    if (PlanarDistance(point, camp) < campBuffer) continue;
                    if (points.Where((other, index) => PlanarDistance(point, other) <
                        Mathf.Max(Spacing, radius + radii[index] + 5f)).Any()) continue;
                    if (!NavMesh.FindClosestEdge(point, out var edge, filter) || edge.distance < agent.radius) continue;
                    // Ignore terrain itself, but reject rocks, buildings, trees and other solid props.
                    var overlaps = Physics.OverlapBox(point + rotation * bounds.center,
                        bounds.extents, rotation, ~0, QueryTriggerInteraction.Ignore);
                    if (overlaps.Any(hit => !(hit is TerrainCollider) &&
                        !hit.transform.IsChildOf(instance.transform))) continue;

                    instance.transform.position = point;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    points.Add(point);
                    radii.Add(radius);
                    Physics.SyncTransforms();
                    Debug.Log($"Island dinosaur: {name} at {point}, camp distance {PlanarDistance(point, camp):F1}m, footprint {radius:F1}m.");
                    placed = true;
                    break;
                }
                if (!placed)
                    throw new InvalidOperationException($"No clear island position for {name} (bounds {bounds.size}, footprint {radius:F1}m); previous population preserved.");
            }

            var manager = population.AddComponent<DinoAIManager>();
            manager.activeDistance = 100f;
            manager.maxActive = 12;
            manager.senseFrameBuckets = 4;
            if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = population;
            Debug.Log($"Island demo ready: {points.Count} dinosaurs, {CampRadius}m campsite exclusion, {PredatorCampRadius}m predator exclusion.", population);
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
    }

    static List<Vector3> CollectCandidates(Terrain terrain, Vector3 camp,
        NavMeshQueryFilter filter, System.Random random)
    {
        var result = new List<Vector3>();
        var origin = terrain.transform.position;
        var size = terrain.terrainData.size;
        // Sample throughout the island, excluding its flat sea floor and steep slopes.
        for (float x = 15f; x < size.x - 15f; x += 15f)
        for (float z = 15f; z < size.z - 15f; z += 15f)
        {
            var point = origin + new Vector3(x + (float)random.NextDouble() * 8f - 4f,
                0f, z + (float)random.NextDouble() * 8f - 4f);
            point.y = terrain.SampleHeight(point) + origin.y;
            if (point.y < origin.y + 2f || PlanarDistance(point, camp) < CampRadius) continue;
            if (terrain.terrainData.GetSteepness((point.x - origin.x) / size.x,
                (point.z - origin.z) / size.z) > 30f) continue;
            if (!NavMesh.SamplePosition(point, out var hit, 2f, filter)) continue;
            // Reject navmesh on rooftops/props and recheck land height after snapping.
            float ground = terrain.SampleHeight(hit.position) + origin.y;
            if (ground < origin.y + 2f || Mathf.Abs(hit.position.y - ground) > 1f) continue;
            result.Add(hit.position);
        }
        return result;
    }

    static float PlanarDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
