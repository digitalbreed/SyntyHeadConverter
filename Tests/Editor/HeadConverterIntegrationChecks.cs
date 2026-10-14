// Runs opt-in conversion, animation, persistence and custom-texture checks in a disposable Unity fixture project.
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace digitalbreed.SyntyHeadConverter.Tests {
    /// <summary>Opt-in integration fixture. Requires separately installed licensed assets.</summary>
    public static class HeadConverterIntegrationChecks {
        private static readonly bool[] ComparisonSourceModes = { true, false };
        private static readonly (string, float, float, float)[] EvidenceViews = {
            ("front", 0f, 6f, 0f),
            ("side", 90f, 6f, 0f),
            ("back", 180f, 6f, 0f),
            ("crown-back", 180f, -35f, 0f),
            ("back-turn", 180f, -20f, 35f),
            ("under-chin", 20f, 30f, 0f),
            ("neck-left", 45f, 12f, 0f),
            ("neck-right", -45f, 12f, 0f),
            ("turn-left", 45f, 12f, -35f),
            ("turn-right", -45f, 12f, 35f),
            ("turn", 0f, 6f, 35f)
        };
        private static readonly (string, float, float)[] NeckEvidenceViews = {
            ("neck-close-above", -25f, 0f),
            ("neck-close-below", 25f, 0f),
            ("neck-close-turn", -25f, 35f)
        };
        [Serializable]
        public sealed class Entry {
            public string id;
            public string prefab;
            public string fittingJson;
            public bool scruff;
            public bool customTextures;
            public bool verifyRaisedCollar;
            public bool verifyPatternedCollar;
            public string expectedScalpShell;
            public float[] expectedSkinRgb;
            public int expectedNecklaceRegions = -1;
            public int expectedFemale = -1;
            public bool verifyProportionFit;
        }

        [Serializable]
        public sealed class Fixture {
            public string sidekickRoot;
            public Entry[] entries;
        }

        private static Fixture fixture;
        private static int index;
        private static double deadline;
        private static GameObject sentinel;
        private static HeadConversionRecipe recipe;
        private static int sceneCount;
        private static System.Collections.Generic.Dictionary<string, string> donorSourceHashes;
        public static void Run() {
            if (!Application.isBatchMode || !Environment.GetCommandLineArgs().Contains("-syntHeadChecks") || !File.Exists("Library/SyntyHeadConverterChecks.fixture.json")) {
                throw new Exception("Run only in an isolated batch project with -syntHeadChecks and a licensed-asset fixture in Library.");
            }

            CheckRecipeOptions();
            CheckJsonSerialization();
            SidekickImportChecks.Run("Library/SyntyHeadConverterMetadataChecks/" + Guid.NewGuid().ToString("N"));
            Debug.Log("SYNTY_DONOR_PROFILE_CHECK_PASS");
            CheckInsetOrdering();
            CheckFaceAppearance();
            CheckProportionScaling();
            fixture = HeadConverterJson.Deserialize<Fixture>(File.ReadAllText("Library/SyntyHeadConverterChecks.fixture.json"));
            HeadConversionPipeline.Sidekick = fixture.sidekickRoot;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            sentinel = new GameObject("Unsaved scene sentinel");
            sentinel.transform.position = new Vector3(7, 8, 9);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            sceneCount = SceneManager.sceneCount;
            index = 0;
            HeadConversionPipeline.Changed += Check;
            EditorApplication.update += Timeout;
            Start();
        }

        private static void CheckProportionScaling() {
            var source = new HeadSource.Source {
                bones = new[] { new HeadSource.Bone { name = "Head", position = new Vector3(0, 1, 0) } },
                skins = new[] { new HeadSource.Part {
                    vertices = new[] {
                        new Vector3(-.12f, 1.02f, -.08f), new Vector3(.12f, 1.02f, -.08f),
                        new Vector3(-.10f, 1.16f, -.03f), new Vector3(.10f, 1.16f, -.03f),
                        new Vector3(-.10f, 1.10f, .10f), new Vector3(.10f, 1.10f, .10f)
                    },
                    uv = Enumerable.Repeat(new Vector2(.02f, .18f), 6).ToArray()
                } }
            };
            var fit = new HeadFitSettings();
            HeadSource.SuggestProportions(source, fit);
            if (fit.canonicalScale < 1.1f || fit.canonicalScale > 1.3f || fit.canonicalOffset < .25f) {
                throw new Exception("Disproportionate head fitting still follows body height instead of skull dimensions.");
            }

            var originalVertices = source.skins[0].vertices.ToArray();
            const float bakedScale = .9f;
            source.bones[0].position *= bakedScale;
            source.skins[0].vertices = originalVertices.Select(position => position * bakedScale).ToArray();
            var scaledFit = new HeadFitSettings();
            HeadSource.SuggestProportions(source, scaledFit);
            for (int vertex = 0; vertex < originalVertices.Length; vertex++) {
                Vector3 expected = originalVertices[vertex] * fit.canonicalScale + Vector3.up * fit.canonicalOffset;
                Vector3 actual = source.skins[0].vertices[vertex] * scaledFit.canonicalScale + Vector3.up * scaledFit.canonicalOffset;
                if (Vector3.Distance(expected, actual) > .00001f) {
                    throw new Exception("Baked source scale changed the canonical head fit.");
                }
            }

            source.bones[0].position = new Vector3(0, 1.569112f, 0);
            source.skins[0].vertices = originalVertices.Select(position => new Vector3(position.x * 1.1f, position.y + .569112f, position.z)).ToArray();
            var adultFit = new HeadFitSettings();
            HeadSource.SuggestProportions(source, adultFit);
            if (Mathf.Abs(adultFit.canonicalScale - 1) > .00001f || adultFit.canonicalOffset != 0) {
                throw new Exception("Ordinary adult proportions changed fitting defaults.");
            }

            Debug.Log("SYNTY_PROPORTION_SCALING_CHECK_PASS");
        }

        private static void CheckFaceAppearance() {
            var reference = new Color(244f / 255, 197f / 255, 169f / 255, .6f);
            var complexions = new[] { new Color(.15f, .09f, .06f), new Color(.57f, .41f, .31f), new Color(1, .8f, .68f) };
            foreach (var complexion in complexions) {
                Color neutral = FaceTextures.AdaptMakeup(complexion, reference, reference);
                if (Mathf.Max(Mathf.Abs(neutral.r - complexion.r), Mathf.Abs(neutral.g - complexion.g),
                    Mathf.Abs(neutral.b - complexion.b)) > .0001f || neutral.a != reference.a) {
                    throw new Exception("Neutral makeup foundation changed the complexion or overlay alpha.");
                }

                Color pigment = FaceTextures.AdaptMakeup(complexion, new Color(reference.r, reference.g * .7f, reference.b * .7f, .4f), reference);
                if (pigment.g >= complexion.g || pigment.b >= complexion.b || pigment.a != .4f) {
                    throw new Exception("Skin-relative makeup lost its pigment or alpha.");
                }
            }

            var surface = new NativeSurface();
            NativeVertex Vertex(Vector3 position, Vector3 normal) {
                var vertex = new NativeVertex { position = position, normal = normal, uv = new Vector2(.2f, .3f) };
                vertex.weights.Add("Head", 1);
                vertex.shapes.Add("smile", Vector3.up * .01f);
                return vertex;
            }

            surface.faces.Add(new NativeTriangle(Vertex(Vector3.zero, Vector3.forward), Vertex(Vector3.right, Vector3.forward), Vertex(Vector3.up, Vector3.forward), 0, true));
            surface.faces.Add(new NativeTriangle(Vertex(Vector3.right, Vector3.up), Vertex(Vector3.zero, Vector3.up), Vertex(Vector3.forward, Vector3.up), 0, true));
            var positions = surface.faces.SelectMany(face => face.vertices.Select(vertex => vertex.position)).ToArray();
            surface.SmoothSkin();
            var vertices = surface.faces.SelectMany(face => face.vertices).ToArray();
            if (surface.faces.Count != 2 || vertices.Where((vertex, index) => vertex.position != positions[index] ||
                vertex.weights["Head"] != 1 || vertex.shapes["smile"] != Vector3.up * .01f || vertex.uv != new Vector2(.2f, .3f)).Any() ||
                Vector3.Angle(vertices[0].normal, vertices[4].normal) > .001f || Vector3.Angle(vertices[1].normal, vertices[3].normal) > .001f) {
                throw new Exception("Facial smoothing changed geometry, skinning, UVs or expressions, or retained a shared-position normal seam.");
            }

            Debug.Log("SYNTY_FACE_APPEARANCE_CHECK_PASS");
        }

        private static void CheckInsetOrdering() {
            var angles = new[] { -150f, -90f, -30f, 6.8f, 5.9f, 6.0f, 60f, 90f, 150f, 179f };
            var original = angles.Select(angle => new Vector3(.06f * Mathf.Cos(angle * Mathf.Deg2Rad),
                1.445f, -.012f + .06f * Mathf.Sin(angle * Mathf.Deg2Rad))).ToArray();
            var ring = original.Select(position => new NativeVertex { position = position }).ToList();
            NativeHeadBuilder.OrderInsetRing(ring);
            float turn = 0;
            for (int index = 0; index < ring.Count; index++) {
                Vector3 current = ring[index].position;
                Vector3 next = ring[(index + 1) % ring.Count].position;
                float delta = Mathf.DeltaAngle(Mathf.Atan2(current.z + .012f, current.x) * Mathf.Rad2Deg,
                    Mathf.Atan2(next.z + .012f, next.x) * Mathf.Rad2Deg);
                if (delta <= 0 || current.y != original[index].y ||
                    Mathf.Abs(new Vector2(current.x, current.z + .012f).magnitude - .06f) > .000001f ||
                    Vector3.Distance(current, original[index]) > .001f) {
                    throw new Exception("Notched inset ordering changed its height/radius, overcorrected, or retained an angular reversal.");
                }

                turn += delta;
            }

            if (Mathf.Abs(turn - 360) > .001f) {
                throw new Exception("The generated inset no longer makes one complete turn.");
            }

            var ordered = ring.Select(vertex => vertex.position).ToArray();
            NativeHeadBuilder.OrderInsetRing(ring);
            if (ring.Where((vertex, index) => vertex.position != ordered[index]).Any()) {
                throw new Exception("An already ordered inset was changed.");
            }

            Debug.Log("SYNTY_INSET_ORDERING_CHECK_PASS");
        }

        private static void CheckRecipeOptions() {
            var options = new RecipeOptions("{\"scale\":1.25e0,\"enabled\":true,\"label\":\"line\\nnext\",\"ranges\":[[0,0.04],[1,2]],\"parts\":[{\"name\":\"neck\"}],\"names\":[\"left\",\"right\"],\"unknown\":{\"nested\":[3]}}");
            if (options.Number("scale") != 1.25f || !options.Flag("enabled") || options.Text("label") != "line\nnext" || options.Rows("ranges")[1][1] != 2 || options.Objects("parts")[0].Text("name") != "neck" || options.Strings("names")[1] != "right" || !options.Has("unknown") || options.Number("missing", 7) != 7 || options.Numbers("missing").Length != 0) {
                throw new Exception("Advanced recipe JSON values or missing-key defaults changed.");
            }

            var invalidRecipes = new[] { "[]", "{\"value\":1,\"value\":2}", "{} trailing" };
            foreach (string invalidJson in invalidRecipes) {
                bool rejected = false;
                try {
                    new RecipeOptions(invalidJson);
                } catch (FormatException) {
                    rejected = true;
                }

                if (!rejected) {
                    throw new Exception("Invalid advanced recipe JSON was accepted: " + invalidJson);
                }
            }

            Debug.Log("SYNTY_RECIPE_JSON_CHECK_PASS");
        }

        // Raised-collar regressions: source UVs identify Cowgirl's red jacket or the 70s male's patterned shirt.
        // Its raised collar tips are above the old head-removal threshold and must remain intact.
        private static void CheckRaisedCollar(bool patterned) {
            var original = HeadSource.Capture(recipe.sourcePrefab);
            var converted = HeadConversionPipeline.Result;
            var actual = converted.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(renderer =>
                HeadSource.Read(renderer, renderer.sharedMesh, converted.transform, renderer.bones).vertices).ToArray();
            var expected = original.skins.SelectMany(part => part.vertices.Where((position, vertexIndex) =>
                position.y > 1.53f && position.y < 1.58f &&
                (patterned ? part.uv[vertexIndex].x > .5f :
                    Vector2.Distance(part.uv[vertexIndex], new Vector2(.284f, .377f)) < .01f))).ToArray();
            if (expected.Length < 4) {
                throw new Exception("Raised-collar fixture no longer contains the expected jacket tips.");
            }

            if (expected.Any(position => !actual.Any(candidate => Vector3.Distance(candidate, position) < .0001f))) {
                throw new Exception("Head conversion removed or moved a raised jacket collar vertex.");
            }

            Debug.Log("SYNTY_RAISED_COLLAR_CHECK_PASS");
        }

        private static void CheckNecklacePreservation() {
            var source = HeadSource.Capture(recipe.sourcePrefab).skins[0];
            var triangles = source.slots.SelectMany(slot => slot.triangles).ToArray();
            var expected = recipe.regions.Where(region => region.role == "Necklace").SelectMany(region => region.faces)
                .SelectMany(face => triangles.Skip(face * 3).Take(3)).Distinct().Select(index => source.vertices[index]);
            var converted = HeadConversionPipeline.Result;
            var actual = converted.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(renderer =>
                HeadSource.Read(renderer, renderer.sharedMesh, converted.transform, renderer.bones).vertices).ToArray();
            if (expected.Any(position => !actual.Any(candidate => Vector3.Distance(candidate, position) < .0001f))) {
                throw new Exception("Conversion removed or moved original necklace geometry.");
            }

            Debug.Log("SYNTY_NECKLACE_PRESERVATION_CHECK_PASS");
        }

        private static void CheckHairPreservation() {
            string name = fixture.entries[index].expectedScalpShell;
            var expected = HeadSource.Capture(recipe.sourcePrefab).accessories.First(part => part.name == name);
            var converted = HeadConversionPipeline.Result;
            var filter = converted.GetComponentsInChildren<MeshFilter>().FirstOrDefault(candidate => candidate.name == name);
            if (!filter) {
                throw new Exception("The original hair object was lost during scalp fitting.");
            }

            // Compare mesh-local geometry: the source capture normalizes the skeleton to bind
            // pose, while the saved humanoid prefab may retain a different bone pose.
            var actual = filter.sharedMesh.vertices;
            if (actual.Length != expected.localVertices.Length || expected.localVertices.Where((position, index) =>
                Vector3.Distance(position, actual[index]) > .0001f).Any()) {
                throw new Exception("Scalp fitting altered the original hair geometry.");
            }

            Debug.Log("SYNTY_HAIR_PRESERVATION_CHECK_PASS");
        }

        public static void CheckJsonSerialization() {
            var fit = new HeadFitSettings();
            HeadConverterJson.Populate("{\"width\":1.2,\"skin\":[12,34,56],\"accessories\":[7,9],\"unknownOption\":true}", fit);
            HeadConverterJson.Populate("{\"accessories\":[11]}", fit);
            if (fit.width != 1.2f || fit.cut != 1.47f || !fit.skin.SequenceEqual(new[] { 12f, 34f, 56f }) ||
                !fit.accessories.SequenceEqual(new[] { 11 })) {
                throw new Exception("JSON population changed defaults, ignored fields or array replacement.");
            }

            string fitJson = HeadConverterJson.Serialize(fit);
            if (HeadConverterJson.Serialize(HeadConverterJson.Deserialize<HeadFitSettings>(fitJson)) != fitJson) {
                throw new Exception("Fitting JSON did not round-trip.");
            }

            const string legacyRegion = "{\"label\":\"legacy\",\"role\":\"Preserve\",\"faces\":[2,5],\"center\":{\"x\":1,\"y\":2,\"z\":3},\"size\":{\"x\":4,\"y\":5,\"z\":6},\"color\":{\"r\":0.25,\"g\":0.5,\"b\":0.75,\"a\":1}}";
            var region = HeadConverterJson.Deserialize<HeadRegion>(legacyRegion);
            string regionJson = HeadConverterJson.Serialize(region);
            var fields = JObject.Parse(regionJson);
            if (region.center != new Vector3(1, 2, 3) || region.size != new Vector3(4, 5, 6) ||
                region.color != new Color(.25f, .5f, .75f, 1) ||
                ((JObject)fields["center"]).Count != 3 || ((JObject)fields["color"]).Count != 4 ||
                HeadConverterJson.Serialize(HeadConverterJson.Deserialize<HeadRegion>(regionJson)) != regionJson) {
                throw new Exception("Legacy vector/color fields changed or computed Unity properties were serialized.");
            }

            var loadedFixture = HeadConverterJson.Deserialize<Fixture>("{\"sidekickRoot\":\"Sidekicks\",\"entries\":[{\"id\":\"Example\",\"prefab\":\"Assets/Example.prefab\"}]}");
            if (loadedFixture.entries.Single().expectedFemale != -1 || loadedFixture.entries[0].id != "Example") {
                throw new Exception("Fixture JSON changed field defaults or array contents.");
            }

            Debug.Log("SYNTY_JSON_SERIALIZATION_CHECK_PASS");
        }

        private static void CheckRecipeReferences(HeadConversionRecipe sourceRecipe) {
            var copy = Object.Instantiate(sourceRecipe);
            var restored = ScriptableObject.CreateInstance<HeadConversionRecipe>();
            try {
                copy.customAgeTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Packages/digitalbreed.syntyheadconverter/Editor/Textures/MatureSoft.png");
                if (!copy.customAgeTexture) {
                    throw new Exception("The bundled texture is missing from the installed package.");
                }

                copy.customMakeupTexture = copy.customAgeTexture;
                copy.customScruffTexture = null;
                string json = HeadConverterJson.Serialize(copy);
                HeadConverterJson.Populate(json, restored);
                if (restored.sourcePrefab != copy.sourcePrefab || restored.customAgeTexture != copy.customAgeTexture ||
                    restored.customMakeupTexture != copy.customMakeupTexture || restored.customScruffTexture ||
                    HeadConverterJson.Serialize(restored) != json) {
                    throw new Exception("Recipe JSON did not preserve fields and Unity asset references.");
                }

                restored.settings.width += .01f;
                if (HeadConverterJson.Serialize(restored) == json) {
                    throw new Exception("Recipe JSON did not detect changed preview settings.");
                }

                var legacy = JObject.Parse(json);
                legacy["sourcePrefab"] = new JObject { ["instanceID"] = copy.sourcePrefab.GetInstanceID() };
                legacy["customAgeTexture"] = new JObject { ["instanceID"] = copy.customAgeTexture.GetInstanceID() };
                HeadConverterJson.Populate(legacy.ToString(), restored);
                if (restored.sourcePrefab != copy.sourcePrefab || restored.customAgeTexture != copy.customAgeTexture) {
                    throw new Exception("Legacy editor-session asset references were not restored.");
                }

                Debug.Log("SYNTY_RECIPE_REFERENCES_CHECK_PASS");
            } finally {
                Object.DestroyImmediate(copy);
                Object.DestroyImmediate(restored);
            }
        }

        private static void Start() {
            try {
                var entry = fixture.entries[index];
                var original = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefab);
                recipe = HeadSource.Suggest(original, false);
                CheckRecipeReferences(recipe);
                if (!string.IsNullOrEmpty(entry.expectedScalpShell) && recipe.settings.scalpHairEnvelope != entry.expectedScalpShell) {
                    throw new Exception("Automatic scalp fitting selected the wrong hair shell.");
                }

                if (entry.expectedSkinRgb != null && entry.expectedSkinRgb.Length == 3 &&
                    Enumerable.Range(0, 3).Any(channel => Mathf.Abs(recipe.settings.skin[channel] - entry.expectedSkinRgb[channel]) > 1)) {
                    throw new Exception("Automatic skin sampling selected the wrong source palette color.");
                }

                if (entry.expectedNecklaceRegions >= 0 && recipe.regions.Count(region => region.role == "Necklace") != entry.expectedNecklaceRegions) {
                    throw new Exception("Automatic neck-band classification changed.");
                }
                if (entry.expectedFemale >= 0 && recipe.settings.female != entry.expectedFemale) {
                    throw new Exception("Automatic face selection did not match the source name.");
                }

                if (entry.verifyProportionFit && (recipe.settings.canonicalScale < 1.1f ||
                    recipe.settings.canonicalScale > 1.3f || recipe.settings.canonicalOffset < .25f)) {
                    throw new Exception("Automatic child head fitting reverted to body-height scaling.");
                }

                if (!string.IsNullOrEmpty(entry.fittingJson)) {
                    recipe.baseConfig = entry.fittingJson;
                    recipe.settings.throatSoftening = 0;
                    recipe.settings.collarInset = false;
                    HeadConverterJson.Populate(entry.fittingJson, recipe.settings);
                }

                recipe.settings.scruff = entry.scruff;
                donorSourceHashes = SidekickAssets.Resolve(recipe).Values.SelectMany(part =>
                    new[] { part.fullPath, part.fullPath + ".meta" }).Where(File.Exists).Distinct()
                    .ToDictionary(path => path, SidekickImportProfile.FileHash);
                if (entry.customTextures) {
                    recipe.customAgeTexture = CustomTexture("Age", new Color(.8f, .2f, .1f, .5f));
                    recipe.customMakeupTexture = CustomTexture("Makeup", new Color(.1f, .8f, .3f, .5f));
                    recipe.customScruffTexture = CustomTexture("Scruff", new Color(.1f, .1f, .9f, .8f));
                    recipe.settings.age = .7f;
                    recipe.settings.makeup = .3f;
                    recipe.settings.scruff = true;
                }

                recipe.characterId = entry.id;
                recipe.outputFolder = "Assets/ConverterCheckResults/" + entry.id + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                if (index == 0) {
                    HeadConversionPipeline.Changed -= Check;
                    HeadConversionPipeline.Begin(recipe);
                    HeadConversionPipeline.Cancel();
                    if (HeadConversionPipeline.Busy || HeadConversionPipeline.Result) {
                        throw new Exception("Cancellation retained a running conversion or stale result.");
                    }

                    HeadConversionPipeline.Changed += Check;
                }

                deadline = EditorApplication.timeSinceStartup + 300;
                HeadConversionPipeline.Begin(recipe);
            } catch (Exception error) {
                Finish(error);
            }
        }

        private static void Check() {
            if (HeadConversionPipeline.Busy) {
                return;
            }

            try {
                if (!string.IsNullOrEmpty(HeadConversionPipeline.Error)) {
                    throw new Exception(HeadConversionPipeline.Error);
                }

                if (!HeadConversionPipeline.Result || !HeadConversionPipeline.Matches(recipe)) {
                    throw new Exception("No current Unity preview.");
                }

                CheckDonorReuse();

                if (!sentinel || sentinel.transform.position != new Vector3(7, 8, 9) || SceneManager.sceneCount != sceneCount || !SceneManager.GetActiveScene().isDirty) {
                    throw new Exception("Conversion changed the user's scene.");
                }

                if (File.Exists(recipe.outputFolder + "/" + recipe.characterId + ".prefab")) {
                    throw new Exception("A preview wrote production assets.");
                }

                if (fixture.entries[index].verifyRaisedCollar || fixture.entries[index].verifyPatternedCollar) {
                    CheckRaisedCollar(fixture.entries[index].verifyPatternedCollar);
                }

                if (fixture.entries[index].expectedNecklaceRegions > 0) {
                    CheckNecklacePreservation();
                }

                if (!string.IsNullOrEmpty(fixture.entries[index].expectedScalpShell)) {
                    CheckHairPreservation();
                }

                float width = recipe.settings.width;
                recipe.settings.width += .01f;
                if (HeadConversionPipeline.Matches(recipe)) {
                    throw new Exception("An outdated preview was accepted.");
                }

                recipe.settings.width = width;
                string directory = "Library/SyntyHeadConverterEvidence/" + recipe.characterId;
                Directory.CreateDirectory(directory);
                File.WriteAllText(directory + "/validation.txt", HeadConversionPipeline.Report);
                foreach (bool original in ComparisonSourceModes) {
                    using (var preview = new HeadPreview()) {
                        preview.Set(original ? recipe.sourcePrefab : HeadConversionPipeline.Result);
                        foreach (var view in EvidenceViews) {
                            preview.yaw = view.Item2;
                            preview.pitch = view.Item3;
                            var image = (Texture2D)preview.Draw(new Rect(0, 0, 640, 800), view.Item4, 0, view.Item1 == "turn" ? 35 : 0, 0, recipe.lipFullness, false, false);
                            File.WriteAllBytes(directory + "/" + (original ? "original-" : "converted-") + view.Item1 + ".png", image.EncodeToPNG());
                            Object.DestroyImmediate(image);
                        }

                        preview.yaw = 0;
                        preview.zoom = .26f;
                        preview.targetHeightOffset = -.105f;
                        foreach (var view in NeckEvidenceViews) {
                            preview.pitch = view.Item2;
                            var image = (Texture2D)preview.Draw(new Rect(0, 0, 800, 600), view.Item3, 0, 0, 0, recipe.lipFullness, false, false);
                            File.WriteAllBytes(directory + "/" + (original ? "original-" : "converted-") + view.Item1 + ".png", image.EncodeToPNG());
                            Object.DestroyImmediate(image);
                        }
                    }
                }

                HeadConversionPipeline.Changed -= Check;
                var result = HeadConversionPipeline.Apply(recipe);
                string assetPath = AssetDatabase.GetAssetPath(result);
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                string texturePath = recipe.outputFolder + "/Models/FaceDetail.asset";
                string textureGuid = AssetDatabase.AssetPathToGUID(texturePath);
                HeadConversionPipeline.Apply(recipe);
                if (AssetDatabase.AssetPathToGUID(assetPath) != guid ||
                    string.IsNullOrEmpty(textureGuid) || AssetDatabase.AssetPathToGUID(texturePath) != textureGuid ||
                    !sentinel || SceneManager.sceneCount != sceneCount) {
                    throw new Exception("Repeated apply changed a GUID or scene.");
                }

                CheckNativeTextures();
                CheckApplyRollback();

                var saved = AssetDatabase.LoadAssetAtPath<HeadConversionRecipe>(recipe.outputFolder + "/" + recipe.characterId + "Conversion.asset");
                if (!saved || saved.regions.Any(r => r.role == "Auto") ||
                    saved.settings.faceSmoothing != recipe.settings.faceSmoothing ||
                    saved.settings.makeupPreserveSkinTone != recipe.settings.makeupPreserveSkinTone ||
                    !saved.settings.makeupReferenceSkin.SequenceEqual(recipe.settings.makeupReferenceSkin)) {
                    throw new Exception("Saved recipe is incomplete or contains redundant region snapshots.");
                }

                if (fixture.entries[index].customTextures) {
                    CheckCustomTextures(saved);
                }

                Debug.Log("SYNTY_NATIVE_CHECK_PASS " + recipe.characterId);
                HeadConversionPipeline.Changed += Check;
                Object.DestroyImmediate(recipe);
                recipe = null;
                index++;
                if (index < fixture.entries.Length) {
                    Start();
                } else {
                    Finish(null);
                }
            } catch (Exception error) {
                Finish(error);
            }
        }

        private static Texture2D CustomTexture(string name, Color color) {
            string directory = "Assets/ConverterCheckInputs";
            HeadAssetFolders.Ensure(directory);
            string path = directory + "/" + name + ".asset";
            var image = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            image.SetPixels(Enumerable.Repeat(color, 64).ToArray());
            image.Apply();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture) {
                EditorUtility.CopySerialized(image, texture);
                Object.DestroyImmediate(image);
                EditorUtility.SetDirty(texture);
            } else {
                texture = image;
                AssetDatabase.CreateAsset(texture, path);
            }

            AssetDatabase.SaveAssetIfDirty(texture);
            // Discard this session's CPU pixels after saving, to exercise non-readable GPU inputs.
            texture.Apply(false, true);
            return texture;
        }

        private static void CheckDonorReuse() {
            foreach (var pair in donorSourceHashes) {
                if (SidekickImportProfile.FileHash(pair.Key) != pair.Value) {
                    throw new Exception("Conversion changed a source FBX or its metadata: " + pair.Key);
                }
            }

            string directory = HeadConversionPipeline.CacheRoot + "/Inputs";
            string[] Files() => Directory.Exists(directory) ? Directory.GetFiles(directory, "*", SearchOption.AllDirectories).OrderBy(path => path).ToArray() : Array.Empty<string>();
            var before = Files().ToDictionary(path => path, SidekickImportProfile.FileHash);
            SidekickAssets.Read(recipe);
            if (!Files().SequenceEqual(before.Keys.OrderBy(path => path)) || before.Any(pair => SidekickImportProfile.FileHash(pair.Key) != pair.Value)) {
                throw new Exception("Reading unchanged donors rewrote or added cache files.");
            }

            Debug.Log("SYNTY_DONOR_REUSE_CHECK_PASS");
        }

        private static void CheckNativeTextures() {
            foreach (string name in recipe.settings.scruff ? new[] { "FaceDetail", "JawWrap" } : new[] { "FaceDetail" }) {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(recipe.outputFolder + "/Models/" + name + ".asset");
                var expected = new Texture2D(2, 2, TextureFormat.RGB24, false);
                try {
                    expected.LoadImage(File.ReadAllBytes(HeadConversionPipeline.JobDirectory + "/authoring/unity/" + name + ".png"));
                    if (!texture || texture.width != expected.width || texture.height != expected.height ||
                        texture.mipmapCount <= 1 || !texture.isDataSRGB ||
                        !texture.GetPixels32().SequenceEqual(expected.GetPixels32())) {
                        throw new Exception("Native texture dimensions, pixels, mipmaps or color space changed: " + name);
                    }
                } finally {
                    Object.DestroyImmediate(expected);
                }
            }

            if (Directory.GetFiles(recipe.outputFolder + "/Models", "*.png").Length != 0) {
                throw new Exception("Applying a new character created imported PNG outputs.");
            }

            Debug.Log("SYNTY_NATIVE_TEXTURE_CHECK_PASS");
        }

        private static void CheckApplyRollback() {
            string path = recipe.outputFolder + "/Models/FaceDetail.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Color original = texture.GetPixel(0, 0);
            string guid = AssetDatabase.AssetPathToGUID(path);
            string added = recipe.outputFolder + "/RollbackOnly.mat";
            var failure = new InvalidOperationException("Intentional rollback check");
            Action<HeadConversionRecipe, GameObject> fail = (current, result) => {
                texture.SetPixel(0, 0, Color.magenta);
                texture.Apply();
                EditorUtility.SetDirty(texture);
                AssetDatabase.SaveAssetIfDirty(texture);
                AssetDatabase.CreateAsset(new Material(HeadShaders.Lit()), added);
                throw failure;
            };
            HeadConversionExtensions.AfterApply += fail;
            try {
                try {
                    HeadConversionPipeline.Apply(recipe);
                    throw new Exception("The failing apply hook was not invoked.");
                } catch (InvalidOperationException error) when (ReferenceEquals(error, failure)) {
                    if (AssetDatabase.AssetPathToGUID(path) != guid || texture.GetPixel(0, 0) != original ||
                        AssetDatabase.LoadMainAssetAtPath(added) || File.Exists(added) ||
                        !sentinel || SceneManager.sceneCount != sceneCount || !SceneManager.GetActiveScene().isDirty ||
                        !HeadConversionPipeline.Matches(recipe)) {
                        throw new Exception("Rollback did not restore native texture contents, GUIDs or newly created assets.");
                    }
                }
            } finally {
                HeadConversionExtensions.AfterApply -= fail;
            }

            Debug.Log("SYNTY_APPLY_ROLLBACK_CHECK_PASS");
        }

        private static void CheckCustomTextures(HeadConversionRecipe saved) {
            if (saved.customAgeTexture != recipe.customAgeTexture || saved.customMakeupTexture != recipe.customMakeupTexture || saved.customScruffTexture != recipe.customScruffTexture) {
                throw new Exception("Saved recipe lost custom texture references.");
            }

            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try {
                image.LoadImage(File.ReadAllBytes(HeadConversionPipeline.JobDirectory + "/authoring/unity/FaceDetail.png"));
                var skin = recipe.settings.skin;
                var expected = Color.Lerp(new Color(skin[0] / 255, skin[1] / 255, skin[2] / 255), new Color(.8f, .2f, .1f), .5f * .7f);
                expected = Color.Lerp(expected, new Color(.1f, .8f, .3f), .5f * .3f);
                Color actual = image.GetPixel(image.width / 2, (int)(image.height * .8f));
                if (Mathf.Max(Mathf.Abs(expected.r - actual.r), Mathf.Abs(expected.g - actual.g), Mathf.Abs(expected.b - actual.b)) > .015f) {
                    throw new Exception("Custom overlay color/alpha composition differs: " + actual + " expected " + expected);
                }

                image.LoadImage(File.ReadAllBytes(HeadConversionPipeline.JobDirectory + "/authoring/unity/JawWrap.png"));
                if (!image.GetPixels().Any(c => c.b > c.r + .2f && c.b > c.g + .2f)) {
                    throw new Exception("Custom stubble tile did not reach the jaw texture.");
                }
            } finally {
                Object.DestroyImmediate(image);
            }

            var customTextures = new[] { recipe.customAgeTexture, recipe.customMakeupTexture, recipe.customScruffTexture };
            foreach (var texture in customTextures) {
                if (texture.isReadable) {
                    throw new Exception("Conversion changed the custom texture's Read/Write setting.");
                }
            }

            CustomTexture("Age", new Color(.2f, .3f, .7f, .5f));
            if (HeadConversionPipeline.Matches(recipe)) {
                throw new Exception("Editing a custom overlay did not invalidate the preview.");
            }

            Debug.Log("SYNTY_CUSTOM_TEXTURE_CHECK_PASS");
        }

        private static void Timeout() {
            if (EditorApplication.timeSinceStartup > deadline) {
                Finish(new Exception("The native conversion timed out."));
            }
        }

        private static void Finish(Exception error) {
            HeadConversionPipeline.Changed -= Check;
            EditorApplication.update -= Timeout;
            HeadConversionPipeline.Cancel();
            if (error == null) {
                Debug.Log("SYNTY_NATIVE_ALL_CHECKS_PASS");
            } else {
                Debug.LogException(error);
            }

            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
