using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Primordia.Editor
{
    /// <summary>Repairs the mirrored left hand's inward-facing imported triangles.</summary>
    public sealed class LowPolyHandMeshPostprocessor : AssetPostprocessor
    {
        const string LeftHandModelPath =
            "Assets/ThirdParty/LowPolyHand/Models/LeftHand_Rigged.fbx";

        public override uint GetVersion() => 1;

        void OnPostprocessModel(GameObject model)
        {
            if (!string.Equals(assetPath, LeftHandModelPath, StringComparison.OrdinalIgnoreCase))
                return;

            var processed = new HashSet<Mesh>();
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh != null && processed.Add(renderer.sharedMesh))
                    RepairWinding(renderer.sharedMesh);
            }
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null && processed.Add(filter.sharedMesh))
                    RepairWinding(filter.sharedMesh);
            }
        }

        static void RepairWinding(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var hasNormals = normals.Length == vertices.Length;
            double normalAgreement = 0;
            var reversedTriangles = false;

            for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                if (mesh.GetTopology(subMesh) != MeshTopology.Triangles)
                    continue;

                // Keep each submesh's base vertex and material assignment intact.
                var indices = mesh.GetIndices(subMesh, false);
                var baseVertex = (int)mesh.GetBaseVertex(subMesh);
                for (var triangle = 0; triangle + 2 < indices.Length; triangle += 3)
                {
                    (indices[triangle + 1], indices[triangle + 2]) =
                        (indices[triangle + 2], indices[triangle + 1]);
                    if (!hasNormals)
                        continue;

                    var a = indices[triangle] + baseVertex;
                    var b = indices[triangle + 1] + baseVertex;
                    var c = indices[triangle + 2] + baseVertex;
                    var face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                    normalAgreement += Vector3.Dot(face, normals[a] + normals[b] + normals[c]);
                }
                mesh.SetIndices(indices, MeshTopology.Triangles, subMesh, false, baseVertex);
                reversedTriangles = true;
            }

            if (!reversedTriangles)
                return;

            if (!hasNormals || Math.Abs(normalAgreement) < 1e-12)
            {
                mesh.RecalculateNormals();
                if (mesh.uv.Length == vertices.Length)
                    mesh.RecalculateTangents();
                return;
            }

            // Some exporters preserve outward normals while mirroring triangle
            // winding. Keep those authored normals, including smoothing seams.
            if (normalAgreement >= 0)
                return;

            for (var vertex = 0; vertex < normals.Length; vertex++)
                normals[vertex] = -normals[vertex];
            mesh.normals = normals;

            var tangents = mesh.tangents;
            for (var vertex = 0; vertex < tangents.Length; vertex++)
            {
                // Flipping N and tangent handedness keeps the UV bitangent fixed.
                tangents[vertex].w = -tangents[vertex].w;
            }
            mesh.tangents = tangents;
        }
    }
}
