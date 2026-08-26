# SUS Router —— 中文快速入门

> 本文件是 [README.zh-CN.md](../README.zh-CN.md) 的延伸，内容改编自 sus-ui.dev 官网中文指南
> （`docs/router/02-router-api.md`）。英文权威版见 [`docs/02-router-api.md`](./02-router-api.md)——
> 版本号、API 以那份英文文档为准，本文件由 release 角色随发行手动同步，不参与 `docs:loop`
> 自动打标。

**前置条件：** 先安装并跑通 `sus-core`（见产品网站
[快速入门](https://sus-ui.dev/docs/getting-started)）。
本包只负责导航，响应式/编译器/主题/overlay 都在 `sus-core`。

**适用引擎：** Unity 6000.3 及以上（全球版 Unity 6），与 `sus-core` 要求一致。 <!-- sus:ok -->

---

## 1. 安装

```
https://github.com/antaresdk/sus-router.git#v1.0.15 <!-- sus:ok -->
```

（权威版本号见英文 [README.md](../README.md) 顶部的自动生成区块。）

## 2. 优先使用 `SusApp.UseRouter`

该扩展位于 `Runtime/SusAppRouterExtensions.cs`——它会注册路由，并在 `SusApp` 完成
初始化的正确时机挂载：

```csharp
using Sharq.Core;
using Sharq.Router;

SusApp.Create(doc)
    .UseTheme(SusTheme.Dark)
    .UseRouter(new SusRouter(), r =>
    {
        r.Register("/", typeof(HomeScreen));
        r.Register("/settings", typeof(SettingsScreen));
    }, initialPath: "/")
    .Run();
```

也可以用 `SusRouteBuilder` 的声明式重载（支持嵌套、命名路由、KeepAlive 等）：

```csharp
SusApp.Create(doc)
    .UseRouter(new SusRouter(), routes => routes
        .Route("/", typeof(HomeScreen)).Name("home")
        .Route("/user/:id", typeof(UserScreen)).KeepAlive()
        .Route("/settings", typeof(SettingsLayout)).Children(c => c
            .Route("profile", typeof(ProfileScreen))),
        initialPath: "/")
    .Run();
```

## 3. `SusRouter` API 速查

```csharp
public class SusRouter
{
    // 注册
    public SusRouteRecord Register(string path, Type screenType, SusRouteConfig config = null);
    public SusRouteRecord Resolve(string path);
    public List<SusRouteRecord> ResolveChain(string path);
    public bool HasRoute(string name);
    public bool RemoveRoute(string name);
    public IReadOnlyList<SusRouteRecord> Routes { get; }

    // 导航
    public NavigationResult Push(string path, Dictionary<string, object> props = null);
    public NavigationResult Replace(string path, Dictionary<string, object> props = null);
    public NavigationResult Back();
    public NavigationResult Forward();
    public NavigationResult Go(int n);
    public void NavigateWithTransition(string path, float duration = 0.3f);

    // 命名路由
    public NavigationResult PushNamed(string name, Dictionary<string, string> pathParams = null, Dictionary<string, object> props = null);
    public NavigationResult ReplaceNamed(string name, Dictionary<string, string> pathParams = null, Dictionary<string, object> props = null);
    public string ResolvePath(string name, Dictionary<string, string> pathParams = null);

    // 异步导航（先跑 BeforeEachAsync / BeforeResolveAsync，再跑同步管线）
    public Task<NavigationResult> PushAsync(string path, Dictionary<string, object> props = null);
}
```

完整 API（含守卫钩子、`SusRouteRecord`、`NavigationResult` 字段说明）见英文
[`docs/02-router-api.md`](./02-router-api.md)。

## 4. 屏幕生命周期（`SusScreen`）

屏幕继承 `SusScreen`，可以覆盖以下生命周期钩子：`BeforeEnter` → `Entered` →
`BeforeRouteUpdate`（同一屏幕、参数变化）→ `BeforeLeave` → `Left`。嵌套路由的子视图
通过 `ChildView`（`SusRouteView`）渲染。详见英文 [`docs/03-susscreen.md`](./03-susscreen.md)。

## 5. 模态

```csharp
public class ConfirmModal : SusModal
{
    // ...
}

modalService.Open<ConfirmModal>();
```

模态通过 `OverlayHost` 挂载（见 `sus-core` 的 bootstrap 树结构），栈由
`SusModalService` 管理，支持 `DismissOnClickOutside`。详见英文
[`docs/05-modals.md`](./05-modals.md)。

## 6. KeepAlive

给路由加 `.KeepAlive()`——离开后屏幕实例被缓存（LRU），而不是销毁重建；缓存键由
`KeepAliveKey(route)` 决定，`KeepAliveIgnoreQuery` 控制 query 参数是否参与缓存键。
详见英文 [`docs/04-routeview.md`](./04-routeview.md)。

## 7. 下一步

- 守卫与过渡（`beforeEach`/`CanEnter`/`CanLeave`/`BeforeResolve`，代码驱动的过渡）——
  英文 [`docs/06-guards-transitions.md`](./06-guards-transitions.md)。
- 7 个可运行示例（BasicRouting、KeepAlive、Guards、Modals、Nested+Named、RouteLink、
  FullDemo）——英文 [`docs/10-examples.md`](./10-examples.md)。
- 与 Vue Router 的差距分析——英文 [`docs/11-gap-analysis.md`](./11-gap-analysis.md)。
- 产品网站（含完整中文站点指南）：[sus-ui.dev](https://sus-ui.dev)
