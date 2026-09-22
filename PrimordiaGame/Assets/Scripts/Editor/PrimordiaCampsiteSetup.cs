using System;
using System.Collections.Generic;
using System.Linq;
using Primordia;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using Unity.XR.CoreUtils;

/// <summary>Packages the existing SampleScene player and connects the campsite spawn.</summary>
public static class PrimordiaCampsiteSetup
{
    const string SourceScene = "Assets/Scenes/SampleScene.unity";
    const string GameplayScene = "Assets/Scenes/assembly_scene.unity";
    const string PlayerPrefab = "Assets/Prefabs/Primordia Player.prefab";

    [MenuItem("Tools/Primordia/Scene/Set Up Campsite Player")]
    public static void SetUpCampsitePlayer()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before setting up the campsite.");

        var scene = SceneManager.GetSceneByPath(GameplayScene);
        if (!scene.IsValid() || !scene.isLoaded)
            scene = EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Additive);

        var objects = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        var spawn = objects.Single(item => item.name == "PlayerSpawn");
        var campfire = objects.Single(item => item.name == "campfire Site");
        var prefab = ExportPlayerPrefab();

        // Stand four metres in front of the fire, on the terrain rather than at
        // the old marker's fixed height. Face into camp on the first frame.
        var position = campfire.position + Vector3.back * 4f;
        var terrain = objects.Select(item => item.GetComponent<Terrain>())
            .FirstOrDefault(item => item != null && Contains(item, position));
        if (terrain == null)
            throw new InvalidOperationException("No terrain found beneath the campsite spawn.");
        position.y = terrain.SampleHeight(position) + terrain.transform.position.y + 0.1f;
        var facing = Vector3.ProjectOnPlane(campfire.position - position, Vector3.up);

        Undo.RecordObject(spawn, "Move player spawn to campsite");
        spawn.SetPositionAndRotation(position, Quaternion.LookRotation(facing));
        var spawner = spawn.GetComponent<PlayerSpawnPoint>();
        if (spawner == null)
            spawner = Undo.AddComponent<PlayerSpawnPoint>(spawn.gameObject);
        var serializedSpawner = new SerializedObject(spawner);
        serializedSpawner.FindProperty("m_PlayerPrefab").objectReferenceValue = prefab;
        serializedSpawner.ApplyModifiedProperties();

        // The map's placeholder camera must not compete with the spawned XR camera.
        foreach (var camera in objects.Select(item => item.GetComponent<Camera>())
                     .Where(item => item != null && item.CompareTag("MainCamera")))
        {
            Undo.RecordObject(camera.gameObject, "Disable map placeholder camera");
            camera.gameObject.SetActive(false);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = spawn.gameObject;
        Debug.Log($"Campsite player ready: {PlayerPrefab}, spawn {position}, four metres from campfire.", spawner);
    }

    static GameObject ExportPlayerPrefab()
    {
        // A preview scene keeps the user's open scene and the source asset untouched.
        var source = EditorSceneManager.OpenPreviewScene(SourceScene);
        try
        {
            var roots = source.GetRootGameObjects();
            var player = roots.Single(root => root.name == "Player");
            var origin = player.GetComponentInChildren<XROrigin>(true);
            if (origin == null ||
                player.GetComponentInChildren<BodyInventoryRig>(true) == null ||
                player.GetComponentInChildren<ControllerHandVisual>(true) == null)
                throw new InvalidOperationException("SampleScene player is missing its configured XR rig, holsters, or controller hands.");

            var included = new HashSet<GameObject> { player };
            foreach (var root in roots)
            {
                if (root.GetComponentInChildren<XRInteractionManager>(true) != null ||
                    root.GetComponentInChildren<EventSystem>(true) != null ||
                    root.GetComponentInChildren<InputActionManager>(true) != null ||
                    root.GetComponentInChildren<PlayerRigController>(true) != null)
                    included.Add(root);
            }

            var container = new GameObject("Primordia Player");
            SceneManager.MoveGameObjectToScene(container, source);
            container.transform.SetPositionAndRotation(player.transform.position, player.transform.rotation);
            foreach (var root in included)
                root.transform.SetParent(container.transform, true);
            player.tag = "Player";
            foreach (var controller in container.GetComponentsInChildren<PlayerRigController>(true))
            {
                var serializedController = new SerializedObject(controller);
                serializedController.FindProperty("m_XROrigin").objectReferenceValue = origin;
                serializedController.ApplyModifiedPropertiesWithoutUndo();
            }

            // Scene-only references would be lost when Unity saves a prefab.
            // Report them before saving so the source rig is never silently stripped.
            foreach (var component in container.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                    throw new InvalidOperationException("The source player contains a missing script.");
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                        continue;
                    var reference = property.objectReferenceValue;
                    var referencedObject = reference is Component linkedComponent
                        ? linkedComponent.gameObject : reference as GameObject;
                    if (referencedObject != null && referencedObject.scene == source &&
                        !referencedObject.transform.IsChildOf(container.transform))
                        throw new InvalidOperationException($"Player reference outside prefab: {component.name}.{property.propertyPath} -> {referencedObject.name}");
                }
            }

            container.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var prefab = PrefabUtility.SaveAsPrefabAsset(container, PlayerPrefab);
            if (prefab == null)
                throw new InvalidOperationException("Could not save the configured player prefab.");
            Debug.Log("Packaged SampleScene player roots: " + string.Join(", ", included.Select(root => root.name)));
            return prefab;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(source);
        }
    }

    static bool Contains(Terrain terrain, Vector3 position)
    {
        var local = position - terrain.transform.position;
        var size = terrain.terrainData.size;
        return local.x >= 0f && local.z >= 0f && local.x <= size.x && local.z <= size.z;
    }
}
