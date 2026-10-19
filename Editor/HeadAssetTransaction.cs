// Backs up apply outputs and restores native objects in memory without refreshing the project.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace digitalbreed.SyntyHeadConverter {
    internal sealed class HeadAssetTransaction : IDisposable {
        private sealed class Snapshot {
            public Object original;
            public Object copy;
            public HideFlags hideFlags;
        }

        private readonly string[] roots;
        private readonly string backup;
        private readonly HashSet<string> before;
        private readonly List<Snapshot> objects = new List<Snapshot>();
        private readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

        internal HeadAssetTransaction(IEnumerable<string> paths, string backup) {
            string project = HeadConversionPipeline.Project.Replace('\\', '/').TrimEnd('/') + "/";
            roots = paths.Select(path => {
                string full = Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
                if (!full.StartsWith(project + "Assets/", StringComparison.OrdinalIgnoreCase)) {
                    throw new ArgumentException("Rollback paths must stay beneath Assets: " + path);
                }

                return full.Substring(project.Length);
            }).Distinct().ToArray();
            this.backup = backup;
            before = new HashSet<string>(Files());
            try {
                foreach (string path in before) {
                    string copy = Path.Combine(backup, path);
                    Directory.CreateDirectory(Path.GetDirectoryName(copy));
                    File.Copy(path, copy, false);
                    if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }

                    if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) {
                        prefabs.Add(path, PrefabUtility.LoadPrefabContents(path));
                        continue;
                    }

                    var main = AssetDatabase.LoadMainAssetAtPath(path);
                    if (!main || !AssetDatabase.IsNativeAsset(main)) {
                        continue;
                    }

                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) {
                        if (!asset) {
                            continue;
                        }

                        var snapshot = Object.Instantiate(asset);
                        snapshot.name = asset.name;
                        snapshot.hideFlags = HideFlags.HideAndDontSave;
                        objects.Add(new Snapshot { original = asset, copy = snapshot, hideFlags = asset.hideFlags });
                        // Keep persistent references, including controller subassets, pointing at their
                        // original objects rather than any children duplicated by Instantiate.
                        using (var source = new SerializedObject(asset)) {
                            using (var destination = new SerializedObject(snapshot)) {
                                var property = destination.GetIterator();
                                while (property.Next(true)) {
                                    if (property.propertyType == SerializedPropertyType.ObjectReference) {
                                        var original = source.FindProperty(property.propertyPath);
                                        if (original != null) {
                                            property.objectReferenceValue = original.objectReferenceValue;
                                        }
                                    }
                                }

                                destination.ApplyModifiedPropertiesWithoutUndo();
                            }
                        }
                    }
                }
            } catch {
                Dispose();
                throw;
            }
        }

        private IEnumerable<string> Files() {
            return roots.SelectMany(path => Directory.Exists(path) ? Directory.GetFiles(path, "*", SearchOption.AllDirectories) :
                File.Exists(path) ? new[] { path } : Array.Empty<string>())
                .Concat(roots.Select(path => path + ".meta").Where(File.Exists))
                .Select(path => path.Replace('\\', '/')).Distinct();
        }

        internal void Restore() {
            // Delete newly registered assets through Unity so cached references are removed as well.
            var added = Files().Where(path => !before.Contains(path)).OrderByDescending(path => path.Length).ToArray();
            foreach (string path in added.Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))) {
                if (!AssetDatabase.DeleteAsset(path) && File.Exists(path)) {
                    File.Delete(path);
                }
            }

            foreach (string path in added.Where(path => File.Exists(path))) {
                string asset = path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ? path.Substring(0, path.Length - 5) : path;
                if (Directory.Exists(asset)) {
                    if (!Directory.EnumerateFileSystemEntries(asset).Any() && !AssetDatabase.DeleteAsset(asset)) {
                        Directory.Delete(asset);
                        File.Delete(path);
                    }
                } else {
                    File.Delete(path);
                }
            }

            // Keep unchanged inputs (including old PNG outputs and their metadata) untouched.
            foreach (string path in before) {
                string copy = Path.Combine(backup, path);
                if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(copy))) {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.Copy(copy, path, true);
                }
            }

            foreach (var snapshot in objects) {
                if (snapshot.original) {
                    if (snapshot.copy is Mesh meshCopy && snapshot.original is Mesh originalMesh) {
                        HeadMeshAssets.CopySerialized(meshCopy, originalMesh);
                    } else {
                        EditorUtility.CopySerialized(snapshot.copy, snapshot.original);
                    }
                    snapshot.original.hideFlags = snapshot.hideFlags;
                    EditorUtility.SetDirty(snapshot.original);
                }
            }

            foreach (var pair in prefabs) {
                if (!PrefabUtility.SaveAsPrefabAsset(pair.Value, pair.Key)) {
                    throw new IOException("Could not restore prefab " + pair.Key + ". Its backup is retained at " + backup);
                }
            }

            AssetDatabase.SaveAssets();
        }

        public void Dispose() {
            foreach (var root in prefabs.Values) {
                if (root) {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            foreach (var snapshot in objects) {
                if (snapshot.copy) {
                    Object.DestroyImmediate(snapshot.copy);
                }
            }

            prefabs.Clear();
            objects.Clear();
        }
    }
}
