#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Vessels row that replaced the arena cards' intensity row (Garrett, 2026-11-06): the four selection modes'
    /// pure rules (<see cref="VesselSelectionRules"/>) and the arena cards' one fixed intensity.
    /// </summary>
    public class VesselSelectionRulesTests
    {
        const string ArenaGamesPath = "Assets/_SO_Assets/Games/GameLists/ArenaGames.asset";

        [Test]
        public void Members_KeepTheirNumbers()
        {
            // Replicated as an int in the lobby snapshot: a renumbering would show a guest the wrong mode.
            Assert.AreEqual(1, (int)VesselSelectionMode.Pick);
            Assert.AreEqual(2, (int)VesselSelectionMode.Random);
            Assert.AreEqual(3, (int)VesselSelectionMode.Draft);
            Assert.AreEqual(4, (int)VesselSelectionMode.Squad);
        }

        [Test]
        public void Resolve_TurnsAnUnwrittenOrUnknownValueIntoPick()
        {
            Assert.AreEqual(VesselSelectionMode.Pick, VesselSelectionRules.Resolve(0));
            Assert.AreEqual(VesselSelectionMode.Pick, VesselSelectionRules.Resolve(99));
            Assert.AreEqual(VesselSelectionMode.Draft, VesselSelectionRules.Resolve(3));
        }

        [Test]
        public void OnlyRandomAndSquad_DealTheHumans()
        {
            Assert.IsFalse(VesselSelectionRules.HumanIsDealt(VesselSelectionMode.Pick));
            Assert.IsTrue(VesselSelectionRules.HumanIsDealt(VesselSelectionMode.Random));
            Assert.IsFalse(VesselSelectionRules.HumanIsDealt(VesselSelectionMode.Draft));
            Assert.IsTrue(VesselSelectionRules.HumanIsDealt(VesselSelectionMode.Squad));
            Assert.IsTrue(VesselSelectionRules.HumanPicksFreely(VesselSelectionMode.Pick));
            Assert.IsFalse(VesselSelectionRules.HumanPicksFreely(VesselSelectionMode.Draft));
        }

        // ── the snake draft ───────────────────────────────────────────────────────────────

        static string Order(params int[] seatsPerTeam) =>
            string.Join(" ", VesselSelectionRules.SnakeOrder(seatsPerTeam).Select(t => $"{(char)('A' + t.Team)}{t.Seat}"));

        [Test]
        public void Snake_TwoTeamsOfThree_GoesABBAAB()
        {
            Assert.AreEqual("A0 B0 B1 A1 A2 B2", Order(3, 3));
        }

        [Test]
        public void Snake_ThreeTeams_TurnsAtEachEnd()
        {
            Assert.AreEqual("A0 B0 C0 C1 B1 A1", Order(2, 2, 2));
        }

        [Test]
        public void Snake_UnevenTeams_SkipATeamWithNoSeatLeft()
        {
            // One human against a team of three AI: the solo team's only pick is first, the rest are the AI's.
            Assert.AreEqual("A0 B0 B1 B2", Order(1, 3));
            Assert.AreEqual("A0 B0 B1 A1 A2 A3", Order(4, 2));
        }

        [Test]
        public void Snake_EveryTeamsSeatsAppearOnceEach()
        {
            var order = VesselSelectionRules.SnakeOrder(new[] { 2, 3, 1 });
            Assert.AreEqual(6, order.Count);
            Assert.AreEqual(order.Count, order.Distinct().Count());
            Assert.IsEmpty(VesselSelectionRules.SnakeOrder(new int[0]));
        }

        // ── the deals ─────────────────────────────────────────────────────────────────────

        [Test]
        public void DealRandom_NeverDealsATakenHull()
        {
            var card = new[] { VesselClassType.Squirrel, VesselClassType.Dolphin, VesselClassType.Manta };
            var taken = new HashSet<VesselClassType> { VesselClassType.Squirrel, VesselClassType.Manta };
            var rng = new System.Random(7);
            for (int i = 0; i < 50; i++)
            {
                Assert.IsTrue(VesselSelectionRules.TryDealRandom(card, taken, rng, out var hull));
                Assert.AreEqual(VesselClassType.Dolphin, hull);
            }
        }

        [Test]
        public void DealRandom_FailsWhenEveryHullIsTaken_AndSkipsTheNoPickSentinels()
        {
            var card = new[] { VesselClassType.Random, VesselClassType.Any, VesselClassType.Squirrel };
            Assert.IsFalse(VesselSelectionRules.TryDealRandom(card, new HashSet<VesselClassType> { VesselClassType.Squirrel },
                new System.Random(1), out _));
        }

        [Test]
        public void DealRandom_ReachesEveryFreeHull()
        {
            var card = new[] { VesselClassType.Squirrel, VesselClassType.Dolphin, VesselClassType.Manta, VesselClassType.Rhino };
            var seen = new HashSet<VesselClassType>();
            var rng = new System.Random(3);
            for (int i = 0; i < 200; i++)
                if (VesselSelectionRules.TryDealRandom(card, null, rng, out var hull)) seen.Add(hull);
            Assert.AreEqual(4, seen.Count, "a random deal must be able to give every hull on the card");
        }

        [Test]
        public void SquadDeal_DealsFromTheSquad_ThenFallsBackToTheCard()
        {
            var card = new[] { VesselClassType.Squirrel, VesselClassType.Dolphin, VesselClassType.Manta };
            // The Serpent is not on the card, so only the Dolphin can come from this squad.
            var squad = new[] { VesselClassType.Dolphin, VesselClassType.Serpent, VesselClassType.Dolphin };

            Assert.IsTrue(VesselSelectionRules.TryDealFromSquad(squad, card, null, new System.Random(5), out var hull));
            Assert.AreEqual(VesselClassType.Dolphin, hull);

            var taken = new HashSet<VesselClassType> { VesselClassType.Dolphin };
            Assert.IsTrue(VesselSelectionRules.TryDealFromSquad(squad, card, taken, new System.Random(5), out hull));
            Assert.AreNotEqual(VesselClassType.Dolphin, hull, "every squad vessel is taken: the card's free hulls are next");
            CollectionAssert.Contains(card, hull);

            Assert.IsTrue(VesselSelectionRules.TryDealFromSquad(null, card, null, new System.Random(5), out hull),
                "no squad at all is a random deal");
        }

        [Test]
        public void ArenaAutopilotSkill_FollowsTheDifficulty()
        {
            Assert.AreEqual(0.25f, VesselSelectionRules.ArenaAutopilotSkill(AIDifficulty.Easy), 1e-6f);
            Assert.AreEqual(0.5f, VesselSelectionRules.ArenaAutopilotSkill(AIDifficulty.Medium), 1e-6f);
            Assert.AreEqual(1f, VesselSelectionRules.ArenaAutopilotSkill(AIDifficulty.Legendary), 1e-6f);
            Assert.AreEqual(0.5f, VesselSelectionRules.ArenaAutopilotSkill((AIDifficulty)0), 1e-6f, "unset reads as the default, Medium");
        }

        // ── the arena cards ───────────────────────────────────────────────────────────────

        [Test]
        public void EveryArenaCard_PlaysTheOneArenaIntensity()
        {
            var list = AssetDatabase.LoadAssetAtPath<SO_GameList>(ArenaGamesPath);
            Assert.IsNotNull(list, $"{ArenaGamesPath} is missing");
            foreach (var card in list.Games)
            {
                Assert.IsNotNull(card, "ArenaGames holds a null card");
                Assert.AreEqual(SO_ArcadeGame.ArenaIntensity, card.MinIntensity,
                    $"'{card.DisplayName}' has no intensity row any more; its MinIntensity must be the arena intensity.");
                Assert.AreEqual(SO_ArcadeGame.ArenaIntensity, card.MaxIntensity,
                    $"'{card.DisplayName}' has no intensity row any more; its MaxIntensity must be the arena intensity.");
            }
        }
    }
}
#endif
