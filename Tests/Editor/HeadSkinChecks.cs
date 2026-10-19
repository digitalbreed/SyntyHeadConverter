using System;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace digitalbreed.SyntyHeadConverter.Tests {
    public static class HeadSkinChecks {
        public static string Run() {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var skin = new Material(shader);
            var palette = new Material(shader);
            var atlas = new Texture2D(2, 2);
            var mesh = new Mesh();
            Mesh live = null;
            Material neck = null;
            Material retinted = null;
            try {
                palette.mainTexture = atlas;
                palette.mainTextureScale = new Vector2(.75f, .5f);
                palette.mainTextureOffset = new Vector2(.1f, .2f);
                palette.color = new Color(.9f, .8f, .7f);
                skin.SetFloat("_Smoothness", .17f);
                palette.SetFloat("_Smoothness", .85f);
                neck = HeadSkinMaterial.Create(skin, palette, "Retained neck skin");
                Check(neck.shader == skin.shader && Mathf.Approximately(neck.GetFloat("_Smoothness"), .17f), "Head shading differs");
                Check(neck.mainTexture == atlas && neck.color == palette.color && neck.mainTextureScale == palette.mainTextureScale && neck.mainTextureOffset == palette.mainTextureOffset, "Tattoo albedo changed");
                Check(Mathf.Approximately(palette.GetFloat("_Smoothness"), .85f), "Clothing material changed");
                var reference = new Color(.6f, .4f, .2f, 1);
                atlas.SetPixels(Enumerable.Repeat(reference, 4).ToArray()); atlas.Apply();
                skin.color = new Color(.8f, .7f, .6f, 1);
                var sampled = HeadSkinMaterial.SampleAtlas(palette, Vector2.one * .5f);
                Check(Mathf.Abs(sampled.r - reference.r) < .005f && Mathf.Abs(sampled.g - reference.g) < .005f, "Atlas sample changed color space");
                retinted = HeadSkinMaterial.Create(skin, palette, "Retinted neck", sampled);
                var neutral = sampled.linear * retinted.color.linear;
                var target = skin.color.linear;
                Check(Mathf.Abs(neutral.r - target.r) < .001f && Mathf.Abs(neutral.g - target.g) < .001f && Mathf.Abs(neutral.b - target.b) < .001f, "Retained neck does not follow the chosen complexion");
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one };
                mesh.normals = Enumerable.Repeat(Vector3.forward, 4).ToArray();
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
                mesh.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1 }, 4).ToArray();
                mesh.bindposes = new[] { Matrix4x4.identity };
                mesh.subMeshCount = 2;
                mesh.SetTriangles(new[] { 0, 1, 2, 1, 3, 2 }, 0);
                mesh.SetTriangles(new[] { 2, 3, 0 }, 1);
                var deltas = Enumerable.Repeat(Vector3.up * .01f, 4).ToArray();
                mesh.AddBlendShapeFrame("expression", 100, deltas, deltas, null);
                live = Object.Instantiate(mesh);
                live.UploadMeshData(false);
                VerifyGpuIndices(live);
                var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv; var bounds = mesh.bounds;
                Check(HeadSkinSubmeshes.Separate(mesh, 0, new[] { 1 }) == 2, "Slot was not appended");
                Check(mesh.GetTriangles(0).SequenceEqual(new[] { 0, 1, 2 }) && mesh.GetTriangles(1).SequenceEqual(new[] { 2, 3, 0 }) && mesh.GetTriangles(2).SequenceEqual(new[] { 1, 3, 2 }), "Wrong triangles moved");
                Check(mesh.vertices.SequenceEqual(vertices) && mesh.normals.SequenceEqual(normals) && mesh.uv.SequenceEqual(uv) && mesh.bounds == bounds && mesh.boneWeights.All(w => w.weight0 == 1) && mesh.bindposes[0] == Matrix4x4.identity, "Mesh attributes changed");
                var actual = new Vector3[4]; mesh.GetBlendShapeFrameVertices(0, 0, actual, null, null);
                Check(actual.SequenceEqual(deltas), "Facial expression changed");
                HeadMeshAssets.CopySerialized(mesh, live);
                Check(live.subMeshCount == 3 && live.GetTriangles(1).SequenceEqual(new[] { 2, 3, 0 }), "Live mesh layout was not replaced");
                VerifyGpuIndices(live);
                try { HeadSkinSubmeshes.Separate(mesh, 0, new[] { 0, 0 }); throw new Exception("Duplicate selection accepted"); }
                catch (ArgumentException) { }
                Check(mesh.subMeshCount == 3 && mesh.GetTriangles(0).Length == 3, "Invalid selection mutated mesh");
                return "PASS: head shading, complexion, atlas sampling, garment preservation, triangle routing, mesh attributes, expressions, invalid selections and GPU index buffers after live mesh replacement.";
            } finally {
                if (neck) Object.DestroyImmediate(neck);
                if (retinted) Object.DestroyImmediate(retinted);
                if (live) Object.DestroyImmediate(live);
                Object.DestroyImmediate(skin); Object.DestroyImmediate(palette); Object.DestroyImmediate(atlas); Object.DestroyImmediate(mesh);
            }
        }
        private static void Check(bool passed, string message) { if (!passed) throw new Exception(message); }
        private static void VerifyGpuIndices(Mesh mesh) {
            using (var buffer = mesh.GetIndexBuffer()) {
                int[] indices;
                if (mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt16) {
                    var data = new ushort[buffer.count]; buffer.GetData(data);
                    indices = data.Select(i => (int)i).ToArray();
                } else {
                    indices = new int[buffer.count]; buffer.GetData(indices);
                }
                for (int s = 0; s < mesh.subMeshCount; s++) {
                    var descriptor = mesh.GetSubMesh(s);
                    Check(indices.Skip(descriptor.indexStart).Take(descriptor.indexCount).SequenceEqual(mesh.GetIndices(s, false)),
                        "GPU index buffer does not match material slot " + s);
                }
            }
        }
    }
}
