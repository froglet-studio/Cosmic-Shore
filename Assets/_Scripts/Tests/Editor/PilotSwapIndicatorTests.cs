#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The top bar's pilot-swap indicator (Docs/GAME_MODE_TOPBAR.md §2.7). Its promise is that the
    /// tiles show exactly where a D-pad / 1-2 press goes, so the target resolution must agree with the
    /// gesture's own ring pick; and on a phone the tiles ARE the control, so they must be a fingertip
    /// wide and every arena hull must have an icon to draw.
    /// </summary>
    public class PilotSwapIndicatorTests
    {
        const string ArenaGamesPath = "Assets/_SO_Assets/Games/GameLists/ArenaGames.asset";

        // ── where each tile points ───────────────────────────────────────────────────────────

        [Test]
        public void Targets_LeftIsPrevious_RightIsNext_TheSameAsTheGesture()
        {
            var hulls = new List<ulong> { 40, 10, 30, 20 };
            var ring = new List<ulong>();

            int count = PilotSwap.ResolveSwapTargets(hulls, 20, ring, out var left, out var right);

            Assert.AreEqual(2, count);
            Assert.AreEqual(10UL, left, "LEFT is D-pad left / key 1: the previous hull round the ring");
            Assert.AreEqual(30UL, right, "RIGHT is D-pad right / key 2: the next hull round the ring");

            Assert.IsTrue(PilotSwap.TryPickRingTarget(hulls, 20, -1, out var gestureLeft));
            Assert.IsTrue(PilotSwap.TryPickRingTarget(hulls, 20, +1, out var gestureRight));
            Assert.AreEqual(gestureLeft, left);
            Assert.AreEqual(gestureRight, right);
        }

        [Test]
        public void Targets_OneTeammate_IsOneTile_BothWaysTheSameHull()
        {
            var ring = new List<ulong>();
            int count = PilotSwap.ResolveSwapTargets(new List<ulong> { 10, 20 }, 10, ring, out var left, out var right);

            Assert.AreEqual(1, count);
            Assert.AreEqual(20UL, left);
            Assert.AreEqual(20UL, right);
        }

        [Test]
        public void Targets_NoTeammate_IsNothing()
        {
            var ring = new List<ulong>();
            Assert.AreEqual(0, PilotSwap.ResolveSwapTargets(new List<ulong> { 10 }, 10, ring, out _, out _));
            Assert.AreEqual(0, PilotSwap.ResolveSwapTargets(new List<ulong>(), 10, ring, out _, out _));
            Assert.AreEqual(0, PilotSwap.ResolveSwapTargets(null, 10, ring, out _, out _));
        }

        [Test]
        public void Targets_ReuseTheScratchRing_WithoutCarryingOldEntries()
        {
            var ring = new List<ulong> { 99, 98, 97 };   // stale contents from an earlier poll
            int count = PilotSwap.ResolveSwapTargets(new List<ulong> { 5, 6, 7 }, 6, ring, out var left, out var right);

            Assert.AreEqual(2, count);
            Assert.AreEqual(5UL, left);
            Assert.AreEqual(7UL, right);
        }

        // ── fingertip size and placement ─────────────────────────────────────────────────────

        [Test]
        public void TileUnits_IsAtLeastNineMillimetres_OnAHighDensityPhone()
        {
            // 400 dpi, canvas scale 1: 9 mm = 141.7 px = 141.7 units, well above the authored 72.
            float units = PilotSwapIndicator.TileUnits(72f, 9f, 400f, 1f);
            Assert.AreEqual(9f / 25.4f * 400f, units, 0.01f);
        }

        [Test]
        public void TileUnits_KeepsTheAuthoredSize_WhenThatIsAlreadyBigEnough()
        {
            Assert.AreEqual(72f, PilotSwapIndicator.TileUnits(72f, 9f, 96f, 1f), 0.001f);
        }

        [Test]
        public void TileUnits_UnknownDpi_FallsBackTo160_AndDividesByTheCanvasScale()
        {
            float expected = 9f / 25.4f * 160f / 0.5f;
            Assert.AreEqual(expected, PilotSwapIndicator.TileUnits(10f, 9f, 0f, 0.5f), 0.01f);
        }

        [Test]
        public void PlaceInGap_CentresWhenItFits_ElseHugsTheScoreBar()
        {
            Assert.AreEqual(500f, PilotSwapIndicator.PlaceInGap(400f, 600f, 100f), 0.001f);
            Assert.AreEqual(450f, PilotSwapIndicator.PlaceInGap(400f, 600f, 300f), 0.001f,
                "too wide for the gap: the right edge stays on the score bar's margin");
        }

        // ── the shipped config ───────────────────────────────────────────────────────────────

        [Test]
        public void Config_Ships_AndResolvesAnIcon_ForEveryArenaHull()
        {
            var config = Resources.Load<PilotSwapIndicatorConfigSO>(PilotSwapIndicator.ConfigResourcePath);
            Assert.IsNotNull(config, $"Resources/{PilotSwapIndicator.ConfigResourcePath} is missing: " +
                                     "the top bar would show no swap targets and a phone could not swap");
            Assert.IsNotNull(config.Vessels, "the config names no vessel list, so no tile can draw a hull");
            Assert.GreaterOrEqual(config.MinTouchMillimetres, 9f, "a tile must stay a fingertip target");

            var arena = AssetDatabase.LoadAssetAtPath<SO_GameList>(ArenaGamesPath);
            Assert.IsNotNull(arena, $"{ArenaGamesPath} is missing");

            foreach (var card in arena.Games)
            {
                if (!card || card.Vessels == null) continue;
                foreach (var vessel in card.Vessels)
                {
                    if (!vessel) continue;
                    Assert.IsTrue(config.Vessels.TryGetVesselByClass(vessel.Class, out var listed, reportMissing: false),
                        $"{card.name}: {vessel.Class} is not in the indicator's vessel list, so its tile would be blank");
                    Assert.IsTrue(listed.IconActive || listed.IconInactive,
                        $"{card.name}: {vessel.Class} has no icon for the indicator to draw");
                }
            }
        }
    }
}
#endif
