// Provides the converter window, appearance/fit controls, recipe editing and synchronized comparison previews.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace digitalbreed.SyntyHeadConverter {
    public sealed class HeadConverterWindow : EditorWindow {
        private static readonly (string, float, float)[] PreviewViews = { ("Front", 0f, 6f), ("Side", 90f, 6f), ("Back", 180f, 6f), ("Under chin", 20f, 30f) };
        HeadConversionRecipe recipe;
        [SerializeField]
        HeadConversionRecipe recipeAsset;
        [SerializeField]
        GameObject source;
        [SerializeField]
        string sessionRecipe;
        HeadSource.Source captured;
        HeadPreview originalPreview, resultPreview;
        Vector2 scroll, regionsScroll;
        int tab, selectedRegion = -1;
        float headTurn, blink, jaw, smile;
        bool wire, fullBody, play, reviewed, settings;
        double started;
        string error;
        readonly string[] variants = Enumerable.Range(1, 10).Select(i => i.ToString("00")).ToArray();
        readonly string[] roles = { "Auto", "Preserve", "Accessory", "Necklace", "Remove" };
        [MenuItem("Tools/digitalbreed/Synty Head Converter")]
        public static void Open() {
            var w = GetWindow<HeadConverterWindow>("Synty Head Converter");
            w.minSize = new Vector2(940, 660);
        }

        void OnEnable() {
            originalPreview = new HeadPreview();
            resultPreview = new HeadPreview();
            resultPreview.Set(HeadConversionPipeline.Result);
            HeadConversionPipeline.Changed += Changed;
            EditorApplication.update += Tick;
            if (!string.IsNullOrEmpty(sessionRecipe)) {
                Run(() => {
                    var restored = CreateInstance<HeadConversionRecipe>();
                    HeadConverterJson.Populate(sessionRecipe, restored);
                    SetRecipe(restored);
                });
            }
        }

        void OnDisable() {
            if (recipe) {
                sessionRecipe = HeadConverterJson.Serialize(recipe);
            }

            originalPreview?.Dispose();
            resultPreview?.Dispose();
            HeadConversionPipeline.Changed -= Changed;
            EditorApplication.update -= Tick;
            if (recipe && !EditorUtility.IsPersistent(recipe)) {
                DestroyImmediate(recipe);
            }
        }

        void Changed() {
            resultPreview.Set(HeadConversionPipeline.Result);
            reviewed = false;
            Repaint();
        }

        void Tick() {
            if (play || HeadConversionPipeline.Busy) {
                Repaint();
            }
        }

        void SetRecipe(HeadConversionRecipe next) {
            if (recipe && !EditorUtility.IsPersistent(recipe)) {
                DestroyImmediate(recipe);
            }

            recipe = next;
            source = next ? next.sourcePrefab : null;
            captured = source ? HeadSource.Capture(source) : null;
            if (recipe && captured != null) {
                var overrides = recipe.regions;
                recipe.regions = HeadSource.Regions(captured);
                foreach (var region in recipe.regions) {
                    var previous = overrides.FirstOrDefault(r => r.faces != null && r.faces.SequenceEqual(region.faces));
                    if (previous != null) {
                        region.role = previous.role;
                    }
                }
            }

            originalPreview.Set(source);
            selectedRegion = -1;
            originalPreview.Region(null, null);
            reviewed = false;
            error = null;
        }

        void Run(Action action) {
            try {
                action();
                error = null;
            } catch (Exception e) {
                error = e.Message;
                Debug.LogException(e);
            }
        }

        void OnGUI() {
            DrawWindow();
        }

        void DrawWindow() {
            EditorGUILayout.Space();
            GUILayout.Label("POLYGON \u2192 Sidekick", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope()) {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(370))) {
                    scroll = EditorGUILayout.BeginScrollView(scroll);
                    using (new EditorGUI.DisabledScope(HeadConversionPipeline.Busy)) {
                        var next = (GameObject)EditorGUILayout.ObjectField("Original prefab", source, typeof(GameObject), false);
                        if (next != source) {
                            source = next;
                            reviewed = false;
                        }

                        if (GUILayout.Button("Analyze source / load existing fit")) {
                            Run(() => SetRecipe(HeadSource.Suggest(source)));
                        }

                        var load = (HeadConversionRecipe)EditorGUILayout.ObjectField("Saved recipe", recipeAsset, typeof(HeadConversionRecipe), false);
                        if (load != recipeAsset) {
                            recipeAsset = load;
                            if (load) {
                                Run(() => SetRecipe(Instantiate(load)));
                            }
                        }

                        if (recipe) {
                            if (source != recipe.sourcePrefab) {
                                EditorGUILayout.HelpBox("Analyze the newly selected source before building.", MessageType.Warning);
                            }

                            EditorGUI.BeginChangeCheck();
                            recipe.characterId = EditorGUILayout.TextField("Character name", recipe.characterId);
                            recipe.outputFolder = EditorGUILayout.TextField("Output folder", recipe.outputFolder);
                            tab = GUILayout.Toolbar(tab, new[] { "Appearance", "Fit", "Regions", "Recipe" });
                            EditorGUILayout.Space();
                            if (tab == 0) {
                                Appearance();
                            }

                            if (tab == 1) {
                                Fit();
                            }

                            if (tab == 2) {
                                Regions();
                            }

                            if (tab == 3) {
                                Advanced();
                            }

                            if (EditorGUI.EndChangeCheck()) {
                                reviewed = false;
                            }

                            if (GUILayout.Button("Save recipe\u2026")) {
                                Run(SaveRecipe);
                            }

                            using (new EditorGUI.DisabledScope(source != recipe.sourcePrefab)) {
                                if (GUILayout.Button("Build preview", GUILayout.Height(32))) {
                                    Run(() => {
                                        reviewed = false;
                                        HeadConversionPipeline.Begin(recipe);
                                    });
                                }
                            }
                        }
                    }

                    settings = EditorGUILayout.Foldout(settings, "Settings", true);
                    if (settings) {
                        HeadConversionPipeline.Sidekick = EditorGUILayout.TextField("SidekickCharacters", HeadConversionPipeline.Sidekick);
                        EditorGUILayout.HelpBox("Choose a SidekickCharacters or Humans folder, including one in another staging project. Leave empty to discover a single installation in this project. The staging project does not need to be open.", MessageType.Info);
                        if (GUILayout.Button("Choose Sidekick folder\u2026")) {
                            string p = EditorUtility.OpenFolderPanel("SidekickCharacters", HeadConversionPipeline.Sidekick, "");
                            if (p.Length > 0) {
                                HeadConversionPipeline.Sidekick = p;
                            }
                        }
                    }

                    if (HeadConversionPipeline.Busy) {
                        EditorGUILayout.HelpBox("Unity is fitting the head. Production assets have not changed.", MessageType.Info);
                        if (GUILayout.Button("Cancel conversion")) {
                            HeadConversionPipeline.Cancel();
                        }
                    }

                    if (!string.IsNullOrEmpty(error ?? HeadConversionPipeline.Error)) {
                        EditorGUILayout.HelpBox(error ?? HeadConversionPipeline.Error, MessageType.Error);
                    }

                    if (!string.IsNullOrEmpty(HeadConversionPipeline.Report)) {
                        EditorGUILayout.HelpBox(HeadConversionPipeline.Report, MessageType.Info);
                    }

                    if (HeadConversionPipeline.Result && recipe && !HeadConversionPipeline.Matches(recipe)) {
                        EditorGUILayout.HelpBox("The recipe changed. Rebuild the preview before applying it.", MessageType.Warning);
                    }

                    if (!string.IsNullOrEmpty(HeadConversionPipeline.JobDirectory) && GUILayout.Button("Open local job folder")) {
                        EditorUtility.RevealInFinder(HeadConversionPipeline.JobDirectory);
                    }

                    using (new EditorGUI.DisabledScope(!recipe || source != recipe.sourcePrefab || !HeadConversionPipeline.Matches(recipe) || HeadConversionPipeline.Busy)) {
                        reviewed = EditorGUILayout.ToggleLeft("I reviewed the neck, hair and accessories from all sides", reviewed);
                        using (new EditorGUI.DisabledScope(!reviewed)) {
                            if (GUILayout.Button("Apply to character assets", GUILayout.Height(32))) {
                                Run(() => {
                                    var result = HeadConversionPipeline.Apply(recipe);
                                    Selection.activeObject = result;
                                    EditorGUIUtility.PingObject(result);
                                });
                            }
                        }
                    }

                    EditorGUILayout.EndScrollView();
                }

                using (new EditorGUILayout.VerticalScope()) {
                    Controls();
                    var rect = GUILayoutUtility.GetRect(400, 400, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    var left = new Rect(rect.x, rect.y, rect.width / 2 - 3, rect.height);
                    var right = new Rect(rect.center.x + 3, rect.y, rect.width / 2 - 3, rect.height);
                    if (Event.current.type == EventType.Repaint) {
                        float t = (float)(EditorApplication.timeSinceStartup - started);
                        float turn = play ? 35 * Mathf.Sin(t) : headTurn;
                        float mouth = play ? 40 * Mathf.Max(0, Mathf.Sin(t * 2)) : jaw;
                        var a = originalPreview.Draw(left, turn, blink, mouth, smile, 0, wire, fullBody);
                        if (a) {
                            GUI.DrawTexture(left, a, ScaleMode.StretchToFill);
                        }

                        var b = resultPreview.Draw(right, turn, blink, mouth, smile, recipe ? recipe.lipFullness : 0, wire, fullBody);
                        if (b) {
                            GUI.DrawTexture(right, b, ScaleMode.StretchToFill);
                        }
                    }

                    GUI.Label(new Rect(left.x + 8, left.y + 8, 180, 24), "Original", EditorStyles.whiteBoldLabel);
                    GUI.Label(new Rect(right.x + 8, right.y + 8, 180, 24), "Converted preview", EditorStyles.whiteBoldLabel);
                    var e = Event.current;
                    if (rect.Contains(e.mousePosition)) {
                        if (e.type == EventType.MouseDrag && e.button == 0) {
                            originalPreview.yaw += e.delta.x;
                            originalPreview.pitch = Mathf.Clamp(originalPreview.pitch - e.delta.y, -75, 75);
                            SyncCamera();
                            e.Use();
                            Repaint();
                        }

                        if (e.type == EventType.ScrollWheel) {
                            originalPreview.zoom = Mathf.Clamp(originalPreview.zoom + e.delta.y * .04f, .35f, 3);
                            SyncCamera();
                            e.Use();
                            Repaint();
                        }
                    }

                    GUILayout.Label("Drag to orbit \u00b7 scroll to zoom \u00b7 green highlights the selected source region", EditorStyles.miniLabel);
                }
            }
        }

        void Appearance() {
            var settings = recipe.settings;
            settings.female = EditorGUILayout.Popup("Face", settings.female, new[] { "Masculine", "Feminine" });
            settings.noseVariant = variants[EditorGUILayout.Popup("Native nose", Mathf.Max(0, Array.IndexOf(variants, settings.noseVariant)), variants)];
            settings.browVariant = variants[EditorGUILayout.Popup("Native eyebrows", Mathf.Max(0, Array.IndexOf(variants, settings.browVariant)), variants)];
            settings.skin = ColorField("Skin", settings.skin);
            settings.brow = ColorField("Eyebrows / facial hair", settings.brow);
            settings.lip = ColorField("Lips", settings.lip);
            settings.iris = ColorField("Eyes", settings.iris);
            recipe.lipFullness = EditorGUILayout.Slider("Lip fullness", recipe.lipFullness, 0, 100);
            settings.faceSmoothing = EditorGUILayout.Slider(new GUIContent("Face smoothing", "0 keeps the existing normal seams; 1 smooths facial skin shading. Does not change geometry."), settings.faceSmoothing, 0, 1);
            settings.age = EditorGUILayout.Slider("Age texture strength", settings.age, 0, 1);
            recipe.customAgeTexture = TexturePicker("Custom age texture", recipe.customAgeTexture);
            using (new EditorGUI.DisabledScope(recipe.customAgeTexture)) {
                settings.ageOverlay = EditorGUILayout.Popup("Age texture", settings.ageOverlay == "WeatheredCreases.png" ? 1 : 0, new[] { "Mature soft", "Weathered creases" }) == 0 ? "MatureSoft.png" : "WeatheredCreases.png";
            }

            settings.makeup = EditorGUILayout.Slider("Makeup", settings.makeup, 0, 1);
            recipe.customMakeupTexture = TexturePicker("Custom makeup texture", recipe.customMakeupTexture);
            settings.makeupPreserveSkinTone = EditorGUILayout.Toggle(new GUIContent("Adapt makeup to skin", "Apply blush and eye shadow relative to the overlay's foundation color, preserving the character's complexion."), settings.makeupPreserveSkinTone);
            if (settings.makeupPreserveSkinTone) {
                settings.makeupReferenceSkin = ColorField("Makeup base skin", settings.makeupReferenceSkin);
                EditorGUILayout.HelpBox("Match the plain foundation color in the makeup texture. The default matches Daytime Rose.", MessageType.None);
            }
            settings.scruff = EditorGUILayout.Toggle("Scruff", settings.scruff);
            if (settings.scruff) {
                recipe.scruffOpacity = EditorGUILayout.Slider("Scruff visibility", recipe.scruffOpacity, 0, 1);
                recipe.customScruffTexture = TexturePicker("Custom scruff texture", recipe.customScruffTexture);
            }

            settings.sidekickBeard = EditorGUILayout.TextField(new GUIContent("Sidekick facial hair ID", "The exact filename of an installed Sidekick facial-hair FBX, without its path or .fbx extension."), settings.sidekickBeard);
            EditorGUILayout.HelpBox("Optional beard or moustache mesh. Paste its Sidekick FBX filename without .fbx, for example SK_HUMN_BASE_08_09FCHR_HU01. Find these models by searching for 09FCHR in your Sidekick assets. Leave empty to retain the original facial hair. Uses the Eyebrows / facial hair color; Scruff adds texture separately.", MessageType.Info);
        }

        Texture2D TexturePicker(string label, Texture2D current) {
            return (Texture2D)EditorGUILayout.ObjectField(new GUIContent(label, "Optional project texture. Clear to use the bundled texture. Age and makeup use front-face overlays; scruff uses a repeating hair tile."), current, typeof(Texture2D), false);
        }

        float[] ColorField(string label, float[] rgb) {
            var c = EditorGUILayout.ColorField(label, new Color(rgb[0] / 255, rgb[1] / 255, rgb[2] / 255));
            return new[] { Mathf.Round(c.r * 255), Mathf.Round(c.g * 255), Mathf.Round(c.b * 255) };
        }

        void Fit() {
            var settings = recipe.settings;
            settings.canonicalScale = EditorGUILayout.FloatField("Source-to-fit scale", settings.canonicalScale);
            settings.canonicalOffset = EditorGUILayout.FloatField("Source height offset", settings.canonicalOffset);
            EditorGUILayout.HelpBox("These map the original bind pose into fitting coordinates and back. The original body size, including baked child/teen scale, is retained.", MessageType.None);
            settings.width = EditorGUILayout.Slider("Head width", settings.width, .7f, 1.4f);
            settings.cut = EditorGUILayout.Slider("Neck cut height", settings.cut, 1.30f, 1.56f);
            settings.neckScale[0] = EditorGUILayout.Slider("Neck width", settings.neckScale[0], .65f, 1.15f);
            settings.neckScale[1] = EditorGUILayout.Slider("Neck depth", settings.neckScale[1], .65f, 1.15f);
            settings.collarInset = EditorGUILayout.Toggle("Blend collar skin shading", settings.collarInset);
            settings.throatSoftening = EditorGUILayout.Slider("Soften throat", settings.throatSoftening, 0, 1);
            var shells = new[] { "", "embedded" }.Concat(captured.accessories.Select(meshPart => meshPart.name)).ToArray();
            var shellLabels = new[] { "Off", "Integrated hair / headwear" }.Concat(captured.accessories.Select(meshPart2 => meshPart2.name)).ToArray();
            settings.scalpHairEnvelope = shells[EditorGUILayout.Popup("Fit scalp beneath", Mathf.Max(0, Array.IndexOf(shells, settings.scalpHairEnvelope)), shellLabels)];
            if (!string.IsNullOrEmpty(settings.scalpHairEnvelope)) {
                settings.scalpHairClearance = EditorGUILayout.Slider("Hair clearance", settings.scalpHairClearance, .002f, .02f);
            }

            if (settings.skinSampleUv.Length == 2) {
                var uv = EditorGUILayout.Vector2Field("Skin palette sample", new Vector2(settings.skinSampleUv[0], settings.skinSampleUv[1]));
                settings.skinSampleUv = new[] { uv.x, uv.y };
            }

            EditorGUILayout.HelpBox("Review collar clearance, rear scalp, necklaces and head turns. Numeric checks cannot certify appearance.", MessageType.Info);
        }

        void Regions() {
            EditorGUILayout.HelpBox("Select a region to highlight it on the original. Auto uses the saved fit. Preserve keeps clothing; Accessory separates hair/attachments; Necklace separates jewelry before repairing its skin; Remove discards original facial parts.", MessageType.None);
            regionsScroll = EditorGUILayout.BeginScrollView(regionsScroll, GUILayout.Height(280));
            for (int i = 0; i < recipe.regions.Length; i++) {
                var r = recipe.regions[i];
                if (r.center.y * recipe.settings.canonicalScale + recipe.settings.canonicalOffset < 1.28f && r.role == "Auto") {
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Toggle(selectedRegion == i, r.label, "Button", GUILayout.Width(215)) && selectedRegion != i) {
                        selectedRegion = i;
                        play = false;
                        headTurn = 0;
                        originalPreview.Region(captured, r);
                    }

                    r.role = roles[EditorGUILayout.Popup(Mathf.Max(0, Array.IndexOf(roles, r.role)), roles)];
                }
            }

            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Clear highlight")) {
                selectedRegion = -1;
                originalPreview.Region(null, null);
            }
        }

        void Advanced() {
            EditorGUILayout.HelpBox("Existing recipe fields are preserved here. The Appearance, Fit and Regions controls override their corresponding fields when building.", MessageType.None);
            recipe.baseConfig = EditorGUILayout.TextArea(recipe.baseConfig, GUILayout.MinHeight(280));
        }

        void Controls() {
            using (new EditorGUILayout.HorizontalScope()) {
                foreach (var view in PreviewViews) {
                    if (GUILayout.Button(view.Item1)) {
                        originalPreview.yaw = view.Item2;
                        originalPreview.pitch = view.Item3;
                        SyncCamera();
                    }
                }

                wire = GUILayout.Toggle(wire, "Wireframe", "Button");
                fullBody = GUILayout.Toggle(fullBody, "Full body", "Button");
            }

            using (new EditorGUILayout.HorizontalScope()) {
                bool next = GUILayout.Toggle(play, "Animate", "Button", GUILayout.Width(80));
                if (next != play) {
                    play = next;
                    started = EditorApplication.timeSinceStartup;
                }

                headTurn = EditorGUILayout.Slider("Head turn", headTurn, -45, 45);
            }

            using (new EditorGUILayout.HorizontalScope()) {
                blink = EditorGUILayout.Slider("Blink", blink, 0, 100);
                jaw = EditorGUILayout.Slider("Mouth", jaw, 0, 60);
                smile = EditorGUILayout.Slider("Smile", smile, 0, 60);
            }
        }

        void SyncCamera() {
            resultPreview.yaw = originalPreview.yaw;
            resultPreview.pitch = originalPreview.pitch;
            resultPreview.zoom = originalPreview.zoom;
        }

        void SaveRecipe() {
            string path = EditorUtility.SaveFilePanelInProject("Save head recipe", recipe.characterId + "Conversion", "asset", "Save the fitting recipe.");
            if (path.Length == 0) {
                return;
            }

            var copy = recipe.CopyForSaving();
            var old = AssetDatabase.LoadAssetAtPath<HeadConversionRecipe>(path);
            if (old) {
                EditorUtility.CopySerialized(copy, old);
                EditorUtility.SetDirty(old);
                DestroyImmediate(copy);
            } else {
                AssetDatabase.CreateAsset(copy, path);
            }

            AssetDatabase.SaveAssets();
            recipeAsset = AssetDatabase.LoadAssetAtPath<HeadConversionRecipe>(path);
        }
    }
}
