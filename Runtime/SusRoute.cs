using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.UIElements;

using Sharq.Core;

namespace Sharq.Router
{
    // ════════════════════════════════════════════════════════════════
    //  SusRouteConfig — route settings
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Per-route configuration: KeepAlive, guards, children, aliases, redirects, lazy loading.
    /// </summary>
    public class SusRouteConfig
    {
        /// <summary>Route name for PushNamed/ReplaceNamed.</summary>
        public string Name;

        /// <summary>Keep screen instance alive when leaving this route.</summary>
        public bool KeepAlive;

        /// <summary>Alternative paths that resolve to this route.</summary>
        public List<string> Alias;

        /// <summary>Nested child routes.</summary>
        public List<SusRouteRecord> Children;

        /// <summary>Redirect path. When this route is pushed, navigate here instead.</summary>
        public string Redirect;

        /// <summary>Default props passed to the screen instance.</summary>
        public Dictionary<string, object> DefaultProps;

        /// <summary>
        /// Functional props generator. Called with the resolved route,
        /// returns a dictionary merged with DefaultProps and explicit props.
        /// Analogous to props: route => ({...}) in Vue Router.
        /// </summary>
        public Func<SusRoute, Dictionary<string, object>> PropsFn;

        /// <summary>Lazy factory for creating the screen (alternative to Activator.CreateInstance).</summary>
        public Func<SusScreen> LazyFactory;

        /// <summary>Per-route guard (CanEnter/CanLeave).</summary>
        public ISusRouteGuard Guard;

        /// <summary>
        /// Per-route beforeEnter guard (function-based, without ISusRouteGuard class).
        /// Analogous to beforeEnter on a route record in Vue Router.
        /// Called AFTER ISusRouteGuard.CanEnter (when both are set).
        /// </summary>
        public SusRouterGuard BeforeEnter;

        /// <summary>Transition animation for this route.</summary>
        public SusRouteTransition Transition;

        /// <summary>Optional metadata dictionary (e.g. requiresAuth, title).</summary>
        public Dictionary<string, object> Meta;

        /// <summary>When true, path matching is case-sensitive. Default: false (case-insensitive).</summary>
        public bool CaseSensitive;

        /// <summary>When true, trailing slash matters. "/about" ≠ "/about/" if strict.</summary>
        public bool Strict;
    }

    // ════════════════════════════════════════════════════════════════
    //  SusRouteRecord — entry in the route registry
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// A registered route entry. Holds the type, path template, and config.
    /// Created by SusRouter.Register().
    /// </summary>
    public class SusRouteRecord
    {
        /// <summary>Path template, e.g. "/users/:id".</summary>
        public string Path { get; }

        /// <summary>Screen type (must inherit SusScreen).</summary>
        public Type ScreenType { get; }

        /// <summary>Route configuration.</summary>
        public SusRouteConfig Config { get; }

        /// <summary>Parent record for nested routes.</summary>
        public SusRouteRecord Parent { get; set; }

        /// <summary>Parameter names extracted from the path template (e.g. ["id"] from "/users/:id").</summary>
        public List<string> ParamNames { get; }

        private readonly Regex _matchRegex;

        public SusRouteRecord(string path, Type screenType, SusRouteConfig config = null)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            ScreenType = screenType;
            Config = config ?? new SusRouteConfig();

            ParamNames = new List<string>();
            if (path.Contains(":"))
            {
                var regexOptions = RegexOptions.Compiled;
                if (config != null && !config.CaseSensitive)
                    regexOptions |= RegexOptions.IgnoreCase;

                // Build normalized pattern (strip leading '/' for regex)
                var normalized = path.TrimStart('/');
                var unescaped = Regex.Escape(normalized).Replace("\\:", ":");
                var pattern = "^" + Regex.Replace(unescaped, ":([^/]+)", "(?<$1>[^/]+)") + "$";
                _matchRegex = new Regex(pattern, regexOptions);

                foreach (var part in normalized.Split('/'))
                {
                    if (part.StartsWith(":"))
                        ParamNames.Add(part.Substring(1));
                }
            }
        }

