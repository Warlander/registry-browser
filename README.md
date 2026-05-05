# Registry Browser

Editor tool for browsing, installing, updating, and embedding packages from UPM scoped registries directly within the Unity Editor.

The main goal of this tool is to enable faster working with packages - Unity's built-in package manager doesn't offer any functionality to embed and work on packages, this tool makes it very easy and automated process.

<img width="912" height="598" alt="image" src="https://github.com/user-attachments/assets/763d5c9c-6032-4a2b-a53c-70f92fc964e2" />

# Installation

## Via Git URL

Open **Window → Package Manager**, click **+**, and choose **Add package from git URL**.

To install the latest version:
```
https://github.com/Warlander/registry-browser.git
```

To install a specific release, append the tag:
```
https://github.com/Warlander/registry-browser.git#1.6.0
```

## Via Scoped Registry

Add the Warlogic registry to your `Packages/manifest.json`:

```json
{
  "scopedRegistries": [
    {
      "name": "Warlogic",
      "url": "https://upm.maciejcyranowicz.com",
      "scopes": ["com.warlogic"]
    }
  ],
  "dependencies": {
    "com.warlogic.registrybrowser": "1.6.0"
  }
}
```

Then open **Window > Package Manager** and look for `com.warlogic.registrybrowser`.

# Prerequisites

- **Git** — must be installed and available on your system `PATH`. Used for all embed and de-embed operations (cloning repositories, checking out commits, checking dirty state). No specific version requirement.

## Publishing Tools

The following are only required if you intend to publish packages to a registry from within the tool. Basic browsing, installing, and embedding do not require them.

- **npm** — must be installed and available on `PATH`. On Windows it is invoked via `cmd /c npm`; on other platforms it is called directly.
- **npm registry authentication** — the target registry must already be authenticated in your `.npmrc` (e.g. via `npm login --registry <url>` or a pre-configured auth token). The tool passes `--registry <url>` to npm commands but does not manage authentication itself.

# Setup

1. Open **Edit > Project Settings > Registry Browser**.
2. Click **Add Registry** and enter a scope prefix (e.g. `com.warlogic`) and the registry URL (e.g. `https://upm.maciejcyranowicz.com`). Repeat for each registry you want to track.
3. **[Optional]** To automatically gitignore embedded packages, click **Add Packages/Embeds/ to .gitignore** in the same settings page (or allow the one-time prompt that appears on first use).

# Usage

Open the browser via **Window > Warlander > Registry Browser**.

- **Browse packages** — the left panel lists all packages found across configured registries. Installed packages show their current version and an update indicator if a newer version is available.
- **Install / Update / Remove** — select a package in the list and use the action buttons in the detail panel. A version selector lets you pick any published version.
- **Embed a package** — click **Embed** to clone the package repository into `Packages/Embeds/` at a selected git commit. Embedded packages can be edited directly and committed to your own repository.
- **De-embed a package** — click **De-embed** to remove the local clone and restore the registry-hosted version.

A pre-commit git hook is automatically installed to prevent accidentally committing with embedded packages still in place.
