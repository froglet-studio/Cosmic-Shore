using System.IO;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Editor;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The browser build's card scope (Shombith, 2026-11-21: the Toybox, Squirrel's games, every Time game, Regatta,
    /// and a Maelstrom dealing only Time games, 2 / 4 / 6 / 8 by intensity). The failure these guard against is a
    /// card the browser offers, or the Maelstrom draws, whose scene is not in the WebGL build: its launch would load
    /// a scene that is not there. <c>Tools/Build/web_build_scope.py --check</c> holds the same rule outside Unity.
    /// </summary>
    public class WebBuildScopeTests
    {
        const string MaelstromPath = "Assets/_SO_Assets/Maelstrom/MaelstromData.asset";

        static WebBuildScopeSO Scope()
        {
            var scope = AssetDatabase.LoadAssetAtPath<WebBuildScopeSO>(WebToyboxBuild.ScopeAssetPath);
            Assert.IsNotNull(scope, $"{WebToyboxBuild.ScopeAssetPath} is missing (Tools/Build/web_build_scope.py --write)");
            return scope;
        }

        [Test]
        public void TheScopeAsset_IsWhatTheGameLoads()
        {
            Assert.AreSame(Scope(), WebBuildScopeSO.Load(), "the build reads the asset at Resources/" + WebBuildScopeSO.ResourcePath);
        }

        [Test]
        public void EveryCard_IsThere_AndItsSceneIsInTheBuild()
        {
            var scope = Scope();
            var scenes = WebToyboxBuild.RequireScenes();
            CollectionAssert.IsSubsetOf(WebToyboxBuild.Scenes, scenes, "the Toybox's own scenes stay in the build");

            foreach (var game in scope.Games)
            {
                Assert.IsNotNull(game, "the scope lists a missing card");
                Assert.IsTrue(scenes.Any(p => Path.GetFileNameWithoutExtension(p) == game.SceneName.Trim()),
                    $"{game.name}: its scene '{game.SceneName}' is not in the WebGL build");
            }
        }

        [Test]
        public void TheRule_SquirrelsCards_Regatta_AndTheMaelstrom_AreCarried()
        {
            var modes = Scope().Games.Select(g => g.Mode).ToList();
            foreach (var mode in new[] { GameModes.SkimRace, GameModes.Joust, GameModes.Regatta, GameModes.Maelstrom })
                CollectionAssert.Contains(modes, mode, $"{mode} is in the browser build by Shombith's rule");
            CollectionAssert.DoesNotContain(modes, GameModes.Cleave, "a Space game is not in the browser build");
        }

        [Test]
        public void TheMaelstrom_DealsOnlyCarriedCards_FromItsQueue_TwoFourSixEight()
        {
            var scope = Scope();
            var data = AssetDatabase.LoadAssetAtPath<MaelstromDataSO>(MaelstromPath);
            Assert.IsNotNull(data, MaelstromPath);

            var sizes = new[] { 2, 4, 6, 8 };
            for (int level = 1; level <= 4; level++)
            {
                var tier = scope.MaelstromTiers.SingleOrDefault(t => t.Intensity == level);
                Assert.IsNotNull(tier, $"no browser bucket for I{level}");
                Assert.AreEqual(sizes[level - 1], tier.Games.Count, $"I{level} deals {sizes[level - 1]} games");
                foreach (var game in tier.Games)
                {
                    Assert.IsTrue(scope.Carries(game), $"I{level} deals {game?.name}, which the browser build does not carry");
                    CollectionAssert.Contains(data.GameQueue, game, $"I{level} deals {game?.name}, which is not in the Maelstrom's GameQueue");
                }
            }

            var dealt = scope.MaelstromTiers.SelectMany(t => t.Games).ToList();
            Assert.AreEqual(dealt.Count, dealt.Distinct().Count(), "the buckets are exclusive");
        }

        [Test]
        public void Offers_IsEveryCardOutsideTheBrowser_AndOnlyTheScopesInIt()
        {
            var scope = Scope();
            var carried = scope.Games[0];
            var other = AssetDatabase.FindAssets("t:SO_ArcadeGame")
                .Select(g => AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(AssetDatabase.GUIDToAssetPath(g)))
                .First(g => g && !scope.Carries(g));

            Assert.IsTrue(WebToybox.Offers(other, webBuild: false, scope: null), "outside the browser every card is offered");
            Assert.IsTrue(WebToybox.Offers(carried, webBuild: true, scope: scope));
            Assert.IsFalse(WebToybox.Offers(other, webBuild: true, scope: scope), $"{other.name} is not in the browser build");
            Assert.IsFalse(WebToybox.Offers(carried, webBuild: true, scope: null), "no scope asset means no card");
            Assert.IsFalse(WebToybox.Offers(null, webBuild: false, scope: scope));
        }
    }
}
