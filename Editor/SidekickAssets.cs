// Discovers licensed Sidekick parts and imports their geometry, skeletons and expression deltas for conversion.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace digitalbreed.SyntyHeadConverter {
    internal static class SidekickAssets {
        internal sealed class Donor {
            public Vector3[] vertices;
            public Vector3[] normals;
            public Vector2[] uv;
            public int[][] faces;
            public HeadAssetBuilder.Weight[] weights;
            public readonly Dictionary<string, Vector3[]> shapes = new Dictionary<string, Vector3[]>();
            public HeadAssetBuilder.Bone[] bones;
        }

        private static readonly string[] Codes = { "01HEAD", "05EYEL", "06EYER", "03EBRL", "04EBRR", "35NOSE", "36TETH", "37TONG", "07EARL", "08EARR" };
        private static string Part(HeadConversionRecipe recipe, string code) {
            string variant = code == "03EBRL" || code == "04EBRR" ? recipe.settings.browVariant : code == "35NOSE" ? recipe.settings.noseVariant : "01";
            return "SK_HUMN_BASE_" + variant + "_" + code + "_HU01";
        }

        internal sealed class PartSource {
            public string assetPath;
            public string fullPath;
        }

        private sealed class CacheReceipt {
            public string profileKey;
            public string sourceHash;
            public string metadataHash;
        }

        internal static Dictionary<string, PartSource> Resolve(HeadConversionRecipe recipe) {
            var names = Codes.ToDictionary(code => code, code => Part(recipe, code));
            string beard = recipe.settings.sidekickBeard;
            if (!string.IsNullOrEmpty(beard)) {
                if (!System.Text.RegularExpressions.Regex.IsMatch(beard, "^SK_HUMN_[A-Z0-9_]+_09FCHR_HU01$")) {
                    throw new Exception("Choose a Sidekick facial hair FBX filename ending in _09FCHR_HU01.");
                }

                names.Add("09FCHR", beard);
            }

            string selected = HeadConversionPipeline.Sidekick;
            if (!string.IsNullOrWhiteSpace(selected)) {
                string root = selected.Replace('\\', '/').TrimEnd('/');
                string fullRoot = root.StartsWith("Packages/", StringComparison.Ordinal) ? PhysicalPath(root) : Path.GetFullPath(root);
                string humans = Path.Combine(fullRoot, "Meshes/Species/Humans");
                string directory = Directory.Exists(humans) ? humans : fullRoot;
                return ResolveDirectory(directory, names);
            }

            const string anchor = "SK_HUMN_BASE_01_01HEAD_HU01";
            var directories = AssetDatabase.FindAssets(anchor + " t:Model")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !path.StartsWith(HeadConversionPipeline.CacheRoot + "/", StringComparison.Ordinal) &&
                    Path.GetFileNameWithoutExtension(path) == anchor)
                .Select(path => Path.GetDirectoryName(path).Replace('\\', '/'))
                .Distinct().OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var complete = directories.Where(directory => names.Values.All(name =>
                File.Exists(PhysicalPath(directory + "/" + name + ".fbx")))).ToArray();
            if (complete.Length == 1) {
                string directory = complete[0];
                return names.ToDictionary(pair => pair.Key, pair => new PartSource {
                    assetPath = directory + "/" + pair.Value + ".fbx",
                    fullPath = PhysicalPath(directory + "/" + pair.Value + ".fbx")
                });
            }

            if (complete.Length > 1) {
                throw new Exception("Several complete Sidekick installations were found. Select one folder in Settings:\n" + string.Join("\n", complete));
            }

            if (directories.Length == 1) {
                return ResolveDirectory(PhysicalPath(directories[0]), names);
            }

            throw new Exception("No complete Sidekick installation was found. Select its SidekickCharacters or Humans folder in Settings. This can be inside a different staging project; that project does not need to be open.");
        }

        internal static Dictionary<string, PartSource> ResolveDirectory(string directory, Dictionary<string, string> names) {
            var missing = names.Values.Where(name => !File.Exists(Path.Combine(directory, name + ".fbx"))).ToArray();
            if (missing.Length > 0) {
                throw new Exception("Sidekick parts are missing from " + directory + ":\n" + string.Join("\n", missing) +
                    "\nSelect a complete SidekickCharacters or Humans folder in Settings. Parts from other installations are not mixed into this selection.");
            }

            return names.ToDictionary(pair => pair.Key, pair => {
                string full = Path.GetFullPath(Path.Combine(directory, pair.Value + ".fbx"));
                return new PartSource { fullPath = full, assetPath = AssetPath(full) };
            });
        }

        internal static string PhysicalPath(string assetPath) {
            assetPath = assetPath.Replace('\\', '/');
            if (assetPath.StartsWith("Packages/", StringComparison.Ordinal)) {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
                if (package == null) {
                    throw new IOException("Unity cannot resolve the package containing " + assetPath);
                }

                string relative = assetPath.Substring(("Packages/" + package.name).Length).TrimStart('/');
                return Path.GetFullPath(Path.Combine(package.resolvedPath, relative));
            }

            return Path.GetFullPath(assetPath);
        }

        private static string AssetPath(string fullPath) {
            string full = fullPath.Replace('\\', '/');
            string project = HeadConversionPipeline.Project.Replace('\\', '/') + "/";
            if (full.StartsWith(project + "Assets/", StringComparison.OrdinalIgnoreCase)) {
                return full.Substring(project.Length);
            }

            foreach (var package in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()) {
                string root = package.resolvedPath.Replace('\\', '/').TrimEnd('/') + "/";
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) {
                    return "Packages/" + package.name + "/" + full.Substring(root.Length);
                }
            }

            return null;
        }

        public static void Validate(HeadConversionRecipe recipe) => Resolve(recipe);

        private static bool Compatible(string path) {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            return importer && importer.keepQuads && importer.importBlendShapes && importer.isReadable &&
                !importer.optimizeGameObjects && importer.meshCompression == ModelImporterMeshCompression.Off;
        }

        private static string Import(PartSource source) {
            if (!string.IsNullOrEmpty(source.assetPath) && Compatible(source.assetPath)) {
                return source.assetPath;
            }

            string metadata = File.Exists(source.fullPath + ".meta") ? File.ReadAllText(source.fullPath + ".meta") : null;
            string hash = SidekickImportProfile.Fingerprint(source.fullPath, metadata);
            string sourceHash = SidekickImportProfile.FileHash(source.fullPath);
            string inputs = HeadConversionPipeline.CacheRoot + "/Inputs";
            string filename = Path.GetFileName(source.fullPath);
            if (Directory.Exists(inputs)) {
                foreach (string existing in Directory.GetDirectories(inputs, hash + "*").OrderBy(path => path, StringComparer.Ordinal)) {
                    string cached = existing.Replace('\\', '/') + "/" + filename;
                    if (Reusable(cached, hash, sourceHash)) {
                        return cached;
                    }
                }
            }

            string directory = inputs + "/" + hash;
            if (Directory.Exists(directory) || File.Exists(directory + ".meta")) {
                directory += "-" + Guid.NewGuid().ToString("N");
            }

            HeadAssetFolders.Ensure(directory);
            string target = directory + "/" + filename;
            // The new metadata exists before the new FBX can be observed by Unity.
            File.WriteAllText(target + ".meta", SidekickImportProfile.Prepare(metadata, Guid.NewGuid().ToString("N")));
            File.Copy(source.fullPath, target, false);
            if (SidekickImportProfile.FileHash(target) != sourceHash) {
                throw new IOException("The Sidekick source changed while being copied. Build again after it finishes changing: " + source.fullPath);
            }

            // Only this newly created file is imported. Existing cache entries are never overwritten.
            AssetDatabase.ImportAsset(target);
            if (!Compatible(target)) {
                throw new Exception("The new Sidekick copy did not retain its prepared import settings: " + target +
                    ". Existing assets were not reconfigured or reimported.");
            }

            string receiptPath = ReceiptPath(target);
            Directory.CreateDirectory(Path.GetDirectoryName(receiptPath));
            File.WriteAllText(receiptPath, HeadConverterJson.Serialize(new CacheReceipt {
                profileKey = hash,
                sourceHash = sourceHash,
                metadataHash = SidekickImportProfile.FileHash(target + ".meta")
            }));

            return target;
        }

        private static string ReceiptPath(string assetPath) {
            return "Library/SyntyHeadConverter/Inputs/" + AssetDatabase.AssetPathToGUID(assetPath) + ".json";
        }

        private static bool Reusable(string path, string profileKey, string sourceHash) {
            if (!File.Exists(path) || !File.Exists(path + ".meta") || !Compatible(path)) {
                return false;
            }

            string receiptPath = ReceiptPath(path);
            if (!File.Exists(receiptPath)) {
                return false;
            }

            try {
                var receipt = HeadConverterJson.Deserialize<CacheReceipt>(File.ReadAllText(receiptPath));
                return receipt != null && receipt.profileKey == profileKey && receipt.sourceHash == sourceHash &&
                    receipt.metadataHash == SidekickImportProfile.FileHash(path + ".meta") &&
                    sourceHash == SidekickImportProfile.FileHash(path);
            } catch (Newtonsoft.Json.JsonException) {
                return false;
            }
        }

        public static Dictionary<string, Donor> Read(HeadConversionRecipe recipe, Dictionary<string, PartSource> sources = null) {
            return (sources ?? Resolve(recipe)).ToDictionary(pair => pair.Key, pair => ReadPart(Import(pair.Value)));
        }

        private static Donor ReadPart(string path) {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) {
                throw new Exception("Unity could not load the Sidekick model: " + path);
            }

            var scene = EditorSceneManager.NewPreviewScene();
            GameObject instance = null;
            try {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var skin = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(skinRenderer => skinRenderer.sharedMesh);
                if (!skin) {
                    throw new Exception("Sidekick part has no skinned mesh: " + path);
                }

                var mesh = skin.sharedMesh;
                if (!mesh.isReadable || mesh.vertexCount == 0 || mesh.normals.Length != mesh.vertexCount ||
                    mesh.uv.Length != mesh.vertexCount || mesh.boneWeights.Length != mesh.vertexCount ||
                    skin.bones.Any(bone => !bone) || skin.bones.Length != mesh.bindposes.Length) {
                    throw new Exception("The Sidekick part has incomplete readable geometry, normals, UVs or skeleton data: " + path);
                }

                var faces = new List<int[]>();
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++) {
                    var topology = mesh.GetTopology(submesh);
                    if (topology != MeshTopology.Quads && topology != MeshTopology.Triangles) {
                        throw new Exception("Unsupported Sidekick surface topology: " + path);
                    }

                    int count = topology == MeshTopology.Quads ? 4 : 3;
                    var indices = mesh.GetIndices(submesh);
                    if (indices.Length % count != 0) {
                        throw new Exception("Incomplete Sidekick surface indices: " + path);
                    }
                    for (int i = 0; i < indices.Length; i += count) {
                        faces.Add(indices.Skip(i).Take(count).ToArray());
                    }
                }

                Matrix4x4 matrix = skin.transform.localToWorldMatrix;
                var normalMatrix = matrix.inverse.transpose;
                var data = new Donor {
                    vertices = mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray(),
                    normals = mesh.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray(),
                    uv = mesh.uv,
                    faces = faces.ToArray(),
                    weights = mesh.boneWeights.Select(boneWeight => new HeadAssetBuilder.Weight { names = new[] { skin.bones[boneWeight.boneIndex0].name, skin.bones[boneWeight.boneIndex1].name, skin.bones[boneWeight.boneIndex2].name, skin.bones[boneWeight.boneIndex3].name }, values = new[] { boneWeight.weight0, boneWeight.weight1, boneWeight.weight2, boneWeight.weight3 } }).ToArray(),
                    bones = skin.bones.Select((boneTransform, i) => new HeadAssetBuilder.Bone { name = boneTransform.name, parent = boneTransform.parent ? boneTransform.parent.name : "", position = (matrix * mesh.bindposes[i].inverse).GetColumn(3) }).ToArray()
                };
                for (int shape = 0; shape < mesh.blendShapeCount; shape++) {
                    int frame = mesh.GetBlendShapeFrameCount(shape) - 1;
                    if (frame < 0 || mesh.GetBlendShapeFrameWeight(shape, frame) <= 0) {
                        throw new Exception("Invalid Sidekick expression frame: " + path);
                    }
                    var delta = new Vector3[mesh.vertexCount];
                    mesh.GetBlendShapeFrameVertices(shape, frame, delta, null, null);
                    float factor = 100 / mesh.GetBlendShapeFrameWeight(shape, frame);
                    string name = mesh.GetBlendShapeName(shape).Split('.').Last();
                    data.shapes.Add(name, delta.Select(v => matrix.MultiplyVector(v) * factor).ToArray());
                }

                if (!data.shapes.ContainsKey("jawOpen") && Path.GetFileName(path).Contains("01HEAD")) {
                    throw new Exception("The Sidekick head is missing native expression blendshapes: " + path + ". Select an installation containing the original expressive Sidekick meshes.");
                }

                return data;
            } finally {
                if (instance) {
                    Object.DestroyImmediate(instance);
                }

                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
