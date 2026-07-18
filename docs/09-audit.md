# 9. Post-audit additions (1 July 2026)

All changes landed in sus-router v0.2.0.

## KeepAlive — screen caching

**Affects:** `SusRouteConfig`, ` SusRouteView`, ` SusRouter`

```csharp
router.Register("/battle/:id", typeof(BattleScreen), new SusRouteConfig
{
    KeepAlive = true,
    Transition = SusRouteTransition.Fade(0.2f)
});
```

1. **On leave from KeepAlive:** screen is **detached** from the DOM and stored in the outlet’s off-DOM LRU cache (`SusScreenOutlet` / ` SusRouteView`). ` Left()`is **not** called; instance + ` Prop<T>`state are preserved.
2. **On return:** looked up via `TryGetKeepAliveScreen(KeepAliveKey)`, re-attached; screen is NOT recreated.
3. **Not** core `SusKeepAlive` (display:none wrapper) — router uses an intentional off-DOM cache.

## v0.2.1 — post-audit fixes

- **KeepAlive** — off-DOM cache in `SusScreenOutlet` (sus-core), driven by ` SusRouteView`.
- **Service `OverlayHost`** — from ` internal`to ` public`.
- **`SusRouter.Mount()`** — auto-creates ` OverlayHost`via ` SusBootstrap.GetOrCreateOverlay()`.
- **Tests:** 10 new KeepAlive tests.

## v0.2.13 — audit and fixes (1 July 2026, evening)

Fixes from `roadmap/SUSROUTER_AUDIT_FIXES.md`:

### Real transition animations
`SusRouteTransition` now sets `transitionProperty`, ` transitionDuration`, ` transitionTimingFunction`on the element. The start value is applied immediately; the end value on the next frame so UITK can capture the start and interpolate. PlayOut is animated too. Transition properties are cleared after the animation finishes.

### Unified modal mechanism
`router.Modal()` / ` router.CloseModal()`delegate to ` SusModalService`instead of ` SusModalLayer`. ` SusModalLayer`is marked `[Obsolete]`. ` Mount()`no longer creates ` SusModalLayer`.

### Global guards on beforeRouteUpdate
The param-only navigation branch now runs `_beforeEachGuards` and `_beforeResolveGuards` (as in Vue Router).

### Re-entrancy protection
`_isNavigating` flag prevents nested `Navigate` calls. A concurrent navigation returns `NavigationResult.Busy` and is **dropped** — there is no `_pendingNavigation` queue.

### KeepAlive LRU eviction
`SusRouteView.MaxKeepAlive` (default 10). When the limit is exceeded — LRU eviction (remove the oldest inactive screen from the DOM, calling ` Left()`).

### [UxmlElement] + SusRouteLink.Text
`SusRouteLink` and `SusRouteView` are marked `[UxmlElement]`. ` SusRouteLink`got `[UxmlAttribute]` on `To`/` Exact`and a ` Text`property with an internal ` Label`.

### beforeResolve before screen creation
Order changed: `beforeResolve` now runs at step 5.5 — **before** `Activator.CreateInstance`, so an abort does not leave orphaned screens.

## Package layout (changes since v0.2.1+)

```
sus-router/Runtime/
├── SusRouter.cs              (+ auto-OverlayHost in Mount, KeepAlive logic)
├── SusRoute.cs               (+ KeepAlive, Name, Children, Redirect, Alias, LazyFactory, Query)
├── SusRouteView.cs           (extends SusScreenOutlet — off-DOM KeepAlive LRU)
├── SusRouteLink.cs           (new — router-link)
├── SusRouteTransition.cs     (unchanged)
├── SusModalService.cs        (OverlayHost: public, SusModalEntry)  ← Runtime root, not Services/
└── Services/
    └── SusTransitionService.cs (OverlayHost: public)
```
