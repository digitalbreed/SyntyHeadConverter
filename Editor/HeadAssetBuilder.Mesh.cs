// Maps skeleton names, combines skinned mesh data and creates the meshes and materials used by asset export.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace digitalbreed.SyntyHeadConverter {
    public static partial class HeadAssetBuilder {
        private static readonly string[] BoneSideSuffixes = { "l", "r" };
        private static readonly (string, string)[] LimbNameMappings = {
            ("clavicle", "Clavicle"),
            ("upperarm", "Shoulder"),
            ("lowerarm", "Elbow"),
            ("hand", "Hand"),
            ("thigh", "UpperLeg"),
            ("calf", "LowerLeg"),
            ("foot", "Ankle"),
            ("ball", "Ball"),
            ("toes", "Toes")
        };
        private static readonly (string, string)[] FingerNameMappings = { ("thumb", "Thumb"), ("index", "IndexFinger"), ("ring", "Finger") };
        static string outputFolder;
        static HeadConversionRecipe Recipe;
        static string textureSourceFolder;
        static string modelFolder => outputFolder + "/Models";

        static readonly System.Text.StringBuilder Report = new System.Text.StringBuilder();
        [Serializable]
        public class Data {
            public string id;
            public Bone[] bones;
            public Part[] meshes;
            public Vector3[] reference;
        }

        [Serializable]
        public class Bone {
            public string name, parent;
            public Vector3 position;
        }

        [Serializable]
        public class Part {
            public int[] seamPairs = Array.Empty<int>();
            public string name;
            public Vector3[] vertices, normals;
            public Vector2[] uv;
            public Weight[] weights;
            public Mat[] materials;
            public Sub[] submeshes;
            public Shape[] shapes;
        }

        [Serializable]
        public class Weight {
            public string[] names;
            public float[] values;
        }

        [Serializable]
        public class Mat {
            public string name, texture, sourceMaterial;
            public float[] color;
            public bool skinShading;
        }

        [Serializable]
        public class Sub {
            public int[] triangles;
        }

        [Serializable]
        public class Shape {
            public string name;
            public Vector3[] delta;
        }

        static readonly Dictionary<string, string> Names = new Dictionary<string, string> {
            { "root", "Root" },
            { "pelvis", "Hips" },
            { "spine_01", "Spine_01" },
            { "spine_02", "Spine_02" },
            { "spine_03", "Spine_03" },
            { "neck_01", "Neck" },
            { "head", "Head" }
        };
        static HashSet<string> OriginalNames;
        static readonly List<(int, int)> SeamPairs = new List<(int, int)>();
        static string Name(string boneName) {
            return OriginalNames != null && OriginalNames.Contains(boneName) ? boneName : CanonicalName(boneName);
        }

        static string CanonicalName(string boneName) {
            foreach (string sideSuffix in BoneSideSuffixes) {
                foreach (var mapping in LimbNameMappings) {
                    Names[mapping.Item1 + "_" + sideSuffix] = mapping.Item2 + "_" + sideSuffix.ToUpper();
                }

                foreach (var fingerMapping in FingerNameMappings) {
                    for (int i = 1; i <= 4; i++) {
                        Names[fingerMapping.Item1 + "_0" + i + "_" + sideSuffix] = fingerMapping.Item2 + "_0" + i + (sideSuffix == "r" ? " 1" : "");
                    }
                }
            }

            return Names.TryGetValue(boneName, out var result) ? result : boneName.Replace(".001", " 1");
        }

        static void Replace(SkinnedMeshRenderer skinRenderer, Data conversionData, bool create) {
            var old = skinRenderer.sharedMesh;
            var oldBones = skinRenderer.bones;
            var oldBind = old.bindposes;
            OriginalNames = new HashSet<string>(oldBones.Select(boneTransform => boneTransform.name));
            SeamPairs.Clear();
            var max = conversionData.reference.Max(v => old.vertices.Min(p => (p - v).sqrMagnitude));
            if (max > 1e-8f) {
                throw new Exception(conversionData.id + " source alignment mismatch: " + Mathf.Sqrt(max));
            }

            var bones = oldBones.ToList();
            var bind = oldBind.ToList();
            var names = bones.Select(boneTransform2 => boneTransform2.name).ToList();
            foreach (var boneData in conversionData.bones) {
                var name = Name(boneData.name);
                if (names.Contains(name)) {
                    continue;
                }

                // Facial additions are parented into the existing skeleton; original bones never move.
                if (!new[] { "jaw", "eye_l", "eye_r", "eyeLight_l", "eyeLight_r" }.Contains(boneData.name) && !boneData.name.StartsWith("fchr_dyn_")) {
                    continue;
                }

                int parent = names.IndexOf(Name(boneData.parent));
                if (parent < 0) {
                    throw new Exception("Missing facial parent " + boneData.parent);
                }

                var rest = bind[parent].inverse;
                var local = rest.inverse.MultiplyPoint3x4(boneData.position);
                var t = new GameObject(name).transform;
                t.SetParent(bones[parent], false);
                t.localPosition = local;
                bones.Add(t);
                names.Add(name);
                bind.Add((rest * Matrix4x4.Translate(local)).inverse);
            }

            var mesh = new Mesh {
                name = conversionData.id + "_Mesh",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var mats = new List<Material>();
            var matNames = new List<string>();
            var triangles = new List<List<int>>();
            var shapes = new Dictionary<string, Vector3[]>();
            int count = conversionData.meshes.Sum(meshPart => meshPart.vertices.Length);
            foreach (var meshPart2 in conversionData.meshes) {
                int offset = vertices.Count;
                for (int i = 0; i < meshPart2.seamPairs.Length; i += 2) {
                    SeamPairs.Add((offset + meshPart2.seamPairs[i], offset + meshPart2.seamPairs[i + 1]));
                }

                vertices.AddRange(meshPart2.vertices);
                normals.AddRange(meshPart2.normals);
                uv.AddRange(meshPart2.uv);
                foreach (var vertexWeight in meshPart2.weights) {
                    int[] idx = vertexWeight.names.Select(n => names.IndexOf(Name(n))).ToArray();
                    if (idx.Any(i => i < 0)) {
                        throw new Exception("Unmapped bone " + string.Join(",", vertexWeight.names));
                    }

                    var boneWeight = new BoneWeight();
                    for (int j = 0; j < idx.Length; j++) {
                        switch (j) {
                            case 0:
                                boneWeight.boneIndex0 = idx[j];
                                boneWeight.weight0 = vertexWeight.values[j];
                                break;
                            case 1:
                                boneWeight.boneIndex1 = idx[j];
                                boneWeight.weight1 = vertexWeight.values[j];
                                break;
                            case 2:
                                boneWeight.boneIndex2 = idx[j];
                                boneWeight.weight2 = vertexWeight.values[j];
                                break;
                            case 3:
                                boneWeight.boneIndex3 = idx[j];
                                boneWeight.weight3 = vertexWeight.values[j];
                                break;
                        }
                    }

                    weights.Add(boneWeight);
                }

                for (int s = 0; s < meshPart2.materials.Length; s++) {
                    var def = meshPart2.materials[s];
                    int index = matNames.IndexOf(def.name);
                    if (index < 0) {
                        index = mats.Count;
                        matNames.Add(def.name);
                        mats.Add(Material(def));
                        triangles.Add(new List<int>());
                    }

                    triangles[index].AddRange(meshPart2.submeshes[s].triangles.Select(i => i + offset));
                }

                foreach (var shape in meshPart2.shapes) {
                    if (!shapes.ContainsKey(shape.name)) {
                        shapes[shape.name] = new Vector3[count];
                    }

                    Array.Copy(shape.delta, 0, shapes[shape.name], offset, shape.delta.Length);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = bind.ToArray();
            mesh.subMeshCount = triangles.Count;
            for (int s = 0; s < triangles.Count; s++) {
                mesh.SetTriangles(triangles[s], s);
            }

            foreach (var s in shapes) {
                mesh.AddBlendShapeFrame(s.Key, 100, s.Value, null, null);
            }

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            // Validate untouched lower-body weights in the original bind space, independent of pose.
            float skinError = 0;
            var ov = old.vertices;
            var ow = old.boneWeights;
            for (int i = 0; i < vertices.Count; i++) {
                if (vertices[i].y < (1.3f - Recipe.settings.canonicalOffset) / Recipe.settings.canonicalScale) {
                    int nearest = 0;
                    float dist = float.MaxValue;
                    for (int j = 0; j < ov.Length; j++) {
                        float ds = (ov[j] - vertices[i]).sqrMagnitude;
                        if (ds < dist) {
                            dist = ds;
                            nearest = j;
                        }
                    }

                    if (dist > 1e-8f) {
                        throw new Exception("Lower body moved");
                    }

                    var test = new Vector3(.21f, .17f, .11f);
                    Vector3 Deform(Vector3 v, BoneWeight boneWeight2) {
                        int[] ids = { boneWeight2.boneIndex0, boneWeight2.boneIndex1, boneWeight2.boneIndex2, boneWeight2.boneIndex3 };
                        float[] ws = { boneWeight2.weight0, boneWeight2.weight1, boneWeight2.weight2, boneWeight2.weight3 };
                        var value = Vector3.zero;
                        for (int k = 0; k < 4; k++) {
                            var rot = Quaternion.Euler(test * (ids[k] * 17));
                            value += (rot * v) * ws[k];
                        }

                        return value;
                    }

                    skinError = Mathf.Max(skinError, Vector3.Distance(Deform(vertices[i], weights[i]), Deform(ov[nearest], ow[nearest])));
                }
            }

            if (skinError > .00002f) {
                throw new Exception(conversionData.id + " lower-body weighting changed " + skinError);
            }

            string asset = modelFolder + "/" + mesh.name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(asset);
            if (create) {
                if (saved) {
                    HeadMeshAssets.CopySerialized(mesh, saved);
                    Object.DestroyImmediate(mesh);
                    EditorUtility.SetDirty(saved);
                } else {
                    AssetDatabase.CreateAsset(mesh, asset);
                    saved = mesh;
                }
            } else {
                Object.DestroyImmediate(mesh);
                if (!saved) {
                    throw new Exception("Missing mesh");
                }
            }

            skinRenderer.sharedMesh = saved;
            skinRenderer.bones = bones.ToArray();
            skinRenderer.sharedMaterials = mats.ToArray();
            skinRenderer.localBounds = new Bounds(saved.bounds.center, saved.bounds.size + Vector3.one * .4f);
            skinRenderer.quality = SkinQuality.Bone4;
            for (int i = 0; i < saved.blendShapeCount; i++) {
                skinRenderer.SetBlendShapeWeight(i, 0);
            }

            Report.AppendLine(conversionData.id + ": " + saved.vertexCount + " vertices; " + saved.triangles.Length / 3 + " triangles; " + saved.blendShapeCount + " expressions; " + bones.Count + " bones; source max error " + Mathf.Sqrt(max) + "m; lower-body deformation error " + skinError + "m.");
        }

        static Material Material(Mat materialData) {
            string path = modelFolder + "/" + materialData.name.Replace(' ', '_') + ".mat";
            var original = string.IsNullOrEmpty(materialData.sourceMaterial) ? null : AssetDatabase.LoadAssetAtPath<Material>(materialData.sourceMaterial);
            if (original) {
                if (materialData.skinShading) {
                    var rgb = Recipe.settings.skin;
                    var tint = new Color(rgb[0] / 255, rgb[1] / 255, rgb[2] / 255).linear;
                    var skin = Material(new Mat { name = Recipe.characterId + " skin", color = new[] { tint.r, tint.g, tint.b, 1f } });
                    var sample = Recipe.settings.skinSampleUv;
                    var uv = sample != null && sample.Length == 2 ? new Vector2(sample[0], sample[1]) : new Vector2(.018f, .19f);
                    return Save(HeadSkinMaterial.Create(skin, original, materialData.name, HeadSkinMaterial.SampleAtlas(original, uv)), path);
                }
                return Save(new Material(original), path);
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool createMaterial = !material;
            if (!material) {
                material = new Material(HeadShaders.Lit()) {
                    name = materialData.name
                };
            }

            if (material.HasProperty("_Smoothness")) {
                material.SetFloat("_Smoothness", .2f);
            }

            if (material.HasProperty("_Metallic")) {
                material.SetFloat("_Metallic", 0);
            }

            string color = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            material.SetColor(color, new Color(materialData.color[0], materialData.color[1], materialData.color[2], materialData.color[3]).gamma);
            if (!string.IsNullOrEmpty(materialData.texture)) {
                string texture = material.HasProperty("_BaseMap") ? "_BaseMap" : material.HasProperty("_BaseColorMap") ? "_BaseColorMap" : "_MainTex";
                string texturePath = modelFolder + "/" + Path.GetFileNameWithoutExtension(materialData.texture) + ".asset";
                material.SetTexture(texture, AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                material.SetColor(color, Color.white);
            }

            if (createMaterial) {
                AssetDatabase.CreateAsset(material, path);
            } else {
                EditorUtility.SetDirty(material);
            }

            return material;
        }
    }
}
