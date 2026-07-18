using System;
using System.Collections.Generic;
using Sharq.Core;
using UnityEngine.UIElements;

namespace Sharq.Router
{
    /// <summary>
    /// Router wiring for the core <see cref="SusApp"/> facade. Lives in sus-router so
    /// sus-core stays navigation-agnostic (no reverse dependency core → router).
    ///
    /// Collapses the old "LoadTokenCascade + register routes + Router.Mount" pattern into a
    /// single fluent step, run at the right point of <see cref="SusApp"/> finalization
    /// (after the token cascade / custom styles, before the theme is applied).
    /// </summary>
    public static class SusAppRouterExtensions
    {
        /// <summary>
        /// Registers routes/guards on <paramref name="router"/> via <paramref name="configure"/>,
        /// then mounts it into the app root at <paramref name="initialPath"/>.
        /// </summary>
        /// <example>
        /// <code>
        /// SusApp.Create(doc)
        ///       .UseTheme(SusTheme.Dark)
        ///       .UseRouter(new SusRouter(), r => {
        ///           r.Register("/", typeof(HomeScreen));
        ///           r.Register("/settings", typeof(SettingsScreen));
        ///       }, initialPath: "/")
        ///       .Run();
        /// </code>
        /// </example>
        public static SusApp UseRouter(
            this SusApp app,
            SusRouter router,
            Action<SusRouter> configure,
            string initialPath,
            Dictionary<string, object> initialProps = null)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            if (router == null) throw new ArgumentNullException(nameof(router));
            if (string.IsNullOrEmpty(initialPath)) throw new ArgumentNullException(nameof(initialPath));

            return app.Configure(root =>
            {
                configure?.Invoke(router);
                router.Mount(root, initialPath, initialProps);
            });
        }

        /// <summary>
        /// Declarative overload: builds the route tree with the fluent
        /// <see cref="SusRouteBuilder"/>, then mounts <paramref name="router"/> at
        /// <paramref name="initialPath"/>.
        /// </summary>
        /// <example>
        /// <code>
        /// SusApp.Create(doc)
        ///       .UseRouter(new SusRouter(), routes => routes
        ///           .Route("/", typeof(HomeScreen)).Name("home")
        ///           .Route("/settings", typeof(SettingsScreen)),
        ///           initialPath: "/")
        ///       .Run();
        /// </code>
        /// </example>
        public static SusApp UseRouter(
            this SusApp app,
            SusRouter router,
            Action<SusRouteBuilder> build,
            string initialPath,
            Dictionary<string, object> initialProps = null)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            if (router == null) throw new ArgumentNullException(nameof(router));
            if (string.IsNullOrEmpty(initialPath)) throw new ArgumentNullException(nameof(initialPath));

            return app.Configure(root =>
            {
                var builder = new SusRouteBuilder();
                build?.Invoke(builder);
                builder.ApplyTo(router);
                router.Mount(root, initialPath, initialProps);
            });
        }
    }
}
