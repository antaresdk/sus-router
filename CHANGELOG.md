# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
- A screen `OnBeforeEnter` that returns `false` now cancels the navigation at every level of the target: fresh and KeepAlive-reused levels of a nested chain, and KeepAlive single-level targets. `OnBeforeRouteUpdate` returning `false` on a reused shared-prefix screen cancels it too. Previously only a fresh single-level screen was honoured and the navigation committed anyway.
- A cancelled navigation has no side effects: `Props` of reused screens are restored, no `OnLeft`, no `Entered`, KeepAlive caches stay intact.

### Notes
- Behavior change to check in your screens: a nested or KeepAlive `OnBeforeEnter` / `OnBeforeRouteUpdate` that returned `false` by accident (and was ignored before) now blocks the navigation.

## [1.0.21] - 2026-10-11

### Changed
- `OnLeft` now runs once per leave, leaf to root, while the screen is still in the tree (`panel` and `parent` are live), after `BeforeResolve` and after the target's `BeforeEnter`, i.e. after the last point where the navigation can still be cancelled. Previously `OnLeft` could run before those guards and the chain root could be detached before the leaf's `OnLeft`. A cancelled navigation no longer calls `OnLeft` on any screen.
- `OnLeaving` / `Leaving` documentation updated: `OnLeaving` runs before `OnLeft`; both run in the tree on a leave.

### Fixed
- `OnLeft` was not called when leaving a nested chain for a single-level route, and when leaving a KeepAlive leaf of a nested chain. It is now called on both paths.
- `KeepAlive` is now applied per level of a nested chain: a kept level is cached in its parent's `ChildView` (the same instance returns on re-entry, `Entered` fires again) instead of being lost without `OnLeft`.
- A leaving non-KeepAlive parent clears its `ChildView` KeepAlive cache first, so cached children receive `OnLeft` (leaf to root).

### Notes
- Behavior change to check in your screens: `OnLeft` cleanup that relied on running early (before guards) or outside the tree should move accordingly. Screens moved into the KeepAlive cache do not get `OnLeft` on leave; on KeepAlive eviction `OnLeft` runs outside the tree (`panel` is null).

## [1.0.20] - 2026-09-25

### Added
- `SusScreen.Leaving(SusRoute toRoute)` and `protected virtual void OnLeaving(SusRoute toRoute)`: called once per leave for every screen that leaves the active route chain (removed from the tree or moved into the KeepAlive cache), after all leave/enter guards and before any screen is detached, in leaf-to-root order, so `parent` and `panel` are still live. Use it to undo decorations a nested screen applied to its shell. Not called for screens that stay (shared nested-chain prefix, same-record props update) or on KeepAlive eviction (T-4246).
- `AdvancedRouting` sample: `SettingsScreen` now hosts a `ChildView`, and its nested panes set the shell caption in `OnEntered` and clear it in `OnLeaving`.

### Notes
- `Left` / `OnLeft` timing is unchanged. The order of `OnLeaving` against `OnLeft` is not part of the contract.
- A `BeforeResolve` guard runs after `OnLeaving`; if it aborts the navigation, the screen stays active although `OnLeaving` was already called.

### Known issues
- `OnLeft` is not called on some paths: leaving a nested chain for a single-level route, and leaving a KeepAlive leaf of a nested chain (that leaf is also not cached). When the chain root changes, and on KeepAlive eviction, `OnLeft` runs after the screen is detached (`panel` is null). Use `OnLeaving` for cleanup that needs the live tree. Tracked as T-4260.

## [1.0.19] - 2026-09-23

### Changed
- `SusRouter.cs` split into partial-class files (`SusRouter.NavigateCore.cs`, `SusRouter.Navigation.cs`, `SusRouter.Async.cs`, `SusRouterTypes.cs`); pure code move, no API or behavior change (T-3599).

## [1.0.18] - 2026-09-11

### Changed
- Install URLs restamped for sus-core v1.1.0; no runtime or API change.

## [1.0.17] - 2026-09-11

### Changed
- Test assemblies declare `overrideReferences: true` and reference the Unity Test Runner assemblies explicitly, so the package compiles the same way inside a consuming project as it does in the development project (T-3224).
- Four remaining inline `.style` writes converted to USS classes / marker classes, per the styles-from-code rule (T-2982, campaign T-2973).
- `SusModalService` reads `modal.ResolvedLayer` instead of the `OverlayCategory.Modal` literal (T-2826).

### Fixed
- `SusRouterModal` XML documentation states the self-mount ban for the family and what "open" means (T-2827).
- Package docs: styles-from-code rule refreshed (T-3008); leading UTF-8 BOM stripped from `docs/03,04,05,10,12` (T-2605); install URLs restamped for `sus-core` v1.0.31.

## [1.0.16] - 2026-08-26

### Changed
- Samples~ (FullDemo, routing demos) migrate `Debug.Log*` to `SusLog` per ARCH-SUS-LOG §3 (T-2016).

## [1.0.15] - 2026-08-21

### Added
- `SusTransitionService` on SusMotion — route curtain fades use `SusMotion` / `SusEaseUtil`;
  `SusRouteTransition` via SusMotion presets (`KeywordNull` = no transition) (T-685, T-1121).
- `SusModalService.ShowAsync` completes with the modal result (T-683).
- Router focus lifecycle: `AutoFocus` on route enter, modal focus snapshot / trap / restore (T-677).

### Changed
- `SusModalLayer` dropped — modals go only through `SusModalService`; the scrim is a `modal-scrim`
  USS class instead of C# appearance (T-1121, T-1323); package docs tree and gap table follow (T-1471).

## [1.0.14] - 2026-08-20

### Fixed
- Async navigation re-entrancy: `NavigateAsync` now holds the `_isNavigating` guard across awaited
  guards, closing a from/to race when a second navigation started during an awaited guard (T-1105).

### Changed
- Internal refactor: six copies of the route-build sequence (Push/Replace/Named/Record/Async) merged
  into one `TryBuildToRoute`, `NavigateCore` split into private steps — no behavior change, 169 tests
  green before and after (T-1105).
- Docs stamps: install URLs pinned to core 1.0.23 / router 1.0.14.

## [1.0.13] - 2026-08-19

### Changed
- `Samples~` examples wrapped in the `Sharq.Router.Examples` namespace (Asset Store Validator
  "Type Namespaces") (T-659).
- Docs stamps: install URLs pinned to core 1.0.21 / router 1.0.13.

## [1.0.12] - 2026-08-19

### Changed
- Docs stamps: install URLs pinned to core 1.0.19 / router 1.0.12.

## [1.0.11] - 2026-08-18

### Changed
- README: first-touch section (requirements and scope bounds).
- Versioned hooks: `scripts~/pre-push` / `scripts~/prepare-commit-msg` carry the attribution
  guard (no AI trailers in the pushed range); docs re-stamped (sus-core 1.0.18).

## [1.0.10] - 2026-08-17

### Changed
- README: community links (Discord / Telegram).
- `scripts~/prepare-commit-msg` is the versioned commit-message hook (PowerShell copy removed);
  docs re-stamped (sus-core 1.0.17).

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
