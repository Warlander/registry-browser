using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Warlogic.RegistryBrowser
{
    public static class RegistryBrowserAPI
    {
        public static async Task EmbedAsync(string packageId, string repositoryUrl, string commitSha)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentException("Package ID cannot be empty.", nameof(packageId));
            }

            if (string.IsNullOrWhiteSpace(repositoryUrl))
            {
                throw new ArgumentException("Repository URL cannot be empty.", nameof(repositoryUrl));
            }

            if (string.IsNullOrWhiteSpace(commitSha))
            {
                throw new ArgumentException("Commit SHA cannot be empty.", nameof(commitSha));
            }

            await GitEmbedOperations.CloneAndCheckoutAsync(packageId, repositoryUrl, commitSha);
            PackageManifestEditor.AddOrUpdateDependency(packageId, $"file:Embeds/{packageId}");
        }

        public static async Task DeEmbedAsync(string packageId, string targetVersion)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentException("Package ID cannot be empty.", nameof(packageId));
            }

            if (string.IsNullOrWhiteSpace(targetVersion))
            {
                throw new ArgumentException("Target version cannot be empty.", nameof(targetVersion));
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

        public static async Task PublishAsync(string packageId, string registryUrl, bool confirmRepublish)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentException("Package ID cannot be empty.", nameof(packageId));
            }

            if (string.IsNullOrWhiteSpace(registryUrl))
            {
                throw new ArgumentException("Registry URL cannot be empty.", nameof(registryUrl));
            }

            IReadOnlyList<RegistryScope> registries = RegistryBrowserConfig.LoadRegistries();

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
    }
}
