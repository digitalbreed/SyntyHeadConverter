// Builds the Humanoid avatar and a facial/head-turn demonstration animation for the converted skeleton.
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace digitalbreed.SyntyHeadConverter {
    public static partial class HeadAssetBuilder {
        private static readonly string[] HumanoidSides = { "Left", "Right" };
        private static readonly (string, string)[] HumanoidLimbMappings = {
            ("Shoulder", "Clavicle"),
            ("UpperArm", "Shoulder"),
            ("LowerArm", "Elbow"),
            ("Hand", "Hand"),
            ("UpperLeg", "UpperLeg"),
            ("LowerLeg", "LowerLeg"),
            ("Foot", "Ankle"),
            ("Toes", "Ball")
        };
        private static readonly (string, string)[] HumanoidFingerMappings = { ("Thumb", "Thumb"), ("Index", "IndexFinger"), ("Middle", "Finger") };
        private static readonly string[] DemoBoneNames = { "Head", "eye_l", "eye_r" };
        static string ResolveBone(GameObject characterInstance, string name) {
            var names = characterInstance.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
            var legacy = name.EndsWith("_L") ? name.Substring(0, name.Length - 2) : name.EndsWith("_R") ? name.Substring(0, name.Length - 2) + " 1" : name;
            return names.FirstOrDefault(n => n == name) ?? names.FirstOrDefault(n => CanonicalName(n) == name || CanonicalName(n) == legacy) ?? legacy;
        }

        static Avatar MakeAvatar(GameObject characterInstance) {
            var pairs = new Dictionary<string, string> {
                { "Hips", "Hips" },
                { "Spine", "Spine_01" },
                { "Chest", "Spine_02" },
                { "UpperChest", "Spine_03" },
                { "Neck", "Neck" },
                { "Head", "Head" }
            };
            foreach (string side in HumanoidSides) {
                string sideSuffix = side == "Left" ? "L" : "R";
                foreach (var mapping in HumanoidLimbMappings) {
                    pairs[side + mapping.Item1] = mapping.Item2 + "_" + sideSuffix;
                }

                foreach (var fingerMapping in HumanoidFingerMappings) {
                    for (int i = 1; i <= 3; i++) {
                        pairs[side + fingerMapping.Item1 + (i == 1 ? "Proximal" : i == 2 ? "Intermediate" : "Distal")] = fingerMapping.Item2 + "_0" + i + "_" + sideSuffix;
                    }
                }
            }

            var description = new HumanDescription {
                human = pairs.Select(p => new HumanBone { humanName = p.Key, boneName = ResolveBone(characterInstance, p.Value), limit = new HumanLimit { useDefaultValues = true } }).ToArray(),
                skeleton = characterInstance.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray(),
                upperArmTwist = .5f,
                lowerArmTwist = .5f,
                upperLegTwist = .5f,
                lowerLegTwist = .5f,
                armStretch = 0,
                legStretch = 0
            };
            var avatar = AvatarBuilder.BuildHumanAvatar(characterInstance, description);
            if (!avatar.isValid || !avatar.isHuman) {
                throw new Exception("Invalid Humanoid avatar");
            }

            return avatar;
        }

        static AnimationClip FacialDemo(GameObject characterInstance, SkinnedMeshRenderer skin) {
            var clip = new AnimationClip {
                name = "HeadFacialDemo",
                frameRate = 24
            };
            float[] times = { 0, .5f, .8f, 1.5f, 2.5f, 3.25f, 3.75f, 4.5f, 5.25f, 6 };
            foreach (var name in DemoBoneNames) {
                var bone = skin.bones.Single(boneTransform => boneTransform.name == ResolveBone(characterInstance, name));
                var baseline = bone.localRotation;
                var curves = Enumerable.Range(0, 4).Select(_ => new AnimationCurve()).ToArray();
                for (int i = 0; i < times.Length; i++) {
                    float angle = i == 3 ? -35 : i == 4 ? 35 : 0;
                    if (name != "Head") {
                        angle = i == 3 ? 12 : i == 4 ? -12 : 0;
                    }

                    var rotation = Quaternion.AngleAxis(angle, bone.parent.InverseTransformDirection(Vector3.up)) * baseline;
                    for (int k = 0; k < 4; k++) {
                        curves[k].AddKey(times[i], rotation[k]);
                    }
                }

                for (int k = 0; k < 4; k++) {
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bone, characterInstance.transform), typeof(Transform), "m_LocalRotation." + "xyzw"[k]), curves[k]);
                }
            }

            for (int n = 0; n < skin.sharedMesh.blendShapeCount; n++) {
                var name = skin.sharedMesh.GetBlendShapeName(n);
                if (name.StartsWith("lipFullness")) {
                    continue;
                }

                var curve = new AnimationCurve();
                for (int i = 0; i < times.Length; i++) {
                    float value = i == 1 && name.StartsWith("eyeBlink") ? 100 : i == 6 && name == "jawOpen" ? 40 : i == 7 && name == "mouthFunnel" ? 30 : i == 8 && (name == "mouthSmileLeft" || name == "mouthSmileRight") ? 35 : i == 8 && (name == "browRaiseLeft" || name == "browRaiseRight") ? 65 : 0;
                    curve.AddKey(times[i], value);
                }

                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(skin.transform, characterInstance.transform), typeof(SkinnedMeshRenderer), "blendShape." + name), curve);
            }

            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return Save(clip, outputFolder + "/Animations/" + Recipe.characterId + "FacialDemo.anim");
        }
    }
}
