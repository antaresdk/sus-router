# 2. SusRouter — navigation core

## Prefer `SusApp.UseRouter`

Extension in `Runtime/SusAppRouterExtensions.cs` — registers routes and mounts at the correct ` SusApp`finalization point:

```csharp
SusApp.Create(doc)
    .UseTheme(SusTheme.Dark)
    .UseRouter(new SusRouter(), r =>
    {
        r.Register("/", typeof(HomeScreen));
        r.Register("/settings", typeof(SettingsScreen));
    }, initialPath: "/")
    .Run();
```

Declarative overload with `SusRouteBuilder`:

```csharp
SusApp.Create(doc)
    .UseRouter(new SusRouter(), routes => routes
        .Route("/", typeof(HomeScreen)).Name("home")
        .Route("/settings", typeof(SettingsScreen)),
        initialPath: "/")
    .Run();
```

## API

```csharp
public class SusRouter
{
    // Registration
    public SusRouteRecord Register(string path, Type screenType, SusRouteConfig config = null);
    public SusRouteRecord Resolve(string path);
    public List<SusRouteRecord> ResolveChain(string path);
    public bool HasRoute(string name);
    public bool RemoveRoute(string name);
    public IReadOnlyList<SusRouteRecord> Routes { get; }

    // Navigation
    public NavigationResult Push(string path, Dictionary<string, object> props = null);
    public NavigationResult Replace(string path, Dictionary<string, object> props = null);
    public NavigationResult Back();
    public NavigationResult Forward();
    public NavigationResult Go(int n);
    public void NavigateWithTransition(string path, float duration = 0.3f);

    // Named routes
    public NavigationResult PushNamed(string name, Dictionary<string, string> pathParams = null, Dictionary<string, object> props = null);
    public NavigationResult ReplaceNamed(string name, Dictionary<string, string> pathParams = null, Dictionary<string, object> props = null);
    public string ResolvePath(string name, Dictionary<string, string> pathParams = null);

    // Async navigation (runs BeforeEachAsync / BeforeResolveAsync, then sync pipeline)
    public Task<NavigationResult> PushAsync(string path, Dictionary<string, object> props = null);
    public Task<NavigationResult> ReplaceAsync(string path, Dictionary<string, object> props = null);

    // Guards
    public void BeforeEach(SusRouterGuard guard);
    public void AfterEach(SusRouterAfterHook hook);
    public void BeforeResolve(SusRouterGuard guard);
    public void BeforeEachAsync(SusRouterAsyncGuard guard);
    public void BeforeResolveAsync(SusRouterAsyncGuard guard);

    // State
    public Prop<SusRoute> CurrentRoute { get; }
    public bool CanGoBack { get; }
    public bool CanGoForward { get; }
    public IReadOnlyList<SusRoute> History { get; }
    public int HistoryIndex { get; }
    public int RouteCount { get; }
    public int MaxHistory { get; set; } = 100;   // 0 or less = unlimited
    public bool KeepAliveIgnoreQuery { get; set; } = false;
    public event Action<NavigationError> OnNavigationError;

    // Initialization
    public void Init(OverlayHost overlayHost);
    public NavigationResult Mount(VisualElement container, string initialPath, Dictionary<string, object> props = null);
    public NavigationResult Mount(UIDocument uiDocument, string initialPath, Dictionary<string, object> props = null);
}

public enum NavigationResult
{
    Success,
    Aborted,
    NotFound,
    CantGoBack,
    CantGoForward,
    Busy,   // concurrent navigation — request dropped (no queue)
}
```

## SusRouteConfig

```csharp
public class SusRouteConfig
{
    public string Name;                                          // PushNamed / ReplaceNamed
    public bool KeepAlive;                                       // Off-DOM instance cache (see KeepAlive)
    public List<string> Alias;                                   // Alternative paths
    public List<SusRouteRecord> Children;                        // Nested routes
    public string Redirect;                                      // Redirect target
    public Dictionary<string, object> DefaultProps;              // Default props
    public Func<SusRoute, Dictionary<string, object>> PropsFn;   // Functional props (Vue-style)
    public Func<SusScreen> LazyFactory;                          // Lazy creation
    public ISusRouteGuard Guard;                                 // Per-route CanEnter/CanLeave
    public SusRouterGuard BeforeEnter;                           // Function beforeEnter (after Guard.CanEnter)
    public SusRouteTransition Transition;                        // Animation
    public Dictionary<string, object> Meta;                      // Metadata
    public bool CaseSensitive;                                   // Default false
    public bool Strict;                                          // Trailing slash matters
}
```

