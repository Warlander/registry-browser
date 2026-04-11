using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Warlogic.RegistryBrowser
{
    public readonly struct PublishPreflightResult
    {
        public bool CanPublish { get; }
        public string ErrorMessage { get; }
        public string LocalVersion { get; }
        public bool IsRepublish { get; }
        public IReadOnlyList<RegistryScope> CandidateRegistries { get; }
        public string RegistryUrl { get; }

        private PublishPreflightResult(bool canPublish, string errorMessage, string localVersion,
            bool isRepublish, IReadOnlyList<RegistryScope> candidateRegistries, string registryUrl)
        {
            CanPublish = canPublish;
            ErrorMessage = errorMessage;
            LocalVersion = localVersion;
            IsRepublish = isRepublish;
            CandidateRegistries = candidateRegistries;
            RegistryUrl = registryUrl;
        }

        public static PublishPreflightResult Fail(string errorMessage)
            => new PublishPreflightResult(false, errorMessage, null, false, null, null);

        public static PublishPreflightResult Success(string localVersion, bool isRepublish,
            IReadOnlyList<RegistryScope> candidateRegistries, string registryUrl)
            => new PublishPreflightResult(true, null, localVersion, isRepublish, candidateRegistries, registryUrl);
    }

    public static class PackagePublishOperations
    {
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private static readonly Regex VersionKeyRegex = new Regex(
            @"""(\d+\.\d+(?:\.\d+)?)""\s*:",
            RegexOptions.Compiled);

        private const string PackOutputRelativePath = "Library/RegistryBrowser/Pack";

        public static async Task<PublishPreflightResult> RunPreflightAsync(
            string packageId, PackageDetails details, IReadOnlyList<RegistryScope> registries)
        {
            // 1. Check for uncommitted git changes
            if (GitEmbedOperations.HasGitRepo(packageId))
            {
                bool hasChanges = await GitEmbedOperations.EmbedHasChangesAsync(packageId);
                if (hasChanges)
                    return PublishPreflightResult.Fail(
                        "Cannot publish: there are uncommitted local changes in the package's Git repository. " +
                        "Commit or discard changes before publishing.");
            }

            // 2–3. Validate package.json fields
            string json = ReadLocalPackageJson(packageId);
            if (json == null)
                return PublishPreflightResult.Fail("Cannot publish: package.json not found.");

            string changelogUrl = ParsePackageJsonField(json, "changelogUrl");
            if (string.IsNullOrEmpty(changelogUrl))
                return PublishPreflightResult.Fail(
                    "Cannot publish: package.json is missing the \"changelogUrl\" field.");

            var missingFields = new List<string>();
            if (string.IsNullOrEmpty(ParsePackageJsonField(json, "name")))
                missingFields.Add("name");
            if (string.IsNullOrEmpty(ParsePackageJsonField(json, "displayName")))
                missingFields.Add("displayName");
            if (string.IsNullOrEmpty(ParsePackageJsonField(json, "version")))
                missingFields.Add("version");
            if (string.IsNullOrEmpty(ParsePackageJsonField(json, "description")))
                missingFields.Add("description");

            if (missingFields.Count > 0)
                return PublishPreflightResult.Fail(
                    $"Cannot publish: package.json is missing required fields: {string.Join(", ", missingFields)}.");

            string localVersion = ParsePackageJsonField(json, "version");

            // 4. Resolve target registry
            string registryUrl = details.RegistryUrl;
            IReadOnlyList<RegistryScope> candidates = null;

            if (string.IsNullOrEmpty(registryUrl))
            {
                candidates = FindMatchingRegistries(packageId, registries);
                if (candidates.Count == 0)
                    return PublishPreflightResult.Fail(
                        "Cannot publish: no configured registry matches this package's scope prefix. " +
                        "Add a matching registry in Project Settings > Registry Browser.");

                if (candidates.Count == 1)
                    registryUrl = candidates[0].RegistryUrl;
            }

            // 5. Check if version already exists on registry
            bool isRepublish = false;
            if (!string.IsNullOrEmpty(registryUrl))
            {
                isRepublish = await VersionExistsOnRegistryAsync(packageId, localVersion, registryUrl);
            }

            return PublishPreflightResult.Success(localVersion, isRepublish, candidates, registryUrl);
        }

        public static async Task<string> PackAsync(string packageId)
        {
            string sourcePath = GitEmbedOperations.GetEmbedAbsolutePath(packageId);
            string targetPath = Path.Combine(GitEmbedOperations.GetProjectRoot(), PackOutputRelativePath);

            if (!Directory.Exists(targetPath))
                Directory.CreateDirectory(targetPath);

            string orgId = RegistryBrowserConfig.LoadOrganizationId();
            PackRequest request = Client.Pack(sourcePath, targetPath, orgId);
            string tarballPath = await WaitForPackRequestAsync(request);
            return tarballPath;
        }

        public static Task NpmPublishAsync(string tarballPath, string registryUrl)
        {
            string args = $"publish \"{tarballPath}\" --registry \"{registryUrl.TrimEnd('/')}\"";
            return RunNpmAsync(args);
        }

        public static Task NpmUnpublishAsync(string packageId, string version, string registryUrl)
        {
            string args = $"unpublish \"{packageId}@{version}\" --registry \"{registryUrl.TrimEnd('/')}\"";
            return RunNpmAsync(args);
        }

        private static IReadOnlyList<RegistryScope> FindMatchingRegistries(
            string packageId, IReadOnlyList<RegistryScope> allRegistries)
        {
            var matches = new List<RegistryScope>();
            foreach (RegistryScope registry in allRegistries)
            {
                if (!string.IsNullOrEmpty(registry.Scope) && packageId.StartsWith(registry.Scope))
                    matches.Add(registry);
            }
            return matches;
        }

        private static async Task<bool> VersionExistsOnRegistryAsync(
            string packageId, string version, string registryUrl)
        {
            try
            {
                string url = registryUrl.TrimEnd('/') + "/" + packageId;
                HttpResponseMessage response = await HttpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return false;

                string json = await response.Content.ReadAsStringAsync();

                // Look for the version as a key inside the "versions" object.
                // A simple check: the exact version string appears as a JSON key.
                string escapedVersion = Regex.Escape(version);
                var versionRegex = new Regex($@"""{escapedVersion}""\s*:");
                return versionRegex.IsMatch(json);
            }
            catch
            {
                return false;
            }
        }

        private static string ReadLocalPackageJson(string packageId)
        {
            string path = Path.Combine(GitEmbedOperations.GetEmbedAbsolutePath(packageId), "package.json");
            if (!File.Exists(path))
                return null;
            return File.ReadAllText(path);
        }

        private static string ParsePackageJsonField(string json, string field)
        {
            var regex = new Regex($@"""{Regex.Escape(field)}""\s*:\s*""([^""\\]*)""");
            Match match = regex.Match(json);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static Task<string> WaitForPackRequestAsync(PackRequest request)
        {
            var tcs = new TaskCompletionSource<string>();

            void CheckCompletion()
            {
                if (!request.IsCompleted)
                    return;

                EditorApplication.update -= CheckCompletion;

                if (request.Status == StatusCode.Success)
                    tcs.SetResult(request.Result.tarballPath);
                else
                    tcs.SetException(new Exception(request.Error?.message ?? "Pack failed with unknown error"));
            }

            EditorApplication.update += CheckCompletion;
            return tcs.Task;
        }

        private static Task RunNpmAsync(string args)
        {
            var tcs = new TaskCompletionSource<bool>();

            bool isWindows = Application.platform == RuntimePlatform.WindowsEditor;
            string fileName = isWindows ? "cmd" : "npm";
            string processArgs = isWindows ? $"/c npm {args}" : args;

            var psi = new ProcessStartInfo(fileName, processArgs)
            {
                WorkingDirectory = GitEmbedOperations.GetProjectRoot(),
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            var proc = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true,
            };

            var stderr = new StringBuilder();
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
            proc.Exited += (_, __) =>
            {
                proc.WaitForExit();
                int code = proc.ExitCode;
                proc.Dispose();
                if (code == 0)
                    tcs.TrySetResult(true);
                else
                    tcs.TrySetException(new Exception($"npm {args} failed (exit {code}): {stderr}"));
            };

            proc.Start();
            proc.BeginErrorReadLine();
            return tcs.Task;
        }
    }
}
