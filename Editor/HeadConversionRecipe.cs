// Defines the serialized conversion recipe, appearance defaults and source-region selections.
using System;
using UnityEngine;

namespace digitalbreed.SyntyHeadConverter {
    /// <summary>Serializable appearance and fit controls. Positions and distances use canonical fitting meters.</summary>
    /// <remarks>Field names are the persisted recipe schema. RGB arrays contain three 0..255 color channels.</remarks>
    [Serializable]
    public class HeadFitSettings {
        /// <summary>Sidekick face morph selection: 0 masculine, 1 feminine.</summary>
        public int female;
        /// <summary>Multiplier from source bind coordinates to the canonical fitting space; export reverses it.</summary>
        public float canonicalScale = 1;
        /// <summary>Vertical offset in canonical meters, applied after Source-to-fit scale.</summary>
        public float canonicalOffset;
        /// <summary>Horizontal donor-head multiplier; 1.08 gives the default POLYGON-style proportions.</summary>
        public float width = 1.08f;
        /// <summary>Source neck cut plane on the canonical Y axis, in meters.</summary>
        public float cut = 1.47f;
        /// <summary>Base skin RGB (0..255); default warm light skin, #FFCCAE.</summary>
        public float[] skin = { 255, 204, 174 };
        /// <summary>Eyebrow and mesh facial-hair RGB (0..255); default dark brown, #503728.</summary>
        public float[] brow = { 80, 55, 40 };
        /// <summary>Lip tint RGB (0..255); default muted rose, #B46E69, blended through the lip mask.</summary>
        public float[] lip = { 180, 110, 105 };
        /// <summary>Iris RGB (0..255); default blue-gray, #607B84.</summary>
        public float[] iris = { 96, 123, 132 };
        /// <summary>Native Sidekick eyebrow variant suffix used to resolve the left and right meshes.</summary>
        public string browVariant = "01";
        /// <summary>Native Sidekick nose variant suffix.</summary>
        public string noseVariant = "01";
        /// <summary>Optional facial-hair FBX basename; empty retains source facial hair.</summary>
        public string sidekickBeard = "";
        /// <summary>Bundled age-overlay filename used when no custom age texture is assigned.</summary>
        public string ageOverlay = "MatureSoft.png";
        /// <summary>Facial skin normal smoothing, 0..1; zero retains the existing shading. Does not move vertices.</summary>
        public float faceSmoothing;
        /// <summary>Age-overlay blend strength, 0..1.</summary>
        public float age;
        /// <summary>Makeup-overlay blend strength, 0..1.</summary>
        public float makeup;
        /// <summary>Apply makeup as color changes relative to its foundation instead of replacing the character's skin color.</summary>
        public bool makeupPreserveSkinTone;
        /// <summary>Unpainted foundation RGB (0..255) in the makeup overlay. Default #F4C5A9 is sampled from DaytimeRose's central upper forehead.</summary>
        public float[] makeupReferenceSkin = { 244, 197, 169 };
        /// <summary>Amount of front-throat profile correction, 0..1; 0 retains the fitted profile.</summary>
        public float throatSoftening = .65f;
        /// <summary>Front-depth limit used by throat correction, in canonical meters.</summary>
        public float throatLowerFrontLimit = .11f;
        /// <summary>Enables textured stubble over the lower face, jaw and upper neck.</summary>
        public bool scruff;
        /// <summary>Insets the neck bridge beneath a shirt collar and blends its skin material; disable for exposed necks.</summary>
        public bool collarInset = true;
        /// <summary>Connected-component vertex counts used to recognize retained source accessories.</summary>
        public int[] accessories = Array.Empty<int>();
        /// <summary>Neck cross-section multipliers: [horizontal width, front-to-back depth].</summary>
        public float[] neckScale = { 1, 1 };
        /// <summary>Optional [u, v] sample in the source color palette; empty enables automatic selection.</summary>
        public float[] skinSampleUv = Array.Empty<float>();
        /// <summary>Hair object name, "embedded" for integrated hair, or empty to disable scalp fitting.</summary>
        public string scalpHairEnvelope = "";
        /// <summary>Inset beneath the chosen hair shell in canonical meters; default 1 cm.</summary>
        public float scalpHairClearance = .01f;
    }

    [Serializable]
    public class HeadRegion {
        public string label, role = "Auto";
        public int[] faces;
        public Vector3 center, size;
        public Color color = Color.gray;
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "digitalbreed.Characters.Editor", "Assembly-CSharp-Editor", "HeadConversionRecipe")]
    public sealed class HeadConversionRecipe : ScriptableObject {
        public GameObject sourcePrefab;
        public string characterId = "NewCharacter";
        public string outputFolder = "Assets/SyntyHeadConverter/Characters/NewCharacter";
        public HeadFitSettings settings = new HeadFitSettings();
        public float lipFullness = 50, scruffOpacity = .85f;
        public Texture2D customAgeTexture;
        public Texture2D customMakeupTexture;
        public Texture2D customScruffTexture;
        [TextArea(4, 30)]
        public string baseConfig = "{}";
        public HeadRegion[] regions = Array.Empty<HeadRegion>();
        public string sourceFingerprint;
        public HeadConversionRecipe CopyForSaving() {
            var copy = Instantiate(this);
            copy.hideFlags = HideFlags.None;
            copy.regions = System.Array.FindAll(regions, r => r.role != "Auto");
            return copy;
        }
    }
}
