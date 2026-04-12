# Registry Browser

Editor tool for browsing, installing, updating, and embedding packages from UPM scoped registries directly within the Unity Editor.

The main goal of this tool is to enable faster working with packages - Unity's built-in package manager doesn't offer any functionality to embed and work on packages, this tool makes it very easy and automated process.

# Installation

## Via Git URL

Open **Window → Package Manager**, click **+**, and choose **Add package from git URL**.

To install the latest version:
```
https://github.com/Warlander/registry-browser.git
```

To install a specific release, append the tag:
```
https://github.com/Warlander/registry-browser.git#1.1.0
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
    "com.warlogic.registrybrowser": "1.1.0"
  }
}
```

Alternatively, open **Window → Package Manager**, click **+**, choose
**Add package by name**, and enter `com.warlogic.registrybrowser`.

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
