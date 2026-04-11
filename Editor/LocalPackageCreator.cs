using System;
using System.IO;
using System.Threading.Tasks;

namespace Warlogic.RegistryBrowser
{
    public static class LocalPackageCreator
    {
        public static string DeriveDisplayName(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
                return "";

            string[] segments = packageId.Split('.');
            string lastSegment = segments[segments.Length - 1];
            if (string.IsNullOrEmpty(lastSegment))
                return packageId;

            return char.ToUpper(lastSegment[0]) + lastSegment.Substring(1);
        }

        // Derives the assembly name base from the package ID.
        // Skips the first segment if it's a TLD prefix (com, net, org, io), then PascalCases each remaining segment.
        // Example: com.mycompany.mycoolsystem -> Mycompany.Mycoolsystem
        public static string DeriveAssemblyBase(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
                return "Package";

            string[] segments = packageId.Split('.');
            int startIndex = 0;
            if (segments.Length > 1)
            {
                string first = segments[0].ToLowerInvariant();
                if (first == "com" || first == "net" || first == "org" || first == "io")
                    startIndex = 1;
            }

            var parts = new System.Collections.Generic.List<string>();
            for (int i = startIndex; i < segments.Length; i++)
            {
                string seg = segments[i];
                if (string.IsNullOrEmpty(seg))
                    continue;
                parts.Add(char.ToUpper(seg[0]) + seg.Substring(1));
            }

            return parts.Count > 0 ? string.Join(".", parts) : packageId;
        }

        public static async Task CreatePackageAsync(string packageId, string displayName, bool initGit)
        {
            string projectRoot = GitEmbedOperations.GetProjectRoot();
            string packageDir = GitEmbedOperations.GetEmbedAbsolutePath(packageId);

            if (Directory.Exists(packageDir))
                throw new InvalidOperationException($"Package directory already exists: {packageDir}");

            string embedsDir = Path.Combine(projectRoot, "Packages", "Embeds");
            if (!Directory.Exists(embedsDir))
                Directory.CreateDirectory(embedsDir);

            Directory.CreateDirectory(packageDir);
            WriteMetaForDirectory(packageDir);

            string asmBase = DeriveAssemblyBase(packageId);

            WriteFile(packageDir, "package.json", BuildPackageJson(packageId, displayName));
            WriteFile(packageDir, "CHANGELOG.md", "# Changelog\n\n## [1.0.0]\n\n- Initial release\n");
            WriteFile(packageDir, "README.md", $"# {displayName}\n");

            string runtimeDir = CreateSubDir(packageDir, "Runtime");
            WriteFile(runtimeDir, $"{asmBase}.asmdef", BuildAsmdef(asmBase, null, false, false));

            string editorDir = CreateSubDir(packageDir, "Editor");
            WriteFile(editorDir, $"{asmBase}.Editor.asmdef", BuildAsmdef($"{asmBase}.Editor", new[] { asmBase }, true, false));

            string testsDir = CreateSubDir(packageDir, "Tests");

            string testsRuntimeDir = CreateSubDir(testsDir, "Runtime");
            WriteFile(testsRuntimeDir, $"{asmBase}.Tests.asmdef", BuildAsmdef($"{asmBase}.Tests", new[] { asmBase }, false, true));

            string testsEditorDir = CreateSubDir(testsDir, "Editor");
            WriteFile(testsEditorDir, $"{asmBase}.Editor.Tests.asmdef", BuildAsmdef($"{asmBase}.Editor.Tests", new[] { asmBase, $"{asmBase}.Editor" }, true, true));

            PackageManifestEditor.AddOrUpdateDependency(packageId, $"file:Embeds/{packageId}");

            if (initGit)
                await GitEmbedOperations.InitRepoAsync(packageDir);
        }

        private static string BuildPackageJson(string packageId, string displayName)
        {
            return "{\n" +
                   $"  \"name\": \"{packageId}\",\n" +
                   $"  \"displayName\": \"{displayName}\",\n" +
                   "  \"version\": \"1.0.0\",\n" +
                   "  \"description\": \"\"\n" +
                   "}\n";
        }

        private static string BuildAsmdef(string name, string[] references, bool editorOnly, bool isTest)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"name\": \"{name}\",");

            if (references != null && references.Length > 0)
            {
                sb.AppendLine("  \"references\": [");
                for (int i = 0; i < references.Length; i++)
                {
                    string comma = i < references.Length - 1 ? "," : "";
                    sb.AppendLine($"    \"{references[i]}\"{comma}");
                }
                sb.AppendLine("  ],");
            }
            else
            {
                sb.AppendLine("  \"references\": [],");
            }

            if (editorOnly)
                sb.AppendLine("  \"includePlatforms\": [\"Editor\"],");
            else
                sb.AppendLine("  \"includePlatforms\": [],");

            sb.AppendLine("  \"excludePlatforms\": [],");
            sb.AppendLine($"  \"autoReferenced\": {(isTest ? "false" : "true")},");

            if (isTest)
                sb.AppendLine("  \"optionalUnityReferences\": [\"TestAssemblies\"]");
            else
                sb.AppendLine("  \"optionalUnityReferences\": []");

            sb.Append("}");
            return sb.ToString();
        }

        private static string CreateSubDir(string parent, string name)
        {
            string dir = Path.Combine(parent, name);
            Directory.CreateDirectory(dir);
            WriteMetaForDirectory(dir);
            return dir;
        }

        private static void WriteFile(string dir, string fileName, string content)
        {
            string filePath = Path.Combine(dir, fileName);
            File.WriteAllText(filePath, content);
            WriteMetaForFile(filePath);
        }

        private static void WriteMetaForDirectory(string dirPath)
        {
            string metaPath = dirPath.TrimEnd('/', '\\') + ".meta";
            string guid = Guid.NewGuid().ToString("N");
            File.WriteAllText(metaPath, $"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n");
        }

        private static void WriteMetaForFile(string filePath)
        {
            string metaPath = filePath + ".meta";
            string guid = Guid.NewGuid().ToString("N");
            File.WriteAllText(metaPath, $"fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n");
        }
    }
}
