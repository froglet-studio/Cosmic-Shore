#if UNITY_EDITOR
using System;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The starter crew IS the painted pilots (<see cref="PaintedCrewSO"/>; Garrett, 2026-11-12: "none of the stories
    /// matched the art"): a starter record takes the pictured person's name, animals, faction, home and story, keeps
    /// what a save hangs on (id, callsign, origin, chronicle), and a recruit keeps their own. The shipped asset is the
    /// one <c>Tools/Build/author_pilot_scene_assets.py</c> writes from <c>Docs/Pilots/art/scenes/people.json</c>.
    /// </summary>
    [TestFixture]
    public class PaintedCrewTests
    {
        const string CatalogPath = "Assets/_SO_Assets/Pilots/PilotSceneCatalog.asset";

        static Func<ulong> Seeds(ulong first)
        {
            ulong next = first;
            return () => next++;
        }

        static PaintedCrewSO Crew()
        {
            var crew = PaintedCrewSO.Load();
            Assert.IsNotNull(crew, "Resources/Pilots/PaintedCrew is missing; run Tools/Build/author_pilot_scene_assets.py");
            return crew;
        }

        [Test]
        public void EveryPaintedScene_HasItsPerson_UnderTheSameName()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PilotSceneCatalogSO>(CatalogPath);
            Assert.IsNotNull(catalog);
            var crew = Crew();
            Assert.AreEqual(catalog.Scenes.Count, crew.People.Count, "one person a painted scene");
            foreach (var scene in catalog.Scenes)
            {
                Assert.IsTrue(crew.TryGet(scene.callsign, out var person), $"{scene.callsign} has no painted person");
                Assert.AreEqual(scene.painted, person.given + " " + person.family, $"{scene.callsign}'s name");
                Assert.AreNotEqual(person.speciesOne, person.speciesTwo);
                Assert.AreNotEqual(AnimalSpecies.None, person.speciesOne);
                Assert.AreNotEqual(FactionType.None, person.factionType);
                Assert.AreEqual(person.factionType, FactionCatalog.TypeOf(FactionCatalog.IdOf(person.factionType, person.factionName)));
                StringAssert.Contains("\n\n", person.story, $"{scene.callsign}'s story is short paragraphs");
            }
        }

        [Test]
        public void Paint_MakesAStarterThePaintedPilot_AndKeepsWhatASaveHangsOn()
        {
            var crew = Crew();
            var inv = new AccountInventory();
            var p = inv.MintPilot(PilotVision.Volt, FactionType.Pirate, PaintedCrewSO.StarterOrigin, 1, Seeds(5));
            p.AddChronicle("Joined the crew.");
            string id = p.Id;
            Assert.IsTrue(PaintedCrewSO.IsPaintable(p));
            Assert.IsTrue(crew.TryGet(PilotVision.Volt, out var painted));

            Assert.IsTrue(crew.Paint(p), "a rolled starter changes");
            Assert.AreEqual(painted.given + " " + painted.family, p.DisplayName());
            Assert.AreEqual(painted.speciesOne, p.SpeciesOne);
            Assert.AreEqual(painted.speciesTwo, p.SpeciesTwo);
            Assert.AreEqual(painted.factionType, p.FactionType);
            Assert.AreEqual(painted.factionName, FactionCatalog.NameOf(p.FactionId));
            Assert.AreEqual(painted.homeworld, p.Homeworld);
            Assert.AreEqual(painted.story, p.Backstory);
            Assert.AreEqual(id, p.Id, "a squad that seats them still finds them");
            Assert.AreEqual(PaintedCrewSO.StarterOrigin, p.Origin);
            Assert.AreEqual(1, p.Chronicle.Count, "the story they grew stays");
            Assert.IsFalse(crew.Paint(p), "a second read changes nothing");
        }

        [Test]
        public void ARecruit_KeepsTheirOwnRecord()
        {
            var inv = new AccountInventory();
            var p = inv.MintPilot(PilotVision.Volt, FactionType.Pirate, "recruit", 1, Seeds(9));
            Assert.IsFalse(PaintedCrewSO.IsPaintable(p));
            Assert.IsFalse(PaintedCrewSO.IsPaintable(null));
        }

        [Test]
        public void Normalize_KeepsAStorysParagraphs()
        {
            var inv = new AccountInventory();
            var p = inv.MintPilot(PilotVision.Volt, FactionType.Pirate, PaintedCrewSO.StarterOrigin, 1, Seeds(5));
            Crew().Paint(p);
            string story = p.Backstory;
            p.Backstory = story + "\u0007";
            inv.Normalize();
            Assert.AreEqual(story, p.Backstory, "line breaks stay; what the font cannot draw goes");
        }
    }
}
#endif
