using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Sharq.Router
{
    /// <summary>
    /// Declarative, fluent route tree builder — a nicer front-end over
    /// <see cref="SusRouter.Register(string, Type, SusRouteConfig)"/> and the verbose
    /// <see cref="SusRouteConfig"/> / <see cref="SusRouteRecord"/> objects.
    ///
    /// <code>
    /// SusApp.Create(doc)
    ///       .UseRouter(new SusRouter(), routes => routes
    ///           .Route("/", typeof(HomeScreen)).Name("home")
    ///           .Route&lt;LoginScreen&gt;("/login").Alias("/signin")
    ///           .Route("/users/:id", typeof(UserScreen))
    ///               .Name("user").KeepAlive().Meta("requiresAuth", true)
    ///               .Children(c => c
    ///                   .Route("profile", typeof(ProfileScreen))
    ///                   .Route("posts", typeof(PostsScreen))),
    ///           initialPath: "/")
    ///       .Run();
    /// </code>
    ///
    /// Children use the router's one-level nesting (parent + children) — the same
    /// mechanism as <c>SusRouteConfig.Children</c>.
    /// </summary>
    public sealed class SusRouteBuilder
    {
        private readonly List<SusRouteEntry> _entries = new();

        /// <summary>Begins a route definition. <paramref name="screenType"/> may be null when a lazy factory is set.</summary>
        public SusRouteEntry Route(string path, Type screenType = null)
        {
            var entry = new SusRouteEntry(this, path, screenType);
            _entries.Add(entry);
            return entry;
        }

        /// <summary>Typed convenience: <c>Route&lt;HomeScreen&gt;("/")</c>.</summary>
        public SusRouteEntry Route<T>(string path) where T : SusScreen
            => Route(path, typeof(T));

        /// <summary>Applies all collected routes to <paramref name="router"/> via Register().</summary>
        public void ApplyTo(SusRouter router)
        {
            if (router == null) throw new ArgumentNullException(nameof(router));
            foreach (var e in _entries)
                router.Register(e.Path, e.ScreenType, e.BuildConfig());
        }

        /// <summary>Builds child records (for nesting into a parent's <see cref="SusRouteConfig.Children"/>).</summary>
        internal List<SusRouteRecord> BuildRecords()
        {
            var list = new List<SusRouteRecord>(_entries.Count);
            foreach (var e in _entries)
                list.Add(new SusRouteRecord(e.Path, e.ScreenType, e.BuildConfig()));
            return list;
        }
    }

    /// <summary>Fluent per-route configuration. Returned by <see cref="SusRouteBuilder.Route(string, Type)"/>.</summary>
    public sealed class SusRouteEntry
    {
        private readonly SusRouteBuilder _owner;
        private readonly SusRouteConfig _config = new();
        private SusRouteBuilder _children;

        internal SusRouteEntry(SusRouteBuilder owner, string path, Type screenType)
        {
            _owner = owner;
            Path = path ?? throw new ArgumentNullException(nameof(path));
            ScreenType = screenType;
        }

        internal string Path { get; }
        internal Type ScreenType { get; }

        // ── Fluent config. Each returns `this` so options chain; Route()/Children() below
        //    return the owner / new scope to continue the tree. ──

        /// <summary>Sets the route name used by <see cref="SusRouter.PushNamed"/> / <see cref="SusRouter.ReplaceNamed"/>.</summary>
        public SusRouteEntry Name(string name) { _config.Name = name; return this; }
        /// <summary>Keeps the screen instance alive off-DOM when leaving this route (default <c>true</c> when called with no args).</summary>
        public SusRouteEntry KeepAlive(bool keepAlive = true) { _config.KeepAlive = keepAlive; return this; }
        /// <summary>When this route is pushed, navigate to <paramref name="path"/> instead.</summary>
        public SusRouteEntry Redirect(string path) { _config.Redirect = path; return this; }
        /// <summary>Lazy screen factory (alternative to <c>Activator.CreateInstance</c> on the registered type).</summary>
        public SusRouteEntry Lazy(Func<SusScreen> factory) { _config.LazyFactory = factory; return this; }
        /// <summary>Per-route <see cref="ISusRouteGuard"/> (<c>CanEnter</c> / <c>CanLeave</c>).</summary>
        public SusRouteEntry Guard(ISusRouteGuard guard) { _config.Guard = guard; return this; }
        /// <summary>Function-based beforeEnter guard. Runs after <see cref="ISusRouteGuard.CanEnter"/> when both are set.</summary>
        public SusRouteEntry BeforeEnter(SusRouterGuard guard) { _config.BeforeEnter = guard; return this; }
        /// <summary>Per-route enter/leave animation (<see cref="SusRouteTransition"/> factories).</summary>
        public SusRouteEntry Transition(SusRouteTransition transition) { _config.Transition = transition; return this; }
        /// <summary>When <c>true</c>, path matching is case-sensitive. Default of the config is case-insensitive.</summary>
        public SusRouteEntry CaseSensitive(bool value = true) { _config.CaseSensitive = value; return this; }
        /// <summary>When <c>true</c>, trailing slash is significant (<c>/about</c> ≠ <c>/about/</c>).</summary>
        public SusRouteEntry Strict(bool value = true) { _config.Strict = value; return this; }
        /// <summary>Default props merged into the screen (after <see cref="SusRouteConfig.PropsFn"/>, before query/params/Push props).</summary>
        public SusRouteEntry Props(Dictionary<string, object> props) { _config.DefaultProps = props; return this; }
        /// <summary>Functional props: called with the resolved route; merged first, then <see cref="Props"/> and explicit Push props.</summary>
        public SusRouteEntry PropsFn(Func<SusRoute, Dictionary<string, object>> fn) { _config.PropsFn = fn; return this; }

        /// <summary>Adds one or more alternative paths that resolve to this route.</summary>
        public SusRouteEntry Alias(params string[] aliases)
        {
            if (aliases == null || aliases.Length == 0) return this;
            _config.Alias ??= new List<string>();
            _config.Alias.AddRange(aliases);
            return this;
        }

        /// <summary>Adds a metadata entry (e.g. <c>Meta("requiresAuth", true)</c>). Accumulates.</summary>
        public SusRouteEntry Meta(string key, object value)
        {
            if (string.IsNullOrEmpty(key)) return this;
            _config.Meta ??= new Dictionary<string, object>();
            _config.Meta[key] = value;
            return this;
        }

        /// <summary>Declares nested child routes (one-level nesting, like <see cref="SusRouteConfig.Children"/>).</summary>
        public SusRouteEntry Children(Action<SusRouteBuilder> build)
        {
            if (build == null) return this;
            _children ??= new SusRouteBuilder();
            build(_children);
            return this;
        }

        /// <summary>Starts a sibling route on the owning builder (lets you chain Route().Route()).</summary>
        public SusRouteEntry Route(string path, Type screenType = null) => _owner.Route(path, screenType);

        /// <summary>Typed sibling route.</summary>
        public SusRouteEntry Route<T>(string path) where T : SusScreen => _owner.Route<T>(path);

        internal SusRouteConfig BuildConfig()
        {
            if (_children != null)
                _config.Children = _children.BuildRecords();
            return _config;
        }
    }
}
