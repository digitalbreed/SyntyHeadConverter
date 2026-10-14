// Captures source prefab geometry in bind space, classifies connected regions and suggests initial fitting values.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace digitalbreed.SyntyHeadConverter {
    public static class HeadSource {
        private static readonly string[] FeminineNameTerms = { "female", "girl", "woman", "dress" };
        private static readonly string[] BaseTextureProperties = { "_BaseMap", "_MainTex", "_Albedo_Map" };
        [Serializable]
        public class Bone {
            public string name, parent;
            public Vector3 position;
        }

        [Serializable]
        public class Weight {
            public string[] names;
            public float[] values;
        }

        [Serializable]
        public class Slot {
            public string texture, shader, name, material;
            public Color skin, hair;
            public int[] triangles;
        }

        [Serializable]
        public class Part {
            public string name;
            public Vector3[] vertices, normals, localVertices;
            public Vector2[] uv;
            public Weight[] weights;
            public Slot[] slots;
            public float[] rootToMesh;
        }

        [Serializable]
        public class Source {
            public string id, prefab;
            public Bone[] bones;
            public Part[] skins;
            public Part[] accessories;
        }

        static string Texture(Material material) {
            foreach (var propertyName in BaseTextureProperties) {
                if (material.HasProperty(propertyName) && material.GetTexture(propertyName)) {
                    return AssetDatabase.GetAssetPath(material.GetTexture(propertyName));
                }
            }

            return "";
        }

        public static Part Read(Renderer renderer, Mesh mesh, Transform root, Transform[] bones) {
            var toRoot = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var inverse = toRoot.inverse;
            var meshPart = new Part {
                name = renderer.name,
                vertices = mesh.vertices.Select(toRoot.MultiplyPoint3x4).ToArray(),
                normals = mesh.normals.Select(toRoot.MultiplyVector).Select(n => n.normalized).ToArray(),
                localVertices = mesh.vertices,
                uv = mesh.uv,
                rootToMesh = Enumerable.Range(0, 16).Select(i => inverse[i / 4, i % 4]).ToArray()
            };
            meshPart.slots = renderer.sharedMaterials.Select((material2, i) => new Slot { name = material2.name, material = AssetDatabase.GetAssetPath(material2), texture = Texture(material2), shader = material2.shader.name, skin = material2.HasProperty("_Skin_Color") ? material2.GetColor("_Skin_Color") : Color.clear, hair = material2.HasProperty("_Hair_Color") ? material2.GetColor("_Hair_Color") : Color.clear, triangles = mesh.GetTriangles(i) }).ToArray();
            if (bones != null) {
                meshPart.weights = mesh.boneWeights.Select(boneWeight => new Weight { names = new[] { bones[boneWeight.boneIndex0].name, bones[boneWeight.boneIndex1].name, bones[boneWeight.boneIndex2].name, bones[boneWeight.boneIndex3].name }, values = new[] { boneWeight.weight0, boneWeight.weight1, boneWeight.weight2, boneWeight.weight3 } }).ToArray();
            }

            return meshPart;
        }

        public static Source Capture(GameObject prefab) {
            if (!prefab || !EditorUtility.IsPersistent(prefab) || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(prefab))) {
                throw new Exception("Choose a prefab asset from the Project window.");
            }

            var scene = EditorSceneManager.NewPreviewScene();
            GameObject characterInstance = null;
            try {
                characterInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                characterInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                characterInstance.transform.localScale = Vector3.one;
                var skins = characterInstance.GetComponentsInChildren<SkinnedMeshRenderer>().Where(skinRenderer => skinRenderer.enabled && skinRenderer.sharedMesh).ToArray();
                if (skins.Length == 0) {
                    throw new Exception("The source has no active skinned body mesh.");
                }

                var primary = skins.OrderByDescending(skinRenderer2 => skinRenderer2.sharedMesh.vertexCount).First();
                if (primary.sharedMesh.blendShapeCount >= 80) {
                    throw new Exception("Choose the original POLYGON prefab, not an already converted head.");
                }

                if (primary.bones.Any(boneTransform => !boneTransform) || primary.bones.Length != primary.sharedMesh.bindposes.Length) {
                    throw new Exception("Source has incomplete bones or bind poses.");
                }

                foreach (var skinRenderer3 in skins) {
                    var rest = skinRenderer3.sharedMesh.bindposes.Select(transformMatrix => skinRenderer3.transform.localToWorldMatrix * transformMatrix.inverse).ToArray();
                    for (int i = 0; i < skinRenderer3.bones.Length; i++) {
                        skinRenderer3.bones[i].SetPositionAndRotation(rest[i].GetColumn(3), rest[i].rotation);
                    }
                }

                var source = new Source {
                    prefab = AssetDatabase.GetAssetPath(prefab),
                    bones = primary.bones.Select(boneTransform2 => new Bone { name = boneTransform2.name, parent = boneTransform2.parent ? boneTransform2.parent.name : "", position = characterInstance.transform.InverseTransformPoint(boneTransform2.position) }).ToArray(),
                    skins = skins.OrderByDescending(skinRenderer4 => skinRenderer4.sharedMesh.vertexCount).Select(skinRenderer5 => Read(skinRenderer5, skinRenderer5.sharedMesh, characterInstance.transform, skinRenderer5.bones)).ToArray(),
                    accessories = characterInstance.GetComponentsInChildren<MeshRenderer>().Where(meshRenderer => meshRenderer.enabled && meshRenderer.GetComponent<MeshFilter>() && meshRenderer.GetComponent<MeshFilter>().sharedMesh).Select(meshRenderer2 => Read(meshRenderer2, meshRenderer2.GetComponent<MeshFilter>().sharedMesh, characterInstance.transform, null)).ToArray()
                };
                if (!source.bones.Any(boneData => boneData.name.Equals("Head", StringComparison.OrdinalIgnoreCase))) {
                    throw new Exception("Source has no Head bone.");
                }

                return source;
            } finally {
                if (characterInstance) {
                    Object.DestroyImmediate(characterInstance);
                }

                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        public static string Fingerprint(GameObject prefab) => AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(prefab)).ToString();
        public static HeadRegion[] Regions(Source source) {
            var meshPart = source.skins[0];
            var triangles = meshPart.slots.SelectMany(s => s.triangles).ToArray();
            int count = triangles.Length / 3;
            var parent = Enumerable.Range(0, count).ToArray();
            int Root(int i) {
                while (parent[i] != i) {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }

                return i;
            }

            var shared = new Dictionary<string, int>();
            for (int f = 0; f < count; f++) {
                for (int k = 0; k < 3; k++) {
                    int i = triangles[f * 3 + k];
                    var v = meshPart.vertices[i];
                    var uv = meshPart.uv[i];
                    string key = Mathf.RoundToInt(v.x * 1e6f) + "," + Mathf.RoundToInt(v.y * 1e6f) + "," + Mathf.RoundToInt(v.z * 1e6f) + "/" + Mathf.RoundToInt(uv.x * 1000) + "," + Mathf.RoundToInt(uv.y * 1000);
                    if (shared.TryGetValue(key, out int other)) {
                        parent[Root(f)] = Root(other);
                    } else {
                        shared[key] = f;
                    }
                }
            }

            return Enumerable.Range(0, count).GroupBy(Root).Select(g => {
                var faces = g.ToArray();
                var indices = faces.SelectMany(f => triangles.Skip(f * 3).Take(3)).Distinct().ToArray();
                var bounds = new Bounds(meshPart.vertices[indices[0]], Vector3.zero);
                foreach (int i in indices) {
                    bounds.Encapsulate(meshPart.vertices[i]);
                }

                var uv = meshPart.uv[indices[0]];
                return new HeadRegion {
                    label = faces.Length + " faces · palette " + uv.x.ToString("F3") + ", " + uv.y.ToString("F3"),
                    faces = faces,
                    center = bounds.center,
                    size = bounds.size
                };
            }).OrderByDescending(r => r.center.y).ToArray();
        }

        public static HeadConversionRecipe Suggest(GameObject prefab, bool reuseExistingFit = true) {
            var source = Capture(prefab);
            var recipe = ScriptableObject.CreateInstance<HeadConversionRecipe>();
            recipe.sourcePrefab = prefab;
            recipe.characterId = System.Text.RegularExpressions.Regex.Replace(prefab.name, "[^A-Za-z0-9_]", "");
            recipe.outputFolder = "Assets/SyntyHeadConverter/Characters/" + recipe.characterId;
            recipe.sourceFingerprint = Fingerprint(prefab);
            HeadRegion[] previousRegions = null;
            if (reuseExistingFit) {
                foreach (string guid in AssetDatabase.FindAssets("t:HeadConversionRecipe")) {
                    var old = AssetDatabase.LoadAssetAtPath<HeadConversionRecipe>(AssetDatabase.GUIDToAssetPath(guid));
                    if (old && old.sourcePrefab == prefab && old.sourceFingerprint == recipe.sourceFingerprint) {
                        Object.DestroyImmediate(recipe);
                        recipe = Object.Instantiate(old);
                        previousRegions = recipe.regions;
                        break;
                    }
                }

                HeadConversionExtensions.ConfigureRecipe?.Invoke(recipe);
            }

            bool automaticFit = recipe.baseConfig == "{}";
            if (automaticFit) {
                float head = source.bones.First(boneData => boneData.name.Equals("Head", StringComparison.OrdinalIgnoreCase)).position.y;
                if (head < .1f) {
                    throw new Exception("Cannot infer upright character scale.");
                }

                SuggestProportions(source, recipe.settings);
                recipe.settings.female = FeminineNameTerms.Any(term => prefab.name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) ? 1 : 0;
                recipe.baseConfig = "{\"preserveSourceWeights\":true,\"skinUvRegions\":[[0,0.04,0.15,0.22]]}";
                recipe.settings.accessories = SuggestAccessories(source, recipe.settings.canonicalScale, recipe.settings.canonicalOffset);
                SuggestColors(recipe, source);
                SuggestScalpShell(recipe, source);
            }

            recipe.regions = Regions(source);
            if (automaticFit) {
                SuggestNeckBands(recipe, source);
            }
            if (previousRegions != null) {
                foreach (var r in recipe.regions) {
                    var previous = previousRegions.FirstOrDefault(p => p.faces != null && p.faces.SequenceEqual(r.faces));
                    if (previous != null) {
                        r.role = previous.role;
                    }
                }
            }

            return recipe;
        }

        // Canonical POLYGON adult measurements in meters: head pivot, skull width (including
        // ears), and crown rise above the pivot. Keep ordinary adult fits unchanged; a width
        // mismatch above 25% indicates different head/body proportions, not uniform model scale.
        internal static void SuggestProportions(Source source, HeadFitSettings settings) {
            const float referenceHeadHeight = 1.569112f;
            const float referenceSkullWidth = .2661f;
            const float referenceCrownRise = .2190f;
            const float proportionTolerance = .25f;
            float headHeight = source.bones.First(bone => bone.name.Equals("Head", StringComparison.OrdinalIgnoreCase)).position.y;
            settings.canonicalScale = referenceHeadHeight / headHeight;
            settings.canonicalOffset = 0;
            var part = source.skins[0];
            var scalp = Enumerable.Range(0, part.vertices.Length).Where(index =>
                part.vertices[index].y > headHeight && part.uv[index].x < .04f &&
                part.uv[index].y > .15f && part.uv[index].y < .22f).Select(index => part.vertices[index]).ToArray();
            if (scalp.Length < 6) {
                return;
            }

            float width = scalp.Max(point => point.x) - scalp.Min(point => point.x);
            float crown = scalp.Max(point => point.y);
            float rise = crown - headHeight;
            if (width <= 0 || rise <= 0 || Mathf.Abs(width * settings.canonicalScale / referenceSkullWidth - 1) <= proportionTolerance) {
                return;
            }

            // Balance width and height instead of allowing short legs/body to shrink the face.
            // Anchor the crown so the retained hair and replacement skull agree vertically.
            settings.canonicalScale = Mathf.Sqrt(referenceSkullWidth / width * referenceCrownRise / rise);
            settings.canonicalOffset = referenceHeadHeight + referenceCrownRise - crown * settings.canonicalScale;
        }

        // Prefer a separate hair mesh over headwear. Bounds ensure names such as hair accessories
        // below the head cannot enable scalp fitting; all measurements use canonical fitting meters.
        private static void SuggestScalpShell(HeadConversionRecipe recipe, Source source) {
            float scale = recipe.settings.canonicalScale;
            Vector3 offset = Vector3.up * recipe.settings.canonicalOffset;
            var candidates = source.accessories.Where(part => part.vertices.Length > 0).Select(part => {
                bool hair = part.name.IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0;
                bool headwear = part.name.IndexOf("hat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    part.name.IndexOf("helmet", StringComparison.OrdinalIgnoreCase) >= 0;
                var positions = part.vertices.Select(position => position * scale + offset).ToArray();
                float left = positions.Min(position => position.x);
                float right = positions.Max(position => position.x);
                float back = positions.Min(position => position.z);
                float front = positions.Max(position => position.z);
                return new { part, hair, headwear, left, right, back, front,
                    top = positions.Max(position => position.y), coverage = (right - left) * (front - back) };
            }).Where(candidate => (candidate.hair || candidate.headwear) && candidate.top > 1.72f &&
                candidate.left < -.04f && candidate.right > .04f && candidate.back < -.035f)
                .OrderByDescending(candidate => candidate.hair).ThenByDescending(candidate => candidate.coverage)
                .ThenBy(candidate => candidate.part.name, StringComparer.Ordinal).FirstOrDefault();
            if (candidates != null) {
                recipe.settings.scalpHairEnvelope = candidates.part.name;
                return;
            }

            // Integrated hair follows the same palette and height rules used to retain it during cutting.
            var mesh = source.skins[0];
            bool integratedHair = Enumerable.Range(0, mesh.vertices.Length).Any(index => {
                Vector3 position = mesh.vertices[index] * scale + offset;
                Vector2 uv = mesh.uv[index];
                return position.y > 1.79f && position.z < -.015f && uv.x > .04f && uv.x < .14f;
            });
            if (integratedHair) {
                recipe.settings.scalpHairEnvelope = "embedded";
            }
        }

        // A small, closed band around the neck is separated before fitting. Its two boundary
        // loops distinguish it from a pendant or a broad garment region; all distances are canonical meters.
        private static void SuggestNeckBands(HeadConversionRecipe recipe, Source source) {
            var part = source.skins[0];
            var triangles = part.slots.SelectMany(slot => slot.triangles).ToArray();
            float scale = recipe.settings.canonicalScale;
            Vector3 offset = Vector3.up * recipe.settings.canonicalOffset;
            foreach (var region in recipe.regions) {
                var indices = region.faces.SelectMany(face => triangles.Skip(face * 3).Take(3)).Distinct().ToArray();
                var positions = indices.Select(index => part.vertices[index] * scale + offset).ToArray();
                if (positions.Min(position => position.y) < 1.38f || positions.Max(position => position.y) > 1.55f ||
                    positions.Any(position => Mathf.Abs(position.x) > .11f || Mathf.Abs(position.z + .012f) > .10f) ||
                    indices.Any(index => part.uv[index].x < .04f)) {
                    continue;
                }

                var surface = new NativeSurface();
                foreach (int face in region.faces) {
                    var vertices = Enumerable.Range(0, 3).Select(corner => new NativeVertex {
                        position = part.vertices[triangles[face * 3 + corner]] * scale + offset
                    }).ToArray();
                    surface.faces.Add(new NativeTriangle(vertices[0], vertices[1], vertices[2], 0));
                }

                var boundary = surface.Edges().Where(edge => edge.Value.Count == 1).Select(edge => edge.Key).ToHashSet();
                try {
                    var firstLoop = surface.Ring(1.47f, boundary);
                    foreach (var edge in firstLoop.Select((vertex, index) =>
                        NativeSurface.Edge(vertex.position, firstLoop[(index + 1) % firstLoop.Count].position))) {
                        boundary.Remove(edge);
                    }

                    var secondLoop = surface.Ring(1.47f, boundary);
                    if (boundary.Count != secondLoop.Count) {
                        continue;
                    }

                    // A necklace band follows the same neck contour along both edges.
                    // Folded shirt collars flare outward: treating them as jewelry creates
                    // a skin copy of the collar, including its folded lapels, inside the neck.
                    float firstRadius = firstLoop.Average(vertex => new Vector2(vertex.position.x, vertex.position.z + .012f).magnitude);
                    float secondRadius = secondLoop.Average(vertex => new Vector2(vertex.position.x, vertex.position.z + .012f).magnitude);
                    const float maximumBandFlare = 1.12f;
                    if (Mathf.Max(firstRadius, secondRadius) > Mathf.Min(firstRadius, secondRadius) * maximumBandFlare) {
                        continue;
                    }

                    region.role = "Necklace";
                    // Exposed necks need a continuous skin surface, not a recessed shirt lining.
                    recipe.settings.collarInset = false;
                } catch (InvalidOperationException) {
                    // An open collar, pendant or unrelated detail is left for normal region handling.
                }
            }
        }

        static void SuggestColors(HeadConversionRecipe recipe, Source source) {
            var part = source.skins[0];
            float scale = recipe.settings.canonicalScale;
            Vector3 offset = Vector3.up * recipe.settings.canonicalOffset;
            // Palette meshes split vertices at shading seams. Surface area, rather than vertex
            // count, keeps small detailed lips and eyebrows from outweighing the facial skin.
            var candidates = part.slots.SelectMany(slot => Enumerable.Range(0, slot.triangles.Length / 3).Select(faceIndex => {
                int first = slot.triangles[faceIndex * 3];
                int second = slot.triangles[faceIndex * 3 + 1];
                int third = slot.triangles[faceIndex * 3 + 2];
                Vector3 firstPosition = part.vertices[first] * scale + offset;
                Vector3 secondPosition = part.vertices[second] * scale + offset;
                Vector3 thirdPosition = part.vertices[third] * scale + offset;
                return new {
                    slot,
                    uv = (part.uv[first] + part.uv[second] + part.uv[third]) / 3,
                    center = (firstPosition + secondPosition + thirdPosition) / 3,
                    area = Vector3.Cross(secondPosition - firstPosition, thirdPosition - firstPosition).magnitude / 2
                };
            })).Where(face => Mathf.Abs(face.center.x) < .09f && face.center.y > 1.57f &&
                face.center.y < 1.7f && face.center.z > .04f && face.uv.x < .04f &&
                face.uv.y > .15f && face.uv.y < .22f).ToArray();
            var sample = candidates.GroupBy(face => (face.slot, palette: new Vector2Int(
                Mathf.RoundToInt(face.uv.x * 1000), Mathf.RoundToInt(face.uv.y * 1000))))
                .OrderByDescending(group => group.Sum(face => face.area)).FirstOrDefault();
            var uv = sample == null ? new Vector2(.018f, .19f) : sample.First().uv;
            var skinSlot = sample == null ? part.slots[0] : sample.First().slot;
            recipe.settings.skinSampleUv = new[] { uv.x, uv.y };
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(skinSlot.texture);
            if (!texture) {
                return;
            }

            var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Texture2D pixels = null;
            try {
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;
                pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                pixels.Apply();
                var color = pixels.GetPixelBilinear(uv.x, uv.y);
                if (skinSlot.skin.a > 0) {
                    color *= skinSlot.skin;
                }

                recipe.settings.skin = new[] { color.r * 255, color.g * 255, color.b * 255 };
                recipe.settings.lip = new[] { color.r * 220, color.g * 180, color.b * 180 };
            } finally {
                if (pixels) {
                    Object.DestroyImmediate(pixels);
                }

                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        static int[] SuggestAccessories(Source source, float scale, float heightOffset) {
            var mesh = source.skins[0];
            var parent = Enumerable.Range(0, mesh.vertices.Length).ToArray();
            int Root(int i) {
                while (parent[i] != i) {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }

                return i;
            }

            var welded = new Dictionary<Vector3Int, int>();
            var indices = new int[parent.Length];
            for (int i = 0; i < parent.Length; i++) {
                var v = mesh.vertices[i] * scale * 1000000;
                var key = new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
                if (!welded.TryGetValue(key, out int old)) {
                    welded[key] = old = i;
                }

                indices[i] = old;
            }

            foreach (var slot in mesh.slots) {
                for (int i = 0; i < slot.triangles.Length; i += 3) {
                    int a = Root(indices[slot.triangles[i]]);
                    parent[Root(indices[slot.triangles[i + 1]])] = a;
                    parent[Root(indices[slot.triangles[i + 2]])] = a;
                }
            }

            return welded.Values.GroupBy(Root).Where(g => g.Count() >= 16 && g.Count() < 500 && g.Max(i => mesh.vertices[i].y * scale + heightOffset) > 1.45f && !(new[] { 10, 14, 15, 20 }.Contains(g.Count()) && g.Min(i => mesh.vertices[i].y * scale + heightOffset) > 1.65f)).Select(g => g.Count()).Distinct().ToArray();
        }
    }
}
