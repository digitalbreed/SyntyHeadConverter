// Assembles retained source geometry and expressive Sidekick parts into the native conversion result.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace digitalbreed.SyntyHeadConverter {
    internal sealed partial class NativeHeadBuilder {
        private readonly HeadConversionRecipe recipe;
        private readonly HeadFitSettings fit;
        private readonly RecipeOptions options;
        private readonly HeadSource.Source source;
        private readonly List<HeadAssetBuilder.Mat> materials = new List<HeadAssetBuilder.Mat>();
        private readonly NativeSurface body = new NativeSurface();
        private readonly NativeSurface accessories = new NativeSurface();
        private readonly NativeSurface head = new NativeSurface();
        private readonly NativeSurface sourceScalp = new NativeSurface();
        private readonly List<(string, NativeSurface)> parts = new List<(string, NativeSurface)>();
        private readonly List<HeadAssetBuilder.Bone> bones = new List<HeadAssetBuilder.Bone>();
        private List<NativeVertex> sourceRing;
        private int skinMaterial;
        private int detailMaterial;
        private int mouthMaterial;
        private int lidMaterial;
        [Serializable]
        private sealed class NeckReport {
            public Vector3[] lower;
            public Vector3[] upper;
        }

        private NativeHeadBuilder(HeadConversionRecipe recipe, HeadSource.Source source) {
            this.recipe = recipe;
            this.source = source;
            fit = recipe.settings;
            options = new RecipeOptions(recipe.baseConfig);
        }

        public static HeadAssetBuilder.Data Build(HeadConversionRecipe recipe, HeadSource.Source source, Dictionary<string, SidekickAssets.Donor> donors, string textureDirectory) {
            return new NativeHeadBuilder(recipe, source).Build(donors, textureDirectory);
        }

        private int Material(string name, float[] rgb, string texture = null, string original = null) {
            var color = new Color(rgb[0] / 255, rgb[1] / 255, rgb[2] / 255).linear;
            materials.Add(new HeadAssetBuilder.Mat { name = recipe.characterId + " " + name, color = new[] { color.r, color.g, color.b, 1f }, texture = texture, sourceMaterial = original });
            return materials.Count - 1;
        }

        private bool SourceSkin(Vector2 uv) {
            var rows = options.Rows("skinUvRegions");
            if (rows.Length > 0) {
                return rows.Any(r => uv.x >= r[0] && uv.x <= r[1] && uv.y >= r[2] && uv.y <= r[3]);
            }

            var samples = options.Rows("skinUvs");
            if (samples.Length > 0) {
                return samples.Any(p => Mathf.Abs(uv.x - p[0]) < .002f && Mathf.Abs(uv.y - p[1]) < .002f);
            }

            return uv.x < .04f;
        }

        private Vector3 Canonical(Vector3 point) {
            return point * fit.canonicalScale + Vector3.up * fit.canonicalOffset;
        }

        private Vector3 FitDonor(Vector3 point) {
            return new Vector3(point.x * fit.width, point.y + .035f, point.z);
        }

        private Dictionary<string, float> Weights(string[] names, float[] values) {
            var result = new Dictionary<string, float>();
            for (int i = 0; i < names.Length; i++) {
                if (values[i] <= 0) {
                    continue;
                }

                string name = names[i];
                result.TryGetValue(name, out float previous);
                result[name] = previous + values[i];
            }

            return result;
        }

        private NativeSurface SourceSurface(HeadSource.Part part, bool rigid = false) {
            Vector3 Point(Vector3 point) {
                if (rigid) {
                    foreach (var adjustment in options.Objects("rigidFits").Where(a => a.Text("name") == part.name)) {
                        var c = adjustment.Numbers("center");
                        var s = adjustment.Numbers("scale");
                        var o = adjustment.Numbers("offset");
                        var center = new Vector3(c[0], c[1], c[2]);
                        point = center + Vector3.Scale(point - center, new Vector3(s[0], s[1], s[2])) + new Vector3(o[0], o[1], o[2]);
                    }
                }

                return Canonical(point);
            }

            var vertices = part.vertices.Select((p, i) => new NativeVertex { position = Point(p), normal = part.normals.Length > i ? part.normals[i] : Vector3.up, uv = part.uv[i], weights = rigid ? new Dictionary<string, float> { { "Head", 1 } } : Weights(part.weights[i].names, part.weights[i].values) }).ToArray();
            var result = new NativeSurface();
            int faceId = 0;
            foreach (var slot in part.slots) {
                int material = Material(materials.Count == 0 ? "original palette" : slot.name, new[] { 255f, 255f, 255f }, original: slot.material);
                for (int i = 0; i < slot.triangles.Length; i += 3) {
                    var vertex = vertices[slot.triangles[i]];
                    var vertex2 = vertices[slot.triangles[i + 1]];
                    var vertex3 = vertices[slot.triangles[i + 2]];
                    result.faces.Add(new NativeTriangle(vertex, vertex2, vertex3, material, SourceSkin(vertex.uv)) { sourceFace = faceId++ });
                }
            }

            return result;
        }

        private NativeSurface Donor(SidekickAssets.Donor donor, Func<Vector2, int> material) {
            float female = options.Number("feminineShape", fit.female);
            donor.shapes.TryGetValue("masculineFeminine", out var feminine);
            var vertices = donor.vertices.Select((p, i) => new NativeVertex { position = FitDonor(p + (feminine == null ? Vector3.zero : feminine[i] * female)), normal = new Vector3(donor.normals[i].x / fit.width, donor.normals[i].y, donor.normals[i].z).normalized, uv = donor.uv[i], weights = Weights(donor.weights[i].names, donor.weights[i].values), shapes = donor.shapes.Where(s => !new[] { "defaultBuff", "defaultHeavy", "defaultSkinny", "masculineFeminine" }.Contains(s.Key)).ToDictionary(s => s.Key, s => new Vector3(s.Value[i].x * fit.width, s.Value[i].y, s.Value[i].z)) }).ToArray();
            var result = new NativeSurface();
            for (int i = 0; i < donor.faces.Length; i++) {
                var polygon = donor.faces[i];
                var vertex = vertices[polygon[0]];
                int slot = material(vertex.uv);
                for (int j = 1; j + 1 < polygon.Length; j++) {
                    result.faces.Add(new NativeTriangle(vertex, vertices[polygon[j]], vertices[polygon[j + 1]], slot, slot == skinMaterial) { sourceFace = i });
                }
            }

            return result;
        }

        private Dictionary<NativeTriangle, List<NativeVertex>> Components(NativeSurface mesh) {
            var points = mesh.Vertices.GroupBy(vertex => NativeSurface.Key(vertex.position)).ToDictionary(g => g.Key, g => g.First());
            var neighbors = points.Keys.ToDictionary(k => k, k => new HashSet<Vector3Int>());
            foreach (var face in mesh.faces) {
                var keys = face.vertices.Select(vertex2 => NativeSurface.Key(vertex2.position)).ToArray();
                foreach (var key in keys) {
                    neighbors[key].UnionWith(keys);
                }
            }

            var groups = new Dictionary<Vector3Int, List<NativeVertex>>();
            foreach (var key in points.Keys) {
                if (groups.ContainsKey(key)) {
                    continue;
                }

                var group = new List<NativeVertex>();
                var stack = new Stack<Vector3Int>();
                stack.Push(key);
                while (stack.Count > 0) {
                    var current = stack.Pop();
                    if (groups.ContainsKey(current)) {
                        continue;
                    }

                    groups.Add(current, group);
                    group.Add(points[current]);
                    foreach (var next in neighbors[current]) {
                        stack.Push(next);
                    }
                }
            }

            return mesh.faces.ToDictionary(face2 => face2, face3 => groups[NativeSurface.Key(face3.vertices[0].position)]);
        }

        private void CutSource(NativeSurface original) {
            var roles = recipe.regions.Where(r => r.role != "Auto").SelectMany(r => r.faces.Select(f => (f, r.role))).ToDictionary(mapping => mapping.f, mapping2 => mapping2.role);
            // Follow connected clothing across palette/UV seams. Patterned collars can have
            // their own texture region while remaining part of the torso garment.
            var garments = new NativeSurface();
            garments.faces.AddRange(original.faces.Where(face => !face.skin));
            var garmentComponents = Components(garments);
            // Canonical bounds exclude head accessories while retaining collar tips whose
            // weights legitimately include the neck or head: the connected garment must reach
            // below 1.38 m, end below 1.65 m, and include a majority-spine-weighted vertex.
            var retainedGarments = garmentComponents.Values.Distinct().Where(vertices =>
                vertices.Min(vertex => vertex.position.y) < 1.38f &&
                vertices.Max(vertex => vertex.position.y) < 1.65f && vertices.Any(vertex =>
                    vertex.weights.Any(weight => weight.Key.IndexOf("spine", StringComparison.OrdinalIgnoreCase) >= 0 && weight.Value > .5f))).ToHashSet();
            var garmentFaces = garmentComponents.Where(pair => retainedGarments.Contains(pair.Value))
                .Select(pair => pair.Key.sourceFace).ToHashSet();
            SeparateJewelry(original, roles);
            var components = Components(original);
            var hairBuckets = original.faces.Where(face2 => face2.vertices.Max(vertex => vertex.position.y) > 1.79f && face2.vertices[0].uv.x > .04f && face2.vertices[0].uv.x < .14f).Select(face3 => (float)Math.Round(face3.vertices[0].uv.x, 2)).ToHashSet();
            hairBuckets.UnionWith(options.Numbers("hairUvBuckets"));
            var seen = new HashSet<string>();
            foreach (var face in original.faces) {
                var group = components[face];
                Vector2 uv = face.vertices[0].uv;
                float height = face.Center.y;
                bool separate = fit.accessories.Contains(group.Count) && group.Min(vertex2 => vertex2.position.y) > options.Number("accessoryMinZ", 1.35f);
                bool embedded = group.Count > 500 && hairBuckets.Contains((float)Math.Round(uv.x, 2)) && height > 1.38f;
                embedded |= group.Count > options.Number("hairRegionMinComponent") && options.Rows("hairUvRegions").Any(r => uv.x > r[0] && uv.x < r[1] && uv.y > r[2] && uv.y < r[3] && height > r[4]);
                bool remove = options.Numbers("removeComponents").Contains(group.Count) && group.Min(vertex3 => vertex3.position.y) > options.Number("removeComponentsMinZ", -100);
                remove |= group.Count < 500 && options.Rows("removeComponentUvRegions").Any(r => uv.x > r[0] && uv.x < r[1] && uv.y > r[2] && uv.y < r[3]);
                bool accessory = (separate || embedded) && !remove;
                bool keep = group.Count > 500 && !SourceSkin(uv);
                if (options.Flag("preserveSourceUpperBody")) {
                    keep = !SourceSkin(uv) || Mathf.Abs(face.Center.x) > .15f;
                    string key = string.Join("|", face.vertices.Select(vertex4 => NativeSurface.Key(vertex4.position).ToString()).OrderBy(v => v)) + "/" + face.material;
                    if (!seen.Add(key)) {
                        continue;
                    }
                } else if (options.Has("skinUvRegions") && face.vertices.Max(vertex5 => vertex5.position.y) > 1.55f) {
                    keep = garmentFaces.Contains(face.sourceFace);
                }

                if (roles.TryGetValue(face.sourceFace, out string role)) {
                    remove = role == "Remove";
                    accessory = role == "Accessory";
                    keep = role == "Preserve";
                }

                if (remove) {
                    continue;
                }

                if (accessory) {
                    accessories.faces.Add(face);
                } else if (keep) {
                    // Split retained garments too, so the cut skin cannot leave a
                    // T-junction that opens as neighboring bone weights deform it.
                    if (face.vertices.Min(vertex6 => vertex6.position.y) < fit.cut - .000001f && face.vertices.Max(vertex7 => vertex7.position.y) > fit.cut + .000001f) {
                        body.Polygon(NativeSurface.Clip(face, fit.cut), face.material, face.skin);
                        body.Polygon(NativeSurface.Clip(face, fit.cut, true), face.material, face.skin);
                    } else {
                        body.faces.Add(face);
                    }
                } else {
                    body.Polygon(NativeSurface.Clip(face, fit.cut), face.material, face.skin);
                }
            }
        }

        private HeadAssetBuilder.Data Build(Dictionary<string, SidekickAssets.Donor> donors, string textureDirectory) {
            var original = SourceSurface(source.skins[0]);
            // Keep the original head envelope independent of subsequent cutting and fitting.
            // Hair closures inside it must not pull an exposed bald scalp inward.
            foreach (var face in original.faces.Where(face => face.Center.y > 1.55f && Mathf.Abs(face.Center.x) < .2f)) {
                var vertices = face.vertices.Select(vertex => vertex.Clone()).ToArray();
                sourceScalp.faces.Add(new NativeTriangle(vertices[0], vertices[1], vertices[2], 0));
            }
            skinMaterial = Material("skin", fit.skin);
            mouthMaterial = Material("mouth interior", new[] { 72f, 24f, 23f });
            lidMaterial = Material("lid margin", fit.skin.Select(c => c * .68f).ToArray());
            detailMaterial = Material("facial detail", fit.skin, "FaceDetail.png");
            CutSource(original);
            RepairHiddenBoundaries();
            var donorHead = Donor(donors["01HEAD"], uv => uv.x < .04f ? skinMaterial : uv.y > .23f && uv.y < .29f ? lidMaterial : mouthMaterial);
            var removed = donorHead.faces.GroupBy(face2 => face2.sourceFace).Where(g => g.SelectMany(face3 => face3.vertices).Distinct().Average(vertex => vertex.weights.Where(w => w.Key == "neck_01" || w.Key == "spine_03").Sum(w => w.Value)) > .35f).SelectMany(g => g).ToHashSet();
            var cutEdges = donorHead.Edges().Where(e => e.Value.Count == 2 && e.Value.Count(removed.Contains) == 1).Select(e => e.Key).ToHashSet();
            head.faces.AddRange(donorHead.faces.Where(face4 => !removed.Contains(face4)));
            var lower = sourceRing ?? body.Ring(fit.cut);
            var upper = head.Ring(1.52f, cutEdges);
            System.IO.File.WriteAllText(System.IO.Path.Combine(textureDirectory, "../neck.json"), HeadConverterJson.Serialize(new NeckReport { lower = lower.Select(vertex2 => vertex2.position).ToArray(), upper = upper.Select(vertex3 => vertex3.position).ToArray() }, true));
            Bridge(lower, upper);
            body.faces.AddRange(head.faces);
            parts.Add((recipe.characterId + "_BodyHead", body));
            parts.Add((recipe.characterId + "_OriginalHairAccessories", accessories));
            foreach (var donor in donors) {
                foreach (var bone in donor.Value.bones) {
                    if (!bones.Any(boneData => boneData.name == bone.name)) {
                        bones.Add(new HeadAssetBuilder.Bone { name = bone.name, parent = bone.parent, position = FitDonor(bone.position) });
                    }
                }

                if (donor.Key == "01HEAD") {
                    continue;
                }

                int material;
                NativeSurface surface;
                if (donor.Key.Contains("EYE")) {
                    var slots = new[] {
                        Material(donor.Key + " sclera", new[] { 237f, 231f, 217f }),
                        Material(donor.Key + " iris rim", fit.iris.Select(c => c * .5f).ToArray()),
                        Material(donor.Key + " pupil", new[] { 20f, 25f, 29f }),
                        Material(donor.Key + " iris", fit.iris),
                        Material(donor.Key + " catchlight", new[] { 255f, 255f, 255f })
                    };
                    surface = Donor(donor.Value, uv => slots[uv.y > .3f ? 0 : uv.y > .24f ? 1 : uv.y > .18f ? 2 : uv.y > .11f ? 3 : 4]);
                } else {
                    material = donor.Key.Contains("EBR") || donor.Key == "09FCHR" ? Material(donor.Key + " hair", fit.brow) : donor.Key == "36TETH" ? Material("teeth", new[] { 239f, 232f, 211f }) : donor.Key == "37TONG" ? Material("tongue", new[] { 173f, 84f, 79f }) : skinMaterial;
                    surface = Donor(donor.Value, uv => material);
                }

                parts.Add((recipe.characterId + "_" + donor.Key, surface));
            }

            AddExpressiveAccessories();
            AdjustFit();
            var finalEdges = body.Edges();
            foreach (var face in body.faces.Where(face5 => face5.graft)) {
                for (int i = 0; i < 3; i++) {
                    var edge = NativeSurface.Edge(face.vertices[i].position, face.vertices[(i + 1) % 3].position);
                    if (finalEdges[edge].Count != 2) {
                        throw new Exception("A fitting adjustment opened the neck seam. The preview was rejected.");
                    }
                }
            }

            foreach (var part in parts) {
                if (part.Item1.EndsWith("_BodyHead") || part.Item1.EndsWith("_35NOSE")) {
                    if (fit.faceSmoothing > 0) {
                        // Smooth only skin surfaces; keep teeth, mouth interiors and accessories separate.
                        // Fade into the established neck shading beneath the jaw in canonical meters.
                        part.Item2.SmoothSkin(null, position => fit.faceSmoothing *
                            Mathf.SmoothStep(0, 1, Mathf.Clamp01((position.y - 1.53f) / .04f)));
                    }

                    foreach (var face in part.Item2.faces) {
                        if (part.Item1.EndsWith("_35NOSE") || face.material == skinMaterial && face.Center.y > 1.50f && face.Center.z > .035f) {
                            face.material = detailMaterial;
                            for (int i = 0; i < 3; i++) {
                                var vertex4 = face.vertices[i].Clone();
                                vertex4.uv = new Vector2((-vertex4.position.x + .14f) / .28f, (vertex4.position.y - 1.49f) / .32f);
                                face.vertices[i] = vertex4;
                            }
                        }
                    }
                }
            }

            FaceTextures.Create(recipe, textureDirectory, body, materials, skinMaterial, detailMaterial);
            var matrix = new Matrix4x4();
            for (int i = 0; i < 16; i++) {
                matrix[i / 4, i % 4] = source.skins[0].rootToMesh[i];
            }

            Vector3 Position(Vector3 v) {
                return matrix.MultiplyPoint3x4((v - Vector3.up * fit.canonicalOffset) / fit.canonicalScale);
            }

            Vector3 Delta(Vector3 v) {
                return matrix.MultiplyVector(v / fit.canonicalScale);
            }

            return new HeadAssetBuilder.Data {
                id = recipe.characterId,
                reference = source.skins[0].localVertices,
                bones = bones.Select(boneData2 => new HeadAssetBuilder.Bone { name = boneData2.name, parent = boneData2.parent, position = Position(boneData2.position) }).ToArray(),
                meshes = parts.Where(mapping => mapping.Item2.faces.Count > 0).Select(mapping2 => Export(mapping2.Item1, mapping2.Item2, Position, Delta, matrix.inverse.transpose)).ToArray()
            };
        }

        private HeadAssetBuilder.Part Export(string name, NativeSurface surface, Func<Vector3, Vector3> position, Func<Vector3, Vector3> delta, Matrix4x4 normals) {
            var vertices = surface.Vertices.ToArray();
            var indices = vertices.Select((vertex, i) => (vertex, i)).ToDictionary(mapping => mapping.vertex, mapping2 => mapping2.i);
            var slots = surface.faces.Select(face => face.material).Distinct().ToArray();
            return new HeadAssetBuilder.Part {
                name = name,
                vertices = vertices.Select(vertex2 => position(vertex2.position)).ToArray(),
                normals = vertices.Select(vertex3 => normals.MultiplyVector(vertex3.normal).normalized).ToArray(),
                uv = vertices.Select(vertex4 => vertex4.uv).ToArray(),
                weights = vertices.Select(vertex5 => {
                    var weights = vertex5.weights.Where(w => w.Value > 0).OrderByDescending(w => w.Value).Take(4).ToArray();
                    float total = weights.Sum(w => w.Value);
                    return new HeadAssetBuilder.Weight {
                        names = weights.Select(w => w.Key).ToArray(),
                        values = weights.Select(w => w.Value / total).ToArray()
                    };
                }).ToArray(),
                materials = slots.Select(s => materials[s]).ToArray(),
                submeshes = slots.Select(s => new HeadAssetBuilder.Sub { triangles = surface.faces.Where(face2 => face2.material == s).SelectMany(face3 => face3.vertices.Select(vertex6 => indices[vertex6])).ToArray() }).ToArray(),
                seamPairs = name.EndsWith("_BodyHead") ? vertices.Where(vertex7 => vertex7.position.y > 1.38f && vertex7.position.y < 1.61f && Mathf.Abs(vertex7.position.x) < .15f).GroupBy(vertex8 => NativeSurface.Key(vertex8.position)).SelectMany(g => g.Skip(1).SelectMany(vertex9 => new[] { indices[g.First()], indices[vertex9] })).ToArray() : Array.Empty<int>(),
                shapes = vertices.SelectMany(vertex10 => vertex10.shapes.Keys).Distinct().Select(s => new HeadAssetBuilder.Shape { name = s, delta = vertices.Select(vertex11 => vertex11.shapes.TryGetValue(s, out var value) ? delta(value) : Vector3.zero).ToArray() }).ToArray()
            };
        }
    }
}
