// Creates only new asset folders through Unity without refreshing the project.
using System;
using System.IO;
using UnityEditor;

namespace digitalbreed.SyntyHeadConverter {
    internal static class HeadAssetFolders {
        internal static void Ensure(string path) {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                Array.Exists(path.Split('/'), part => part == "." || part == ".." || part.Length == 0)) {
                throw new ArgumentException("Asset folders must be beneath Assets: " + path);
            }

            string parent = "Assets";
            foreach (string part in path.Substring(7).Split('/')) {
                string folder = parent + "/" + part;
                if (!AssetDatabase.IsValidFolder(folder)) {
                    if (Directory.Exists(folder)) {
                        throw new IOException("The folder exists on disk but Unity has not registered it yet: " + folder + ". Let Unity finish its normal asset processing before converting.");
                    }

                    if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, part))) {
                        throw new IOException("Unity could not create the asset folder: " + folder);
                    }
                }

                parent = folder;
            }
        }
    }
}
