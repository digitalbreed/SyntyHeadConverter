// Composes skin colors, age/makeup overlays and cylindrical jaw stubble into generated character textures.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;
using Object = UnityEngine.Object;

namespace digitalbreed.SyntyHeadConverter {
    internal sealed class FaceTextures : IDisposable {
        private sealed class Image {
            public int width;
            public int height;
            public Color[] pixels;
            public Color Sample(float u, float v, bool repeat = false) {
                float x = (repeat ? Mathf.Repeat(u, 1) : Mathf.Clamp01(u)) * (width - 1);
                float y = (repeat ? Mathf.Repeat(v, 1) : Mathf.Clamp01(v)) * (height - 1);
                int ix = (int)x;
                int iy = (int)y;
                int jx = Mathf.Min(ix + 1, width - 1);
                int jy = Mathf.Min(iy + 1, height - 1);
                return Color.Lerp(Color.Lerp(pixels[iy * width + ix], pixels[iy * width + jx], x - ix), Color.Lerp(pixels[jy * width + ix], pixels[jy * width + jx], x - ix), y - iy);
            }
        }

        private readonly HeadConversionRecipe recipe;
        private readonly Color skin;
        private readonly Color lips;
        private readonly Color makeupReference;
        private readonly Image age;
        private readonly Image makeup;
        private readonly Image stubble;
        // Two fixed tile phase offsets decorrelate the repeated hair pattern without randomizing rebuilds.
        // Values are fractions of one tile, not distances on the face.
        private static readonly Vector2[] StubbleOffsets = { Vector2.zero, new Vector2(.371f, .619f) };
        // Lip-color contour sampled on the neutral, positioned Sidekick head in canonical fitting meters.
        // X is absolute distance from the midline (mirrored for both sides); Y is vertical head height.
        // The three arrays share indices. Linear interpolation forms the upper/lower tint boundaries;
        // they control color coverage, not the separate lip-fullness geometry blendshapes.
        private static readonly float[] LipLateralSamples = { 0, .0105f, .020f, .0285f, .0385f };
        private static readonly float[] UpperLipHeightSamples = { 1.5817f, 1.5846f, 1.5831f, 1.5799f, 1.5745f };
        private static readonly float[] LowerLipHeightSamples = { 1.5643f, 1.5647f, 1.5658f, 1.5680f, 1.5745f };
        private FaceTextures(HeadConversionRecipe recipe) {
            this.recipe = recipe;
            var settings = recipe.settings;
            skin = new Color(settings.skin[0] / 255, settings.skin[1] / 255, settings.skin[2] / 255, 1);
            lips = new Color(settings.lip[0] / 255, settings.lip[1] / 255, settings.lip[2] / 255, 1);
            makeupReference = new Color(settings.makeupReferenceSkin[0] / 255,
                settings.makeupReferenceSkin[1] / 255, settings.makeupReferenceSkin[2] / 255, 1);
            if (settings.age > 0) {
                age = Load(recipe.customAgeTexture, settings.ageOverlay);
            }

            if (settings.makeup > 0) {
                makeup = Load(recipe.customMakeupTexture, "DaytimeRose.png");
            }

            if (settings.scruff) {
                stubble = Load(recipe.customScruffTexture, "FineStubbleTile.png");
            }
        }

        internal static string CustomTextureFingerprint(HeadConversionRecipe recipe) {
            var textures = new[] { recipe.customAgeTexture, recipe.customMakeupTexture, recipe.customScruffTexture };
            var hashes = new List<string>();
            foreach (var texture in textures) {
                if (!texture) {
                    hashes.Add("default");
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(texture);
                if (string.IsNullOrEmpty(path) || !AssetDatabase.Contains(texture)) {
                    throw new ArgumentException("Custom face textures must be saved project assets.");
                }

                if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType == TextureImporterType.NormalMap) {
                    throw new ArgumentException("Choose a color overlay, not a normal map: " + path);
                }

                hashes.Add(AssetDatabase.AssetPathToGUID(path) + ":" + AssetDatabase.GetAssetDependencyHash(path));
            }

            return string.Join("|", hashes);
        }

        private static Image Load(Texture2D source, string fallback) {
            if (!source) {
                return Load(fallback);
            }

            // Read the imported texture without changing the user's import settings.
            // Composition uses color values like those in the bundled PNG files.
            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            bool previousSrgbWrite = GL.sRGBWrite;
            Texture2D readable = null;
            try {
                GL.sRGBWrite = false;
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                readable = new Texture2D(source.width, source.height, TextureFormat.RGBAFloat, false, true);
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readable.Apply();
                var pixels = readable.GetPixels();
                if (QualitySettings.activeColorSpace == ColorSpace.Linear && source.isDataSRGB) {
                    for (int i = 0; i < pixels.Length; i++) {
                        float alpha = pixels[i].a;
                        pixels[i] = pixels[i].gamma;
                        pixels[i].a = alpha;
                    }
                }

                return new Image {
                    width = source.width,
                    height = source.height,
                    pixels = pixels
                };
            } finally {
                RenderTexture.active = previous;
                GL.sRGBWrite = previousSrgbWrite;
                RenderTexture.ReleaseTemporary(target);
                if (readable) {
                    Object.DestroyImmediate(readable);
                }
            }
        }

