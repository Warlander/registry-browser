using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Warlogic.RegistryBrowser
{
    public static class RegistryBrowserAPI
    {
        public static async Task EmbedAsync(string packageId, string repositoryUrl = null, string commitSha = null)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentException("Package ID cannot be empty.", nameof(packageId));
            }

            if (string.IsNullOrWhiteSpace(repositoryUrl))
            {
                repositoryUrl = await ResolveRepositoryUrlAsync(packageId);
            }

            if (string.IsNullOrWhiteSpace(commitSha))
            {
                commitSha = await ResolveLatestCommitShaAsync(repositoryUrl);
            }

            await GitEmbedOperations.CloneAndCheckoutAsync(packageId, repositoryUrl, commitSha);
            PackageManifestEditor.AddOrUpdateDependency(packageId, $"file:Embeds/{packageId}");
        }

        public static async Task DeEmbedAsync(string packageId, string targetVersion = null)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentException("Package ID cannot be empty.", nameof(packageId));
            }

            if (string.IsNullOrWhiteSpace(targetVersion))
            {
                targetVersion = await ResolveLatestVersionAsync(packageId);
            }

            if (GitEmbedOperations.IsEmbedDirectoryInUse(packageId, out string lockedFile))
            {
                throw new InvalidOperationException(
                    $"Cannot de-embed: the package directory has locked files ({Path.GetFileName(lockedFile)}). " +
                    "Close any applications accessing the package and try again.");
            }

            if (GitEmbedOperations.HasGitRepo(packageId))
            {
                bool hasChanges = await GitEmbedOperations.EmbedHasChangesAsync(packageId);
                if (hasChanges)
                {
                    throw new InvalidOperationException(
                        "Cannot de-embed: the package has uncommitted local changes. " +
                        "Commit or discard changes before de-embedding.");
                }
            }

            await GitEmbedOperations.RemoveEmbedAsync(packageId);
            PackageManifestEditor.SetRegistryVersion(packageId, targetVersion);
        }

        public static async Task PublishAsync(string packageId, string registryUrl = null, bool confirmRepublish = false)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentException("Package ID cannot be empty.", nameof(packageId));
            }

            IReadOnlyList<RegistryScope> registries = RegistryBrowserConfig.LoadRegistries();

            if (string.IsNullOrWhiteSpace(registryUrl))
            {
                registryUrl = ResolveRegistryUrl(packageId, registries);
                if (string.IsNullOrWhiteSpace(registryUrl))
                {
                    throw new InvalidOperationException(
                        "Cannot publish: no registry URL provided and no configured registry matches this package's scope prefix.");
                }
            }

            bool isCurrentlyEmbedded = GitEmbedOperations.IsEmbedded(packageId);

            var apiClient = new RegistryApiClient();
            PackageDetails details = await apiClient.FetchPackageDetailsAsync(packageId, registryUrl);

            PublishPreflightResult preflight = await PackagePublishOperations.RunPreflightAsync(
                packageId, details, registries);

            if (!preflight.CanPublish)
            {
                throw new InvalidOperationException(preflight.ErrorMessage);
            }

            if (preflight.IsRepublish && !confirmRepublish)
            {
                throw new InvalidOperationException(
                    $"Version {preflight.LocalVersion} already exists on the registry. " +
                    "Set confirm_republish to true to unpublish the existing version and publish the new one.");
            }

            if (preflight.IsRepublish)
            {
                await PackagePublishOperations.NpmUnpublishAsync(packageId, preflight.LocalVersion, registryUrl);
            }

            string tarballPath = await PackagePublishOperations.PackAsync(packageId);
            await PackagePublishOperations.NpmPublishAsync(tarballPath, registryUrl);

            if (isCurrentlyEmbedded)
            {
                await GitEmbedOperations.RemoveEmbedAsync(packageId);
                PackageManifestEditor.SetRegistryVersion(packageId, preflight.LocalVersion);
            }
        }

        public static async Task CreatePackageAsync(string packageId, string displayName, bool initGit)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentException("Package ID cannot be empty.", nameof(packageId));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
            }

            if (GitEmbedOperations.IsEmbedded(packageId))
            {
                throw new InvalidOperationException(
                    $"A package directory for '{packageId}' already exists in Packages/Embeds/.");
            }

            await LocalPackageCreator.CreatePackageAsync(packageId, displayName, initGit);
        }

        private static async Task<string> ResolveRepositoryUrlAsync(string packageId)
        {
            IReadOnlyList<RegistryScope> registries = RegistryBrowserConfig.LoadRegistries();
            string registryUrl = ResolveRegistryUrl(packageId, registries);
            if (string.IsNullOrWhiteSpace(registryUrl))
            {
                throw new InvalidOperationException(
                    "Cannot embed: no repository URL provided and no configured registry matches this package's scope prefix.");
            }

            var apiClient = new RegistryApiClient();
            PackageDetails details = await apiClient.FetchPackageDetailsAsync(packageId, registryUrl);

            if (string.IsNullOrWhiteSpace(details.RepositoryUrl))
            {
                throw new InvalidOperationException(
                    "Cannot embed: package details do not contain a repository URL.");
            }

            return details.RepositoryUrl;
        }

        private static async Task<string> ResolveLatestCommitShaAsync(string repositoryUrl)
        {
            var apiClient = new RegistryApiClient();
            IReadOnlyList<CommitInfo> commits = await apiClient.FetchCommitsAsync(repositoryUrl, count: 1);

            if (commits == null || commits.Count == 0)
            {
                throw new InvalidOperationException(
                    "Cannot embed: failed to resolve latest commit from repository.");
            }

            return commits[0].Sha;
        }

        private static async Task<string> ResolveLatestVersionAsync(string packageId)
        {
            IReadOnlyList<RegistryScope> registries = RegistryBrowserConfig.LoadRegistries();
            string registryUrl = ResolveRegistryUrl(packageId, registries);
            if (string.IsNullOrWhiteSpace(registryUrl))
            {
                throw new InvalidOperationException(
                    "Cannot de-embed: no target version provided and no configured registry matches this package's scope prefix.");
            }

            var apiClient = new RegistryApiClient();
            PackageDetails details = await apiClient.FetchPackageDetailsAsync(packageId, registryUrl);

            if (string.IsNullOrWhiteSpace(details.LatestVersion))
            {
                throw new InvalidOperationException(
                    "Cannot de-embed: failed to resolve latest version from registry.");
            }

            return details.LatestVersion;
        }

        private static string ResolveRegistryUrl(string packageId, IReadOnlyList<RegistryScope> registries)
        {
            foreach (RegistryScope registry in registries)
            {
                if (!string.IsNullOrEmpty(registry.Scope) && packageId.StartsWith(registry.Scope))
                {
                    return registry.RegistryUrl;
                }
            }

            return null;
        }
    }
}
