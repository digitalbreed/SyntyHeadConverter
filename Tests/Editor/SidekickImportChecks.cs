// Checks donor metadata preservation and immutable cache keys without invoking Unity imports.
using System;
using System.Collections.Generic;
using System.IO;

namespace digitalbreed.SyntyHeadConverter.Tests {
    internal static class SidekickImportChecks {
        internal static void Run(string directory) {
            Directory.CreateDirectory(directory);
            const string metadata = "fileFormatVersion: 2\nguid: 11111111111111111111111111111111\nModelImporter:\n" +
                "  serializedVersion: 21300\n  externalObjects:\n  - first:\n      name: COLOR\n" +
                "    second: {fileID: 2100000, guid: 22222222222222222222222222222222, type: 2}\n" +
                "  meshes:\n    globalScale: 1.7\n    useFileUnits: 0\n    bakeAxisConversion: 1\n" +
                "    weldVertices: 0\n    keepQuads: 0\n    importBlendShapes: 0\n    meshCompression: 2\n" +
                "  tangentSpace:\n    normalImportMode: 0\n    normalSmoothAngle: 42\n    blendShapeNormalImportMode: 2\n" +
                "  importAnimation: 1\n";
            const string guid = "33333333333333333333333333333333";
            string prepared = SidekickImportProfile.Prepare(metadata, guid);
            foreach (string preserved in new[] { "globalScale: 1.7", "useFileUnits: 0", "bakeAxisConversion: 1",
                "weldVertices: 0", "normalImportMode: 0", "normalSmoothAngle: 42", "blendShapeNormalImportMode: 2" }) {
                if (!prepared.Contains(preserved)) {
                    throw new Exception("Donor preparation changed source geometry settings: " + preserved);
                }
            }

            foreach (string required in new[] { "guid: " + guid, "externalObjects: {}", "keepQuads: 1",
                "importBlendShapes: 1", "meshCompression: 0", "isReadable: 1", "optimizeGameObjects: 0",
                "materialImportMode: 0", "importAnimation: 0", "animationType: 2" }) {
                if (!prepared.Contains(required)) {
                    throw new Exception("Donor metadata was not configured before first import: " + required);
                }
            }

            if (prepared.Contains("22222222222222222222222222222222") ||
                !SidekickImportProfile.Prepare(null, guid).Contains("keepQuads: 1")) {
                throw new Exception("Donor metadata retained a foreign material or lost its default profile.");
            }

            string file = Path.Combine(directory, "Source.fbx");
            File.WriteAllText(file, "first source content");
            DateTime timestamp = File.GetLastWriteTimeUtc(file);
            string first = SidekickImportProfile.Fingerprint(file, metadata);
            if (first != SidekickImportProfile.Fingerprint(file, metadata.Replace("11111111111111111111111111111111", guid)) ||
                first != SidekickImportProfile.Fingerprint(file, metadata.Replace("\n", "\r\n"))) {
                throw new Exception("Cache keys depend on source GUIDs or metadata line endings.");
            }

            File.WriteAllText(file, "other source content");
            File.SetLastWriteTimeUtc(file, timestamp);
            if (first == SidekickImportProfile.Fingerprint(file, metadata)) {
                throw new Exception("A source change with an unchanged timestamp reused its cache key.");
            }

            File.WriteAllText(file, "first source content");
            if (first == SidekickImportProfile.Fingerprint(file, metadata.Replace("globalScale: 1.7", "globalScale: 2"))) {
                throw new Exception("A geometry import setting change reused its cache key.");
            }

            try {
                SidekickAssets.ResolveDirectory(Path.Combine(directory, "Missing"), new Dictionary<string, string> {
                    { "head", "MissingHead" }, { "eye", "MissingEye" }
                });
                throw new Exception("An incomplete explicit Sidekick folder was accepted.");
            } catch (Exception error) when (error.Message.Contains("MissingHead") && error.Message.Contains("MissingEye")) {
                // An explicit selection must report every missing part without consulting other installations.
            }
        }
    }
}