        /// <summary>
        /// Attempts to match a concrete path against this route's template.
        /// Returns extracted parameter values, or null if no match.
        /// </summary>
        public Dictionary<string, string> Match(string concretePath)
        {
            if (concretePath == null) return null;

            // Strip query string
            var queryStart = concretePath.IndexOf('?');
            var pathOnly = queryStart >= 0 ? concretePath.Substring(0, queryStart) : concretePath;

            // Normalize: strip leading '/' for consistent comparison
            if (pathOnly.Length > 1 && pathOnly.StartsWith("/"))
                pathOnly = pathOnly.Substring(1);

            // Strip trailing slash unless Strict
            var isStrict = Config?.Strict ?? false;
            if (!isStrict && pathOnly.Length > 1 && pathOnly.EndsWith("/"))
                pathOnly = pathOnly.TrimEnd('/');

            if (_matchRegex == null)
            {
                // Static route
                var comparePath = Path;
                if (comparePath.Length > 1 && comparePath.StartsWith("/"))
                    comparePath = comparePath.Substring(1);
                if (!isStrict && comparePath.Length > 1 && comparePath.EndsWith("/"))
                    comparePath = comparePath.TrimEnd('/');

                if (!isStrict && (Config?.CaseSensitive ?? false) == false)
                    return string.Equals(pathOnly, comparePath, StringComparison.OrdinalIgnoreCase)
                        ? new Dictionary<string, string>() : null;
                return pathOnly == comparePath ? new Dictionary<string, string>() : null;
            }

            var match = _matchRegex.Match(pathOnly);
            if (!match.Success) return null;

            var result = new Dictionary<string, string>();
            foreach (var name in ParamNames)
            {
                if (match.Groups[name].Success)
                    result[name] = match.Groups[name].Value;
            }
            return result;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  SusRoute — active route in the history stack
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Represents an active route in the history stack.
    /// Holds the resolved record, full path, extracted params, and the screen instance.
    /// </summary>
    public class SusRoute
    {
        /// <summary>The registered route record.</summary>
        public SusRouteRecord Record { get; }

        /// <summary>
        /// Full concrete path (e.g. "/users/42?tab=profile").
        /// Includes query string if present.
        /// </summary>
        public string FullPath { get; }

        /// <summary>Path parameter values extracted from the template (e.g. { "id": "42" }).</summary>
        public Dictionary<string, string> Params { get; }

        /// <summary>Query parameters parsed from ?key=val.</summary>
        public Dictionary<string, string> Query { get; }

        /// <summary>The screen instance (created by the router).</summary>
        public SusScreen Screen { get; set; }

        /// <summary>Props passed during Push/Replace.</summary>
        public Dictionary<string, object> Props { get; set; }

        /// <summary>Whether this route is currently active (screen is in DOM).</summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Full chain of route records from root to leaf (for nested routes).
        /// For single-level routes, contains only the record itself.
        /// </summary>
        public List<SusRouteRecord> MatchedChain { get; set; }

        /// <summary>
        /// Screen instances for each level of the chain (for nested routes).
        /// Index 0 = root screen, last = deepest child.
        /// </summary>
        public List<SusScreen> ChainScreens { get; set; }

        /// <summary>
        /// A sentinel route representing "no route" (used as fromRoute on first navigation).
        /// </summary>
        public static readonly SusRoute None = new SusRoute(null, "<none>", null);

        public SusRoute(SusRouteRecord record, string fullPath, Dictionary<string, string> @params)
        {
            Record = record;
            FullPath = fullPath ?? string.Empty;
            Params = @params ?? new Dictionary<string, string>();

            // Parse query string from FullPath
            Query = new Dictionary<string, string>();
            if (fullPath != null)
            {
                var qIdx = fullPath.IndexOf('?');
                if (qIdx >= 0 && qIdx < fullPath.Length - 1)
                {
                    var qs = fullPath.Substring(qIdx + 1);
                    foreach (var pair in qs.Split('&'))
                    {
                        var eqIdx = pair.IndexOf('=');
                        if (eqIdx > 0)
                            Query[pair.Substring(0, eqIdx)] =
                                Uri.UnescapeDataString(pair.Substring(eqIdx + 1));
                        else if (eqIdx < 0 && pair.Length > 0)
                            Query[pair] = string.Empty;
                    }
                }
            }

            MatchedChain = record != null
                ? new List<SusRouteRecord> { record }
                : new List<SusRouteRecord>();
        }
    }
}