## Navigation stack

Cursor-based history with `_historyIndex`. Cap via ` MaxHistory` (default **100**): on Push overflow, oldest entries are evicted. ` MaxHistory <= 0`means unlimited (dev builds warn on unbounded growth).

- Push(/battle) → [.../home, .../about, .../battle] idx=2
- Back() → [.../home, .../about] idx=1
- Forward() → [.../home, .../about, .../battle] idx=2
- Replace(/settings) → [.../home, .../settings] idx=1
- Push after Back truncates the forward tail

## Guard pipeline (10 steps)

Step 0:   no-op (from.Path == to.Path)
Step 0.5: beforeRouteUpdate
Step 1:   BeforeLeave on the current screen
Step 2:   ISusRouteGuard.CanLeave
Step 3:   Global BeforeEach
Step 4:   ISusRouteGuard.CanEnter + SusRouteConfig.BeforeEnter
Step 5:   Left()
Step 5.5: BeforeResolve (BEFORE screen creation)
Step 6:   Create / KeepAlive reuse + BeforeEnter (screen)
Step 7:   Stack update
Step 8:   OnRouteChanged
Step 9:   Entered()
Step 10:  CurrentRoute + AfterEach

### Re-entrancy — Busy drop (no pending queue)

While a navigation is in progress (`_isNavigating`), a concurrent ` Push` / ` Replace` / ` Back` / ` Forward` **returns ` NavigationResult.Busy`and is dropped**. There is **no** `_pendingNavigation` queue.

Callers (e.g. tab handlers) should not auto-retry; resync UI to `CurrentRoute` after the active navigation completes.

```csharp
var result = router.Push("/settings");
if (result == NavigationResult.Busy)
{
    // Dropped — another navigation is in flight
}
```

### Async guards

`BeforeEachAsync` / ` BeforeResolveAsync`run only on ` PushAsync` / ` ReplaceAsync`. Sync ` Push`/` Replace`/` Back`/` Forward`skip them (dev builds warn). Async guards are awaited first, then the sync pipeline runs.

## Named routes

```csharp
router.Register("/battle/:id", typeof(BattleScreen), new SusRouteConfig
{
    Name = "battle",
    Transition = SusRouteTransition.SlideLeft()
});
router.PushNamed("battle", new() { ["id"] = "42" }, new() { ["mode"] = "ranked" });
router.ReplaceNamed("battle", new() { ["id"] = "99" });

router.HasRoute("battle");   // true
router.RemoveRoute("battle"); // also clears aliases + child routes
```

## Nested routes

```csharp
router.Register("/settings", typeof(SettingsScreen), new SusRouteConfig
{
    Children = new List<SusRouteRecord>
    {
        new SusRouteRecord("profile", typeof(ProfileScreen)),
        new SusRouteRecord("privacy", typeof(PrivacyScreen)),
    }
});
```

## Redirect, Alias, Query, Lazy

```csharp
// Redirect
router.Register("/old-menu", typeof(MenuScreen), new SusRouteConfig { Redirect = "/main-menu" });
// Alias
router.Register("/main-menu", typeof(MenuScreen), new SusRouteConfig { Alias = new() { "/menu" } });
// Query
router.Push("/search?q=vue&page=2"); // CurrentRoute.Value.Query["q"] == "vue"
// Lazy
router.Register("/lazy", null, new SusRouteConfig { LazyFactory = () => new MyScreen() });
```

## KeepAlive (router off-DOM cache)

`SusRouteConfig.KeepAlive = true` caches the screen instance in `SusRouteView` / ` SusScreenOutlet` **off the DOM** (detach → cache → re-attach on return). This is **not** core ` SusKeepAlive` (display:none wrapper).

- Leave: DOM-detach → `Unmounted()`; ` Left()`is **not** called (instance stays alive).
- Return: retrieve from cache → `Mounted()` + ` Entered()`.
- LRU eviction / `ClearKeepAliveCache`: ` OnScreenEvicted` → ` Left()`.
- Cache key: `KeepAliveKey(route)` (` FullPath`, or path without query if ` KeepAliveIgnoreQuery`).
