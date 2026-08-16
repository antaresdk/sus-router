# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.9] - 2026-08-16

### Fixed
- Sample asmdefs (all 7 samples) are `autoReferenced: false` — a project's `Assembly-CSharp` no
  longer implicitly references sample code.

### Changed
- Package push gate is versioned in `scripts~/pre-push` (docs check for this package + hard version
  bump), identical across the SUS packages; docs re-stamped (sus-core 1.0.16).

## [1.0.8] - 2026-08-13

### Fixed
- Samples: dangling `PanelSettings` reference repointed across all 7 samples.

### Changed
- Docs: translation/backtick artifacts fixed; version banners re-stamped (sus-core 1.0.15).

## [1.0.7] - 2026-08-12

### Changed
- Navigation/DeadRoute/Guard/Stack audits → `SusLog.Verbose`; concurrent drop / MaxModalDepth / Init stay Warn; OverlayHost null stays Error. Depends on sus-core ≥1.0.10.

## [1.0.6] - 2026-08-11

### Changed
- Samples rewritten on plain UI Toolkit controls - no external UI library required to compile and run them.

## [1.0.5] - 2026-08-11

### Changed
- Docs and sample descriptions: public-repo reference hygiene — external product references removed, sample guides reworded around plain UI Toolkit controls; gallery images removed.
- CI: tests workflow is manual-only (`workflow_dispatch`) until license secrets are configured.

## [1.0.4] - 2026-08-11

### Fixed
- `SusTransitionService`: fade uses fixed 16 ms ticks instead of wall-clock `unscaledTime` — transitions complete reliably in batchmode / `-nographics` where many frames run within milliseconds.

## [1.0.3] - 2026-08-02

### Changed
- Samples updated to mount the routing examples into the `SusApp` `ScreenHost` scaffold.

## [1.0.2] - 2026-07-19

### Fixed
- Move git hook tooling to `scripts~` so Unity AssetDatabase no longer imports them (GUID conflicts with core)

## [1.0.1] - 2026-07-19

### Fixed
- SusTransitionService: pause prior animation; avoid NRE when curtain is cleared mid-fade
- Transition tests use zero-duration fades for reliable completion

## [1.0.0] - 2026-07-18

### Added
- Initial public release (MIT, open-core with sus-core)
- Vue Router-like navigation: guards, query/params, nested/named routes, history, KeepAlive
- SusRouteView mounts into SusApp `ScreenHost` when present
- Samples, documentation, and tests
