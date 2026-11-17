using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>The Ark escort's assaults (<see cref="ArkAssaultClock"/>, ArkEscortRun.Assault.cs): the fauna of a hostile
    /// world turn on the Ark in announced waves the squad drives off by killing attackers near it. Pure state, no scene.</summary>
    public class ArkAssaultTests
    {
        static ArkAssaultClock.Settings S => new()
        {
            FirstDelay = 10f, Warning = 5f, Duration = 30f, Gap = 40f, RepelKills = 3, LureShare = 0.6f,
        };

        [Test]
        public void AWorld_WarnsThenAttacks_AfterTheFirstDelay()
        {
            var c = new ArkAssaultClock();
            Assert.AreEqual(ArkAssaultClock.Change.None, c.Tick(0f, true, S));
            Assert.AreEqual(10f, c.NextIn(0f), 1e-4f);
            Assert.AreEqual(ArkAssaultClock.Change.None, c.Tick(9f, true, S));
            Assert.AreEqual(ArkAssaultClock.Change.Incoming, c.Tick(10f, true, S));
            Assert.AreEqual(ArkAssaultPhase.Incoming, c.Phase);
            Assert.AreEqual(0.05f, c.LureShare(0.05f, S), 1e-4f, "the warning does not lure yet");
            Assert.AreEqual(ArkAssaultClock.Change.Started, c.Tick(15f, true, S));
            Assert.AreEqual(ArkAssaultPhase.Active, c.Phase);
            Assert.AreEqual(0.6f, c.LureShare(0.05f, S), 1e-4f, "an assault turns the lure up");
            Assert.AreEqual(30f, c.SecondsLeft(15f), 1e-4f);
        }

        [Test]
        public void KillingEnoughAttackers_DrivesItOff_AndTheLullFollows()
        {
            var c = Started();
            Assert.AreEqual(ArkAssaultClock.Change.None, c.NoteKill(16f, S));
            Assert.AreEqual(ArkAssaultClock.Change.None, c.NoteKill(17f, S));
            Assert.AreEqual(ArkAssaultClock.Change.Repelled, c.NoteKill(18f, S));
            Assert.AreEqual(ArkAssaultPhase.None, c.Phase);
            Assert.AreEqual(1, c.Repelled);
            Assert.AreEqual(0, c.Passed);
            Assert.AreEqual(0.05f, c.LureShare(0.05f, S), 1e-4f);
            Assert.AreEqual(40f, c.NextIn(18f), 1e-4f, "the next warning comes after the gap");
            Assert.AreEqual(ArkAssaultClock.Change.Incoming, c.Tick(58f, true, S));
        }

        [Test]
        public void AnAssaultNotDrivenOff_RunsOut_AndCountsItsPlates()
        {
            var c = Started();
            c.NotePlateLost();
            c.NotePlateLost();
            Assert.AreEqual(ArkAssaultClock.Change.None, c.Tick(44f, true, S));
            Assert.AreEqual(ArkAssaultClock.Change.Passed, c.Tick(45f, true, S));
            Assert.AreEqual(2, c.PlatesLost);
            Assert.AreEqual(1, c.Passed);
        }

        [Test]
        public void KillsOutsideAnAssault_DoNotCount()
        {
            var c = new ArkAssaultClock();
            Assert.AreEqual(ArkAssaultClock.Change.None, c.NoteKill(1f, S));
            c.Tick(0f, true, S);
            c.Tick(10f, true, S);   // incoming
            Assert.AreEqual(ArkAssaultClock.Change.None, c.NoteKill(11f, S));
            Assert.AreEqual(0, c.Kills);
        }

        [Test]
        public void PacifyingOrLeavingTheWorld_CallsTheAssaultOff()
        {
            var c = Started();
            Assert.AreEqual(ArkAssaultClock.Change.Passed, c.Tick(20f, false, S));
            Assert.AreEqual(ArkAssaultPhase.None, c.Phase);
            Assert.AreEqual(-1f, c.NextIn(20f), "no assault is scheduled while the world cannot be assaulted");

            var warned = new ArkAssaultClock();
            warned.Tick(0f, true, S);
            warned.Tick(10f, true, S);
            Assert.AreEqual(ArkAssaultClock.Change.Passed, warned.NewWorld(), "a dropped warning still clears the HUD");
            Assert.AreEqual(0, warned.Passed, "but it was never an assault");
        }

        [Test]
        public void NoAssaultLure_MeansNoAssaults()
        {
            var s = S;
            s.LureShare = 0f;
            var c = new ArkAssaultClock();
            for (float t = 0f; t < 200f; t += 1f) Assert.AreEqual(ArkAssaultClock.Change.None, c.Tick(t, true, s));
        }

        [Test]
        public void TheShippedEscort_AssaultsAtEveryDifficulty_AndTheEasyOnesCanBeLost()
        {
            var escort = AssetDatabase.LoadAssetAtPath<ArkEscortSO>("Assets/_SO_Assets/Toys/ArkEscort.asset");
            Assert.IsNotNull(escort);
            float lastLure = 0f;
            for (int d = ArkEscortSO.MinDifficulty; d <= ArkEscortSO.MaxDifficulty; d++)
            {
                var tier = escort.TierFor(d);
                Assert.Greater(tier.AssaultLureShare, tier.LureShare, $"d{d}: an assault is stronger than the steady lure");
                Assert.GreaterOrEqual(tier.AssaultLureShare, lastLure, $"d{d}: assaults grow with difficulty");
                Assert.GreaterOrEqual(tier.AssaultRepelKills, 1);
                lastLure = tier.AssaultLureShare;
                // Where nothing gnaws armour, the hull bottoms out at its armoured plates (about a fifth): only a breach
                // line above that can lose the escort.
                if (tier.ShieldBiteMin <= 0f)
                    Assert.Greater(tier.BreachHull, 0.21f, $"d{d}: no armour bite and no breach line - the Ark cannot be lost");
            }
            Assert.IsFalse(string.IsNullOrEmpty(escort.AssaultLine), "the assault line is authored");
        }

        [Test]
        public void AssaultParams_ReachTheTiers()
        {
            var escort = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<ArkEscortSO>("Assets/_SO_Assets/Toys/ArkEscort.asset"));
            try
            {
                var values = ArkEscortParams.ParseFlatJson(
                    "{\"tier.all.assaultLureShare\": 0.7, \"tier.all.assaultRepelKills\": 9, \"escort.assaultFirstDelay\": 3}");
                var target = new ArkEscortParams.Target { Escort = escort, Difficulty = 1 };
                ArkEscortParams.Apply(target, values);
                Assert.AreEqual(3f, escort.AssaultFirstDelay, 1e-4f);
                Assert.Greater(escort.Tiers.Count, 0);
                foreach (var tier in escort.Tiers)
                {
                    Assert.AreEqual(0.7f, tier.AssaultLureShare, 1e-4f);
                    Assert.AreEqual(9, tier.AssaultRepelKills);
                }
            }
            finally { Object.DestroyImmediate(escort); }
        }

        [Test]
        public void AJobsHostileCells_FollowItsPattern_ElseOnlyAHostileDestination()
        {
            var run = new VoyageJob { HostileCells = new List<bool> { false, true, true } };
            Assert.IsFalse(VoyageRules.HostileCell(run, 0, 4, false), "the first cell is calm");
            Assert.IsTrue(VoyageRules.HostileCell(run, 1, 4, false));
            Assert.IsTrue(VoyageRules.HostileCell(run, 3, 4, false), "cells past the pattern take its last entry");
            Assert.IsFalse(VoyageRules.HostileCell(run, 4, 4, true), "no cell past the leg");

            var plain = new VoyageJob();
            Assert.IsFalse(VoyageRules.HostileCell(plain, 0, 3, true));
            Assert.IsTrue(VoyageRules.HostileCell(plain, 2, 3, true), "the old rule: a hostile destination");
            Assert.IsFalse(VoyageRules.HostileCell(plain, 2, 3, false));
        }

        static ArkAssaultClock Started()
        {
            var c = new ArkAssaultClock();
            c.Tick(0f, true, S);
            c.Tick(10f, true, S);
            c.Tick(15f, true, S);
            Assert.AreEqual(ArkAssaultPhase.Active, c.Phase);
            return c;
        }
    }
}
