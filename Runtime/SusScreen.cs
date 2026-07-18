using System.Collections.Generic;
using UnityEngine.UIElements;

using Sharq.Core;

namespace Sharq.Router
{
    /// <summary>
    /// Base class for full-screen views managed by SusRouter.
    ///
    /// Lifecycle (called by SusRouter):
    ///   1. BeforeEnter(fromRoute) — called before the screen becomes active.
    ///                               Return false to block entry. Read Props here.
    ///   2. Entered()              — called after the screen is added to the DOM.
    ///   3. BeforeRouteUpdate(toRoute) → bool — called when route props change
    ///                               on same screen. Return false to block.
    ///   4. BeforeLeave(toRoute) → bool — guard: return false to block navigation away.
    ///   5. Left()                 — called when the screen is being removed.
    ///
    /// A SusScreen is always rendered inside a SusRouteView. Layout comes from
    /// USS <c>.sus-screen</c> (absolute fill of the outlet) in SusRuntime/_global.uss.
    /// </summary>
    public abstract class SusScreen : SusComponent
    {
        public const string UssClassName = "sus-screen";

        /// <summary>
        /// Reference to the router that owns this screen.
        /// Set by SusRouter before BeforeEnter().
        /// </summary>
        public SusRouter Router { get; set; }

        protected SusScreen()
        {
            AddToClassList(UssClassName);
        }

        /// <summary>
        /// Route params and query props passed from the router.
        /// Set by SusRouter before BeforeEnter(). Never null.
        /// </summary>
        public Dictionary<string, object> Props { get; set; } = new();

        /// <summary>
        /// Whether this screen is currently the active route.
        /// </summary>
        public bool IsActive { get; internal set; }

        private readonly List<SusRouteView> _childViews = new();

        /// <summary>
        /// Called by router before this screen becomes active.
        /// Delegates to <see cref="OnBeforeEnter"/>. Do NOT override this method
        /// — override OnBeforeEnter instead.
        /// </summary>
        public bool BeforeEnter(SusRoute fromRoute) => OnBeforeEnter(fromRoute);

        /// <summary>
        /// Override to validate entry, read Props, or start data loading.
        /// Return false to block entry.
        /// </summary>
        protected virtual bool OnBeforeEnter(SusRoute fromRoute) => true;

        /// <summary>
        /// Called by router after the screen has been added to the DOM.
        /// Delegates to <see cref="OnEntered"/>. Do NOT override this method
        /// — override OnEntered instead.
        /// </summary>
        public void Entered() => OnEntered();

        /// <summary>
        /// Override to react to the screen becoming active (schedule animations, etc.).
        /// </summary>
        protected virtual void OnEntered() { }

        /// <summary>
        /// Called by router when route props update on the same screen instance.
        /// Delegates to <see cref="OnBeforeRouteUpdate"/>. Do NOT override this method
        /// — override OnBeforeRouteUpdate instead.
        /// </summary>
        public bool BeforeRouteUpdate(SusRoute toRoute) => OnBeforeRouteUpdate(toRoute);

        /// <summary>
        /// Override to react to prop changes. Return false to block the update.
        /// </summary>
        protected virtual bool OnBeforeRouteUpdate(SusRoute toRoute) => true;

        /// <summary>
        /// Called by router before navigation away from this screen.
        /// Delegates to <see cref="OnBeforeLeave"/>. Do NOT override this method
        /// — override OnBeforeLeave instead.
        /// </summary>
        public bool BeforeLeave(SusRoute toRoute) => OnBeforeLeave(toRoute);

        /// <summary>
        /// Override to guard against leaving (e.g., unsaved changes). Return false to block.
        /// </summary>
        protected virtual bool OnBeforeLeave(SusRoute toRoute) => true;

        /// <summary>
        /// Called by router when this screen is being removed.
        /// Delegates to <see cref="OnLeft"/>. Do NOT override this method
        /// — override OnLeft instead.
        /// </summary>
        public void Left() => OnLeft();

        /// <summary>
        /// Override to clean up (unsubscribe events, release resources).
        /// </summary>
        protected virtual void OnLeft() { }

        // ─── Helpers for standard screens ─────────────────────────────

        /// <summary>
        /// Read a typed prop from <see cref="Props"/> with a default value.
        /// Example: GetProp("title", "Loading...")
        /// </summary>
        protected T GetProp<T>(string key, T defaultValue = default)
        {
            if (Props == null || !Props.TryGetValue(key, out var val)) return defaultValue;
            if (val is T t) return t;
            try { return (T)System.Convert.ChangeType(val, typeof(T)); }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[GetProp] Cannot convert route prop '{key}' from {val?.GetType().Name ?? "null"} to {typeof(T).Name}: {ex.Message}. Using default ({defaultValue}).");
                return defaultValue;
            }
        }

        /// <summary>
        /// Read an untyped prop from <see cref="Props"/> with a default.
        /// </summary>
        protected object GetProp(string key, object defaultValue = null)
        {
            if (Props == null || !Props.TryGetValue(key, out var val)) return defaultValue;
            return val;
        }

        /// <summary>
        /// Read a route parameter from <see cref="Props"/> with a default.
        /// Alias for GetProp — route params and query are merged into Props by SusRouter.
        /// </summary>
        protected string GetParam(string key, string defaultValue = null)
        {
            return GetProp(key, defaultValue);
        }

        /// <summary>
        /// Read a query parameter from <see cref="Props"/> with a default.
        /// Alias for GetProp — route params and query are merged into Props by SusRouter.
        /// </summary>
        protected string GetQuery(string key, string defaultValue = null)
        {
            return GetProp(key, defaultValue);
        }

        /// <summary>
        /// Register a child SusRouteView for nested routing.
        /// The parent routes chain screens into registered child views.
        /// </summary>
        protected void RegisterChildView(SusRouteView view)
        {
            if (view == null) return;
            _childViews.Add(view);
        }

        /// <summary>
        /// All registered child route views for nested routing.
        /// </summary>
        internal IReadOnlyList<SusRouteView> ChildViews => _childViews;

        /// <summary>
        /// The first registered child SusRouteView (for single-child nested routing).
        /// </summary>
        public SusRouteView ChildView =>
            _childViews.Count > 0 ? _childViews[0] : null;
    }
}
