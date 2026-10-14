// Prepares metadata for a new donor copy and fingerprints its source without changing existing imports.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace digitalbreed.SyntyHeadConverter {
    internal static class SidekickImportProfile {
        // Bump when the donor import policy changes so existing copies remain immutable.
        private const string Version = "donor-copy-v2";
        private const string EmptyGuid = "00000000000000000000000000000000";
        private const string DefaultMetadata = "fileFormatVersion: 2\nguid: " + EmptyGuid +
            "\nModelImporter:\n  serializedVersion: 21300\n  internalIDToNameTable: []\n" +
            "  externalObjects: {}\n  materials:\n    materialImportMode: 0\n" +
            "  animations:\n    optimizeGameObjects: 0\n    isReadable: 1\n" +
            "  meshes:\n    globalScale: 1\n    meshCompression: 0\n    importBlendShapes: 1\n" +
            "    useFileUnits: 1\n    keepQuads: 1\n    useFileScale: 1\n" +
            "  tangentSpace:\n    normalImportMode: 0\n  importAnimation: 0\n  animationType: 2\n";

        internal static string Prepare(string sourceMetadata, string guid) {
            string metadata = (string.IsNullOrWhiteSpace(sourceMetadata) ? DefaultMetadata : sourceMetadata.Replace("\r\n", "\n")).TrimEnd() + "\n";
            if (!Regex.IsMatch(metadata, "(?m)^ModelImporter:\\s*$") ||
                !Regex.IsMatch(metadata, "(?m)^guid: [a-fA-F0-9]{32}\\s*$")) {
                throw new InvalidDataException("The Sidekick source metadata is not a supported Unity ModelImporter file.");
            }

            metadata = Regex.Replace(metadata, "(?m)^guid: [^\\n]*", "guid: " + guid);
            metadata = SetBlock(metadata, "externalObjects", "  externalObjects: {}\n");
            metadata = SetValue(metadata, "materials", "materialImportMode", "0");
            metadata = SetValue(metadata, "animations", "optimizeGameObjects", "0");
            metadata = SetValue(metadata, "animations", "isReadable", "1");
            metadata = SetValue(metadata, "meshes", "meshCompression", "0");
            metadata = SetValue(metadata, "meshes", "importBlendShapes", "1");
            metadata = SetValue(metadata, "meshes", "keepQuads", "1");
            metadata = SetValue(metadata, null, "importAnimation", "0");
            // Donor extraction needs the original skeleton, not a retargeted avatar from another project.
            // Geometry, normals, UVs, scale and axis settings remain those of the source metadata.
            metadata = SetValue(metadata, null, "animationType", "2");
            metadata = SetValue(metadata, null, "avatarSetup", "0");
            metadata = SetValue(metadata, null, "lastHumanDescriptionAvatarSource", "{fileID: 0}");
            metadata = SetValue(metadata, null, "assetBundleName", "");
            metadata = SetValue(metadata, null, "assetBundleVariant", "");
            return metadata;
        }

        internal static string Fingerprint(string file, string metadata) {
            return Hash(Encoding.UTF8.GetBytes(Version + "\n" + FileHash(file) + "\n" + Prepare(metadata, EmptyGuid)));
        }

        internal static string FileHash(string file) {
            using (var stream = File.OpenRead(file)) {
                using (var sha = SHA256.Create()) {
                    return Hex(sha.ComputeHash(stream));
                }
            }
        }

        private static string Hash(byte[] bytes) {
            using (var sha = SHA256.Create()) {
                return Hex(sha.ComputeHash(bytes));
            }
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

        private static string BlockPattern(string key) => "(?ms)^  " + Regex.Escape(key) + ":[^\\n]*\\n.*?(?=^  [A-Za-z_][A-Za-z0-9_]*:|\\z)";

        private static string SetBlock(string metadata, string key, string block) {
            string pattern = BlockPattern(key);
            return Regex.IsMatch(metadata, pattern) ? Regex.Replace(metadata, pattern, _ => block, RegexOptions.None) : metadata.TrimEnd() + "\n" + block;
        }

        private static string SetValue(string metadata, string section, string key, string value) {
            string indent = section == null ? "  " : "    ";
            string pattern = "(?m)^" + indent + Regex.Escape(key) + ":[^\\n]*";
            string line = indent + key + ": " + value;
            if (section == null) {
                return Regex.IsMatch(metadata, pattern) ? Regex.Replace(metadata, pattern, _ => line) : metadata.TrimEnd() + "\n" + line + "\n";
            }

            var block = Regex.Match(metadata, BlockPattern(section));
            string contents = block.Success ? block.Value : "  " + section + ":\n";
            contents = Regex.IsMatch(contents, pattern) ? Regex.Replace(contents, pattern, _ => line) : contents.TrimEnd() + "\n" + line + "\n";
            return SetBlock(metadata, section, contents);
        }
    }
}
