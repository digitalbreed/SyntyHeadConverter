// Fits scalp and accessories, softens neck geometry and adds reusable lip-fullness blendshapes.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace digitalbreed.SyntyHeadConverter {
    internal sealed partial class NativeHeadBuilder {
        private static readonly bool[] UpperLipSections = { true, false };
        // Paired height/depth samples for the softened throat profile, in canonical fitting meters.
        private static readonly float[] ThroatProfileHeights = { 1.415f, 1.455f, 1.480f, 1.510f, 1.532f, 1.550f };
        private static readonly float[] ThroatProfileDepths = { .050f, .034f, .028f, .026f, .025f, .020f };
        private void AdjustFit() {
            FinishNeck();
            FitScalp();
            OffsetComponents();
            var skin = body.faces.Where(face2 => face2.skin).SelectMany(face3 => face3.vertices).ToHashSet();
            var garment = body.faces.Where(face4 => !face4.skin).SelectMany(face5 => face5.vertices).Select(vertex2 => NativeSurface.Key(vertex2.position)).ToHashSet();
            var moved = new HashSet<Vector3Int>();
            var geometry = body.Vertices.GroupBy(vertex3 => NativeSurface.Key(vertex3.position)).Select(g => g.ToArray()).ToArray();
            foreach (var group in geometry) {
                var vertex = group.FirstOrDefault(skin.Contains);
                if (vertex == null) {
                    continue;
                }

                Vector3 p = vertex.position;
                var recess = options.Numbers("necklaceClearanceRecess");
                if (recess.Length == 5 && !garment.Contains(NativeSurface.Key(p))) {
                    float start = options.Number("necklaceRecessStart", 1.36f);
                    float rise = options.Number("necklaceRecessRise", .04f);
                    float x = -p.x;
                    if (p.z >= 0 && p.y > start && Mathf.Abs(x - recess[0]) < recess[2] * 2 && Mathf.Abs(p.y - recess[1]) < recess[3] * 2) {
                        vertex.position.z -= recess[4] * Smooth((p.y - start) / rise) * Mathf.Exp(-Mathf.Pow((x - recess[0]) / recess[2], 2) - Mathf.Pow((p.y - recess[1]) / recess[3], 2));
                    }
                }

                if (p.y > 1.38f && p.y < 1.61f && Mathf.Abs(p.x) < .18f && p.z < .11f && p.z > -.13f && !(options.Flag("preserveGarmentBoundary") && garment.Contains(NativeSurface.Key(p)))) {
                    float weight = Smooth(Mathf.Min(1, Mathf.Clamp01((p.y - 1.38f) / .055f)) * Mathf.Min(1, Mathf.Clamp01((1.61f - p.y) / .095f)));
                    vertex.position += new Vector3(p.x * (fit.neckScale[0] - 1) * weight, 0, (p.z + .012f) * (fit.neckScale[1] - 1) * weight);
                }

                if (fit.throatSoftening > 0 && p.y > 1.415f && p.y < 1.550f && Mathf.Abs(p.x) < options.Number("throatLateralLimit", .065f) && p.z > .012f && p.z < (p.y < 1.510f ? fit.throatLowerFrontLimit : .065f) && !garment.Contains(NativeSurface.Key(p))) {
                    float limit = Interpolate(ThroatProfileHeights, ThroatProfileDepths, p.y);
                    limit = .012f + (limit - .012f) * Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Pow(p.x / .075f, 2)));
                    float lateral = options.Number("throatLateralLimit", .065f);
                    float full = options.Number("throatLateralFullWidth", .035f);
                    float weight = fit.throatSoftening * Smooth((lateral - Mathf.Abs(p.x)) / (lateral - full)) * Smooth((p.y - 1.415f) / .025f) * Smooth((1.550f - p.y) / .015f);
                    vertex.position.z -= Mathf.Max(0, p.z - limit) * weight;
                }

                if (vertex.position.y > 1.38f && vertex.position.y < 1.61f) {
                    moved.Add(NativeSurface.Key(vertex.position));
                }

                foreach (var duplicate in group) {
                    duplicate.position = vertex.position;
                    if (skin.Contains(duplicate)) {
                        AddLipFullness(duplicate);
                    }
                }
            }

            foreach (var face in body.faces) {
                if ((fit.collarInset || options.Flag("neckSkinMaterial")) && face.skin && face.Center.y > 1.30f && Mathf.Abs(face.Center.x) < .25f && face.material < skinMaterial) {
                    face.material = skinMaterial;
                }
            }

            int iterations = (int)options.Number("neckRelaxIterations");
            if (iterations > 0) {
                RelaxNeck(iterations);
            }

            body.SmoothSkin(moved, p => Mathf.Clamp01(Mathf.Min((p.y - 1.38f) / .02f, (1.61f - p.y) / .025f)));
            if (options.Flag("neckSurfaceNormals")) {
                foreach (var vertex in body.faces.Where(face6 => face6.skin).SelectMany(face7 => face7.vertices).Distinct()) {
                    var p = vertex.position;
                    if (p.y > 1.38f && p.y < 1.58f && Mathf.Abs(p.x) < .14f && p.z < .095f) {
                        float weight = Smooth(Mathf.Min(1, (p.y - 1.38f) / .035f, (1.58f - p.y) / .04f)) * Smooth((.095f - p.z) / .04f);
                        var outward = new Vector3(p.x * .8f, .012f * Mathf.Clamp01((1.51f - p.y) / .1f), p.z + .012f).normalized;
                        vertex.normal = Vector3.Lerp(vertex.normal, outward, weight).normalized;
                    }
                }
            }
        }

        private void FinishNeck() {
            var groups = body.Vertices.GroupBy(vertex2 => NativeSurface.Key(vertex2.position)).ToDictionary(g => g.Key, g => g.ToArray());
            var adjacent = groups.Keys.ToDictionary(k => k, k => new HashSet<Vector3Int>());
            var sourceBoundary = new HashSet<Vector3Int>();
            foreach (var face in body.faces) {
                var keys = face.vertices.Select(vertex3 => NativeSurface.Key(vertex3.position)).ToArray();
                foreach (var key in keys) {
                    adjacent[key].UnionWith(keys.Where(k => k != key));
                    if (face.material < skinMaterial) {
                        sourceBoundary.Add(key);
                    }
                }
            }

            var original = groups.ToDictionary(g => g.Key, g => g.Value[0].position);
            var positions = new Dictionary<Vector3Int, Vector3>(original);
            for (int pass = 0; pass < 10; pass++) {
                var next = new Dictionary<Vector3Int, Vector3>(positions);
                foreach (var pair in original) {
                    var p = pair.Value;
                    if (sourceBoundary.Contains(pair.Key) || adjacent[pair.Key].Count == 0 || p.y <= 1.50f || p.y >= 1.64f || p.z >= .035f) {
                        continue;
                    }

                    float weight = Mathf.Clamp01(Mathf.Min((p.y - 1.50f) / .025f, (1.64f - p.y) / .035f)) * Mathf.Clamp01((.035f - p.z) / .035f);
                    var mean = adjacent[pair.Key].Aggregate(Vector3.zero, (sum, k) => sum + positions[k]) / adjacent[pair.Key].Count;
                    var target = Vector3.Lerp(positions[pair.Key], mean, .35f * weight);
                    next[pair.Key] = p + Vector3.ClampMagnitude(target - p, .008f);
                }

                positions = next;
            }

            foreach (var group in groups) {
                foreach (var vertex in group.Value) {
                    vertex.position = positions[group.Key];
                }
            }

            body.SmoothSkin(null, p => Smooth((p.y - 1.39f) / .025f) * Smooth((1.67f - p.y) / .08f) * Smooth((.13f - p.z) / .08f));
        }

        private void OffsetComponents() {
            var groups = Components(accessories);
            foreach (var adjustment in options.Objects("componentOffsets")) {
                var offset = adjustment.Numbers("offset");
                var selected = groups.Where(p => p.Value.Count == adjustment.Number("count") && p.Value.Max(vertex2 => -vertex2.position.z) < adjustment.Number("maxY", 100)).SelectMany(p => p.Key.vertices).Distinct().ToArray();
                if (offset.Length != 3 || selected.Length == 0) {
                    throw new Exception("The authored accessory offset no longer matches the original mesh. Review its component selection.");
                }

                foreach (var vertex in selected) {
                    vertex.position += new Vector3(-offset[0], offset[2], -offset[1]);
                }
            }
        }

        internal static float Interpolate(float[] knots, float[] values, float x) {
            for (int i = 0; i + 1 < knots.Length; i++) {
                if (x <= knots[i + 1]) {
                    return Mathf.Lerp(values[i], values[i + 1], Mathf.Clamp01((x - knots[i]) / (knots[i + 1] - knots[i])));
                }
            }

            return values.Last();
        }

        private static void AddLipFullness(NativeVertex vertex) {
            Vector3 p = vertex.position;
            float lateral = Mathf.Pow(Mathf.Max(0, 1 - Mathf.Pow(p.x / .050f, 2)), .65f);
            float front = Mathf.Clamp01((p.z - .104f) / .019f);
            foreach (bool upper in UpperLipSections) {
                float t = upper ? (p.y - 1.578f) / .022f : (1.573f - p.y) / .024f;
                float peak = t > 0 && t < 1 ? Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(t, upper ? .65f : .85f)), 2) : 0;
                var delta = upper ? new Vector3(0, .006f, .003f) : new Vector3(0, -.007f, .0035f);
                vertex.shapes[upper ? "lipFullnessUpper" : "lipFullnessLower"] = delta * (lateral * front * peak);
            }
        }

        private void RelaxNeck(int iterations) {
            var groups = body.Vertices.GroupBy(vertex2 => NativeSurface.Key(vertex2.position)).ToDictionary(g => g.Key, g => g.ToArray());
            var neighbors = groups.Keys.ToDictionary(k => k, k => new HashSet<Vector3Int>());
            var skin = groups.Keys.ToDictionary(k => k, k => true);
            foreach (var face in body.faces) {
                foreach (var vertex in face.vertices) {
                    var key = NativeSurface.Key(vertex.position);
                    skin[key] &= face.skin;
                    neighbors[key].UnionWith(face.vertices.Select(vertex3 => NativeSurface.Key(vertex3.position)).Where(k => k != key));
                }
            }

            var coordinates = groups.ToDictionary(g => g.Key, g => g.Value[0].position);
            for (int pass = 0; pass < iterations; pass++) {
                var next = new Dictionary<Vector3Int, Vector3>(coordinates);
                foreach (var pair in coordinates) {
                    var p = pair.Value;
                    if (!skin[pair.Key] || neighbors[pair.Key].Count == 0 || Mathf.Abs(p.x) > .13f || p.z < -.14f || p.z > .10f || p.y <= 1.405f || p.y >= 1.565f) {
                        continue;
                    }

                    float weight = .35f * Mathf.Min(1, (p.y - 1.405f) / .025f, (1.565f - p.y) / .025f);
                    var mean = neighbors[pair.Key].Aggregate(Vector3.zero, (sum, k) => sum + coordinates[k]) / neighbors[pair.Key].Count;
                    next[pair.Key] = Vector3.Lerp(p, mean, weight);
                }

                coordinates = next;
            }

            foreach (var group in groups) {
                foreach (var vertex in group.Value) {
                    vertex.position = coordinates[group.Key];
                }
            }

            body.SmoothSkin(coordinates.Where(p => p.Value.y > 1.405f && p.Value.y < 1.565f).Select(p => NativeSurface.Key(p.Value)).ToHashSet());
        }

        private void FitScalp() {
            if (string.IsNullOrEmpty(fit.scalpHairEnvelope)) {
                return;
            }

            NativeSurface shell = accessories;
            if (fit.scalpHairEnvelope != "embedded") {
                var part = source.accessories.FirstOrDefault(meshPart => meshPart.name == fit.scalpHairEnvelope);
                if (part == null) {
                    throw new Exception("The selected scalp hair shell is missing: " + fit.scalpHairEnvelope);
                }

                shell = SourceSurface(part, true);
            }

            var origin = new Vector3(0, 1.70f, -.005f);
            foreach (var vertex in body.Vertices) {
                Vector3 p = vertex.position;
                if (!(p.y > 1.64f && (p.z < -.015f || p.y > options.Number("scalpHairFrontHeight", 1.77f)) || p.y > 1.60f && p.z < -.07f && Mathf.Abs(p.x) < .07f)) {
                    continue;
                }

                var direction = p - origin;
                float length = direction.magnitude;
                if (length < .000001f) {
                    continue;
                }

                float outermost = -1;
                foreach (var face in shell.faces) {
                    if (RayTriangle(origin, direction / length, face, out float distance) && distance < .4f) {
                        outermost = Mathf.Max(outermost, distance);
                    }
                }

                float sourceSurface = -1;
                foreach (var face in sourceScalp.faces) {
                    if (RayTriangle(origin, direction / length, face, out float distance) && distance < .4f) {
                        sourceSurface = Mathf.Max(sourceSurface, distance);
                    }
                }

                // A hair cap inside the original head is hidden construction geometry, not
                // coverage of this scalp point. Allow 2 mm for nearly coincident source surfaces.
                // Without a source intersection there is no evidence that moving exposed skin is safe.
                if (sourceSurface <= 0 || outermost < sourceSurface - .002f) {
                    continue;
                }

                if (outermost > 0 && outermost < length + fit.scalpHairClearance) {
                    vertex.position = origin + direction.normalized * Mathf.Max(.01f, outermost - fit.scalpHairClearance);
                }
            }
        }

        private static bool RayTriangle(Vector3 origin, Vector3 ray, NativeTriangle face, out float distance) {
            distance = 0;
            var a = face.vertices[0].position;
            var ab = face.vertices[1].position - a;
            var ac = face.vertices[2].position - a;
            var h = Vector3.Cross(ray, ac);
            float determinant = Vector3.Dot(ab, h);
            if (Mathf.Abs(determinant) < 1e-9f) {
                return false;
            }

            var s = origin - a;
            float u = Vector3.Dot(s, h) / determinant;
            var q = Vector3.Cross(s, ab);
            float v = Vector3.Dot(ray, q) / determinant;
            distance = Vector3.Dot(ac, q) / determinant;
            return u >= 0 && v >= 0 && u + v <= 1 && distance > 0;
        }
    }
}
