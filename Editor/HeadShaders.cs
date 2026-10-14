// Selects compatible material and preview shaders for the active Unity render pipeline.
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace digitalbreed.SyntyHeadConverter {
    internal static class HeadShaders {
        public static Shader Lit() {
            string pipeline = GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.GetType().Name : "";
            string name = pipeline.Contains("HDRender") ? "HDRP/Lit" : pipeline.Contains("Universal") ? "Universal Render Pipeline/Lit" : "Standard";
            var shader = Shader.Find(name);
            if (!shader) {
                throw new InvalidOperationException("No compatible character shader was found for the active render pipeline: " + name);
            }

            return shader;
        }

        public static Shader Unlit() {
            string pipeline = GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.GetType().Name : "";
            string name = pipeline.Contains("HDRender") ? "HDRP/Unlit" : pipeline.Contains("Universal") ? "Universal Render Pipeline/Unlit" : "Unlit/Color";
            var shader = Shader.Find(name);
            if (!shader) {
                throw new InvalidOperationException("No compatible preview highlight shader was found: " + name);
            }

            return shader;
        }
    }
}
