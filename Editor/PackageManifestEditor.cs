using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Warlogic.RegistryBrowser
{
    public static class PackageManifestEditor
    {
        private static string ManifestPath
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", "manifest.json"));

        public static void SetRegistryVersion(string packageId, string version)
        {
            string content = File.ReadAllText(ManifestPath);
            string escaped = Regex.Escape(packageId);
            var regex = new Regex($@"(""{escaped}""\s*:\s*)""[^""]*""");
            string updated = regex.Replace(content, $"$1\"{version}\"");
            File.WriteAllText(ManifestPath, updated);
        }

        public static void SetEmbeddedPath(string packageId)
            => SetRegistryVersion(packageId, $"file:Embeds/{packageId}");

        public static void AddOrUpdateDependency(string packageId, string versionOrPath)
        {
            string content = File.ReadAllText(ManifestPath);
            string escaped = Regex.Escape(packageId);

            if (Regex.IsMatch(content, $@"""{escaped}"""))
            {
                SetRegistryVersion(packageId, versionOrPath);
                return;
            }

            // Insert as the last entry in the dependencies block.
            // Match the last key-value pair before the closing } of dependencies.
            var insertRegex = new Regex(
                @"(""[^""]+"":\s*""[^""]*"")(\s*\n(\s*)\})",
                RegexOptions.RightToLeft);
            string updated = insertRegex.Replace(
                content,
                $"$1,\n$3\"{packageId}\": \"{versionOrPath}\"$2",
                count: 1);
            File.WriteAllText(ManifestPath, updated);
        }

        public static void RemoveDependency(string packageId)
        {
            string content = File.ReadAllText(ManifestPath);
            string escaped = Regex.Escape(packageId);

            // Try removing as a non-last entry: "id": "value", (with trailing comma + whitespace)
            var trailingCommaRegex = new Regex($@"\s*""{escaped}""\s*:\s*""[^""]*""\s*,");
            string updated = trailingCommaRegex.Replace(content, "");

            if (updated == content)
            {
                // Try removing as the last entry: preceding comma + "id": "value"
                var leadingCommaRegex = new Regex($@",\s*""{escaped}""\s*:\s*""[^""]*""");
                updated = leadingCommaRegex.Replace(content, "");
            }

            if (updated == content)
            {
                // Only entry: no comma on either side
                var onlyEntryRegex = new Regex($@"\s*""{escaped}""\s*:\s*""[^""]*""");
                updated = onlyEntryRegex.Replace(content, "");
            }

            File.WriteAllText(ManifestPath, updated);
        }
    }
}
