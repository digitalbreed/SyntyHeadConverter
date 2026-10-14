// Coordinates preview builds, recipe freshness, validation, asset application and rollback after failed writes.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace digitalbreed.SyntyHeadConverter {
    [InitializeOnLoad]
    public static class HeadConversionPipeline {
        public const string CacheRoot = "Assets/SyntyHeadConverterCache";
        public const string PreviewRoot = CacheRoot + "/Previews";
        public static string Project => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        public static string Sidekick {
            get => EditorPrefs.GetString("digitalbreed.SyntyHeadConverter.Sidekick", "");
            set => EditorPrefs.SetString("digitalbreed.SyntyHeadConverter.Sidekick", value);
        }
        public static bool Busy => work != null;
        public static GameObject Result {
            get;
            private set;
        }
        public static string JobDirectory {
            get;
            private set;
        }
        public static string Error {
            get;
            private set;
        }
        public static string Report {
            get;
            private set;
        }
        public static string Progress {
            get;
            internal set;
        }

        public static event Action Changed;
        private static IEnumerator work;
        private static HeadConversionRecipe frozen;
        private static string recipeJson;
        private static string textureFingerprint;
        private static string sidekickSelection;
        private static Dictionary<string, SidekickAssets.PartSource> sidekickSources;
        private static HeadAssetBuilder.Data data;
        static HeadConversionPipeline() {
            AssemblyReloadEvents.beforeAssemblyReload += Cancel;
            EditorApplication.quitting += Cancel;
        }

        public static void ValidateRecipe(HeadConversionRecipe recipe) {
            if (!recipe || !recipe.sourcePrefab) {
                throw new Exception("Choose the original prefab first.");
            }

            if (!Regex.IsMatch(recipe.characterId ?? "", "^[A-Za-z][A-Za-z0-9_]{0,63}$")) {
                throw new Exception("Character name must start with a letter and contain only letters, digits or underscores.");
            }

            string folder = recipe.outputFolder?.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets/", StringComparison.Ordinal) || folder.Split('/').Any(p => p == ".." || p == "." || p.Length == 0) || folder.IndexOfAny(new[] { ':', '*', '?', '"', '<', '>', '|' }) >= 0 || folder.StartsWith(CacheRoot, StringComparison.Ordinal)) {
                throw new Exception("Choose a character folder inside Assets, outside the converter cache.");
            }

            if (AssetDatabase.GetAssetPath(recipe.sourcePrefab) == folder + "/" + recipe.characterId + ".prefab") {
                throw new Exception("The output must not replace the original vendor prefab.");
            }

            if (recipe.settings.canonicalScale <= 0 || recipe.settings.width < .5f || recipe.settings.width > 1.6f || recipe.settings.cut < 1.3f || recipe.settings.cut > 1.56f) {
                throw new Exception("Head scale, width or neck cut is outside the supported range.");
            }

            if (recipe.sourceFingerprint != HeadSource.Fingerprint(recipe.sourcePrefab)) {
                throw new Exception("The source prefab changed. Analyze it again before reusing its region selections.");
            }

            FaceTextures.CustomTextureFingerprint(recipe);
            new RecipeOptions(recipe.baseConfig);
            SidekickAssets.Validate(recipe);
        }

        public static void Begin(HeadConversionRecipe recipe) {
            if (Busy) {
                throw new Exception("A conversion is already running.");
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                throw new Exception("Exit Play mode before converting.");
            }

            ValidateRecipe(recipe);
            Result = null;
            Error = null;
            Report = null;
            data = null;
            if (frozen) {
                Object.DestroyImmediate(frozen);
            }

            frozen = Object.Instantiate(recipe);
            frozen.hideFlags = HideFlags.HideAndDontSave;
            recipeJson = HeadConverterJson.Serialize(recipe);
            textureFingerprint = FaceTextures.CustomTextureFingerprint(recipe);
            sidekickSelection = Sidekick;
            sidekickSources = SidekickAssets.Resolve(recipe);
            JobDirectory = Path.Combine(Project, "Library/SyntyHeadConverter", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(JobDirectory, "authoring/unity"));
            EnsureLocalCache();
            work = Convert();
            EditorApplication.update += Poll;
            Changed?.Invoke();
        }

        private static IEnumerator Convert() {
            Progress = "Reading the original bind pose";
            yield return null;
            var source = HeadSource.Capture(frozen.sourcePrefab);
            int faces = source.skins[0].slots.Sum(s => s.triangles.Length) / 3;
            if (frozen.regions.Any(r => r.faces == null || r.faces.Any(i => i < 0 || i >= faces) || !new[] { "Auto", "Preserve", "Accessory", "Necklace", "Remove" }.Contains(r.role))) {
                throw new Exception("Invalid source region selection. Analyze the source again.");
            }

            Progress = "Reading native Sidekick meshes and expressions";
            yield return null;
            var donors = SidekickAssets.Read(frozen, sidekickSources);
            Progress = "Fitting the head and connected neckline";
            yield return null;
            data = NativeHeadBuilder.Build(frozen, source, donors, Path.Combine(JobDirectory, "authoring/unity"));
            Progress = "Validating animation and creating the Unity preview";
            yield return null;
            Result = HeadAssetBuilder.Build(frozen, data, JobDirectory, PreviewRoot + "/" + Path.GetFileName(JobDirectory));
            Report = HeadAssetBuilder.LastReport;
            Progress = "Preview ready";
        }

        private static void Poll() {
            if (work == null) {
                EditorApplication.update -= Poll;
                return;
            }

            try {
                if (work.MoveNext()) {
                    Changed?.Invoke();
                    return;
                }
            } catch (Exception error) {
                Error = error.Message;
                File.WriteAllText(Path.Combine(JobDirectory, "failure.txt"), error.ToString());
                Debug.LogException(error);
            }

            (work as IDisposable)?.Dispose();
            work = null;
            EditorApplication.update -= Poll;
            Changed?.Invoke();
        }

        public static void Cancel() {
            if (work == null) {
                return;
            }

            (work as IDisposable)?.Dispose();
            work = null;
            data = null;
            EditorApplication.update -= Poll;
            Result = null;
            Error = "Conversion cancelled. Production assets were not changed.";
            Changed?.Invoke();
        }

        public static bool Matches(HeadConversionRecipe recipe) {
            if (!Result || !frozen || !recipe || sidekickSelection != Sidekick || recipeJson != HeadConverterJson.Serialize(recipe)) {
                return false;
            }

            try {
                return textureFingerprint == FaceTextures.CustomTextureFingerprint(recipe) && frozen.sourceFingerprint == HeadSource.Fingerprint(frozen.sourcePrefab);
            } catch (ArgumentException) {
                return false;
            }
        }

        public static GameObject Apply(HeadConversionRecipe recipe) {
            if (Busy || !Matches(recipe)) {
                throw new Exception("Build and review the current recipe before applying it.");
            }

            ValidateRecipe(recipe);
            string output = recipe.outputFolder.Replace('\\', '/').TrimEnd('/');
            string backup = Path.Combine(JobDirectory, "rollback", Guid.NewGuid().ToString("N"));
            var paths = new List<string> {
                output
            };
            var additional = HeadConversionExtensions.AdditionalApplyFiles?.Invoke(recipe);
            if (additional != null) {
                paths.AddRange(additional);
            }

            foreach (string path in paths) {
                string full = Path.GetFullPath(path);
                if (!full.StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
                    throw new Exception("An integration requested a rollback path outside Assets: " + path);
                }
            }

            using var transaction = new HeadAssetTransaction(paths, backup);

            try {
                var result = HeadAssetBuilder.Build(frozen, data, JobDirectory, output);
                string recipePath = output + "/" + recipe.characterId + "Conversion.asset";
                var saved = AssetDatabase.LoadAssetAtPath<HeadConversionRecipe>(recipePath);
                var compact = recipe.CopyForSaving();
                if (saved) {
                    EditorUtility.CopySerialized(compact, saved);
                    EditorUtility.SetDirty(saved);
                    Object.DestroyImmediate(compact);
                } else {
                    AssetDatabase.CreateAsset(compact, recipePath);
                }

                AssetDatabase.SaveAssets();
                HeadConversionExtensions.AfterApply?.Invoke(recipe, result);
                Report = HeadAssetBuilder.LastReport + "\nSaved " + output + ".";
                Changed?.Invoke();
                return result;
            } catch (Exception applyError) {
                try {
                    transaction.Restore();
                } catch (Exception rollbackError) {
                    throw new AggregateException("Apply and rollback failed. The original files are retained at " + backup, applyError, rollbackError);
                }

                throw;
            }
        }

        internal static void EnsureLocalCache() {
            HeadAssetFolders.Ensure(CacheRoot);
            File.WriteAllText(CacheRoot + "/.gitignore", "*\n");
            const string ignores = "\n# Reproducible Synty Head Converter previews and locally imported licensed inputs\n/Assets/SyntyHeadConverterCache/\n/Assets/SyntyHeadConverterCache.meta\n";
            string gitignore = Path.Combine(Project, ".gitignore");
            string existing = File.Exists(gitignore) ? File.ReadAllText(gitignore) : "";
            if (!existing.Contains("/Assets/SyntyHeadConverterCache/")) {
                File.AppendAllText(gitignore, ignores);
            }
        }
    }
}
