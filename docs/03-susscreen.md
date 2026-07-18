# 3. SusScreen — screens, lifecycle, and wiring

A screen (`SusScreen`) is a full-screen view managed by ` SusRouter`.
It inherits `SusComponent`, so it has the full core reactive toolkit
(`Prop<T>`, ` Watch`, ` Build()`, ` Mounted()`), plus router lifecycle and access
to route parameters.

---

## 1. Minimal screen

```csharp
using UnityEngine.UIElements;
using Sharq.Router;

public class HomeScreen : SusScreen
{
    protected override void Build()
    {
        style.flexGrow = 1f;
        Add(new Label("Home"));

        var btn = new SusButton();      // sus-kit component
        btn.Text.Value = "Go to About";
        btn.RegisterCallback<ClickEvent>(_ => Router.Push("/about"));
        Add(btn);
    }
}
```

`Build()` builds the tree (from `SusComponent`). ` Router`and ` Props`are
**not** set yet in `Build()` — read them from ` OnBeforeEnter()` / ` OnEntered()`.

---

## 2. Lifecycle — override `On*` methods

> ⚠️ Important: public `BeforeEnter/Entered/BeforeLeave/Left/BeforeRouteUpdate`
> are called by **the router itself** — do not override them. Override the
> **`protected virtual` ` On*`hooks** (template-method pattern).

```csharp
public class BattleScreen : SusScreen
{
    // 1. Enter validation + reading Props. false = enter cancelled.
    protected override bool OnBeforeEnter(SusRoute fromRoute)
    {
        var matchId = GetParam("matchId");
        return !string.IsNullOrEmpty(matchId);
    }

    // 2. Screen is already in the DOM — start animations, load data.
    protected override void OnEntered() => StartBattle();

    // 3. Props changed on the SAME screen instance (e.g. /users/1 → /users/2).
    protected override bool OnBeforeRouteUpdate(SusRoute toRoute) => true;

    // 4. Leave guard. false = navigation blocked (e.g. dirty form).
    protected override bool OnBeforeLeave(SusRoute toRoute)
    {
        if (_isDirty)
        {
            Router.Modal(typeof(ConfirmLeaveDialog), new() { ["message"] = "Leave without saving?" });
            return false;
        }
        return true;
    }

    // 5. Screen is being removed — unsubscribe, clean up resources.
    protected override void OnLeft() => CleanupBattle();
}
```

### Full sequence

```
Push("/battle/42"):
  1. Activator.CreateInstance / LazyFactory()
       Created()              ← SusComponent; Router/Props NOT set yet
       Build()                ← tree is built
  2. screen.Router = router
  3. screen.Props = params + query + DefaultProps + PropsFn(...)
  4. OnBeforeEnter(from)      ← false = cancel entire navigation
  5. history updates, SusRouteView swaps screen + transition.PlayIn()
  6. OnEntered()             ← screen in DOM
  7. (next frame) Mounted()   ← deferred SusComponent hook

Leave:
  8. OnBeforeLeave(to)       ← guard, false = cancel
  9. OnLeft()
 10. SusRouteView removes screen (or hides on KeepAlive)
 11. Unmounted()            ← SusComponent; NOT called on KeepAlive
```

With **KeepAlive** the screen is not recreated: `OnLeft()` runs, but the instance
is hidden (not destroyed); on return — `OnBeforeEnter()` / ` OnEntered()`again
without a new `Build()`.

---

## 3. Accessing route data

`SusRouter` merges **path parameters** (`:id`) and **query** (`?tab=x`) into one
`Props` dictionary. Read via helpers:

```csharp
// route "/users/:id",  URL "/users/42?tab=profile"
string id  = GetParam("id");          // "42"      (path param)
string tab = GetQuery("tab");         // "profile" (query)
int    page = GetProp("page", 1);     // typed, with default
object raw = GetProp("payload");      // untyped
```

