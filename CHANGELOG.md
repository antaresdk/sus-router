# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
