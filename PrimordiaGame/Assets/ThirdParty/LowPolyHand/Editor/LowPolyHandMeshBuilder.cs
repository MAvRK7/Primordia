using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Primordia.Editor
{
    /// <summary>
    /// Bakes the custom Blender FBXs into meshes compatible with the existing
    /// XR Hands renderer transforms and bind poses.
    /// </summary>
    public static class LowPolyHandMeshBuilder
    {
        private const string Root = "Assets/ThirdParty/LowPolyHand/Models";
        private const string XrHandsRoot =
            "Assets/Samples/XR Hands/1.7.3/HandVisualizer/Models";

        [MenuItem("Tools/Primordia/Rebuild Low-Poly Hand Meshes")]
        public static void Rebuild()
        {
            Build(
                $"{Root}/LeftHand_Rigged.fbx",
                $"{XrHandsRoot}/LeftHand.fbx",
                $"{Root}/LeftHand.asset",
                "LeftHand");
            Build(
                $"{Root}/RightHand_Rigged.fbx",
                $"{XrHandsRoot}/RightHand.fbx",
                $"{Root}/RightHand.asset",
                "RightHand");
            AssetDatabase.SaveAssets();
            Debug.Log("Rebuilt and validated the low-poly XR hand meshes.");
        }

        private static void Build(
            string customModelPath,
            string referenceModelPath,
            string outputPath,
            string meshName)
        {
            var customAsset = AssetDatabase.LoadAssetAtPath<GameObject>(customModelPath);
            var referenceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(referenceModelPath);
            if (customAsset == null || referenceAsset == null)
                throw new InvalidOperationException($"Missing model source for {meshName}.");

            var customInstance = UnityEngine.Object.Instantiate(customAsset);
            var referenceInstance = UnityEngine.Object.Instantiate(referenceAsset);
            try
            {
                var customRenderer =
                    customInstance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var referenceRenderer =
                    referenceInstance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (customRenderer == null || referenceRenderer == null)
                    throw new InvalidOperationException(
                        $"Missing skinned renderer for {meshName}.");

                var result = UnityEngine.Object.Instantiate(customRenderer.sharedMesh);
                result.name = meshName;
                ConvertRendererSpace(result, customRenderer, referenceRenderer);
                RemapBoneIndices(result, customRenderer.bones, referenceRenderer.bones);
                result.bindposes = referenceRenderer.sharedMesh.bindposes;
                result.RecalculateBounds();

                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(outputPath);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(result, outputPath);
                    existing = result;
                }
                else
                {
                    EditorUtility.CopySerialized(result, existing);
                    existing.name = meshName;
                    UnityEngine.Object.DestroyImmediate(result);
                    EditorUtility.SetDirty(existing);
                }

                ValidateReplacement(customRenderer, referenceRenderer, existing, meshName);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(customInstance);
                UnityEngine.Object.DestroyImmediate(referenceInstance);
            }
        }

        private static void ConvertRendererSpace(
            Mesh mesh,
            SkinnedMeshRenderer source,
            SkinnedMeshRenderer destination)
        {
            var transform =
                destination.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
            var vertices = mesh.vertices;
            for (var index = 0; index < vertices.Length; index++)
                vertices[index] = transform.MultiplyPoint3x4(vertices[index]);
            mesh.vertices = vertices;

            var normalTransform = transform.inverse.transpose;
            var normals = mesh.normals;
            for (var index = 0; index < normals.Length; index++)
                normals[index] = normalTransform.MultiplyVector(normals[index]).normalized;
            mesh.normals = normals;

            var tangents = mesh.tangents;
            for (var index = 0; index < tangents.Length; index++)
            {
                var direction = transform.MultiplyVector(
                    new Vector3(
                        tangents[index].x,
                        tangents[index].y,
                        tangents[index].z)).normalized;
                tangents[index] =
                    new Vector4(direction.x, direction.y, direction.z, tangents[index].w);
            }
            mesh.tangents = tangents;
        }

        private static void RemapBoneIndices(
            Mesh mesh,
            Transform[] sourceBones,
            Transform[] destinationBones)
        {
            var destinationIndices = destinationBones
                .Select((bone, index) => new KeyValuePair<string, int>(bone.name, index))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            var remap = sourceBones
                .Select(bone => destinationIndices.TryGetValue(bone.name, out var index)
                    ? index
                    : throw new InvalidOperationException(
                        $"Destination skeleton is missing {bone.name}."))
                .ToArray();

            var weights = mesh.boneWeights;
            for (var index = 0; index < weights.Length; index++)
            {
                var weight = weights[index];
                weight.boneIndex0 = remap[weight.boneIndex0];
                weight.boneIndex1 = remap[weight.boneIndex1];
                weight.boneIndex2 = remap[weight.boneIndex2];
                weight.boneIndex3 = remap[weight.boneIndex3];
                weights[index] = weight;
            }
            mesh.boneWeights = weights;
        }

        private static void ValidateReplacement(
            SkinnedMeshRenderer source,
            SkinnedMeshRenderer destination,
            Mesh replacement,
            string meshName)
        {
            var expected = new Mesh();
            var actual = new Mesh();
            try
            {
                source.BakeMesh(expected, true);
                destination.sharedMesh = replacement;
                destination.BakeMesh(actual, true);
                if (expected.vertexCount != actual.vertexCount)
                    throw new InvalidOperationException(
                        $"Baked vertex counts differ for {meshName}.");

                var expectedVertices = expected.vertices;
                var actualVertices = actual.vertices;
                var maximumWorldError = 0f;
                for (var index = 0; index < expectedVertices.Length; index++)
                {
                    var expectedWorld =
                        source.transform.TransformPoint(expectedVertices[index]);
                    var actualWorld =
                        destination.transform.TransformPoint(actualVertices[index]);
                    maximumWorldError = Mathf.Max(
                        maximumWorldError,
                        Vector3.Distance(expectedWorld, actualWorld));
                }
                if (maximumWorldError > 0.0001f)
                    throw new InvalidOperationException(
                        $"{meshName} replacement bake error is " +
                        $"{maximumWorldError} metres.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(expected);
                UnityEngine.Object.DestroyImmediate(actual);
            }
        }
    }
}