        private static Image Load(string name) {
            if (Path.GetFileName(name) != name) {
                throw new ArgumentException("Choose a texture filename from the package's texture library.");
            }

            string root = PackageInfo.FindForAssembly(typeof(FaceTextures).Assembly)?.resolvedPath ?? Path.GetFullPath("Packages/digitalbreed.syntyheadconverter");
            string path = Path.Combine(root, "Editor/Textures", name);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try {
                if (!texture.LoadImage(File.ReadAllBytes(path))) {
                    throw new Exception("Cannot read the face overlay: " + name);
                }

                return new Image {
                    width = texture.width,
                    height = texture.height,
                    pixels = texture.GetPixels()
                };
            } finally {
                Object.DestroyImmediate(texture);
            }
        }

        private static float Smooth(float a, float b, float value) {
            float t = Mathf.Clamp01((value - a) / (b - a));
            return t * t * (3 - 2 * t);
        }

        // Front-face projection spans X [-0.14, 0.14] and Y [1.49, 1.81] in canonical meters.
        // Small edge ramps feather the lip tint and protect mouth openings from age/makeup overlays.
        private Color Face(float x, float height) {
            float absolute = Mathf.Abs(x);
            float top = NativeHeadBuilder.Interpolate(LipLateralSamples, UpperLipHeightSamples, absolute);
            float bottom = NativeHeadBuilder.Interpolate(LipLateralSamples, LowerLipHeightSamples, absolute);
            float lip = Mathf.Clamp01((top - height) / .0013f) * Mathf.Clamp01((height - bottom) / .0013f) * Mathf.Clamp01((.039f - absolute) / .002f) * .9f;
            Color color = Color.Lerp(skin, lips, lip);
            float u = (x + .14f) / .28f;
            float v = (height - 1.49f) / .32f;
            float mask = Smooth(.12f, .23f, Mathf.Min(u, 1 - u)) * Smooth(.06f, .17f, v) * (1 - (1 - Smooth(.14f, .17f, Mathf.Abs(u - .5f))) * (1 - Smooth(.035f, .055f, Mathf.Abs(v - .266f))));
            if (age != null) {
                Color layer = age.Sample(u, v);
                color = Color.Lerp(color, layer, mask * layer.a * recipe.settings.age);
            }

            if (makeup != null) {
                Color layer = makeup.Sample(u, v);
                if (recipe.settings.makeupPreserveSkinTone) {
                    layer = AdaptMakeup(color, layer, makeupReference);
                }

                color = Color.Lerp(color, layer, mask * layer.a * recipe.settings.makeup);
            }

            color.a = 1;
            return color;
        }

        // Divide out the overlay's unpainted foundation in linear light, then apply the
        // remaining pigment/detail to the current skin. A neutral foundation becomes identity;
        // alpha retains its existing meaning and earlier age marks remain beneath the makeup.
        internal static Color AdaptMakeup(Color complexion, Color overlay, Color reference) {
            Color baseLinear = complexion.linear;
            Color layerLinear = overlay.linear;
            Color referenceLinear = reference.linear;
            var adapted = new Color(
                Mathf.Clamp01(baseLinear.r * layerLinear.r / Mathf.Max(.001f, referenceLinear.r)),
                Mathf.Clamp01(baseLinear.g * layerLinear.g / Mathf.Max(.001f, referenceLinear.g)),
                Mathf.Clamp01(baseLinear.b * layerLinear.b / Mathf.Max(.001f, referenceLinear.b)), 1).gamma;
            adapted.a = overlay.a;
            return adapted;
        }

        // Cylindrical jaw projection around Z=-0.012 m; anatomical height/angle ramps keep stubble
        // on the jaw and upper neck. A 0.07 m tile scale controls hair density independently of opacity.
        private Color Scruff(Vector3 point, bool detail) {
            float x = -point.x;
            float height = point.y;
            float angle = Mathf.Atan2(x, .012f + point.z);
            Color color = detail ? Face(x, height) : skin;
            float top = Mathf.Lerp(1.609f, 1.625f, Smooth(.6f, 1.5f, Mathf.Abs(angle)));
            float mask = (1 - Smooth(top - .007f, top + .002f, height)) * Smooth(1.49f, 1.508f, height) * (1 - Smooth(1.65f, 2.15f, Mathf.Abs(angle)));
            float absolute = Mathf.Abs(x);
            float upper = NativeHeadBuilder.Interpolate(LipLateralSamples, UpperLipHeightSamples, absolute);
            float lower = NativeHeadBuilder.Interpolate(LipLateralSamples, LowerLipHeightSamples, absolute);
            float lip = (1 - Smooth(.039f, .043f, absolute)) * Smooth(lower - .004f, lower - .001f, height) * (1 - Smooth(upper + .001f, upper + .004f, height));
            mask *= 1 - lip;
            color = Color.Lerp(color, new Color(.39f, .32f, .27f), mask * .18f);
            foreach (var shift in StubbleOffsets) {
                Color marks = stubble.Sample(angle * .10f / .07f + shift.x, (height - 1.42f) / .07f + shift.y, true);
                color = Color.Lerp(color, marks, mask * marks.a * recipe.scruffOpacity);
            }

            color.a = 1;
            return color;
        }

