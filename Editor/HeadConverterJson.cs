// Serializes converter fields with Newtonsoft, retaining Unity value layouts and asset references.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using Object = UnityEngine.Object;

namespace digitalbreed.SyntyHeadConverter {
    internal static class HeadConverterJson {
        private static readonly FieldResolver Fields = new FieldResolver();

        public static string Serialize(object value, bool indented = false) {
            return JsonConvert.SerializeObject(value, indented ? Formatting.Indented : Formatting.None,
                Settings(value?.GetType()));
        }

        public static T Deserialize<T>(string json) {
            return JsonConvert.DeserializeObject<T>(json, Settings(typeof(T)));
        }

        public static void Populate(string json, object target) {
            JsonConvert.PopulateObject(json, target, Settings(target.GetType()));
        }

        private static JsonSerializerSettings Settings(Type rootType) {
            return new JsonSerializerSettings {
                ContractResolver = Fields,
                // Populate replaces arrays instead of appending to existing recipe values.
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                Converters = { new AssetReferenceConverter(rootType) }
            };
        }

        private sealed class FieldResolver : DefaultContractResolver {
            protected override List<MemberInfo> GetSerializableMembers(Type objectType) {
                // Keep the existing public-field schema, including x/y/z and r/g/b/a.
                // Unity's computed properties (normalized, linear, etc.) are not data.
                return objectType.GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Where(field => !field.IsInitOnly && !field.IsNotSerialized)
                    .Cast<MemberInfo>().ToList();
            }
        }

        private sealed class AssetReference {
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string guid;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public long localFileId;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int instanceID;
        }

        private sealed class AssetReferenceConverter : JsonConverter {
            private readonly Type rootType;

            public AssetReferenceConverter(Type rootType) {
                this.rootType = rootType;
            }

            public override bool CanConvert(Type objectType) {
                // A root recipe is serialized by value; nested Unity objects are references.
                return objectType != rootType && typeof(Object).IsAssignableFrom(objectType);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
                var asset = (Object)value;
                if (!asset) {
                    writer.WriteNull();
                    return;
                }

                var reference = new AssetReference();
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId)) {
                    reference.guid = guid;
                    reference.localFileId = localId;
                } else {
                    reference.instanceID = asset.GetInstanceID();
                }

                serializer.Serialize(writer, reference);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
                if (reader.TokenType == JsonToken.Null) {
                    return null;
                }

                var reference = serializer.Deserialize<AssetReference>(reader);
                if (!string.IsNullOrEmpty(reference.guid)) {
                    string path = AssetDatabase.GUIDToAssetPath(reference.guid);
                    if (string.IsNullOrEmpty(path)) {
                        return null;
                    }

                    return AssetDatabase.LoadAllAssetsAtPath(path).FirstOrDefault(asset =>
                        objectType.IsInstanceOfType(asset) &&
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId) &&
                        guid == reference.guid && localId == reference.localFileId);
                }

                // Read editor-session snapshots written before GUID/local-file-ID references.
#pragma warning disable CS0618 // Retain Unity 6.0 compatibility for legacy instance IDs.
                var instance = EditorUtility.InstanceIDToObject(reference.instanceID);
#pragma warning restore CS0618
                return objectType.IsInstanceOfType(instance) ? instance : null;
            }
        }
    }
}