| Member | Type | Purpose |
|---|---|---|
| `Router` | ` SusRouter` | screen owner (navigation, modals) |
| `Props` | ` Dictionary<string,object>` | params + query + passed props (never null) |
| `IsActive` | ` bool` | whether the screen is active now |
| `GetProp<T>(key, def)` | ` T` | typed read with conversion |
| `GetParam(key, def)` / ` GetQuery(key, def)` | ` string` | aliases into ` Props` (params/query already merged) |

---

## 4. Wiring a screen — three ways

### 4.1 Imperative — `router.Register(path, type, config?)`

```csharp
var router = new SusRouter();
router.Register("/", typeof(HomeScreen));
router.Register("/about", typeof(AboutScreen));
router.Register("/users/:id", typeof(UserScreen),
    new SusRouteConfig { Name = "user", KeepAlive = true });
router.Mount(root, "/");     // or Mount(uiDocument, "/")
```

`Register` returns `SusRouteRecord` — it can be nested under a parent's
`Children` (see §5, nested).

### 4.2 Declarative — `SusRouteBuilder` via `SusApp.UseRouter`

```csharp
SusApp.Create(uiDocument)
      .UseTheme(SusTheme.Dark)
      .UseRouter(new SusRouter(), routes => routes
          .Route("/", typeof(HomeScreen)).Name("home")
          .Route<LoginScreen>("/login").Alias("/signin")
          .Route("/users/:id", typeof(UserScreen))
              .Name("user").KeepAlive().Meta("requiresAuth", true)
              .Children(c => c
                  .Route("profile", typeof(ProfileScreen))
                  .Route("posts", typeof(PostsScreen))),
          initialPath: "/")
      .Run();
```

### 4.3 Imperative `UseRouter` (Register inside ` SusApp`)

```csharp
SusApp.Create(uiDocument)
      .UseRouter(new SusRouter(), r =>
      {
          r.Register("/", typeof(HomeScreen));
          r.Register("/settings", typeof(SettingsScreen));
      }, initialPath: "/")
      .Run();
```

`UseRouter` embeds registration + `Mount` at the correct `SusApp` finalization
point (after token cascade / custom styles, before theme apply).

---

## 5. All `SusRouteConfig` options

| Option | Type | What it does |
|---|---|---|
| `Name` | ` string` | name for ` PushNamed`/` ReplaceNamed` |
| `KeepAlive` | ` bool` | do not recreate screen on leave (instance cache) |
| `Alias` | ` List<string>` | extra paths that resolve to this route |
| `Children` | ` List<SusRouteRecord>` | nested routes (one level) |
| `Redirect` | ` string` | on enter — navigate here instead of this route |
| `DefaultProps` | ` Dictionary<string,object>` | default props for the screen |
| `PropsFn` | ` Func<SusRoute, Dictionary<string,object>>` | props generator from the route (Vue analog ` props: route => ({...})`) |
| `LazyFactory` | ` Func<SusScreen>` | lazy screen creation (instead of ` Activator.CreateInstance`) |
| `Guard` | ` ISusRouteGuard` | per-route guard ` CanEnter`/` CanLeave` |
| `BeforeEnter` | ` SusRouterGuard` | functional enter guard (after ` Guard.CanEnter`) |
| `Transition` | ` SusRouteTransition` | transition animation (` Fade()`, ` SlideLeft()`, …) |
| `Meta` | ` Dictionary<string,object>` | arbitrary metadata (` requiresAuth`, ` title`) |
| `CaseSensitive` | ` bool` | case-sensitive path matching (default off) |
| `Strict` | ` bool` | trailing slash matters (`/a` ≠ `/a/`) |

### Examples for each option

