// Shares head shading with retained skin while preserving the source color atlas and UV transform.
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace digitalbreed.SyntyHeadConverter {
    public static class HeadSkinMaterial {
        private static readonly string[] ColorMaps = { "_BaseMap", "_BaseColorMap", "_Albedo_Map", "_MainTex" };
        private static readonly string[] Colors = { "_BaseColor", "_Color" };

        /// <summary>Creates an unsaved skin material. The caller owns and saves or destroys it.</summary>
        /// <remarks>Clothing/hair materials and mesh UVs are never modified. Shader, lighting,
        /// surface response and render state come from headSkin; only albedo comes from source.</remarks>
        public static Material Create(Material headSkin, Material source, string name, Color? sourceSkinSample = null) {
            if (!headSkin || !source) {
                throw new ArgumentNullException(!headSkin ? nameof(headSkin) : nameof(source));
            }
            string targetMap = ColorMaps.FirstOrDefault(headSkin.HasProperty);
            string sourceMap = ColorMaps.FirstOrDefault(p => source.HasProperty(p) && source.GetTexture(p));
            string targetColor = Colors.FirstOrDefault(headSkin.HasProperty);
            string sourceColor = Colors.FirstOrDefault(source.HasProperty);
            if (sourceMap != null && targetMap == null || sourceColor != null && targetColor == null) {
                throw new InvalidOperationException("The head skin shader cannot preserve the source skin albedo.");
            }
            var result = new Material(headSkin) { name = name };
            if (targetMap != null) {
                result.SetTexture(targetMap, sourceMap == null ? null : source.GetTexture(sourceMap));
                result.SetTextureScale(targetMap, sourceMap == null ? Vector2.one : source.GetTextureScale(sourceMap));
                result.SetTextureOffset(targetMap, sourceMap == null ? Vector2.zero : source.GetTextureOffset(sourceMap));
            }
            if (targetColor != null) {
                result.SetColor(targetColor, sourceColor == null ? Color.white : source.GetColor(sourceColor));
                if (sourceSkinSample.HasValue) {
                    var reference = sourceSkinSample.Value.linear;
                    var complexion = headSkin.GetColor(targetColor).linear;
                    result.SetColor(targetColor, new Color(complexion.r / Mathf.Max(.001f, reference.r),
                        complexion.g / Mathf.Max(.001f, reference.g), complexion.b / Mathf.Max(.001f, reference.b), 1).gamma);
                }
            }
            result.SetOverrideTag("SyntyHeadConverterSkin", "Retained");
            string headPath = AssetDatabase.GetAssetPath(headSkin);
            if (!string.IsNullOrEmpty(headPath)) {
                result.SetOverrideTag("SyntyHeadConverterHeadSkin", AssetDatabase.AssetPathToGUID(headPath));
            }
            return result;
        }

        /// <summary>Samples the source atlas without making the imported texture readable.</summary>
        public static Color SampleAtlas(Material source, Vector2 uv) {
            string map = ColorMaps.FirstOrDefault(p => source.HasProperty(p) && source.GetTexture(p));
            if (map == null) return Color.white;
            var rt = RenderTexture.GetTemporary(1, 1, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Texture2D pixel = null;
            try {
                uv = Vector2.Scale(uv, source.GetTextureScale(map)) + source.GetTextureOffset(map);
                Graphics.Blit(source.GetTexture(map), rt, Vector2.zero, uv);
                RenderTexture.active = rt;
                pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false, false);
                pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                pixel.Apply();
                return pixel.GetPixel(0, 0);
            } finally {
                if (pixel) UnityEngine.Object.DestroyImmediate(pixel);
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }

    /// <summary>Separates explicitly selected retained-skin triangles without rebuilding a mesh.</summary>
    public static class HeadSkinSubmeshes {
        /// <summary>Follows shared geometric edges through classified skin, including triangles below a neck seed's height.</summary>
        /// <remarks>Position keys join UV/normal splits without changing any vertex attributes. Non-skin faces are barriers.</remarks>
        public static int[] ConnectedTriangles(IReadOnlyList<Vector3> positions, IReadOnlyList<int> triangles,
            IReadOnlyCollection<int> candidates, IReadOnlyCollection<int> seeds) {
            if (positions == null || triangles == null || candidates == null || seeds == null) throw new ArgumentNullException();
            if (triangles.Count % 3 != 0 || triangles.Any(i => i < 0 || i >= positions.Count)) throw new ArgumentException("Invalid triangle indices.");
            var allowed = new HashSet<int>(candidates);
            if (allowed.Any(i => i < 0 || i >= triangles.Count / 3) || seeds.Any(i => !allowed.Contains(i))) throw new ArgumentException("Invalid skin selection.");
            Vector3Int Key(int i) { var p = positions[i]; return new Vector3Int(Mathf.RoundToInt(p.x * 1000000), Mathf.RoundToInt(p.y * 1000000), Mathf.RoundToInt(p.z * 1000000)); }
            var edges = new Dictionary<(Vector3Int, Vector3Int), List<int>>();
            foreach (int face in allowed) for (int corner = 0; corner < 3; corner++) {
                var a = Key(triangles[face * 3 + corner]); var b = Key(triangles[face * 3 + (corner + 1) % 3]);
                // Register both directions so winding and split vertex indices do not affect connectivity.
                foreach (var edge in new[] { (a, b), (b, a) }) {
                    if (!edges.TryGetValue(edge, out var adjacent)) edges.Add(edge, adjacent = new List<int>());
                    adjacent.Add(face);
                }
            }
            var selected = new HashSet<int>(seeds);
            var pending = new Queue<int>(selected);
            while (pending.Count > 0) {
                int face = pending.Dequeue();
                for (int corner = 0; corner < 3; corner++) {
                    var edge = (Key(triangles[face * 3 + corner]), Key(triangles[face * 3 + (corner + 1) % 3]));
                    foreach (int next in edges[edge]) if (selected.Add(next)) pending.Enqueue(next);
                }
            }
            return selected.OrderBy(i => i).ToArray();
        }

        /// <param name="triangleIndices">Triangle ordinals in sourceSlot, not vertex indices.</param>
        /// <returns>The appended material slot, or -1 for an empty selection.</returns>
        public static int Separate(Mesh mesh, int sourceSlot, IReadOnlyCollection<int> triangleIndices) {
            if (!mesh) throw new ArgumentNullException(nameof(mesh));
            if (triangleIndices == null) throw new ArgumentNullException(nameof(triangleIndices));
            if (sourceSlot < 0 || sourceSlot >= mesh.subMeshCount || mesh.GetTopology(sourceSlot) != MeshTopology.Triangles) {
                throw new ArgumentOutOfRangeException(nameof(sourceSlot));
            }
            var triangles = mesh.GetTriangles(sourceSlot);
            var selection = new HashSet<int>(triangleIndices);
            if (selection.Count != triangleIndices.Count || selection.Any(i => i < 0 || i >= triangles.Length / 3)) {
                throw new ArgumentException("Select unique triangle ordinals from the source submesh.", nameof(triangleIndices));
            }
            if (selection.Count == 0) return -1;
            var retained = new List<int>();
            var skin = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3) {
                var target = selection.Contains(i / 3) ? skin : retained;
                target.Add(triangles[i]); target.Add(triangles[i + 1]); target.Add(triangles[i + 2]);
            }
            int slot = mesh.subMeshCount;
            var bounds = mesh.bounds;
            mesh.subMeshCount = slot + 1;
            mesh.SetTriangles(retained, sourceSlot, false);
            mesh.SetTriangles(skin, slot, false);
            mesh.bounds = bounds;
            HeadMeshAssets.RebuildIndexBuffer(mesh);
            return slot;
        }
    }
}
