#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.UI;
using NUnit.Framework;
using W = CosmicShore.UI.ScreenSwitcher.ModalWindows;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The hub window slide (Docs/MENU_REDESIGN_B.md §47.2): LT / RT move along Toy Box, Arcade, Arena, Mission,
    /// stopping at the ends (the owner, 2026-10-17: "slide stop") and skipping a window its hub entry would refuse.
    /// </summary>
    public class HubWindowSlideTests
    {
        static readonly List<W> Row = new() { W.TOYBOX, W.ARCADE, W.ARENA, W.MISSION };
        static bool Any(W _) => true;

        [Test]
        public void RightAndLeftMoveOneWindow()
        {
            Assert.AreEqual(W.ARENA, ScreenSwitcher.NeighbourInRow(Row, W.ARCADE, +1, Any));
            Assert.AreEqual(W.TOYBOX, ScreenSwitcher.NeighbourInRow(Row, W.ARCADE, -1, Any));
        }

        [Test]
        public void TheSlideStopsAtBothEnds()
        {
            Assert.AreEqual(W.NONE, ScreenSwitcher.NeighbourInRow(Row, W.TOYBOX, -1, Any));
            Assert.AreEqual(W.NONE, ScreenSwitcher.NeighbourInRow(Row, W.MISSION, +1, Any));
        }

        [Test]
        public void ALockedWindowIsSkipped()
        {
            bool arenaLocked(W w) => w != W.ARENA;
            Assert.AreEqual(W.MISSION, ScreenSwitcher.NeighbourInRow(Row, W.ARCADE, +1, arenaLocked));
            Assert.AreEqual(W.ARCADE, ScreenSwitcher.NeighbourInRow(Row, W.MISSION, -1, arenaLocked));
        }

        [Test]
        public void ALockedLastWindowEndsTheRowEarly()
        {
            bool missionUnavailable(W w) => w != W.MISSION;
            Assert.AreEqual(W.NONE, ScreenSwitcher.NeighbourInRow(Row, W.ARENA, +1, missionUnavailable));
        }

        [Test]
        public void AWindowOutsideTheRowHasNoNeighbour()
        {
            Assert.AreEqual(W.NONE, ScreenSwitcher.NeighbourInRow(Row, W.ARCADE_GAME_CONFIGURE, +1, Any));
            Assert.AreEqual(W.NONE, ScreenSwitcher.NeighbourInRow(Row, W.ARCADE, 0, Any));
        }
    }
}
#endif