```csharp
// KeepAlive + Transition
router.Register("/feed", typeof(FeedScreen),
    new SusRouteConfig { KeepAlive = true, Transition = SusRouteTransition.Fade() });

// Named + params → PushNamed
router.Register("/users/:id", typeof(UserScreen), new SusRouteConfig { Name = "user" });
router.PushNamed("user", new() { ["id"] = "42" });

// Alias + Redirect
router.Register("/home", typeof(HomeScreen), new SusRouteConfig { Alias = new() { "/start" } });
router.Register("/old", typeof(HomeScreen), new SusRouteConfig { Redirect = "/home" });

// DefaultProps + PropsFn
router.Register("/report", typeof(ReportScreen), new SusRouteConfig
{
    DefaultProps = new() { ["format"] = "pdf" },
    PropsFn = route => new() { ["id"] = route.Params.GetValueOrDefault("id") },
});

// Lazy + Meta + Guard
router.Register("/admin", typeof(AdminScreen), new SusRouteConfig
{
    LazyFactory = () => new AdminScreen(),
    Meta = new() { ["requiresAuth"] = true },
    BeforeEnter = (from, to) => Session.IsAdmin,
});

// Nested (Children) via Register records
var users = router.Register("/users", typeof(UsersScreen), new SusRouteConfig
{
    Children = new()
    {
        router.Register("/users/:id", typeof(UserDetailScreen)),
        router.Register("/users/search", typeof(UserSearchScreen)),
    }
});
```

---

## 6. Navigation (`SusRouter` methods)

| Method | Purpose |
|---|---|
| `Push(path, props?)` | push onto history and navigate |
| `Replace(path, props?)` | replace current entry |
| `PushNamed(name, pathParams?, props?)` | navigate by route name |
| `ReplaceNamed(name, pathParams?, props?)` | replace by name |
| `Back()` / ` Forward()` | history (cursor-based) |
| `Go(n)` | offset by ` n`steps |
| `NavigateWithTransition(path, …)` | navigate with an explicit animation |
| `CanGoBack` / ` CanGoForward` | history availability (for buttons) |
| `Modal(type, props?)` / ` CloseModal()` | modals via ` ModalService` |

All `Push*/Replace*/Back/Forward/Go` return `NavigationResult` (success /
guard abort / redirect).

```csharp
if (Router.CanGoBack) Router.Back();
Router.Push("/checkout", new() { ["cartId"] = cart.Id });
Router.Modal(typeof(InfoDialog), new() { ["text"] = "Done!" });
```

---

## 7. Nested screens

The parent screen registers a child `SusRouteView` where the router mounts
child screens:

```csharp
public class UsersScreen : SusScreen
{
    protected override void Build()
    {
        Add(new Label("Users"));

        var childView = new SusRouteView();
        RegisterChildView(childView);   // router mounts /users/:id etc. here
        Add(childView);
    }
}
```

`ChildView` (first) and ` ChildViews` (all) are available from code. Chain
resolving picks the `MatchedChain` of records from root to leaf.

---

## 8. Entry point (MonoBehaviour)

```csharp
[RequireComponent(typeof(UIDocument))]
public class AppEntry : MonoBehaviour
{
    [SerializeField] private UIDocument _uiDocument;

    private void OnEnable()
    {
        // Create(UIDocument) applies SusDefault.tss to the panel; prefer it over
        // Create(rootVisualElement), which skips TSS.
        SusApp.Create(_uiDocument)
              .UseTheme(SusTheme.Dark)
              .UseRouter(new SusRouter(), routes => routes
                  .Route("/main-menu", typeof(MainMenuScreen)).Name("menu")
                  .Route("/battle/:matchId", typeof(BattleScreen))
                      .KeepAlive().Transition(SusRouteTransition.Fade()),
                  initialPath: "/main-menu")
              .Run();
    }
}
```

---

## Integration with SusComponent

- `OnEntered()` is called **before** `Mounted()`. Put logic that needs children
  not present at `Build()` time into `Mounted()`.
- `Router`/` Props`are available from ` OnBeforeEnter()`, but **not** in ` Created()`/` Build()`.
- `Unmounted()` — on detach from the panel (after ` OnLeft()`); with KeepAlive
  the screen is hidden and `Unmounted()` is not called.

## Related docs

- [02-router-api.md](./02-router-api.md) — full `SusRouter` API
- [04-routeview.md](./04-routeview.md) — `SusRouteView` and KeepAlive
- [05-modals.md](./05-modals.md) — modals
- [06-guards-transitions.md](./06-guards-transitions.md) — guard pipeline and animations
