# 6. Guards and transitions

## ISusRouteGuard — navigation protection

```csharp
public interface ISusRouteGuard
{
    bool CanEnter(SusRoute from, SusRoute to);
    bool CanLeave(SusRoute from, SusRoute to);
}
```

### Global guard (all navigations)

```csharp
router.BeforeEach((from, to) =>
{
    if (to.Record?.Config?.Meta?.ContainsKey("requiresAuth") == true
        && to.Record.Config.Meta["requiresAuth"] is true
        && !AuthService.IsLoggedIn)
    {
        router.Push("/login");
        return false;
    }
    return true;
});
```

### Per-route guard

```csharp
public class AdminGuard : ISusRouteGuard
{
    public bool CanEnter(SusRoute from, SusRoute to) => User.IsAdmin;
    public bool CanLeave(SusRoute from, SusRoute to) => true;
}

router.Register("/admin", typeof(AdminScreen), new SusRouteConfig
{
    Guard = new AdminGuard(),
    Meta = new() { ["requiresAuth"] = true, ["role"] = "admin" }
});
```

### BeforeResolve — before screen creation

```csharp
router.BeforeResolve((from, to) =>
{
    if (to.FullPath == "/battle/0") return false; // invalid id
    return true;
});
```

### Async guards (BeforeEachAsync / BeforeResolveAsync)

For checks that need `await` (server request, data load), async guards are available:

```csharp
router.BeforeEachAsync(async (from, to) =>
{
    var ok = await AuthService.CheckSessionAsync();
    return ok;
});

router.BeforeResolveAsync(async (from, to) =>
{
    return await DataService.PreloadAsync(to.FullPath);
});
```

Order on `PushAsync` / ` ReplaceAsync`:

1. `BeforeEachAsync` (all, sequentially, ` await`)
2. `BeforeResolveAsync` (all, sequentially, ` await`)
3. sync pipeline (`BeforeLeave` → ` BeforeEach` → ` CanEnter` → ` BeforeResolve` → screen creation)

> ⚠️ **Limitation:** sync navigation (`Push` / ` Replace` / ` Back` / ` Forward`) **does not run** async guards — they cannot be ` await`ed synchronously. If async guards are registered but sync ` Push`is called, they are **skipped** (Editor/Development builds log a `[GuardAudit]` warning). For routes with async checks, use `PushAsync` / ` ReplaceAsync`.

## SusRouteTransition — transition animations

### API

```csharp
public class SusRouteTransition
{
    public float Duration { get; }
    public static SusRouteTransition None();
    public static SusRouteTransition Fade(float duration = 0.2f);
    public static SusRouteTransition SlideLeft(float duration = 0.3f);
    public static SusRouteTransition SlideRight(float duration = 0.3f);

    public void PlayIn(VisualElement target);
    public void PlayOut(VisualElement target);
}
```

### Implementation (code-based)

Animations use `schedule.Execute` plus `style.opacity` / ` style.translate`manipulation:

```csharp
public static SusRouteTransition Fade(float d = 0.2f) => new(
    playIn: el => { /* opacity 0 → 1 over d seconds */ },
    playOut: el => { /* opacity 1 → 0 over d seconds */ },
    duration: d
);

public static SusRouteTransition SlideLeft(float d = 0.3f) => new(
    playIn: el => { /* translate X: 100px → 0 over d seconds */ },
    playOut: el => { /* translate X: 0 → -100px over d seconds */ },
    duration: d
);
```

### Usage

```csharp
new SusRouteConfig { Transition = SusRouteTransition.Fade(0.2f) }
new SusRouteConfig { Transition = SusRouteTransition.SlideLeft(0.3f) }
new SusRouteConfig { Transition = SusRouteTransition.SlideRight(0.3f) }
new SusRouteConfig { Transition = SusRouteTransition.None() }
```

Animations play in `SusRouteView.OnRouteChanged`: ` PlayOut`on the old screen, ` PlayIn`on the new one.
