using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Primordia.Editor
{
    /// <summary>Rebuilds the controller-mode hand prefabs from the rigged FBXs.</summary>
    public static class LowPolyControllerHandPrefabBuilder
    {
        private const string Root = "Assets/ThirdParty/LowPolyHand";
        private const string Models = Root + "/Models";
        private const string Materials = Root + "/Materials";
        private const string Prefabs = Root + "/Prefabs";
        private const string InputActionsPath =
            "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/" +
            "XRI Default Input Actions.inputactions";
        private const string HandMaterialPath = Materials + "/LowPolyHand.mat";

        [MenuItem("Tools/Primordia/Rebuild Low-Poly Controller Hand Prefabs")]
        public static void Rebuild()
        {
            EnsureAssetFolders();
            var handMaterial = CreateOrUpdateHandMaterial();
            Build(
                true,
                "LeftHand_Rigged.fbx",
                "Left Controller Hand.prefab",
                6558622148059887818L,
                -4289430672226363583L,
                handMaterial);
            Build(
                false,
                "RightHand_Rigged.fbx",
                "Right Controller Hand.prefab",
                -1758520528963094988L,
                7904272356298805229L,
                handMaterial);
            AssetDatabase.SaveAssets();
            Debug.Log("Rebuilt and validated both low-poly controller hand prefabs.");
        }

        private static void EnsureAssetFolders()
        {
            if (!AssetDatabase.IsValidFolder(Materials))
                AssetDatabase.CreateFolder(Root, "Materials");
            if (!AssetDatabase.IsValidFolder(Prefabs))
                AssetDatabase.CreateFolder(Root, "Prefabs");
        }

        private static Material CreateOrUpdateHandMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("The URP Lit shader is unavailable.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(HandMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "LowPolyHand" };
                AssetDatabase.CreateAsset(material, HandMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            // The supplied Blender model uses one opaque white Principled material,
            // with no textures or vertex-colour layer.
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.5f);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = -1;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Build(
            bool isLeft,
            string modelFile,
            string prefabFile,
            long gripActionId,
            long triggerActionId,
            Material handMaterial)
        {
            var side = isLeft ? "Left" : "Right";
            var modelAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{modelFile}");
            var gripAction = LoadActionReference(gripActionId);
            var triggerAction = LoadActionReference(triggerActionId);
            if (modelAsset == null)
                throw new InvalidOperationException($"Missing {side} controller-hand source assets.");

            var root = new GameObject($"{side} Controller Hand");
            try
            {
                var rig = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
                if (rig == null)
                    throw new InvalidOperationException($"Could not instantiate {modelFile}.");
                PrefabUtility.UnpackPrefabInstance(
                    rig,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
                rig.name = "Rig";
                rig.transform.SetParent(root.transform, false);
                rig.transform.SetLocalPositionAndRotation(
                    new Vector3(0f, 0f, -0.05f),
                    Quaternion.Euler(0f, 180f, 0f));
                rig.transform.localScale = isLeft
                    ? Vector3.one
                    : new Vector3(-1f, 1f, 1f);

                var handRenderer = rig.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (handRenderer == null || handRenderer.bones.Length != 26)
                    throw new InvalidOperationException(
                        $"{side} controller hand does not contain a 26-bone renderer.");
                handRenderer.sharedMaterial = handMaterial;
                foreach (var renderer in rig.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = renderer == handRenderer;
                foreach (var camera in rig.GetComponentsInChildren<Camera>(true))
                    camera.gameObject.SetActive(false);
                foreach (var light in rig.GetComponentsInChildren<Light>(true))
                    light.gameObject.SetActive(false);

                var animator = root.AddComponent<ControllerHandVisual>();
                var serializedAnimator = new SerializedObject(animator);
                serializedAnimator.FindProperty("m_HandRenderer").objectReferenceValue =
                    handRenderer;
                serializedAnimator.FindProperty("m_IsLeftHand").boolValue = isLeft;
                serializedAnimator.FindProperty("m_GripAction").objectReferenceValue =
                    gripAction;
                serializedAnimator.FindProperty("m_TriggerAction").objectReferenceValue =
                    triggerAction;
                serializedAnimator.FindProperty("m_SmoothingSpeed").floatValue = 18f;
                serializedAnimator.ApplyModifiedPropertiesWithoutUndo();

                var outputPath = $"{Prefabs}/{prefabFile}";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, outputPath);
                var savedAnimator = prefab.GetComponent<ControllerHandVisual>();
                var savedRenderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (savedAnimator == null || savedRenderer == null ||
                    savedRenderer.bones.Length != 26 ||
                    savedRenderer.sharedMaterial != handMaterial)
                    throw new InvalidOperationException(
                        $"The saved {side} controller-hand prefab failed validation.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static InputActionReference LoadActionReference(long localId)
        {
            return AssetDatabase.LoadAllAssetsAtPath(InputActionsPath)
                .OfType<InputActionReference>()
                .Single(reference =>
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        reference,
                        out _,
                        out long referenceLocalId) &&
                    referenceLocalId == localId);
        }
    }
}
