#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Chum (the Angler's Space game, CHUM.md): the reef's geometry and counts, and the scoring rule. The reef is laid
    /// on every peer from the intensity alone, so these hold that it is the same list every time, that no two prisms
    /// touch, that every shoal sits between the nucleus and the membrane, and that the rule is decided only by the clock.
    /// </summary>
    [TestFixture]
    public class ChumReefTests
    {
        const float Nucleus = 392f;
        const float Membrane = 1200f;

        readonly List<Object> _owned = new();
        T Track<T>(T o) where T : Object { _owned.Add(o); return o; }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _owned) if (o) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        ChumScoringRuleSO MakeRule()
        {
            var rule = Track(ScriptableObject.CreateInstance<ChumScoringRuleSO>());
            typeof(ScoringRuleSO).GetField("metric", System.Reflection.BindingFlags.Instance |
                                                     System.Reflection.BindingFlags.NonPublic)
                                 .SetValue(rule, ScoringMetric.PrismsDestroyed);
            typeof(ScoringRuleSO).GetField("golfRules", System.Reflection.BindingFlags.Instance |
                                                        System.Reflection.BindingFlags.NonPublic)
                                 .SetValue(rule, false);
            return rule;
        }

        GameDataSO MakeMatch(int domainCount, params (string name, Domains domain, int prisms)[] pilots)
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            gameData.RequestedDomainCount = domainCount;
            foreach (var (name, domain, prisms) in pilots)
                gameData.RoundStatsList.Add(new FakeStats { Name = name, Domain = domain, HostilePrismsDestroyed = prisms });
            return gameData;
        }

        // ── The reef ──

        [Test]
        public void Ladder_GrowsEveryRung()
        {
            int last = 0;
            for (int i = 1; i <= 4; i++)
            {
                int total = ChumReef.TotalPrisms(ChumReef.ForIntensity(i));
                Assert.Greater(total, last, $"I{i} lays more reef than the rung below");
                last = total;
            }
            Assert.IsFalse(ChumReef.ForIntensity(2).Crusted, "the fun rung has no crust");
            Assert.IsTrue(ChumReef.ForIntensity(3).Crusted && ChumReef.ForIntensity(4).Crusted);
        }

        [Test]
        public void Counts_MatchTheLaidList()
        {
            var pieces = new List<ChumReef.Piece>();
            for (int i = 1; i <= 4; i++)
            {
                var rung = ChumReef.ForIntensity(i);
                ChumReef.Lay(rung, Nucleus, Membrane, pieces);
                Assert.AreEqual(ChumReef.TotalPrisms(rung), pieces.Count, $"I{i}");
            }
            Assert.AreEqual(512, ChumReef.TotalPrisms(ChumReef.ForIntensity(1)));
            Assert.AreEqual(768, ChumReef.TotalPrisms(ChumReef.ForIntensity(2)));
        }

        [Test]
        public void Lay_IsTheSameListEveryTime()
        {
            var a = new List<ChumReef.Piece>();
            var b = new List<ChumReef.Piece>();
            ChumReef.Lay(ChumReef.ForIntensity(4), Nucleus, Membrane, a);
            ChumReef.Lay(ChumReef.ForIntensity(4), Nucleus, Membrane, b);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Position, b[i].Position);
                Assert.AreEqual(a[i].Shoal, b[i].Shoal);
                Assert.AreEqual(a[i].Shielded, b[i].Shielded);
            }
        }

        [Test]
        public void NoTwoPrismsTouch_AndEveryShoalIsBetweenNucleusAndMembrane()
        {
            var pieces = new List<ChumReef.Piece>();
            for (int i = 1; i <= 4; i++)
            {
                var rung = ChumReef.ForIntensity(i);
                ChumReef.Lay(rung, Nucleus, Membrane, pieces);
                float minSqr = float.MaxValue;   // the smallest Chebyshev gap
                for (int p = 0; p < pieces.Count; p++)
                {
                    float r = pieces[p].Position.magnitude;
                    Assert.Greater(r, Nucleus, $"I{i}: a prism inside the nucleus");
                    Assert.Less(r, Membrane, $"I{i}: a prism outside the membrane");
                    for (int q = p + 1; q < pieces.Count; q++)
                    {
                        var d = pieces[p].Position - pieces[q].Position;
                        minSqr = Mathf.Min(minSqr, Mathf.Max(Mathf.Abs(d.x), Mathf.Max(Mathf.Abs(d.y), Mathf.Abs(d.z))));
                    }
                }
                // Axis-aligned cubes overlap only when every axis gap is under the side, so the largest axis gap
                // (the Chebyshev distance) over the side means no two touch.
                Assert.Greater(minSqr, ChumReef.PrismSize, $"I{i}: two prisms closer than a side");
            }
        }

        [Test]
        public void Crust_IsOnlyTheOuterShell()
        {
            var pieces = new List<ChumReef.Piece>();
            var rung = ChumReef.ForIntensity(3);
            ChumReef.Lay(rung, Nucleus, Membrane, pieces);
            int shielded = 0;
            foreach (var p in pieces) if (p.Shielded) shielded++;
            Assert.AreEqual(rung.ShoalCount * ChumReef.ShellCount(rung.Shells), shielded);
        }

        [Test]
        public void Slot_IsJadeRubyGold_AndBlueIsNoTeam()
        {
            Assert.AreEqual(0, ChumReef.Slot(Domains.Jade));
            Assert.AreEqual(1, ChumReef.Slot(Domains.Ruby));
            Assert.AreEqual(2, ChumReef.Slot(Domains.Gold));
            Assert.AreEqual(-1, ChumReef.Slot(Domains.Blue));
        }

        [Test]
        public void EatenBook_CountsEachCreditOnce_AndClears()
        {
            var book = new ChumReef.EatenBook();
            book.Credit("A");
            book.Credit("A");
            book.Credit(null);
            Assert.AreEqual(2, book.Eaten("A"));
            Assert.AreEqual(0, book.Eaten("B"));
            book.Clear();
            Assert.AreEqual(0, book.Eaten("A"));
        }

        // ── The rule ──

        [Test]
        public void Rule_NeverEndsTheRoundEarly()
        {
            var rule = MakeRule();
            var match = MakeMatch(2, ("A", Domains.Jade, 100000), ("B", Domains.Ruby, 0));
            Assert.IsFalse(rule.IsObjectiveReached(match, out var winner), "Chum is decided by the clock");
            Assert.AreEqual(Domains.Blue, winner);
        }

        [Test]
        public void Rule_MostPrismsTaken_Wins_SummedByDomain()
        {
            var rule = MakeRule();
            var match = MakeMatch(2, ("A", Domains.Jade, 40), ("B", Domains.Jade, 40), ("C", Domains.Ruby, 70));
            Assert.AreEqual(Domains.Jade, rule.ResolveWinner(match), "two pilots' 80 beats one pilot's 70");
        }

        [Test]
        public void Rule_Results_ReadTheAuthoredTemplates_AndTheEatenBreakdown()
        {
            var rule = MakeRule();
            var match = MakeMatch(2, ("A", Domains.Jade, 90), ("B", Domains.Ruby, 30));
            rule.AssignScores(match, Domains.Jade, 90f);
            Assert.AreEqual(90f, match.RoundStatsList[0].Score, 1e-3f);
            var rows = rule.BuildResults(match, name => name == "A" ? 25 : 0);
            Assert.AreEqual("A", rows[0].Name);
            Assert.AreEqual("90 Prisms", rows[0].ScoreText);
            Assert.AreEqual("25 eaten", rows[0].Secondary);
            Assert.AreEqual("0 eaten", rows[1].Secondary);
            Assert.IsTrue(string.IsNullOrEmpty(rule.BuildResults(match)[0].Secondary),
                "with no snapshot the breakdown is blank rather than a guess");
        }

        private sealed class FakeStats : IRoundStats
        {
        // The interface, stubbed (copied from RegattaTeamPlayTests). Only Name, Domain,
        // HostileVolumeDestroyed and FusesBeaten are read by the rules under test; the rest
        // exist so the stub compiles.
        public string Name { get; set; }
        public Domains Domain { get; set; }
        public float Score { get; set; }
        public int BlocksCreated { get; set; }
        public int BlocksDestroyed { get; set; }
        public int BlocksRestored { get; set; }
        public int PrismStolen { get; set; }
        public int PrismsRemaining { get; set; }
        public int FriendlyPrismsDestroyed { get; set; }
        public int HostilePrismsDestroyed { get; set; }
        public float VolumeCreated { get; set; }
        public float TotalVolumeDestroyed { get; set; }
        public float VolumeRestored { get; set; }
        public float VolumeStolen { get; set; }
        public float VolumeRemaining { get; set; }
        public float FriendlyVolumeDestroyed { get; set; }
        public float HostileVolumeDestroyed { get; set; }
        public int CrystalsCollected { get; set; }
        public int OmniCrystalsCollected { get; set; }
        public int ElementalCrystalsCollected { get; set; }
        public float ChargeCrystalValue { get; set; }
        public float MassCrystalValue { get; set; }
        public float SpaceCrystalValue { get; set; }
        public float TimeCrystalValue { get; set; }
        public int SkimmerShipCollisions { get; set; }
        public int JoustCollisions { get; set; }
        public int GoalsScored { get; set; }
        public int LifeformsKilled { get; set; }
        public int BulletHitsLanded { get; set; }
        public int MissileHitsLanded { get; set; }
        public int DebuffHitsLanded { get; set; }
        public int StrikeHitsLanded { get; set; }
        public int CombatPoints { get; set; }
        public int SwitchesThreaded { get; set; }
        public int FusesBeaten { get; set; }
        public int Lives { get; set; }
        public bool IsEliminated { get; set; }
        public float FullSpeedStraightAbilityActiveTime { get; set; }
        public float RightStickAbilityActiveTime { get; set; }
        public float LeftStickAbilityActiveTime { get; set; }
        public float FlipAbilityActiveTime { get; set; }
        public float Button1AbilityActiveTime { get; set; }
        public float Button2AbilityActiveTime { get; set; }
        public float Button3AbilityActiveTime { get; set; }

#pragma warning disable 67   // never raised: the driver polls, it does not subscribe
        public event Action<IRoundStats> OnAnyStatChanged;
        public event Action OnScoreChanged;
        public event Action<IRoundStats> OnBlocksCreatedChanged;
        public event Action<IRoundStats> OnBlocksDestroyedChanged;
        public event Action<IRoundStats> OnBlocksRestoredChanged;
        public event Action<IRoundStats> OnPrismsStolenChanged;
        public event Action<IRoundStats> OnPrismsRemainingChanged;
        public event Action<IRoundStats> OnFriendlyPrismsDestroyedChanged;
        public event Action<IRoundStats> OnHostilePrismsDestroyedChanged;
        public event Action<IRoundStats> OnVolumeCreatedChanged;
        public event Action<IRoundStats> OnTotalVolumeDestroyedChanged;
        public event Action<IRoundStats> OnFriendlyVolumeDestroyedChanged;
        public event Action<IRoundStats> OnHostileVolumeDestroyedChanged;
        public event Action<IRoundStats> OnVolumeRestoredChanged;
        public event Action<IRoundStats> OnVolumeStolenChanged;
        public event Action<IRoundStats> OnVolumeRemainingChanged;
        public event Action<IRoundStats> OnCrystalsCollectedChanged;
        public event Action<IRoundStats> OnOmniCrystalsCollectedChanged;
        public event Action<IRoundStats> OnElementalCrystalsCollectedChanged;
        public event Action<IRoundStats> OnChargeCrystalValueChanged;
        public event Action<IRoundStats> OnMassCrystalValueChanged;
        public event Action<IRoundStats> OnSpaceCrystalValueChanged;
        public event Action<IRoundStats> OnTimeCrystalValueChanged;
        public event Action<IRoundStats> OnSkimmerShipCollisionsChanged;
        public event Action<IRoundStats> OnJoustCollisionChanged;
        public event Action<IRoundStats> OnGoalsScoredChanged;
        public event Action<IRoundStats> OnLifeformsKilledChanged;
        public event Action<IRoundStats> OnBulletHitsLandedChanged;
        public event Action<IRoundStats> OnMissileHitsLandedChanged;
        public event Action<IRoundStats> OnDebuffHitsLandedChanged;
        public event Action<IRoundStats> OnStrikeHitsLandedChanged;
        public event Action<IRoundStats> OnCombatPointsChanged;
        public event Action<IRoundStats> OnSwitchesThreadedChanged;
        public event Action<IRoundStats> OnFusesBeatenChanged;
        public event Action<IRoundStats> OnLivesChanged;
        public event Action<IRoundStats> OnEliminatedChanged;
        public event Action<IRoundStats> OnFullSpeedStraightAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnRightStickAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnLeftStickAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnFlipAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton1AbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton2AbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton3AbilityActiveTimeChanged;
#pragma warning restore 67
        }
    }
}
#endif