        public static void Create(HeadConversionRecipe recipe, string directory, NativeSurface body, List<HeadAssetBuilder.Mat> materials, int skinMaterial, int detailMaterial) {
            using (var painter = new FaceTextures(recipe)) {
                const int size = 1024;
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++) {
                    for (int x = 0; x < size; x++) {
                        pixels[y * size + x] = painter.Face(x / (size - 1f) * .28f - .14f, y / (size - 1f) * .32f + 1.49f);
                    }
                }

                Write(directory + "/FaceDetail.png", pixels, size);
                if (recipe.settings.scruff) {
                    painter.Wrap(directory, body, materials, skinMaterial, detailMaterial);
                }
            }
        }

        private void Wrap(string directory, NativeSurface mesh, List<HeadAssetBuilder.Mat> materials, int skinMaterial, int detailMaterial) {
            const int size = 2048;
            var pixels = Enumerable.Repeat(skin, size * size).ToArray();
            var covered = new bool[pixels.Length];
            int material = materials.Count;
            materials.Add(new HeadAssetBuilder.Mat { name = recipe.characterId + " jaw skin", color = new[] { 1f, 1f, 1f, 1f }, texture = "JawWrap.png" });
            foreach (var face in mesh.faces.Where(face2 => face2.skin && face2.Center.y > 1.42f &&
                (face2.material == skinMaterial || face2.material == detailMaterial ||
                 materials[face2.material].skinShading && face2.vertices.Skip(1).All(v =>
                     (v.uv - face2.vertices[0].uv).sqrMagnitude < 1e-10f)))) {
                bool detail = face.material == detailMaterial;
                var vertices = face.vertices.Select(vertex => vertex.Clone()).ToArray();
                var angles = vertices.Select(vertex2 => Mathf.Atan2(-vertex2.position.x, .012f + vertex2.position.z)).ToArray();
                if (angles.Max() - angles.Min() > Mathf.PI) {
                    angles = angles.Select(a => a < 0 ? a + 2 * Mathf.PI : a).ToArray();
                }

                for (int i = 0; i < 3; i++) {
                    vertices[i].uv = new Vector2((angles[i] + Mathf.PI) / (2 * Mathf.PI), (vertices[i].position.y - 1.42f) / .43f);
                }

                var tex = vertices.Select(vertex3 => vertex3.uv * size).ToArray();
                int lowX = Mathf.FloorToInt(tex.Min(v => v.x));
                int highX = Mathf.CeilToInt(tex.Max(v => v.x));
                int lowY = Mathf.Max(0, Mathf.FloorToInt(tex.Min(v => v.y)));
                int highY = Mathf.Min(size - 1, Mathf.CeilToInt(tex.Max(v => v.y)));
                float denominator = (tex[1].y - tex[2].y) * (tex[0].x - tex[2].x) + (tex[2].x - tex[1].x) * (tex[0].y - tex[2].y);
                if (Mathf.Abs(denominator) > 1e-8f) {
                    for (int y = lowY; y <= highY; y++) {
                        for (int x = lowX; x <= highX; x++) {
                            float a = ((tex[1].y - tex[2].y) * (x + .5f - tex[2].x) + (tex[2].x - tex[1].x) * (y + .5f - tex[2].y)) / denominator;
                            float b = ((tex[2].y - tex[0].y) * (x + .5f - tex[2].x) + (tex[0].x - tex[2].x) * (y + .5f - tex[2].y)) / denominator;
                            float c = 1 - a - b;
                            if (a >= -1e-5f && b >= -1e-5f && c >= -1e-5f) {
                                Vector3 point = vertices[0].position * a + vertices[1].position * b + vertices[2].position * c;
                                int index = y * size + (x % size + size) % size;
                                pixels[index] = Scruff(point, detail);
                                covered[index] = true;
                            }
                        }
                    }
                }

                face.vertices = vertices;
                face.material = material;
            }

            int[] offsets = { -1, 1, -size, size };
            for (int pass = 0; pass < 8; pass++) {
                var before = (bool[])covered.Clone();
                for (int index = 0; index < pixels.Length; index++) {
                    if (covered[index]) {
                        continue;
                    }

                    foreach (int offset in offsets) {
                        int neighbor = (index + offset + pixels.Length) % pixels.Length;
                        if (before[neighbor]) {
                            pixels[index] = pixels[neighbor];
                            covered[index] = true;
                            break;
                        }
                    }
                }
            }

            Write(directory + "/JawWrap.png", pixels, size);
        }

        private static void Write(string path, Color[] pixels, int size) {
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            try {
                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            } finally {
                Object.DestroyImmediate(texture);
            }
        }

        public void Dispose() {
            // Texture pixel arrays are managed; no imported texture is made readable or modified.
        }
    }
}
