#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Arcade grid's SORT and FILTER (<see cref="ArcadeGridOrder"/>, Docs/MENU_REDESIGN_B.md section 85). The view
    /// asks three questions about a game; these tests answer them by hand, so the rules are pinned without a scene.
    /// </summary>
    [TestFixture]
    public class ArcadeGridOrderTests
    {
        readonly List<SO_ArcadeGame> _made = new();
        HashSet<string> _favourites, _unlocked;
        Dictionary<string, Element> _genre;

        SO_ArcadeGame Game(string name, int maxPlayers = 2)
        {
            var g = ScriptableObject.CreateInstance<SO_ArcadeGame>();
            g.DisplayName = name;
            g.MaxPlayersAllowed = maxPlayers;
            _made.Add(g);
            return g;
        }

        [SetUp]
        public void SetUp()
        {
            _favourites = new HashSet<string> { "Zeta" };
            _unlocked = new HashSet<string> { "Alpha", "Zeta" };
            _genre = new Dictionary<string, Element> { ["Alpha"] = Element.Mass, ["Mid"] = Element.Time, ["Zeta"] = Element.Mass };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _made) Object.DestroyImmediate(g);
            _made.Clear();
        }

        List<string> Names(ArcadeGridSort sort, ArcadeGridFilter filter, System.Func<SO_ArcadeGame, bool> always = null) =>
            ArcadeGridOrder.Apply(new[] { Game("Mid", 1), Game("Zeta"), Game("Alpha") }, sort, filter,
                    g => _favourites.Contains(g.DisplayName), g => _unlocked.Contains(g.DisplayName),
                    (g, e) => _genre.TryGetValue(g.DisplayName, out var x) && x == e, always)
                .Select(g => g.DisplayName).ToList();

        [Test]
        public void TheDefaultIsTheOldOrder_FavouritesThenAToZ() =>
            Assert.AreEqual(new[] { "Zeta", "Alpha", "Mid" }, Names(ArcadeGridSort.FavouritesFirst, ArcadeGridFilter.All));

        [Test]
        public void AToZ_IgnoresFavourites() =>
            Assert.AreEqual(new[] { "Alpha", "Mid", "Zeta" }, Names(ArcadeGridSort.AToZ, ArcadeGridFilter.All));

        [Test]
        public void UnlockedFirst_PutsLockedGamesLast() =>
            Assert.AreEqual(new[] { "Alpha", "Zeta", "Mid" }, Names(ArcadeGridSort.UnlockedFirst, ArcadeGridFilter.All));

        [Test]
        public void Filters_KeepOnlyTheirGames()
        {
            Assert.AreEqual(new[] { "Zeta" }, Names(ArcadeGridSort.AToZ, ArcadeGridFilter.Favourites));
            Assert.AreEqual(new[] { "Alpha", "Zeta" }, Names(ArcadeGridSort.AToZ, ArcadeGridFilter.Unlocked));
            Assert.AreEqual(new[] { "Mid" }, Names(ArcadeGridSort.AToZ, ArcadeGridFilter.Solo));
            Assert.AreEqual(new[] { "Alpha", "Zeta" }, Names(ArcadeGridSort.AToZ, ArcadeGridFilter.Multiplayer));
            Assert.AreEqual(new[] { "Alpha", "Zeta" }, Names(ArcadeGridSort.AToZ, ArcadeGridFilter.Mass));
            Assert.AreEqual(new[] { "Mid" }, Names(ArcadeGridSort.AToZ, ArcadeGridFilter.Time));
            Assert.IsEmpty(Names(ArcadeGridSort.AToZ, ArcadeGridFilter.Charge));
        }

        [Test]
        public void EveryElementFilterAsksForItsOwnElement()
        {
            // Charge..Time map onto Element by offset; a reordered enum would send a filter to the wrong element.
            Assert.AreEqual((int)Element.Time - (int)Element.Charge, ArcadeGridFilter.Time - ArcadeGridFilter.Charge);
            foreach (var e in new[] { Element.Charge, Element.Mass, Element.Space, Element.Time })
            {
                var asked = new List<Element>();
                ArcadeGridOrder.Apply(new[] { Game("Any") }, ArcadeGridSort.AToZ,
                    ArcadeGridFilter.Charge + ((int)e - (int)Element.Charge),
                    _ => false, _ => true, (_, x) => { asked.Add(x); return true; });
                Assert.AreEqual(new[] { e }, asked);
            }
        }

        [Test]
        public void AGuidedCardIsNeverFilteredOut() =>
            Assert.AreEqual(new[] { "Mid", "Zeta" },
                Names(ArcadeGridSort.AToZ, ArcadeGridFilter.Favourites, g => g.DisplayName == "Mid"));
    }
}
#endif
