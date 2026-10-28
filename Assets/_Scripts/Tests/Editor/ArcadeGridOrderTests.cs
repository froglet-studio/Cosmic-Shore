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
    /// The Arcade grid's order and its ELEMENT and VESSEL dropdowns (<see cref="ArcadeGridOrder"/>, Docs/MENU_REDESIGN_B.md
    /// sections 85 and 97). The view asks two questions about a game; these tests answer them by hand, so the rules are
    /// pinned without a scene.
    /// </summary>
    [TestFixture]
    public class ArcadeGridOrderTests
    {
        readonly List<SO_ArcadeGame> _made = new();
        HashSet<string> _favourites;
        Dictionary<string, Element> _genre;

        readonly List<ScriptableObject> _vessels = new();

        SO_ArcadeGame Game(string name, int maxPlayers = 2, params VesselClassType[] seats)
        {
            var g = ScriptableObject.CreateInstance<SO_ArcadeGame>();
            g.DisplayName = name;
            g.MaxPlayersAllowed = maxPlayers;
            g.Vessels = new List<SO_Vessel>();
            foreach (var cls in seats)
            {
                var v = ScriptableObject.CreateInstance<SO_Vessel>();
                v.Class = cls;
                _vessels.Add(v);
                g.Vessels.Add(v);
            }
            _made.Add(g);
            return g;
        }

        [SetUp]
        public void SetUp()
        {
            _favourites = new HashSet<string> { "Zeta" };
            _genre = new Dictionary<string, Element> { ["Alpha"] = Element.Mass, ["Mid"] = Element.Time, ["Zeta"] = Element.Mass };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _made) Object.DestroyImmediate(g);
            foreach (var v in _vessels) Object.DestroyImmediate(v);
            _made.Clear();
            _vessels.Clear();
        }

        List<string> Names(Element element, VesselClassType vessel = VesselClassType.Any, System.Func<SO_ArcadeGame, bool> always = null) =>
            ArcadeGridOrder.Apply(new[] { Game("Mid", 1, VesselClassType.Sparrow), Game("Zeta"), Game("Alpha", 2, VesselClassType.Dolphin, VesselClassType.Sparrow) },
                    element, vessel, g => _favourites.Contains(g.DisplayName),
                    (g, e) => _genre.TryGetValue(g.DisplayName, out var x) && x == e, always)
                .Select(g => g.DisplayName).ToList();

        [Test]
        public void FavouritesComeFirst_ThenAToZ() =>
            Assert.AreEqual(new[] { "Zeta", "Alpha", "Mid" }, Names(Element.None));

        [Test]
        public void TheElementDropdown_KeepsItsGenre()
        {
            Assert.AreEqual(new[] { "Zeta", "Alpha" }, Names(Element.Mass));
            Assert.AreEqual(new[] { "Mid" }, Names(Element.Time));
            Assert.IsEmpty(Names(Element.Charge));
        }

        [Test]
        public void TheVesselDropdown_KeepsTheGamesThatSeatIt()
        {
            Assert.AreEqual(new[] { "Alpha", "Mid" }, Names(Element.None, VesselClassType.Sparrow));
            Assert.AreEqual(new[] { "Alpha" }, Names(Element.None, VesselClassType.Dolphin));
            Assert.AreEqual(new[] { "Alpha" }, Names(Element.Mass, VesselClassType.Sparrow));   // both at once
        }

        [Test]
        public void AGuidedGame_IsNeverFilteredOut() =>
            Assert.AreEqual(new[] { "Mid" }, Names(Element.Charge, VesselClassType.Any, g => g.DisplayName == "Mid"));
    }
}
#endif
