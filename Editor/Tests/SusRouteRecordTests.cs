using NUnit.Framework;
using System.Collections.Generic;

using Sharq.Core;

namespace Sharq.Router.Editor.Tests
{
    public class SusRouteRecordTests
    {
        // Minimal screen type for route registration
        private class DummyScreen : SusScreen
        {
            protected override void Build() { }
        }

        [Test]
        public void Match_StaticPath_ReturnsEmptyParams()
        {
            var record = new SusRouteRecord("/home", typeof(DummyScreen));

            var result = record.Match("/home");

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Match_StaticPath_ExactMatchOnly()
        {
            var record = new SusRouteRecord("/home", typeof(DummyScreen));

            Assert.IsNull(record.Match("/home/sub"));
            Assert.IsNull(record.Match("/hom"));
            Assert.IsNull(record.Match("/HOMEPAGE"));
        }

        [Test]
        public void Match_DynamicParam_ExtractsValue()
        {
            var record = new SusRouteRecord("/battle/:id", typeof(DummyScreen));

            var result = record.Match("/battle/42");

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("42", result["id"]);
        }

        [Test]
        public void Match_DynamicParam_NoMatchWithoutParam()
        {
            var record = new SusRouteRecord("/battle/:id", typeof(DummyScreen));

            Assert.IsNull(record.Match("/battle"));
            Assert.IsNull(record.Match("/battle/"));
        }

        [Test]
        public void Match_MultipleDynamicParams()
        {
            var record = new SusRouteRecord("/users/:userId/settings/:tab",
                typeof(DummyScreen));

            var result = record.Match("/users/1234/settings/profile");

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("1234", result["userId"]);
            Assert.AreEqual("profile", result["tab"]);
        }

        [Test]
        public void Match_IgnoresQueryString()
        {
            var record = new SusRouteRecord("/battle/:id", typeof(DummyScreen));

            var result = record.Match("/battle/42?debug=true&mode=pvp");

            Assert.IsNotNull(result);
            Assert.AreEqual("42", result["id"]);
        }

        [Test]
        public void Match_CaseInsensitive()
        {
            var record = new SusRouteRecord("/Battle", typeof(DummyScreen));

            var result = record.Match("/battle");

            Assert.IsNotNull(result);
        }

        [Test]
        public void Config_KeepAlive_IsReadable()
        {
            var config = new SusRouteConfig { KeepAlive = true };
            var record = new SusRouteRecord("/keep", typeof(DummyScreen), config);

            Assert.IsTrue(record.Config.KeepAlive);
        }

        [Test]
        public void Config_Redirect_IsReadable()
        {
            var config = new SusRouteConfig { Redirect = "/home" };
            var record = new SusRouteRecord("/old", typeof(DummyScreen), config);

            Assert.AreEqual("/home", record.Config.Redirect);
        }

        [Test]
        public void Config_Alias_IsReadable()
        {
            var config = new SusRouteConfig
            {
                Alias = new List<string> { "/alias1", "/alias2" }
            };
            var record = new SusRouteRecord("/main", typeof(DummyScreen), config);

            Assert.AreEqual(2, record.Config.Alias.Count);
            Assert.Contains("/alias1", record.Config.Alias);
            Assert.Contains("/alias2", record.Config.Alias);
        }

        [Test]
        public void Config_Name_IsReadable()
        {
            var config = new SusRouteConfig { Name = "home-screen" };
            var record = new SusRouteRecord("/home", typeof(DummyScreen), config);

            Assert.AreEqual("home-screen", record.Config.Name);
        }

        [Test]
        public void ParamNames_EmptyForStaticPath()
        {
            var record = new SusRouteRecord("/static", typeof(DummyScreen));

            Assert.AreEqual(0, record.ParamNames.Count);
        }

        [Test]
        public void ParamNames_ContainsParamNames_ForDynamicPath()
        {
            var record = new SusRouteRecord("/a/:b/c/:d", typeof(DummyScreen));

            CollectionAssert.AreEqual(new[] { "b", "d" }, record.ParamNames);
        }

        [Test]
        public void Children_AreRegisteredWithParent()
        {
            var config = new SusRouteConfig
            {
                Children = new List<SusRouteRecord>
                {
                    new SusRouteRecord("tab1", typeof(DummyScreen)),
                    new SusRouteRecord("tab2", typeof(DummyScreen)),
                }
            };
            var record = new SusRouteRecord("/settings", typeof(DummyScreen), config);

            Assert.AreEqual(2, record.Config.Children.Count);
        }

        // ─── CaseSensitive / Strict matching ──────────────────────────────

        [Test]
        public void CaseSensitive_DefaultIsFalse()
        {
            var record = new SusRouteRecord("/About", typeof(DummyScreen));

            var match = record.Match("/about");

            Assert.IsNotNull(match, "Default matching should be case-insensitive");
        }

        [Test]
        public void CaseSensitive_True_RejectsDifferentCase()
        {
            var config = new SusRouteConfig { CaseSensitive = true };
            var record = new SusRouteRecord("/About", typeof(DummyScreen), config);

            var match = record.Match("/about");

            Assert.IsNull(match, "Case-sensitive matching should reject different case");
        }

        [Test]
        public void CaseSensitive_True_MatchesExactCase()
        {
            var config = new SusRouteConfig { CaseSensitive = true };
            var record = new SusRouteRecord("/About", typeof(DummyScreen), config);

            var match = record.Match("/About");

            Assert.IsNotNull(match);
        }

        [Test]
        public void Strict_DefaultIsFalse()
        {
            var record = new SusRouteRecord("/about", typeof(DummyScreen));

            var match = record.Match("/about/");

            Assert.IsNotNull(match, "Default matching should ignore trailing slash");
        }

        [Test]
        public void Strict_True_RejectsTrailingSlash()
        {
            var config = new SusRouteConfig { Strict = true };
            var record = new SusRouteRecord("/about", typeof(DummyScreen), config);

            var match = record.Match("/about/");

            Assert.IsNull(match, "Strict matching should reject trailing slash");
        }

        [Test]
        public void Strict_True_MatchesExact()
        {
            var config = new SusRouteConfig { Strict = true };
            var record = new SusRouteRecord("/about", typeof(DummyScreen), config);

            var match = record.Match("/about");

            Assert.IsNotNull(match);
        }

        [Test]
        public void Strict_False_TrimsTrailingSlashFromQuery()
        {
            var record = new SusRouteRecord("/about", typeof(DummyScreen));

            var match = record.Match("/about?q=1");

            Assert.IsNotNull(match);
        }
    }
}
