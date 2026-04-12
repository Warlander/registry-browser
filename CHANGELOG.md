# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

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
