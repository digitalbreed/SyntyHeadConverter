// Updates native mesh assets without leaving their live render buffers on an older submesh layout.
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace digitalbreed.SyntyHeadConverter {
    public static class HeadMeshAssets {
        public static void CopySerialized(Mesh source, Mesh destination) {
            if (!source || !destination) throw new ArgumentNullException(!source ? nameof(source) : nameof(destination));
            if (!source.isReadable) throw new InvalidOperationException("Converted meshes must retain readable native data.");
            EditorUtility.CopySerialized(source, destination);
            RebuildIndexBuffer(destination);
        }

        /// <summary>Invalidates live draw ranges after serialized mesh replacement. Does not import assets.</summary>
        public static void RebuildIndexBuffer(Mesh mesh) {
            if (!mesh) throw new ArgumentNullException(nameof(mesh));
            if (!mesh.isReadable) throw new InvalidOperationException("Cannot rebuild a non-readable mesh's index buffer.");
            var slots = Enumerable.Range(0, mesh.subMeshCount).Select(s => new {
                indices = mesh.GetIndices(s, false), topology = mesh.GetTopology(s),
                baseVertex = mesh.GetSubMesh(s).baseVertex
            }).ToArray();
            var bounds = mesh.bounds;
            for (int s = 0; s < slots.Length; s++) {
                var slot = slots[s];
                mesh.SetIndices(slot.indices, slot.topology, s, false, slot.baseVertex);
            }
            mesh.bounds = bounds;
            if (mesh.vertexCount > 0) mesh.UploadMeshData(false);
        }
    }
}
