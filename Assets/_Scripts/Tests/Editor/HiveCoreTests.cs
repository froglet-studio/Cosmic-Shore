#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Ratel's hive mode (Garrett, 2026-10-31; HONEYGUIDE.md "Hive mode"). The Ratel Raid Lab
    /// (<c>Docs/Studios/HoneyguideRaidLab.html</c>, the <c>hive-core</c> block) is the record:
    /// <c>Tools/Build/hive_lab/parity.cjs</c> holds <see cref="HiveCore"/> to it step by step. These tests pin Garrett's rules
    /// on the C# core itself: every hive is solvable and holds all three domains, a shield in each and some danger; a chain is
    /// at least 3 of one other domain; danger stings and breaks nothing; a shield takes two passes; completing a colour pays,
    /// and both colours pay our own too; the rewards land on the largest planar crystal.
    /// </summary>
    public class HiveCoreTests
    {
        const string ConfigPath = "Assets/_SO_Assets/VesselActions/Honeyguide/HoneyguideConfig.asset";
        static readonly HiveSettings P = new();

        static HiveCell[] Empty() => Enumerable.Range(0, DynamoCore.CellCount).Select(_ => new HiveCell()).ToArray();
        static int I(int q, int r) => DynamoCore.Index(q, r);

        [Test]
        public void EveryHive_IsSolvable_AndHoldsThreeDomains_AShieldInEach_AndDanger()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                int own = 1 + seed % 3;
                var hive = HiveCore.MakeHive(DynamoCore.Mulberry32(seed * 977 + 3), P, own);
                Assert.IsNotNull(hive, $"seed {seed}: no hive made");
                Assert.IsTrue(HiveCore.HiveHolds(hive.Cells, P, out int domains, out int shielded, out int danger),
                              $"seed {seed}: {domains} domains, {shielded} shielded, {danger} danger");
                Assert.IsNotNull(HiveCore.SolveHive(hive.Cells, P.MinChain, own), $"seed {seed}: the hive is not solvable");
                Assert.IsNotNull(hive.Solution, $"seed {seed}: no solution kept for the AI");
            }
        }

        [Test]
        public void TheSolver_CatchesAStar_AndSolvesALine()
        {
            var star = Empty();
            int c = DynamoCore.Centre;
            star[c].Dom = 1; star[c].Hits = 1;
            foreach (int n in DynamoCore.Neighbours[c].Where((_, k) => k % 2 == 0)) { star[n].Dom = 1; star[n].Hits = 1; }
            Assert.IsNull(HiveCore.SolveHive(star, 3, 2), "a centre with three leaves cannot be cleared by chains of 3");
            var line = Empty();
            foreach (int i in new[] { I(-1, 0), I(0, 0), I(1, 0) }) { line[i].Dom = 1; line[i].Hits = 1; }
            Assert.IsNotNull(HiveCore.SolveHive(line, 3, 2), "a line of 3 is one chain");
        }

        static Hive Line(int colour, int hits, int own)
        {
            var cells = Empty();
            foreach (int i in new[] { I(-1, 0), I(0, 0), I(1, 0) }) { cells[i].Dom = colour; cells[i].Hits = hits; }
            cells[I(2, 0)].Dom = 3; cells[I(2, 0)].Hits = 1; cells[I(2, 0)].Danger = true;
            cells[I(-2, 0)].Dom = own; cells[I(-2, 0)].Hits = 1;
            return HiveCore.Create(cells, HiveCore.SolveHive(cells, 3, own), 0, own);
        }

        [Test]
        public void AChainOfTwo_IsDropped_AndOurOwnDomain_IsTheHivesBody()
        {
            var hive = Line(1, 1, 2);
            Assert.AreEqual(HiveCore.Refused, HiveCore.ChainStart(hive, I(-2, 0), P, 2), "our own domain is never chained");
            Assert.AreEqual(HiveCore.Grew, HiveCore.ChainStart(hive, I(-1, 0), P, 2));
            Assert.AreEqual(HiveCore.Grew, HiveCore.ChainAdd(hive, I(0, 0), P));
            Assert.IsNull(HiveCore.ChainEnd(hive, P, 2), "a chain of 2 breaks nothing");
            Assert.AreEqual(3, HiveCore.Remaining(hive, 1));
        }

        [Test]
        public void ADangerCell_Stings_AndBreaksNothing()
        {
            var hive = Line(1, 1, 2);
            HiveCore.ChainStart(hive, I(-1, 0), P, 2);
            HiveCore.ChainAdd(hive, I(0, 0), P);
            HiveCore.ChainAdd(hive, I(1, 0), P);
            Assert.AreEqual(HiveCore.Stung, HiveCore.ChainAdd(hive, I(2, 0), P), "the chain touched danger");
            Assert.AreEqual(0, hive.Chain.Count, "a sting breaks the chain");
            Assert.AreEqual(3, HiveCore.Remaining(hive, 1), "and nothing in it is destroyed");
            Assert.AreEqual(P.StingSeconds, hive.Stun, 1e-9);
            Assert.AreEqual(HiveCore.Refused, HiveCore.ChainStart(hive, I(-1, 0), P, 2), "the thumb is stunned");
            HiveCore.Tick(hive, P.StingSeconds + 0.01);
            Assert.AreEqual(HiveCore.Grew, HiveCore.ChainStart(hive, I(-1, 0), P, 2), "and free again after the stun");
        }

        [Test]
        public void AShield_TakesTwoPasses()
        {
            var hive = Line(1, 2, 2);
            for (int pass = 1; pass <= 2; pass++)
            {
                HiveCore.ChainStart(hive, I(-1, 0), P, 2);
                HiveCore.ChainAdd(hive, I(0, 0), P);
                HiveCore.ChainAdd(hive, I(1, 0), P);
                var res = HiveCore.ChainEnd(hive, P, 2);
                Assert.IsNotNull(res);
                Assert.AreEqual(pass == 1 ? 0 : 3, res.Broken, $"pass {pass}");
                Assert.AreEqual(pass == 1 ? 3 : 0, HiveCore.Remaining(hive, 1), $"pass {pass}");
            }
        }

        [Test]
        public void CompletingAColour_Pays_AndBothColours_PayOurOwnToo()
        {
            var cells = Empty();
            foreach (int i in new[] { I(-1, -1), I(0, -1), I(1, -1) }) { cells[i].Dom = 1; cells[i].Hits = 1; }
            foreach (int i in new[] { I(-1, 1), I(0, 1), I(1, 1) }) { cells[i].Dom = 3; cells[i].Hits = 1; }
            var hive = HiveCore.Create(cells, null, 0, 2);
            void Chain(params int[] p)
            {
                HiveCore.ChainStart(hive, p[0], P, 2);
                for (int k = 1; k < p.Length; k++) HiveCore.ChainAdd(hive, p[k], P);
                HiveCore.ChainEnd(hive, P, 2);
            }
            Chain(I(-1, -1), I(0, -1), I(1, -1));
            CollectionAssert.AreEqual(new[] { 1 }, hive.Rewards, "one colour done: its reward");
            Chain(I(-1, 1), I(0, 1), I(1, 1));
            CollectionAssert.AreEqual(new[] { 1, 3, 2 }, hive.Rewards, "both done: our own colour too");
            Assert.AreEqual(HiveCore.EndClear, HiveCore.EndReason(hive, 0, P, 2));
        }

        [Test]
        public void TheLargestPlanarCrystal_IsFound_AndTheRewardsGrowIt()
        {
            var board = new int[DynamoCore.CellCount];
            Assert.AreEqual(0, HiveCore.LargestCrystal(board).Size, "an empty board has no crystal");
            var perm = HiveCore.Perms[3];
            int c = DynamoCore.Centre;
            foreach (int i in new[] { c, DynamoCore.Neighbours[c][0], DynamoCore.Neighbours[c][1] }) board[i] = perm[HiveCore.Class[i]];
            var lc = HiveCore.LargestCrystal(board);
            Assert.AreEqual(3, lc.Size);
            CollectionAssert.AreEqual(perm, lc.Perm);
            var landed = new List<HiveCore.Landed>();
            var after = HiveCore.PlaceRewards(board, new[] { 1, 3 }, 2, landed);
            Assert.AreEqual(4, landed.Count);
            foreach (var l in landed)
            {
                Assert.AreEqual(0, l.Tier, "each reward lands on the crystal");
                Assert.AreEqual(l.Colour, perm[HiveCore.Class[l.Cell]], "in the colour its class wants");
            }
            Assert.AreEqual(7, after.Size, "the crystal grew by the four");
        }

        [Test]
        public void AHive_CopiedToAnotherMachine_IsTheSameHive()
        {
            var hive = HiveCore.MakeHive(DynamoCore.Mulberry32(41), P, 1);
            var plan = new HiveCore.AIPlan();
            var path = HiveCore.AINextChain(hive, plan, true, 0, DynamoCore.Mulberry32(2), P, 1);
            HiveCore.ChainStart(hive, path[0], P, 1);
            HiveCore.ChainAdd(hive, path[1], P);
            var state = new List<int>();
            HiveCore.WriteState(hive, state);
            int at = 0;
            var copy = HiveCore.ReadState(state.ToArray(), ref at);
            Assert.IsNotNull(copy);
            Assert.AreEqual(state.Count, at);
            Assert.AreEqual(HiveCore.Digest(hive), HiveCore.Digest(copy));
            int cut = 0;
            Assert.IsNull(HiveCore.ReadState(state.GetRange(0, state.Count - 3).ToArray(), ref cut), "a cut-short hive was read");
        }

        [Test]
        public void TheConfig_CarriesTheRaidLabsNumbers()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<HoneyguideConfigSO>(ConfigPath);
            Assert.IsNotNull(cfg);
            var s = cfg.ToHiveSettings();
            Assert.AreEqual(3, s.MinChain, "Garrett 2026-10-31: the shortest chain is 3");
            Assert.AreEqual(2, s.RewardDynamo, "2 prisms of a completed colour on the Dynamo");
            Assert.AreEqual(1, s.RewardBattery, "and 1 in the battery");
            Assert.GreaterOrEqual(s.HiveDangerMin, 1, "every hive holds some danger");
            Assert.AreEqual(15.0, s.RaidSeconds, 1e-9);
            Assert.IsTrue(cfg.HiveMode, "an omnicrystal opens hive mode");
            Assert.IsTrue(cfg.HiveCompletesSpin, "\"the ratel ends their current timer\": the spin completes");
        }
    }
}
#endif
