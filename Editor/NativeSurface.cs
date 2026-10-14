// Provides attributed triangle operations, neck boundary extraction and shared-position normal smoothing.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace digitalbreed.SyntyHeadConverter {
    internal sealed class NativeVertex {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 uv;
        public Dictionary<string, float> weights = new Dictionary<string, float>();
        public Dictionary<string, Vector3> shapes = new Dictionary<string, Vector3>();
        public NativeVertex Clone() {
            return new NativeVertex {
                position = position,
                normal = normal,
                uv = uv,
                weights = new Dictionary<string, float>(weights),
                shapes = new Dictionary<string, Vector3>(shapes)
            };
        }

        public static NativeVertex Mix(NativeVertex vertex, NativeVertex vertex2, float t) {
            var result = new NativeVertex {
                position = Vector3.Lerp(vertex.position, vertex2.position, t),
                normal = Vector3.Lerp(vertex.normal, vertex2.normal, t).normalized,
                uv = Vector2.Lerp(vertex.uv, vertex2.uv, t)
            };
            foreach (string name in vertex.weights.Keys.Union(vertex2.weights.Keys)) {
                vertex.weights.TryGetValue(name, out float av);
                vertex2.weights.TryGetValue(name, out float bv);
                result.weights[name] = Mathf.Lerp(av, bv, t);
            }

            foreach (string name in vertex.shapes.Keys.Union(vertex2.shapes.Keys)) {
                vertex.shapes.TryGetValue(name, out var av);
                vertex2.shapes.TryGetValue(name, out var bv);
                result.shapes[name] = Vector3.Lerp(av, bv, t);
            }

            return result;
        }
    }

    internal sealed class NativeTriangle {
        public NativeVertex[] vertices;
        public int material;
        public int sourceFace = -1;
        public bool skin;
        public bool graft;
        public Vector3 Center => (vertices[0].position + vertices[1].position + vertices[2].position) / 3;

        public NativeTriangle(NativeVertex vertex, NativeVertex vertex2, NativeVertex vertex3, int material, bool skin = false) {
            vertices = new[] { vertex, vertex2, vertex3 };
            this.material = material;
            this.skin = skin;
        }
    }

    internal sealed class NativeSurface {
        public readonly List<NativeTriangle> faces = new List<NativeTriangle>();
        public IEnumerable<NativeVertex> Vertices => faces.SelectMany(f => f.vertices).Distinct();

        public static Vector3Int Key(Vector3 point) {
            return new Vector3Int(Mathf.RoundToInt(point.x * 1000000), Mathf.RoundToInt(point.y * 1000000), Mathf.RoundToInt(point.z * 1000000));
        }

        public static (Vector3Int, Vector3Int) Edge(Vector3 a, Vector3 b) {
            var ka = Key(a);
            var kb = Key(b);
            bool first = ka.x < kb.x || ka.x == kb.x && (ka.y < kb.y || ka.y == kb.y && ka.z < kb.z);
            return first ? (ka, kb) : (kb, ka);
        }

        public static List<NativeVertex> Clip(NativeTriangle face, float height, bool above = false) {
            var result = new List<NativeVertex>();
            for (int i = 0; i < 3; i++) {
                var vertex2 = face.vertices[i];
                var vertex3 = face.vertices[(i + 1) % 3];
                bool insideA = above ? vertex2.position.y >= height - .000001f : vertex2.position.y <= height + .000001f;
                bool insideB = above ? vertex3.position.y >= height - .000001f : vertex3.position.y <= height + .000001f;
                if (insideA) {
                    result.Add(vertex2);
                }

                if (insideA != insideB) {
                    var vertex = NativeVertex.Mix(vertex2, vertex3, (height - vertex2.position.y) / (vertex3.position.y - vertex2.position.y));
                    vertex.position.y = height;
                    result.Add(vertex);
                }
            }

            return result;
        }

        public void Polygon(IList<NativeVertex> vertices, int material, bool skin) {
            for (int i = 1; i + 1 < vertices.Count; i++) {
                if (Vector3.Cross(vertices[i].position - vertices[0].position, vertices[i + 1].position - vertices[0].position).sqrMagnitude > 1e-16f) {
                    faces.Add(new NativeTriangle(vertices[0], vertices[i], vertices[i + 1], material, skin));
                }
            }
        }

        public Dictionary<(Vector3Int, Vector3Int), List<NativeTriangle>> Edges() {
            var edges = new Dictionary<(Vector3Int, Vector3Int), List<NativeTriangle>>();
            foreach (var face in faces) {
                for (int i = 0; i < 3; i++) {
                    var key = Edge(face.vertices[i].position, face.vertices[(i + 1) % 3].position);
                    if (!edges.TryGetValue(key, out var adjacent)) {
                        adjacent = new List<NativeTriangle>();
                        edges.Add(key, adjacent);
                    }

                    adjacent.Add(face);
                }
            }

            return edges;
        }

        // Test the projected polygon, not just whether its edges form a closed loop. A small
        // hole in front of a necklace must never be mistaken for the full neck circumference.
        internal static bool EnclosesNeckAxis(IReadOnlyList<NativeVertex> ring) {
            bool inside = false;
            for (int index = 0; index < ring.Count; index++) {
                Vector3 current = ring[index].position;
                Vector3 next = ring[(index + 1) % ring.Count].position;
                const float axisDepth = -.012f;
                if ((current.z > axisDepth) != (next.z > axisDepth) &&
                    0 < (next.x - current.x) * (axisDepth - current.z) / (next.z - current.z) + current.x) {
                    inside = !inside;
                }
            }

            return inside;
        }

        public List<NativeVertex> Ring(float height, HashSet<(Vector3Int, Vector3Int)> cutEdges = null, bool requireNeckAxis = true) {
            if (cutEdges == null) {
                var planar = Edges().Where(e => e.Value.Count == 1 && new[] { e.Key.Item1, e.Key.Item2 }.All(p => Mathf.Abs(p.y / 1000000f - height) < .0001f)).Select(e => e.Key).ToHashSet();
                if (planar.Count > 5) {
                    try {
                        return Ring(height, planar, requireNeckAxis);
                    } catch (InvalidOperationException) {
                        // Nonplanar collars can continue below the cutting plane.
                    }
                }
            }

            var points = Vertices.GroupBy(vertex => Key(vertex.position)).ToDictionary(g => g.Key, g => g.First());
            var edges = Edges().Where(e => e.Value.Count == 1 && (cutEdges == null ? new[] { points[e.Key.Item1], points[e.Key.Item2] }.All(vertex2 => Mathf.Abs(vertex2.position.x) < .2f && vertex2.position.y > 1.3f && vertex2.position.y < 1.67f) : cutEdges.Contains(e.Key))).Select(e => e.Key).ToList();
            var neighbors = new Dictionary<Vector3Int, List<Vector3Int>>();
            foreach (var edge in edges) {
                if (!neighbors.ContainsKey(edge.Item1)) {
                    neighbors[edge.Item1] = new List<Vector3Int>();
                }

                if (!neighbors.ContainsKey(edge.Item2)) {
                    neighbors[edge.Item2] = new List<Vector3Int>();
                }

                neighbors[edge.Item1].Add(edge.Item2);
                neighbors[edge.Item2].Add(edge.Item1);
            }

            var remaining = new HashSet<Vector3Int>(neighbors.Keys);
            var rings = new List<List<NativeVertex>>();
            while (remaining.Count > 0) {
                var group = new HashSet<Vector3Int>();
                var stack = new Stack<Vector3Int>();
                stack.Push(remaining.First());
                while (stack.Count > 0) {
                    var current = stack.Pop();
                    if (!remaining.Remove(current)) {
                        continue;
                    }

                    group.Add(current);
                    foreach (var next in neighbors[current]) {
                        stack.Push(next);
                    }
                }

                if (group.Count < 6 || group.Any(v => neighbors[v].Count != 2)) {
                    continue;
                }

                var first = group.First();
                var ordered = new List<Vector3Int> {
                    first
                };
                var previous = first;
                var cursor = neighbors[first][0];
                while (cursor != first && ordered.Count <= group.Count) {
                    ordered.Add(cursor);
                    var next = neighbors[cursor].First(v => v != previous);
                    previous = cursor;
                    cursor = next;
                }

                if (ordered.Count == group.Count) {
                    rings.Add(ordered.Select(v => points[v]).ToList());
                }
            }

            if (requireNeckAxis) {
                rings = rings.Where(ring => EnclosesNeckAxis(ring)).ToList();
            }
            if (rings.Count == 0) {
                throw new InvalidOperationException("No closed anatomical neck boundary. Adjust the neck cut or classify interfering hair, clothing and jewelry in Regions; no incomplete graft was produced.");
            }

            var ring = rings.OrderBy(r => Mathf.Abs(r.Average(vertex3 => vertex3.position.y) - height)).First();
            float area = 0;
            for (int i = 0; i < ring.Count; i++) {
                var a = ring[i].position;
                var b = ring[(i + 1) % ring.Count].position;
                area += a.x * b.z - b.x * a.z;
            }

            if (area < 0) {
                ring.Reverse();
            }

            int start = Enumerable.Range(0, ring.Count).OrderBy(i => Mathf.Atan2(ring[i].position.z + .012f, ring[i].position.x)).First();
            return ring.Skip(start).Concat(ring.Take(start)).ToList();
        }

        public void SmoothSkin(HashSet<Vector3Int> selected = null, Func<Vector3, float> strength = null) {
            var normals = new Dictionary<Vector3Int, Vector3>();
            foreach (var face in faces.Where(face2 => face2.skin)) {
                var v = face.vertices;
                var normal = Vector3.Cross(v[1].position - v[0].position, v[2].position - v[0].position).normalized;
                for (int i = 0; i < 3; i++) {
                    var key = Key(v[i].position);
                    if (selected != null && !selected.Contains(key)) {
                        continue;
                    }

                    float angle = Vector3.Angle(v[(i + 1) % 3].position - v[i].position, v[(i + 2) % 3].position - v[i].position);
                    normals.TryGetValue(key, out var sum);
                    normals[key] = sum + normal * angle;
                }
            }

            foreach (var face in faces.Where(face3 => face3.skin)) {
                for (int i = 0; i < 3; i++) {
                    if (normals.TryGetValue(Key(face.vertices[i].position), out var normal)) {
                        var vertex = face.vertices[i].Clone();
                        vertex.normal = Vector3.Lerp(vertex.normal, normal.normalized, strength == null ? 1 : strength(vertex.position)).normalized;
                        face.vertices[i] = vertex;
                    }
                }
            }
        }
    }
}
