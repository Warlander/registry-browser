# CLAUDE.md — com.warlogic.registrybrowser

Editor-only window for browsing, installing, and embedding UPM packages from scoped registries. "Embed" = clone repo into `Packages/Embeds/{packageId}` for in-tree development.

## File Responsibilities

- `RegistryBrowserWindow` — main `EditorWindow`, layout, coordinates panels, owns `LoadPackagesAsync()`
- `RegistryApiClient` — all I/O: UPM (`Client.SearchAll/List`), scoped registry REST, GitHub API (commits/changelogs)
- `PackageListPanel` / `PackageDetailPanel` — left/right panels; detail panel lazy-loads changelogs on foldout expand
- `GitEmbedOperations` — all `Process`-based git commands; `DeleteDirectoryForce()` strips read-only attrs (required on Windows for git pack files)
- `PackageManifestEditor` — regex-based (no JSON parser) read/write of `Packages/manifest.json`
- `LocalPackageCreator` — scaffolds full package tree with `.asmdef` and `.meta` files; `DeriveAssemblyBase()` skips TLD prefixes (`com`, `org`, etc.)
- `RegistryBrowserConfig` — reads/writes `ProjectSettings/RegistryBrowserConfig.json`; also a `SettingsProvider`
- `RegistryBrowserGitHookInstaller` — `[InitializeOnLoad]` auto-installs pre-commit hook blocking commits while any package is embedded
- `PackagePublishOperations` — validation, `Client.Pack`, and `npm publish`/`npm unpublish` via Process
- `PackagePublishWindow` — modal for confirming publish (version display, optional registry selector)
- `PackageVersionSelectorWindow` / `EmbedSelectorWindow` — modals for picking a version or git commit
- `PackageManagerWarningExtension` — `IPackageManagerExtension` warning banner in Unity's built-in Package Manager (cosmetic only, can be disabled)
- `PackageVersionComparator` — semver comparison returning `VersionUpdateLevel` (None/Patch/Minor/Major) for color-coding
- `LabelLoadingAnimation` — animated "Loading..." label via `EditorApplication.update`

## Key Patterns & Gotchas

- **Async bridging:** `PackageManager.Client` is callback-based; wrapped with `TaskCompletionSource` + `EditorApplication.update` polling — see `FetchPackagesAsync()` for the pattern
- **No JSON parser:** `RegistryApiClient` and `PackageManifestEditor` use regex for JSON field access; follow `ParsePackageJsonField()` helper when adding new fields
- **Cancellation:** `RegistryBrowserWindow` cancels and replaces a `CancellationTokenSource` on every selection change — replicate this for any new selection-triggered async flows
- **GitHub fallback:** `FetchCommitsAsync()` tries `master` then `main` branch
- **Pre-commit hook:** only reinstalled if content differs — change the string in `RegistryBrowserGitHookInstaller` when updating hook logic
- **UI:** UIElements only (`VisualElement` in C#, no UXML)
- **Config:** `ProjectSettings/RegistryBrowserConfig.json` (per-project, commit to VCS)
- **Organization ID:** Intentionally stored in `EditorPrefs` (per-user) rather than the project JSON config — see `RegistryBrowserConfig.LoadOrganizationId()`
