using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The board's jobs come from the active squad's three pilots only, and a player with no squad is given a starter
    /// squad of three different pilots so the board always has three jobs (Shombith, 2026-11-16).
    /// </summary>
    public class VoyageStarterSquadTests
    {
        [Test]
        public void StarterSquad_HasThreeDifferentPilots_LeaderHeldByThePlayer()
        {
            var settings = ScriptableObject.CreateInstance<VoyageToyDefinitionSO>();
            try
            {
                var squad = VoyageRun.StarterSquad(settings, "player-1");
                Assert.IsNotNull(squad, "The default settings author no starter squad.");
                var pilots = Enumerable.Range(0, SavedSquad.SlotCount).Select(i => squad.GetSlot(i).PilotId).ToList();
                CollectionAssert.AllItemsAreNotNull(pilots);
                CollectionAssert.AllItemsAreUnique(pilots, "Two starter seats share a pilot: the board would offer fewer than three jobs.");
                Assert.IsTrue(squad.Leader.IsPlayer(), "The starter's leader seat is not the player's.");
                Assert.AreEqual("player-1", squad.Leader.MemberId);
                Assert.AreEqual(SquadSeat.Ai, squad.RogueOne.Seat);
                Assert.AreEqual(SquadSeat.Ai, squad.RogueTwo.Seat);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void StarterSquad_PilotsAreShippedCallsigns_WithAVoyagePerson()
        {
            var settings = VoyageToyDefinitionSO.Load();
            Assert.IsNotNull(settings, "No shipped voyage settings.");
            var catalog = PilotArtifactCatalogSO.Load();
            Assert.IsNotNull(catalog, "No shipped pilot catalog.");
            var content = settings.Content ? settings.Content.Content : null;
            Assert.IsNotNull(content, "No shipped voyage content.");

            var squad = VoyageRun.StarterSquad(settings, null);
            Assert.IsNotNull(squad, "The shipped settings author no starter squad.");
            for (int i = 0; i < SavedSquad.SlotCount; i++)
            {
                var slot = squad.GetSlot(i);
                Assert.IsTrue(catalog.TryGetPilot(slot.PilotId, out _), $"Starter seat {i} names '{slot.PilotId}', which the pilot catalog lacks.");
                Assert.IsNotNull(content.Person(slot.PilotId), $"Starter seat {i}'s pilot '{slot.PilotId}' is nobody in the voyage crew: it would bring no job.");
            }
            Assert.AreEqual(SavedSquad.SlotCount, VoyageRun.SeatedPilots(squad).Distinct().Count(),
                "The starter squad does not seat three different pilots.");
        }

        [Test]
        public void AddStarter_FillsOnlyAnEmptyBook()
        {
            var starter = new SavedSquad("Starter", new SquadSlot(VesselClassType.Sparrow, Element.Charge),
                new SquadSlot(VesselClassType.Squirrel, Element.Mass), new SquadSlot(VesselClassType.Dolphin, Element.Space));

            var empty = new SquadBook();
            Assert.AreSame(starter, VoyageRun.AddStarter(empty, starter));
            Assert.AreSame(starter, empty.GetActive());

            var own = new SquadBook();
            var mine = own.Add(new SavedSquad("Mine", new SquadSlot(VesselClassType.Manta, Element.Time),
                new SquadSlot(VesselClassType.Manta, Element.Time), new SquadSlot(VesselClassType.Manta, Element.Time)));
            Assert.IsNull(VoyageRun.AddStarter(own, starter), "A starter was added to a book that already held a squad.");
            Assert.AreEqual(1, own.Squads.Count);
            Assert.AreSame(mine, own.GetActive());
        }

        [Test]
        public void Board_WithoutTheCrew_IsTheSeatedPilotsOnly()
        {
            var content = VoyageContentSO.Load()?.Content;
            Assert.IsNotNull(content, "No shipped voyage content.");
            var state = VoyageRules.NewGalaxy(content, Domains.Jade);
            var seated = new List<string> { "pilot.callsign.talon", "pilot.callsign.anvil", "pilot.callsign.citadel" };
            var board = VoyageRules.Board(state, content, seated);
            Assert.LessOrEqual(board.Count(j => !j.IsReturn), seated.Count,
                "The board offered more jobs than the squad seats pilots.");
            Assert.IsTrue(board.Where(j => !j.IsReturn).All(j => seated.Contains(j.PilotId)),
                "A job on the board came from a pilot who is not in the squad.");
        }
    }
}
