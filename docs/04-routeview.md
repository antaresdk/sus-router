# 4. SusRouteView — visual container

## API

`SusRouteView` is a subclass of the core primitive `SusScreenOutlet<SusScreen>` — the
navigation-agnostic screens-layer container. It only adds route-aware logic; the mounting +
KeepAlive (LRU) plumbing lives in core.

```csharp
public class SusRouteView : SusScreenOutlet<SusScreen>
{
    public SusRouter Router { get; set; }

    // Inherited from SusScreenOutlet<SusScreen>:
    public int MaxKeepAlive { get; set; } = 10;
    public SusScreen CurrentScreen { get; protected set; }

    public void OnRouteChanged(SusRoute fromRoute, SusRoute toRoute);
    public void RenderNestedChain(List<SusScreen> chainScreens);
}
```

## Flow

```
SusRouter.Navigate(from, to)
      |
SusRouteView.OnRouteChanged(from, to):
    1. if !KeepAlive → from.Screen.parent.Remove(from.Screen)   ← detach + drop
       if  KeepAlive → Remove(from.Screen) + CacheKeepAliveScreen(key, screen)  ← detach + cache
    2. Add(to.Screen)                                           ← add new/cached screen
    3. to.Screen.style.flexGrow = 1; CurrentScreen = to.Screen
```

Transitions (`PlayIn`/`PlayOut`) are driven by `SusRouter` around this swap.

## KeepAlive — off-DOM LRU cache

```csharp
router.Register("/battle/:id", typeof(BattleScreen), new SusRouteConfig
{
    KeepAlive = true,
    Transition = SusRouteTransition.Fade(0.2f)
});
```

### How it works

KeepAlive is an **off-DOM cache**, not a wrapper element — the screen is **detached** from the
outlet (`parent.Remove(screen)`), **not** hidden with `display:none`, and **not** wrapped in a
core `SusKeepAlive` component.

1. **On leave** from a KeepAlive screen: `OnRouteChanged` removes the screen from the outlet and
   calls `CacheKeepAliveScreen(key, screen)`. The detached VisualElement subtree and its `Prop<T>`
   state are preserved in memory. `fromRoute.Screen` is cleared and `IsActive = false`.
2. **On return:** the router asks `TryGetKeepAliveScreen(key, out screen)`; on a hit the cached
   instance is re-added to the outlet — **no new instance, no `Left()`**. The key is
   `SusRouter.KeepAliveKey(route)` (fallback `route.FullPath`, incl. query parameters).
3. **In the router:** if the target KeepAlive route already has a live cached screen, it reuses it
   instead of constructing a new one.

### LRU eviction

Cache and order live in `SusScreenOutlet<TScreen>`:

```csharp
private readonly Dictionary<string, TScreen> _keepAliveCache;
private readonly List<string> _keepAliveOrder; // LRU: front = oldest
public int MaxKeepAlive { get; set; } = 10;
```

When adding a new key would exceed `MaxKeepAlive`, the oldest entries are evicted first:

1. `_keepAliveOrder[0]` (oldest) is removed from the order list;
2. `OnScreenEvicted(oldScreen)` runs — `SusRouteView` overrides it to call `screen.Left()` (teardown);
3. the entry is dropped from `_keepAliveCache`.

`ClearKeepAliveCache()` evicts everything (running the teardown hook for each).

### Usage in App.sharq

```xml
<template>
<ui:VisualElement $MainElement class="app">
    <sus:SusRouteView ref="RouteView" />
</ui:VisualElement>
</template>

<script>
public SusRouter Router = new();
public SusRouteView RouteView;

public override void Mounted()
{
    Router.SetRouteView(RouteView);
    RouteView.Router = Router;

    Router.Register("/main-menu", typeof(MainMenuScreen));
    Router.Register("/battle", typeof(BattleHudScreen),
        new SusRouteConfig { KeepAlive = true });
    Router.Register("/loading", typeof(LoadingScreen));

    Router.Replace("/loading");
}
</script>
```

## Nested routes — nested render

Each parent screen can register a nested `SusRouteView`:

```csharp
public class SettingsScreen : SusScreen
{
    public SusRouteView ChildRouterView;

    protected override void Build()
    {
        ChildRouterView = new SusRouteView();
        RegisterChildView(ChildRouterView);
        Add(ChildRouterView);
    }
}
```

When navigating to `/settings/profile`:
1. `ResolveChain()` builds the chain: SettingsRecord → ProfileRecord
2. Root `SusRouteView` renders `SettingsScreen`
3. `SettingsScreen.ChildRouterView` renders `ProfileTabScreen`

### Limitation

When the child route changes (`/settings/profile` → `/settings/account`) the parent is **recreated**. Chain-level diffing is planned for the future.
