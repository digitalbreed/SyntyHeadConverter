// Writes converted character assets with stable GUIDs and validates mesh alignment and animated deformation.
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace digitalbreed.SyntyHeadConverter {
    public static partial class HeadAssetBuilder {
        private static readonly string[] LipFullnessShapeNames = { "lipFullnessUpper", "lipFullnessLower" };
        private static readonly float[] ValidationHeadAngles = { -35f, 35f };
        public static string LastReport {
            get;
            private set;
        }

        static T Save<T>(T item, string path)
            where T : Object {
            var old = AssetDatabase.LoadAssetAtPath<T>(path);
            if (old) {
                if (item is Mesh sourceMesh && old is Mesh savedMesh) {
                    HeadMeshAssets.CopySerialized(sourceMesh, savedMesh);
                } else {
                    EditorUtility.CopySerialized(item, old);
                }
                Object.DestroyImmediate(item);
                EditorUtility.SetDirty(old);
                return old;
            }

            AssetDatabase.CreateAsset(item, path);
            return item;
        }

        public static GameObject Build(HeadConversionRecipe recipe, Data data, string job, string destination) {
            Recipe = recipe;
            outputFolder = destination.Replace('\\', '/').TrimEnd('/');
            textureSourceFolder = Path.Combine(job, "authoring/unity");
            Report.Clear();
            ValidateData(data);
            HeadAssetFolders.Ensure(modelFolder);
            HeadAssetFolders.Ensure(outputFolder + "/Animations");
            foreach (string filePath in Directory.GetFiles(textureSourceFolder, "*.png")) {
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, true, false) {
                    name = Path.GetFileNameWithoutExtension(filePath),
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 1
                };
                try {
                    if (!texture.LoadImage(File.ReadAllBytes(filePath))) {
                        throw new IOException("Cannot decode the generated texture: " + filePath);
                    }

                    texture.Apply(true, false);
                    Save(texture, modelFolder + "/" + texture.name + ".asset");
                } finally {
                    if (texture && !EditorUtility.IsPersistent(texture)) {
                        Object.DestroyImmediate(texture);
                    }
                }
            }

            var scene = EditorSceneManager.NewPreviewScene();
            GameObject characterInstance = null;
            try {
                characterInstance = (GameObject)PrefabUtility.InstantiatePrefab(recipe.sourcePrefab, scene);
                PrefabUtility.UnpackPrefabInstance(characterInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                characterInstance.name = recipe.characterId;
                characterInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                characterInstance.transform.localScale = Vector3.one;
                foreach (var renderer in characterInstance.GetComponentsInChildren<Renderer>(true)) {
                    if (!renderer.gameObject.activeInHierarchy) {
                        Object.DestroyImmediate(renderer.gameObject);
                    }
                }

                var skin = characterInstance.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(skinRenderer => skinRenderer.sharedMesh.vertexCount).First();
                var palette = skin.sharedMaterial;
                foreach (var skinRenderer2 in characterInstance.GetComponentsInChildren<SkinnedMeshRenderer>().Where(skinRenderer3 => skinRenderer3 != skin).ToArray()) {
                    if (skinRenderer2.name.StartsWith("SM_Chr_Eyes_") || skinRenderer2.name.StartsWith("SM_Chr_Eyebrows_")) {
                        Object.DestroyImmediate(skinRenderer2.gameObject);
                    }
                }

                foreach (var meshRenderer in characterInstance.GetComponentsInChildren<MeshRenderer>()) {
                    if (meshRenderer.name.StartsWith("SM_Chr_Kid_Face_")) {
                        Object.DestroyImmediate(meshRenderer.gameObject);
                    }
                }

                Replace(skin, data, true);
                var body = Save(new Material(palette), modelFolder + "/" + recipe.characterId + "_original_palette.mat");
                skin.sharedMaterials = skin.sharedMaterials.Select(material => material.name.Replace('_', ' ') == recipe.characterId + " original palette" ? body : material).ToArray();
                var rest = skin.sharedMesh.bindposes.Select(transformMatrix => skin.transform.localToWorldMatrix * transformMatrix.inverse).ToArray();
                for (int i = 0; i < skin.bones.Length; i++) {
                    skin.bones[i].SetPositionAndRotation(rest[i].GetColumn(3), rest[i].rotation);
                }

                foreach (var meshRenderer2 in characterInstance.GetComponentsInChildren<MeshRenderer>()) {
                    if (!string.IsNullOrEmpty(recipe.settings.sidekickBeard) && meshRenderer2.name.IndexOf("beard", StringComparison.OrdinalIgnoreCase) >= 0) {
                        Object.DestroyImmediate(meshRenderer2.gameObject);
                        continue;
                    }

                    if (!data.meshes.Any(meshPart => meshPart.name == recipe.characterId + "_" + meshRenderer2.name + "_Expressive")) {
                        continue;
                    }

                    var beard = Save(new Material(meshRenderer2.sharedMaterial), modelFolder + "/" + recipe.characterId + "_retained_beard_original.mat");
                    skin.sharedMaterials = skin.sharedMaterials.Select(material2 => material2.name.Replace('_', ' ') == recipe.characterId + " retained beard" ? beard : material2).ToArray();
                    Object.DestroyImmediate(meshRenderer2.gameObject);
                }

                foreach (var adjustment in new RecipeOptions(recipe.baseConfig).Objects("rigidFits")) {
                    var filter = characterInstance.GetComponentsInChildren<MeshFilter>().Single(f => f.name == adjustment.Text("name"));
                    Vector3 Vector(string field) {
                        var values = adjustment.Numbers(field);
                        return new Vector3(values[0], values[1], values[2]);
                    }

                    var center = Vector("center");
                    var matrix = characterInstance.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    var deformation = matrix.inverse * Matrix4x4.Translate(center + Vector("offset")) * Matrix4x4.Scale(Vector("scale")) * Matrix4x4.Translate(-center) * matrix;
                    var mesh = Object.Instantiate(filter.sharedMesh);
                    mesh.vertices = mesh.vertices.Select(deformation.MultiplyPoint3x4).ToArray();
                    mesh.normals = mesh.normals.Select(n => deformation.inverse.transpose.MultiplyVector(n).normalized).ToArray();
                    mesh.RecalculateBounds();
                    filter.sharedMesh = Save(mesh, modelFolder + "/" + recipe.characterId + "_" + filter.name + "_fitted.asset");
                }

                foreach (string name in LipFullnessShapeNames) {
                    int i = skin.sharedMesh.GetBlendShapeIndex(name);
                    if (i >= 0) {
                        skin.SetBlendShapeWeight(i, recipe.lipFullness);
                    }
                }

                var animator = characterInstance.GetComponent<Animator>() ?? characterInstance.AddComponent<Animator>();
                animator.avatar = Save(MakeAvatar(characterInstance), modelFolder + "/" + recipe.characterId + "Avatar.asset");
                var clip = FacialDemo(characterInstance, skin);
                ValidatePoses(characterInstance, skin, clip);
                foreach (string name in LipFullnessShapeNames) {
                    int i = skin.sharedMesh.GetBlendShapeIndex(name);
                    if (i >= 0) {
                        skin.SetBlendShapeWeight(i, recipe.lipFullness);
                    }
                }

                characterInstance.transform.localScale = recipe.sourcePrefab.transform.localScale;
                string path = outputFolder + "/" + recipe.characterId + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(characterInstance, path);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(outputFolder + "/Animations/" + recipe.characterId + "FacialDemo.controller");
                if (!controller) {
                    controller = AnimatorController.CreateAnimatorControllerAtPath(outputFolder + "/Animations/" + recipe.characterId + "FacialDemo.controller");
                }

                if (controller.layers[0].stateMachine.states.Length == 0) {
                    controller.AddMotion(clip);
                } else {
                    controller.layers[0].stateMachine.states[0].state.motion = clip;
                }

                animator.avatar = null;
                animator.runtimeAnimatorController = controller;
                characterInstance.name = recipe.characterId + "_FacialDemo";
                PrefabUtility.SaveAsPrefabAsset(characterInstance, outputFolder + "/" + characterInstance.name + ".prefab");
                AssetDatabase.SaveAssets();
                LastReport = Report.ToString();
                File.WriteAllText(Path.Combine(job, "validation.txt"), LastReport);
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            } finally {
                if (characterInstance) {
                    Object.DestroyImmediate(characterInstance);
                }

                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void ValidateData(Data conversionData) {
            if (conversionData == null || conversionData.meshes == null || conversionData.meshes.Length == 0 || conversionData.reference == null || conversionData.reference.Length == 0) {
                throw new Exception("The conversion returned incomplete mesh data.");
            }

            bool Finite(Vector3 v) => !float.IsNaN(v.x + v.y + v.z) && !float.IsInfinity(v.x + v.y + v.z);
            foreach (var meshPart in conversionData.meshes) {
                if (meshPart.vertices.Length != meshPart.normals.Length || meshPart.vertices.Length != meshPart.weights.Length || meshPart.vertices.Length != meshPart.uv.Length || meshPart.materials.Length != meshPart.submeshes.Length) {
                    throw new Exception("Invalid mesh arrays: " + meshPart.name);
                }

                if (meshPart.vertices.Any(v => !Finite(v))) {
                    throw new Exception("Nonfinite vertices: " + meshPart.name);
                }

                if (meshPart.submeshes.Any(submeshData => submeshData.triangles.Length % 3 != 0 || submeshData.triangles.Any(i => i < 0 || i >= meshPart.vertices.Length))) {
                    throw new Exception("Invalid triangles: " + meshPart.name);
                }

                if (meshPart.shapes.Any(blendShapeData => blendShapeData.delta.Length != meshPart.vertices.Length || blendShapeData.delta.Any(v => !Finite(v) || v.magnitude > .3f / Recipe.settings.canonicalScale))) {
                    throw new Exception("Invalid facial deltas: " + meshPart.name);
                }

                if (meshPart.weights.Any(vertexWeight => vertexWeight.names.Length != vertexWeight.values.Length || vertexWeight.values.Length > 4 || Mathf.Abs(vertexWeight.values.Sum() - 1) > .001f)) {
                    throw new Exception("Invalid bone weights: " + meshPart.name);
                }
            }
        }

        static void ValidatePoses(GameObject characterInstance, SkinnedMeshRenderer skin, AnimationClip clip) {
            if (skin.sharedMesh.blendShapeCount < 80) {
                throw new Exception("Native facial expressions are missing.");
            }

            var mesh = new Mesh();
            try {
                clip.SampleAnimation(characterInstance, 0);
                skin.BakeMesh(mesh);
                var baseline = mesh.vertices;
                var ids = skin.sharedMesh.vertices.Select((v, i) => (v, i)).Where(mapping => (mapping.v.y * Recipe.settings.canonicalScale + Recipe.settings.canonicalOffset) < 1.3f).Select(mapping2 => mapping2.i).ToArray();
                float error = 0;
                float seamError = 0;
                for (int f = 0; f <= 144; f++) {
                    clip.SampleAnimation(characterInstance, f / 24f);
                    skin.BakeMesh(mesh);
                    var v = mesh.vertices;
                    foreach (int i in ids) {
                        error = Mathf.Max(error, Vector3.Distance(v[i], baseline[i]));
                    }

                    foreach (var pair in SeamPairs) {
                        seamError = Mathf.Max(seamError, Vector3.Distance(v[pair.Item1], v[pair.Item2]));
                    }
                }

                if (error > 1e-5f) {
                    throw new Exception("Facial animation moves the body: " + error + "m");
                }

                if (seamError > .00002f / Recipe.settings.canonicalScale) {
                    throw new Exception("Facial or head animation opens a duplicated neck seam by " + seamError + "m.");
                }

                Report.AppendLine("PASS: connected neck topology after fitting; animated seam error " + seamError + "m.");
                clip.SampleAnimation(characterInstance, 0);
                var head = skin.bones.First(boneTransform => boneTransform.name.Equals("Head", StringComparison.OrdinalIgnoreCase));
                var rotation = head.rotation;
                var inverse = head.worldToLocalMatrix;
                skin.BakeMesh(mesh);
                baseline = mesh.vertices;
                int hi = Array.IndexOf(skin.bones, head);
                var weights = skin.sharedMesh.boneWeights;
                var headIds = Enumerable.Range(0, weights.Length).Where(i => weights[i].boneIndex0 == hi && weights[i].weight0 > .9999f).ToArray();
                float moved = 0, headError = 0;
                var attachments = characterInstance.GetComponentsInChildren<MeshRenderer>().Where(meshRenderer => meshRenderer.transform.IsChildOf(head)).ToArray();
                var local = attachments.Select(meshRenderer2 => head.worldToLocalMatrix * meshRenderer2.transform.localToWorldMatrix).ToArray();
                foreach (float angle in ValidationHeadAngles) {
                    head.rotation = Quaternion.AngleAxis(angle, Vector3.up) * rotation;
                    skin.BakeMesh(mesh);
                    var v = mesh.vertices;
                    foreach (int i in headIds) {
                        var start = skin.transform.TransformPoint(baseline[i]);
                        var actual = skin.transform.TransformPoint(v[i]);
                        var expected = head.localToWorldMatrix.MultiplyPoint3x4(inverse.MultiplyPoint3x4(start));
                        headError = Mathf.Max(headError, Vector3.Distance(expected, actual));
                        moved = Mathf.Max(moved, Vector3.Distance(start, actual));
                    }

                    for (int i = 0; i < attachments.Length; i++) {
                        var transformMatrix = head.worldToLocalMatrix * attachments[i].transform.localToWorldMatrix;
                        for (int k = 0; k < 16; k++) {
                            if (Mathf.Abs(transformMatrix[k] - local[i][k]) > .0001f) {
                                throw new Exception("Detached accessory: " + attachments[i].name);
                            }
                        }
                    }
                }

                head.rotation = rotation;
                clip.SampleAnimation(characterInstance, 0);
                if (headIds.Length < 100 || moved < .01f || headError > 1e-5f) {
                    throw new Exception("The skinned head does not follow the head bone correctly.");
                }

                Report.AppendLine("PASS: 145 facial samples; body motion " + error + "m. Actual head skin and " + attachments.Length + " rigid accessories follow +/-35 degrees; error " + headError + "m.");
                Report.AppendLine("Review required: neck joins, hair, jewelry clearance and source appearance at front, side, rear and under-chin views.");
            } finally {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
