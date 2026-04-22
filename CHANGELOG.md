# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.5.0] - 2026-04-23

### Added
- Added support for display of implicit package references. Packages that are installed as transitive dependencies of other packages (but not directly referenced in the project manifest) now display with a `#` indicator and can be explicitly added or embedded.

## [1.4.0] - 2026-04-19

### Changed
- Updated `com.warlogic.utils.markdown` dependency to 1.2.0. Explicitly reference the new runtime assembly (`Warlogic.Utils.Markdown`) in addition to the editor assembly, required after the markdown package split its types into separate runtime and editor assemblies.

## [1.3.0] - 2026-04-14

### Added
- De-embed and publish operations now check whether the target directory is in use before proceeding, preventing errors on Windows when files are locked.

### Changed
- Changelog rendering now uses the `com.warlogic.utils.markdown` package instead of the browser's own internal Markdown logic.
- Updated `com.warlogic.utils.markdown` dependency to 1.1.0.

## [1.2.0] - 2026-04-12

### Changed
- Publishing an embedded package now automatically de-embeds it, installing the freshly-published registry version so the project stays on a released copy.
- Package action buttons are now visually grouped with a distinct background and border, making them easier to identify.
- Package details begin loading immediately when a package is selected, reducing perceived wait time.

## [1.1.0] - 2026-04-12

### Added
- It's now possible to create and delete local packages.
- Optionally, Git repository can be initialized for said packages.
- It's now possible to publish local and embedded packages from the editor, with registry selection and republish support.
- Local-only packages now appear in the browser alongside registry packages.

### Changed
- Embedded and local Git-backed packages now show their current branch in the package list.
- Package details now show per-version changelog entries inside the browser.

### Fixed
- Embed workflows now keep package repositories on a branch instead of leaving them in detached `HEAD`.

## [1.0.0] - 2026-04-09

### This is the first release of *<Registry Browser>*.
