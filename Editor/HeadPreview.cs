// Renders an isolated animated character preview, including region highlights and wireframe overlays.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace digitalbreed.SyntyHeadConverter {
    public sealed class HeadPreview : IDisposable {
        readonly PreviewRenderUtility view = new PreviewRenderUtility();
        GameObject instance, source;
        Transform head;
        Quaternion headRest;
        readonly List<Mesh> temporary = new List<Mesh>();
        Material highlight;
        Mesh regionMesh;
        public float yaw, pitch = 6, zoom = 1;
        // Additional camera-target height as a fraction of character height, used for neck inspection renders.
        internal float targetHeightOffset;
        public void Set(GameObject prefab) {
            if (source == prefab && instance) {
                return;
            }

            source = prefab;
            if (instance) {
                Object.DestroyImmediate(instance);
            }

            head = null;
            if (!prefab) {
                return;
            }

            instance = view.InstantiatePrefabInScene(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            foreach (var a in instance.GetComponentsInChildren<Animator>()) {
                a.enabled = false;
            }

            foreach (var b in instance.GetComponentsInChildren<MonoBehaviour>()) {
                b.enabled = false;
            }

            foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                if (!skin.sharedMesh || skin.bones.Any(boneTransform => !boneTransform)) {
                    continue;
                }

                var rest = skin.sharedMesh.bindposes.Select(transformMatrix => skin.transform.localToWorldMatrix * transformMatrix.inverse).ToArray();
                for (int i = 0; i < skin.bones.Length; i++) {
                    skin.bones[i].SetPositionAndRotation(rest[i].GetColumn(3), rest[i].rotation);
                }
            }

            head = instance.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.Equals("Head", StringComparison.OrdinalIgnoreCase));
            if (head) {
                headRest = head.rotation;
            }
        }

        public void Region(HeadSource.Source data, HeadRegion region) {
            if (regionMesh) {
                Object.DestroyImmediate(regionMesh);
            }

            regionMesh = null;
            if (data == null || region == null) {
                return;
            }

            var primary = data.skins[0];
            var triangles = primary.slots.SelectMany(s => s.triangles).ToArray();
            regionMesh = new Mesh {
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                vertices = primary.vertices
            };
            regionMesh.triangles = region.faces.SelectMany(f => triangles.Skip(f * 3).Take(3)).ToArray();
            regionMesh.RecalculateNormals();
            if (!highlight) {
                highlight = new Material(HeadShaders.Unlit()) {
                    hideFlags = HideFlags.HideAndDontSave
                };
                highlight.SetColor(highlight.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", new Color(.15f, 1, .45f));
                highlight.SetFloat("_Cull", 0);
            }
        }

        public Texture Draw(Rect rect, float headTurn, float blink, float jaw, float smile, float fullness, bool wire, bool fullBody) {
            if (!instance || rect.width < 4 || rect.height < 4) {
                return null;
            }

            if (head) {
                head.rotation = Quaternion.AngleAxis(headTurn, Vector3.up) * headRest;
            }

            var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>().Where(skinRenderer => skinRenderer.enabled && skinRenderer.sharedMesh).ToArray();
            foreach (var skin in skins) {
                var expressionWeights = new[] {
                    ("eyeBlinkLeft", blink),
                    ("eyeBlinkRight", blink),
                    ("eyeBlinkUpperLeft", blink),
                    ("eyeBlinkUpperRight", blink),
                    ("eyeBlinkLowerLeft", blink),
                    ("eyeBlinkLowerRight", blink),
                    ("jawOpen", jaw),
                    ("mouthSmileLeft", smile),
                    ("mouthSmileRight", smile),
                    ("lipFullnessUpper", fullness),
                    ("lipFullnessLower", fullness)
                };
                foreach (var expressionWeight in expressionWeights) {
                    int i = skin.sharedMesh.GetBlendShapeIndex(expressionWeight.Item1);
                    if (i >= 0) {
                        skin.SetBlendShapeWeight(i, expressionWeight.Item2);
                    }
                }
            }

            var all = instance.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled).ToArray();
            var bounds = all.Length > 0 ? all[0].bounds : new Bounds(Vector3.up, Vector3.one);
            foreach (var renderer2 in all) {
                bounds.Encapsulate(renderer2.bounds);
            }

            float height = Mathf.Max(.1f, bounds.size.y);
            Vector3 target = fullBody ? bounds.center : (head ? head.position + Vector3.up * height * .055f : bounds.center);
            target += Vector3.up * height * targetHeightOffset;
            float size = (fullBody ? height * .57f : height * .205f) * zoom;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            view.camera.orthographic = true;
            view.camera.orthographicSize = size;
            view.camera.nearClipPlane = .01f;
            view.camera.farClipPlane = 100;
            view.camera.transform.position = target + rotation * new Vector3(0, 0, height * 3);
            view.camera.transform.LookAt(target);
            view.camera.clearFlags = CameraClearFlags.SolidColor;
            view.camera.backgroundColor = new Color(.12f, .16f, .19f);
            view.lights[0].intensity = 1;
            view.lights[0].transform.rotation = Quaternion.Euler(35, 155, 0);
            view.lights[1].intensity = .4f;
            view.lights[1].transform.rotation = Quaternion.Euler(0, -30, 0);
            view.ambientColor = new Color(.45f, .45f, .45f);
            bool staticPreview = Event.current == null;
            if (staticPreview) {
                view.BeginStaticPreview(rect);
            } else {
                view.BeginPreview(rect, GUIStyle.none);
            }

            try {
                // Bake on every preview frame: a stale skinned draw can otherwise rotate only hats/glasses.
                foreach (var skin in skins) {
                    var mesh = new Mesh();
                    skin.BakeMesh(mesh);
                    temporary.Add(mesh);
                    skin.enabled = false;
                    for (int s = 0; s < mesh.subMeshCount; s++) {
                        if (s < skin.sharedMaterials.Length && skin.sharedMaterials[s]) {
                            view.DrawMesh(mesh, skin.transform.localToWorldMatrix, skin.sharedMaterials[s], s);
                        }
                    }
                }

                if (regionMesh) {
                    view.DrawMesh(regionMesh, Matrix4x4.identity, highlight, 0);
                }

                GL.wireframe = wire;
                view.Render(true);
            } finally {
                GL.wireframe = false;
                foreach (var skinRenderer2 in skins) {
                    skinRenderer2.enabled = true;
                }

                foreach (var mesh2 in temporary) {
                    Object.DestroyImmediate(mesh2);
                }

                temporary.Clear();
            }

            return staticPreview ? view.EndStaticPreview() : view.EndPreview();
        }

        public void Dispose() {
            if (instance) {
                Object.DestroyImmediate(instance);
            }

            if (regionMesh) {
                Object.DestroyImmediate(regionMesh);
            }

            if (highlight) {
                Object.DestroyImmediate(highlight);
            }

            view.Cleanup();
        }
    }
}
