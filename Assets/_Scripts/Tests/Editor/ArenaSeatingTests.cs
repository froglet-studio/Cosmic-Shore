#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// ARENA seating (<see cref="SO_ArcadeGame.ArenaRules"/>, Docs/HomeHub/ARCHITECTURE.md §3.9):
    /// every hull is flown by one pilot, seats are capped at the hulls a card lists, and a human
    /// can step round a ring of AI teammate hulls. The pieces here are the pure ones every peer
    /// must answer identically - a ring that two machines order differently would send a swap
    /// request for a hull the requester never saw highlighted.
    /// </summary>
    public class ArenaSeatingTests
    {
        const string ArenaGamesPath = "Assets/_SO_Assets/Games/GameLists/ArenaGames.asset";

        // ── the ring ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void Ring_StepsForwardAndBack_InNetworkIdOrder_WhateverTheInputOrder()
        {
            var hulls = new List<ulong> { 40, 10, 30, 20 };   // deliberately unsorted

            Assert.IsTrue(PilotSwap.TryPickRingTarget(hulls, 20, +1, out var next));
            Assert.AreEqual(30UL, next);
            Assert.IsTrue(PilotSwap.TryPickRingTarget(hulls, 20, -1, out var prev));
            Assert.AreEqual(10UL, prev);
        }

        [Test]
        public void Ring_Wraps_AtBothEnds()
        {
            var hulls = new List<ulong> { 10, 20, 30 };
            Assert.IsTrue(PilotSwap.TryPickRingTarget(hulls, 30, +1, out var next));
            Assert.AreEqual(10UL, next);
            Assert.IsTrue(PilotSwap.TryPickRingTarget(hulls, 10, -1, out var prev));
            Assert.AreEqual(30UL, prev);
        }

        [Test]
        public void Ring_WithNoTeammate_HasNoTarget()
        {
            Assert.IsFalse(PilotSwap.TryPickRingTarget(new List<ulong> { 10 }, 10, +1, out _));
            Assert.IsFalse(PilotSwap.TryPickRingTarget(new List<ulong>(), 10, +1, out _),
                "the pilot's own hull is always in the ring, so an empty team list is still a ring of one");
            Assert.IsFalse(PilotSwap.TryPickRingTarget(new List<ulong> { 10, 20 }, 10, 0, out _),
                "no direction, no swap");
        }

        [Test]
        public void Ring_OwnHullMissingFromTheList_IsStillTheAnchor()
        {
            Assert.IsTrue(PilotSwap.TryPickRingTarget(new List<ulong> { 30 }, 20, +1, out var t));
            Assert.AreEqual(30UL, t);
        }

        // ── the free-hull pick ──────────────────────────────────────────────────────────────

        static readonly List<VesselClassType> Card = new()
        {
            VesselClassType.Manta, VesselClassType.Dolphin, VesselClassType.Rhino, VesselClassType.Sparrow,
        };

        [Test]
        public void FreeHull_KeepsThePreferredHull_WhenNobodyHasIt()
        {
            Assert.IsTrue(GameDataSO.TryPickFreeHull(Card, VesselClassType.Rhino,
                new HashSet<VesselClassType> { VesselClassType.Manta }, out var hull));
            Assert.AreEqual(VesselClassType.Rhino, hull);
        }

        [Test]
        public void FreeHull_DealsTheFirstFreeHullInCardOrder_WhenThePreferredIsTaken()
        {
            Assert.IsTrue(GameDataSO.TryPickFreeHull(Card, VesselClassType.Manta,
                new HashSet<VesselClassType> { VesselClassType.Manta, VesselClassType.Dolphin }, out var hull));
            Assert.AreEqual(VesselClassType.Rhino, hull);
        }

        [Test]
        public void FreeHull_NeverDealsAHullTheCardDoesNotList()
        {
            Assert.IsTrue(GameDataSO.TryPickFreeHull(Card, VesselClassType.Urchin,
                new HashSet<VesselClassType>(), out var hull));
            Assert.AreEqual(VesselClassType.Manta, hull);
        }

        [Test]
        public void FreeHull_Fails_WhenEveryHullIsTaken()
        {
            Assert.IsFalse(GameDataSO.TryPickFreeHull(Card, VesselClassType.Manta,
                new HashSet<VesselClassType>(Card), out _));
        }

        // ── the cards ────────────────────────────────────────────────────────────────────────

        [Test]
        public void EveryArenaCard_PlaysByArenaRules_AndSeatsNoMorePilotsThanItHasHulls()
        {
            var list = AssetDatabase.LoadAssetAtPath<SO_GameList>(ArenaGamesPath);
            Assert.IsNotNull(list, $"{ArenaGamesPath} is missing");

            foreach (var card in list.Games)
            {
                Assert.IsNotNull(card, "ArenaGames holds a null card");
                Assert.IsTrue(card.ArenaRules,
                    $"'{card.DisplayName}' is on the Arena roster without ArenaRules - its hulls would " +
                    "not be exclusive and its pilots could not swap into an AI teammate's hull.");
                int teams = System.Math.Max(1, card.MaxDomainsAllowed);
                Assert.LessOrEqual(card.MaxSeats, card.DistinctHullCount,
                    $"'{card.DisplayName}' seats {card.MaxSeats} but lists {card.DistinctHullCount} hulls: " +
                    "under ArenaRules a hull is flown once in the whole match, so the extra pilots would have no hull to fly.");
                Assert.LessOrEqual(card.MaxSeats, SO_ArcadeGame.TeamSeatCap(teams),
                    $"'{card.DisplayName}' seats {card.MaxSeats}; no team fields more than {SO_ArcadeGame.MaxPerTeam}.");
                Assert.LessOrEqual(card.MaxPlayersAllowed, 9,
                    $"'{card.DisplayName}' allows {card.MaxPlayersAllowed} pilots; every game goes up to 9 (Garrett, 2026-11-17).");
            }
        }

        // ── nine vessels, three a team (Garrett, 2026-11-17) ───────────────────────────────

        static SO_ArcadeGame CardOf(int maxPlayers, int maxDomains, bool arena, params VesselClassType[] hulls)
        {
            var g = UnityEngine.ScriptableObject.CreateInstance<SO_ArcadeGame>();
            g.MaxPlayersAllowed = maxPlayers;
            g.MaxDomainsAllowed = maxDomains;
            g.ArenaRules = arena;
            g.Vessels = new List<SO_Vessel>();
            foreach (var h in hulls)
            {
                var v = UnityEngine.ScriptableObject.CreateInstance<SO_Vessel>();
                v.Class = h;
                g.Vessels.Add(v);
            }
            return g;
        }

        [Test]
        public void TeamSeatCap_IsThreeATeam()
        {
            Assert.AreEqual(3, SO_ArcadeGame.TeamSeatCap(1));
            Assert.AreEqual(6, SO_ArcadeGame.TeamSeatCap(2));
            Assert.AreEqual(9, SO_ArcadeGame.TeamSeatCap(3));
            Assert.AreEqual(3, SO_ArcadeGame.TeamSeatCap(0), "no teams is read as one team");
        }

        [Test]
        public void ArcadeCard_SeatsNine_WithThreeTeams_AndSixWithTwo()
        {
            Assert.AreEqual(9, CardOf(9, 3, false, VesselClassType.Dolphin).MaxSeats);
            Assert.AreEqual(6, CardOf(9, 2, false, VesselClassType.Dolphin).MaxSeats,
                "two teams of three is six, whatever the card's player cap says");
            Assert.AreEqual(4, CardOf(4, 3, false, VesselClassType.Dolphin).MaxSeats,
                "a card that caps itself lower keeps its own cap");
        }

        [Test]
        public void ArenaCard_SeatsEachHullOnceInTheWholeMatch()
        {
            var two = CardOf(9, 3, true, VesselClassType.Manta, VesselClassType.Dolphin);
            Assert.AreEqual(2, two.MaxSeats, "two hulls: two pilots, whatever the team count (Garrett, 2026-11-18)");
            var many = CardOf(9, 3, true, VesselClassType.Manta, VesselClassType.Dolphin,
                VesselClassType.Rhino, VesselClassType.Sparrow);
            Assert.AreEqual(9, many.MaxSeats, "four hulls, three teams: the three-a-team cap binds first");
        }

        [Test]
        public void NoArcadeCard_PlaysByArenaRules()
        {
            // ArenaRules caps seats at the hull count, so on a one-hull arcade card it would cap
            // the match at ONE pilot. It belongs to the arena roster and nowhere else.
            var arena = AssetDatabase.LoadAssetAtPath<SO_GameList>(ArenaGamesPath);
            var arenaCards = new HashSet<SO_ArcadeGame>(arena.Games);

            foreach (var path in Directory.GetFiles("Assets/_SO_Assets/Games", "*.asset"))
            {
                var text = File.ReadAllText(path);
                if (!Regex.IsMatch(text, @"^\s*ArenaRules: 1\s*$", RegexOptions.Multiline)) continue;

                var card = AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(path.Replace('\\', '/'));
                Assert.IsTrue(card != null && arenaCards.Contains(card),
                    $"{path} sets ArenaRules but is not on the Arena roster.");
            }
        }
    }
}
#endif
