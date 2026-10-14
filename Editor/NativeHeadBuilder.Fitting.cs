// Cuts and joins neck boundaries, repairs hidden openings and separates jewelry from its skin underlay.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace digitalbreed.SyntyHeadConverter {
    internal sealed partial class NativeHeadBuilder {
        private static readonly float[] NeckTransitionFractions = { 1f / 3, 2f / 3 };
        private static float Smooth(float value) {
            float t = Mathf.Clamp01(value);
            return t * t * (3 - 2 * t);
        }

        private static float[] Parameters(List<NativeVertex> ring) {
            var distances = new float[ring.Count + 1];
            for (int i = 0; i < ring.Count; i++) {
                distances[i + 1] = distances[i] + Vector3.Distance(ring[i].position, ring[(i + 1) % ring.Count].position);
            }

            float length = distances.Last();
            return distances.Select(d => d / length).ToArray();
        }

        private static float[] AngularParameters(List<NativeVertex> ring, float start) {
            var values = new List<float>();
            foreach (var vertex in ring) {
                float angle = Mathf.Atan2(vertex.position.z + .012f, vertex.position.x);
                float t = (Mathf.Repeat(angle - start + Mathf.PI, 2 * Mathf.PI) - Mathf.PI) / (2 * Mathf.PI);
                while (values.Count > 0 && t < values.Last() - .000001f) {
                    t++;
                }

                values.Add(t);
            }

            if (values.Last() - values[0] >= 1.00001f) {
                throw new Exception("The selected neck boundary is not star-shaped. Adjust the cut or its interfering regions.");
            }

            values.Add(values[0] + 1);
            return values.ToArray();
        }

        private void RepairHiddenBoundaries() {
            if (options.Flag("sealHiddenNeckBase")) {
                var edges = body.Edges().Where(e => e.Value.Count == 1 && e.Value[0].skin && new[] { e.Key.Item1, e.Key.Item2 }.All(k => k.y / 1000000f > 1.35f && k.y / 1000000f < fit.cut - .0001f && Mathf.Abs(k.x / 1000000f) < .12f)).Select(e => e.Key).ToHashSet();
                if (edges.Count != 6) {
                    throw new Exception("The hidden neck base changed. Review its boundary before capping it.");
                }

                var ring = body.Ring(1.4f, edges, false);
                body.Polygon(ring, 0, true);
            }

            if (!options.Flag("hoodNeckBacking")) {
                return;
            }

            var points = body.Vertices.GroupBy(vertex2 => NativeSurface.Key(vertex2.position)).ToDictionary(g => g.Key, g => g.First());
            var cut = body.Edges().Where(e => e.Value.Count == 1 && new[] { e.Key.Item1, e.Key.Item2 }.All(k => Mathf.Abs(points[k].position.y - fit.cut) < .00001f)).Select(e => e.Key).ToArray();
            var keys = cut.SelectMany(mapping => new[] { mapping.Item1, mapping.Item2 }).Distinct().ToArray();
            var ends = keys.Where(k => cut.Count(mapping2 => mapping2.Item1 == k || mapping2.Item2 == k) == 1).ToArray();
            if (ends.Length != 2) {
                throw new Exception("Hood backing requires one open anatomical neck chain.");
            }

            var start = points[ends.OrderByDescending(k => points[k].position.x).First()];
            var end = points[ends.First(k => points[k] != start)];
            sourceRing = new List<NativeVertex> {
                start
            };
            var previous = NativeSurface.Key(start.position);
            var cursor = previous;
            while (points[cursor] != end) {
                var next = cut.Where(mapping3 => mapping3.Item1 == cursor || mapping3.Item2 == cursor).Select(mapping4 => mapping4.Item1 == cursor ? mapping4.Item2 : mapping4.Item1).First(k => k != previous);
                previous = cursor;
                cursor = next;
                sourceRing.Add(points[cursor]);
                if (sourceRing.Count > keys.Length) {
                    throw new Exception("The hood neck boundary did not close.");
                }
            }

            NativeVertex Make(float x, float y, float z) {
                var vertex = start.Clone();
                vertex.position = new Vector3(x, y, z);
                vertex.uv = new Vector2(.022f, .187f);
                return vertex;
            }

            var back = new[] { end, Make(-.060f, fit.cut, -.035f), Make(0, fit.cut, -.064f), Make(.060f, fit.cut, -.035f), start };
            sourceRing.AddRange(back.Skip(1).Take(3));
            var bottom = Make(0, fit.cut - .075f, -.025f);
            for (int i = 0; i + 1 < back.Length; i++) {
                body.faces.Add(new NativeTriangle(back[i + 1], back[i], bottom, 0, true));
            }

            body.faces.Add(new NativeTriangle(start, end, bottom, 0, true));
        }

        private static NativeVertex Sample(List<NativeVertex> ring, float t) {
            var parameters = Parameters(ring);
            for (int i = 0; i < ring.Count; i++) {
                if (t >= parameters[i] && t <= parameters[i + 1]) {
                    return NativeVertex.Mix(ring[i], ring[(i + 1) % ring.Count], (t - parameters[i]) / (parameters[i + 1] - parameters[i]));
                }
            }

            return ring[0].Clone();
        }

        private static NativeVertex SamplePolar(List<NativeVertex> ring, float angle) {
            var points = ring.Select(vertex2 => (angle: Mathf.Atan2(vertex2.position.z + .012f, vertex2.position.x), vertex: vertex2)).OrderBy(mapping => mapping.angle).ToList();
            points.Insert(0, (points.Last().angle - Mathf.PI * 2, points.Last().vertex));
            points.Add((points[1].angle + Mathf.PI * 2, points[1].vertex));
            angle = Mathf.Repeat(angle + Mathf.PI, Mathf.PI * 2) - Mathf.PI;
            for (int i = 0; i + 1 < points.Count; i++) {
                if (angle >= points[i].angle && angle <= points[i + 1].angle) {
                    return NativeVertex.Mix(points[i].vertex, points[i + 1].vertex, (angle - points[i].angle) / Mathf.Max(.0000001f, points[i + 1].angle - points[i].angle));
                }
            }

            throw new Exception("Cannot sample a non-star-shaped neck ring.");
        }

        // A collar boundary may contain inward notches. Its generated inset must make one
        // forward turn before polar sampling; changing source order would reconnect unrelated edges.
        // Isotonic pooling moves only generated points, by the least squared angular correction.
        // The small angular gap keeps distinct source edges from collapsing into degenerate triangles.
        internal static void OrderInsetRing(List<NativeVertex> ring) {
            const float minimumTurnFraction = .0001f;
            float start = Mathf.Atan2(ring[0].position.z + .012f, ring[0].position.x);
            float previousAngle = start;
            float accumulated = 0;
            var values = new float[ring.Count];
            var blockStarts = new List<int>();
            var blockCounts = new List<int>();
            var blockMeans = new List<float>();
            for (int index = 0; index < ring.Count; index++) {
                Vector3 position = ring[index].position;
                float angle = Mathf.Atan2(position.z + .012f, position.x);
                accumulated += Mathf.DeltaAngle(previousAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg) / 360;
                previousAngle = angle;
                float value = Mathf.Clamp(accumulated - index * minimumTurnFraction, 0, 1 - ring.Count * minimumTurnFraction);
                blockStarts.Add(index);
                blockCounts.Add(1);
                blockMeans.Add(value);
                while (blockMeans.Count > 1 && blockMeans[blockMeans.Count - 2] > blockMeans.Last()) {
                    int last = blockMeans.Count - 1;
                    int count = blockCounts[last - 1] + blockCounts[last];
                    blockMeans[last - 1] = (blockMeans[last - 1] * blockCounts[last - 1] + blockMeans[last] * blockCounts[last]) / count;
                    blockCounts[last - 1] = count;
                    blockStarts.RemoveAt(last);
                    blockCounts.RemoveAt(last);
                    blockMeans.RemoveAt(last);
                }
            }

            for (int block = 0; block < blockMeans.Count; block++) {
                for (int offset = 0; offset < blockCounts[block]; offset++) {
                    int index = blockStarts[block] + offset;
                    values[index] = blockMeans[block] + index * minimumTurnFraction;
                }
            }

            for (int index = 0; index < ring.Count; index++) {
                Vector3 position = ring[index].position;
                float radius = new Vector2(position.x, position.z + .012f).magnitude;
                float angle = start + values[index] * Mathf.PI * 2;
                float originalAngle = Mathf.Atan2(position.z + .012f, position.x);
                if (Mathf.Abs(Mathf.DeltaAngle(originalAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg)) < .001f) {
                    continue;
                }

                ring[index].position = new Vector3(radius * Mathf.Cos(angle), position.y, -.012f + radius * Mathf.Sin(angle));
            }
        }

        private void Bridge(List<NativeVertex> lower, List<NativeVertex> upper) {
            var rings = new List<List<NativeVertex>> {
                lower
            };
            var allSeam = lower.Concat(upper).Select(vertex2 => NativeSurface.Key(vertex2.position)).ToHashSet();
            var centerLower = lower.Aggregate(Vector3.zero, (s, vertex3) => s + vertex3.position) / lower.Count;
            var centerUpper = upper.Aggregate(Vector3.zero, (s, vertex4) => s + vertex4.position) / upper.Count;
            var baseRing = lower;
            if (fit.collarInset) {
                baseRing = lower.Select(vertex5 => {
                    var clone = vertex5.Clone();
                    var p = vertex5.position;
                    if (p.z < .025f || Mathf.Abs(p.x) > .062f) {
                        float angle = Mathf.Atan2(p.z + .012f, p.x);
                        clone.position = new Vector3(.056f * Mathf.Cos(angle), 1.445f, -.012f + .048f * Mathf.Sin(angle));
                        if (options.Has("collarTransitionRise")) {
                            clone.position.y = Mathf.Max(p.y + options.Number("collarTransitionRise"), 1.465f);
                        }
                    }

                    return clone;
                }).ToList();
                OrderInsetRing(baseRing);
                rings.Add(baseRing);
            }

            bool polar = fit.collarInset || options.Flag("polarNeckBridge");
            float start = Mathf.Atan2(baseRing[0].position.z + .012f, baseRing[0].position.x);
            int closest = Enumerable.Range(0, upper.Count).OrderBy(i => Mathf.Abs(Mathf.DeltaAngle(start * Mathf.Rad2Deg, Mathf.Atan2(upper[i].position.z + .012f, upper[i].position.x) * Mathf.Rad2Deg))).First();
            upper = upper.Skip(closest).Concat(upper.Take(closest)).ToList();
            // Original source weights are immutable. The new bridge interpolates them
            // into the donor weights, so both duplicated render seams deform together.
            foreach (var vertex in head.Vertices) {
                if (allSeam.Contains(NativeSurface.Key(vertex.position)) || vertex.weights.Any(w => (w.Key == "neck_01" || w.Key == "spine_03") && w.Value > .001f)) {
                    float weight = Mathf.Clamp01((vertex.position.y - 1.45f) / .15f);
                    vertex.weights = new Dictionary<string, float> { { "neck_01", 1 - weight }, { "head", weight }
                    };
                }
            }

            foreach (float height in NeckTransitionFractions) {
                var ring = new List<NativeVertex>();
                for (int i = 0; i < 16; i++) {
                    float t = i / 16f;
                    float angle = start + Mathf.PI * 2 * t;
                    var vertex6 = polar ? SamplePolar(baseRing, angle) : Sample(baseRing, t);
                    var vertex7 = polar ? SamplePolar(upper, angle) : Sample(upper, t);
                    var vertex = NativeVertex.Mix(vertex6, vertex7, height);
                    var center = Vector3.Lerp(centerLower, centerUpper, height);
                    if (!polar) {
                        angle = Mathf.Atan2(vertex.position.z - center.z, vertex.position.x - center.x);
                    }

                    if (!options.Flag("polarNeckBridge")) {
                        var target = new Vector3(.070f * Mathf.Cos(angle), vertex.position.y, -.014f + .057f * Mathf.Sin(angle));
                        vertex.position = Vector3.Lerp(vertex.position, target, .55f * Mathf.Sin(Mathf.PI * height));
                    }

                    if (fit.collarInset) {
                        float grow = Mathf.Clamp01((vertex.position.y - 1.49f) / .08f);
                        float rx = .057f + .014f * grow;
                        float rz = .049f + .009f * grow;
                        float radius = Mathf.Sqrt(Mathf.Pow(vertex.position.x / rx, 2) + Mathf.Pow((vertex.position.z + .012f) / rz, 2));
                        if (radius > 1) {
                            var inset = new Vector3(vertex.position.x / radius, vertex.position.y, -.012f + (vertex.position.z + .012f) / radius);
                            vertex.position = Vector3.Lerp(vertex.position, inset, Mathf.Clamp01((1.535f - vertex.position.y) / .045f));
                        }
                    }

                    vertex.uv = Vector2.zero;
                    vertex.normal = new Vector3(vertex.position.x, 0, vertex.position.z + .014f).normalized;
                    ring.Add(vertex);
                }

                rings.Add(ring);
            }

            rings.Add(upper);
            var bridge = new List<NativeTriangle>();
            for (int r = 0; r + 1 < rings.Count; r++) {
                var low = rings[r];
                var high = rings[r + 1];
                var lp = Parameters(low);
                var hp = Parameters(high);
                // Polar-sampled rings must also be stitched by angle. Matching their
                // perimeter fractions rotates unevenly spaced source vertices and twists the neck.
                if (options.Flag("angularNeckTriangles") || fit.collarInset && r > 0) {
                    lp = AngularParameters(low, start);
                    hp = AngularParameters(high, start);
                }

                if (fit.collarInset && r == 0 || low.Count == 16 && high.Count == 16) {
                    lp = Enumerable.Range(0, low.Count + 1).Select(i => i / (float)low.Count).ToArray();
                    hp = Enumerable.Range(0, high.Count + 1).Select(i => i / (float)high.Count).ToArray();
                }

                int i = 0;
                int j = 0;
                while (i < low.Count || j < high.Count) {
                    NativeVertex vertex8 = low[i % low.Count];
                    NativeVertex vertex9;
                    NativeVertex vertex10 = high[j % high.Count];
                    if (j == high.Count || i < low.Count && lp[i + 1] < hp[j + 1]) {
                        vertex9 = low[(++i) % low.Count];
                    } else {
                        vertex9 = high[(++j) % high.Count];
                    }

                    var normal = Vector3.Cross(vertex9.position - vertex8.position, vertex10.position - vertex8.position);
                    if (normal.sqrMagnitude < 1e-16f) {
                        continue;
                    }

                    // Both boundary loops use the same order. Keep winding consistent across the
                    // strip; collar lining can legitimately face upward or inward, unlike a neck shaft.
                    var swap = vertex9;
                    vertex9 = vertex10;
                    vertex10 = swap;

                    bool lining = fit.collarInset && r == 0;
                    if (lining) {
                        Vector2 color = vertex8.uv;
                        vertex8 = vertex8.Clone();
                        vertex9 = vertex9.Clone();
                        vertex10 = vertex10.Clone();
                        vertex8.uv = color;
                        vertex9.uv = color;
                        vertex10.uv = color;
                    }

                    var face = new NativeTriangle(vertex8, vertex9, vertex10, lining ? 0 : skinMaterial, !lining || SourceSkin(vertex8.uv)) {
                        graft = true
                    };
                    bridge.Add(face);
                }
            }

            body.faces.AddRange(bridge);
            var complete = new NativeSurface();
            complete.faces.AddRange(body.faces);
            complete.faces.AddRange(head.faces);
            var incidence = complete.Edges();
            foreach (var edge in bridge.SelectMany(face2 => Enumerable.Range(0, 3).Select(i => NativeSurface.Edge(face2.vertices[i].position, face2.vertices[(i + 1) % 3].position))).Distinct()) {
                if (!incidence.TryGetValue(edge, out var adjacent) || adjacent.Count != 2) {
                    throw new Exception("The neck bridge has an open or nonmanifold edge. Review the cut and accessory regions.");
                }
            }
        }

        private void SeparateJewelry(NativeSurface original, Dictionary<int, string> roles) {
            var selected = original.faces.Where(face2 => roles.TryGetValue(face2.sourceFace, out string role) && role == "Necklace" || Mathf.Abs(face2.Center.x) < options.Number("necklaceMaxX", .16f) && face2.Center.y > 1.35f && options.Rows("necklaceUvRegions").Any(r => face2.vertices[0].uv.x > r[0] && face2.vertices[0].uv.x < r[1] && face2.vertices[0].uv.y > r[2] && face2.vertices[0].uv.y < r[3])).ToHashSet();
            if (selected.Count == 0) {
                return;
            }

            var jewelry = new NativeSurface();
            var originalPoints = original.Vertices.GroupBy(vertex2 => NativeSurface.Key(vertex2.position)).ToDictionary(g => g.Key, g => g.ToList());
            var selectedKeys = selected.SelectMany(face3 => face3.vertices.Select(vertex3 => NativeSurface.Key(vertex3.position))).ToHashSet();
            var boundary = original.faces.Where(face4 => !selected.Contains(face4)).SelectMany(face5 => face5.vertices.Select(vertex4 => NativeSurface.Key(vertex4.position))).Where(selectedKeys.Contains).ToHashSet();
            var neighbors = selectedKeys.ToDictionary(k => k, k => new HashSet<Vector3Int>());
            foreach (var face in selected) {
                var copies = face.vertices.Select(vertex5 => vertex5.Clone()).ToArray();
                jewelry.faces.Add(new NativeTriangle(copies[0], copies[1], copies[2], face.material));
                var keys = face.vertices.Select(vertex6 => NativeSurface.Key(vertex6.position)).ToArray();
                foreach (var key in keys) {
                    neighbors[key].UnionWith(keys.Where(k => k != key));
                }
            }

            var coordinates = selectedKeys.ToDictionary(k => k, k => originalPoints[k][0].position);
            for (int pass = 0; pass < 60; pass++) {
                var updated = new Dictionary<Vector3Int, Vector3>(coordinates);
                foreach (var key in selectedKeys.Except(boundary)) {
                    if (neighbors[key].Count > 0) {
                        updated[key] = Vector3.Lerp(coordinates[key], neighbors[key].Aggregate(Vector3.zero, (sum, n) => sum + coordinates[n]) / neighbors[key].Count, .5f);
                    }
                }

                coordinates = updated;
            }

            var underlayNormals = selectedKeys.ToDictionary(k => k, k => Vector3.zero);
            foreach (var face in selected) {
                var keys = face.vertices.Select(vertex7 => NativeSurface.Key(vertex7.position)).ToArray();
                var points = keys.Select(k => coordinates[k]).ToArray();
                var normal = Vector3.Cross(points[1] - points[0], points[2] - points[0]).normalized;
                for (int i = 0; i < 3; i++) {
                    float angle = Vector3.Angle(points[(i + 1) % 3] - points[i], points[(i + 2) % 3] - points[i]);
                    underlayNormals[keys[i]] += normal * angle;
                }
            }

            foreach (var group in Components(original).Where(p => selected.Contains(p.Key)).GroupBy(p => p.Value)) {
                bool attached = group.Any(p => p.Key.vertices.Any(vertex8 => boundary.Contains(NativeSurface.Key(vertex8.position))));
                foreach (var item in group) {
                    var face = item.Key;
                    if (!attached) {
                        original.faces.Remove(face);
                        continue;
                    }

                    for (int i = 0; i < 3; i++) {
                        var old = face.vertices[i];
                        var key = NativeSurface.Key(old.position);
                        var vertex = old.Clone();
                        vertex.position = coordinates[key];
                        if (!boundary.Contains(key)) {
                            vertex.position -= underlayNormals[key].normalized * options.Number("necklaceUnderlayInset", .002f);
                        }

                        vertex.uv = new Vector2(.018f, .19f);
                        face.vertices[i] = vertex;
                    }

                    face.skin = true;
                    roles.Remove(face.sourceFace);
                }
            }

            var pendant = options.Numbers("necklacePendantFit");
            if (pendant.Length == 3) {
                foreach (var vertex in jewelry.Vertices) {
                    if (vertex.position.z > 0 && Mathf.Abs(vertex.position.x) < .065f) {
                        vertex.position.z += pendant[2] * Smooth((pendant[0] - vertex.position.y) / pendant[1]);
                    }
                }
            }

            parts.Add((recipe.characterId + "_OriginalNecklace", jewelry));
        }

        private void AddExpressiveAccessories() {
            var faceVertices = head.faces.Where(face => face.skin).SelectMany(face2 => face2.vertices).Distinct().ToArray();
            void Transfer(NativeVertex vertex, Vector3 anchor) {
                var closest = faceVertices.OrderBy(vertex2 => (vertex2.position - anchor).sqrMagnitude).Take(4).ToArray();
                var weights = closest.Select(vertex3 => 1 / Mathf.Pow(Mathf.Max(.003f, Vector3.Distance(vertex3.position, anchor)), 2)).ToArray();
                float total = weights.Sum();
                foreach (string shape in closest.SelectMany(vertex4 => vertex4.shapes.Keys).Distinct()) {
                    Vector3 delta = Vector3.zero;
                    for (int i = 0; i < closest.Length; i++) {
                        if (closest[i].shapes.TryGetValue(shape, out var value)) {
                            delta += value * (weights[i] / total);
                        }
                    }

                    vertex.shapes[shape] = delta;
                }
            }

            var components = Components(accessories);
            var selected = new Dictionary<NativeVertex, Vector3>();
            foreach (var group in components.GroupBy(p => p.Value)) {
                var vertices = group.SelectMany(p => p.Key.vertices).Distinct().ToArray();
                if (vertices.Min(vertex5 => vertex5.position.y) > options.Number("expressiveAttachmentMinZ", 1.45f) && vertices.Max(vertex6 => vertex6.position.y) < 1.68f && vertices.Min(vertex7 => vertex7.position.z) > .01f) {
                    var center = vertices.Aggregate(Vector3.zero, (sum, vertex8) => sum + vertex8.position) / vertices.Length;
                    foreach (var vertex9 in vertices) {
                        selected[vertex9] = options.Numbers("rigidExpressiveComponents").Contains(group.Key.Count) ? center : vertex9.position;
                    }
                }
            }

            var region = options.Numbers("expressiveAccessoryRegion");
            if (region.Length == 6) {
                foreach (var vertex10 in accessories.Vertices) {
                    var p = new Vector3(-vertex10.position.x, -vertex10.position.z, vertex10.position.y);
                    if (p.x > region[0] && p.x < region[1] && p.y > region[2] && p.y < region[3] && p.z > region[4] && p.z < region[5]) {
                        var anchor = vertex10.position;
                        anchor.y = Mathf.Max(anchor.y, options.Number("expressiveAccessoryFloor", anchor.y));
                        selected[vertex10] = anchor;
                    }
                }
            }

            foreach (var pair in selected) {
                Transfer(pair.Key, pair.Value);
            }

            foreach (var part in source.accessories.Where(meshPart => meshPart.name.IndexOf("beard", StringComparison.OrdinalIgnoreCase) >= 0 || options.Strings("expressiveRigidAttachments").Contains(meshPart.name))) {
                if (!string.IsNullOrEmpty(fit.sidekickBeard)) {
                    continue;
                }

                var surface = SourceSurface(part, true);
                foreach (var vertex in surface.Vertices) {
                    Transfer(vertex, vertex.position);
                }

                parts.Add((recipe.characterId + "_" + part.name + "_Expressive", surface));
            }
        }
    }
}
