// Reads optional advanced fitting values from a Newtonsoft JSON object without changing the recipe schema.
using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace digitalbreed.SyntyHeadConverter {
    internal sealed class RecipeOptions {
        private readonly JObject values;
        public RecipeOptions(string json) {
            try {
                values = JObject.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            } catch (JsonException error) {
                throw new FormatException("The advanced recipe must be a valid JSON object: " + error.Message, error);
            }
        }

        private RecipeOptions(JObject values) {
            this.values = values;
        }

        public bool Has(string key) {
            return values.ContainsKey(key);
        }

        public float Number(string key, float fallback = 0) {
            return values.TryGetValue(key, out var value) ? value.Value<float>() : fallback;
        }

        public bool Flag(string key, bool fallback = false) {
            return values.TryGetValue(key, out var value) ? value.Value<bool>() : fallback;
        }

        public string Text(string key, string fallback = "") {
            return values.TryGetValue(key, out var value) ? value.Value<string>() : fallback;
        }

        public float[] Numbers(string key) {
            return Array(key).Select(value => value.Value<float>()).ToArray();
        }

        public string[] Strings(string key) {
            return Array(key).Select(value => value.Value<string>()).ToArray();
        }

        public float[][] Rows(string key) {
            return Array(key).Select(row => RequireArray(row, key).Select(value => value.Value<float>()).ToArray()).ToArray();
        }

        public RecipeOptions[] Objects(string key) {
            return Array(key).Select(value => value is JObject nested ? new RecipeOptions(nested) : throw new FormatException("Expected JSON objects in '" + key + "'.")).ToArray();
        }

        private JArray Array(string key) {
            return values.TryGetValue(key, out var value) ? RequireArray(value, key) : new JArray();
        }

        private static JArray RequireArray(JToken value, string key) {
            return value as JArray ?? throw new FormatException("Expected a JSON array for '" + key + "'.");
        }
    }
}
