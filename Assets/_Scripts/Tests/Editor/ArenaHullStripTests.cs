using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Arena hull strip is a window onto the card's hulls (<see cref="ArenaLaunchPanel.StripOffset"/>): the Regatta
    /// card lists nine hulls on a seven-tile strip, and every one of them must be reachable with the prev / next arrows.
    /// </summary>
    public class ArenaHullStripTests
    {
        [Test]
        public void EveryHullFits_NoSlide()
        {
            Assert.AreEqual(0, ArenaLaunchPanel.StripOffset(0, 5, 7, 7));
            Assert.AreEqual(0, ArenaLaunchPanel.StripOffset(3, 2, 4, 7), "a short card always starts at its first hull");
        }

        [Test]
        public void ThePickStaysInView_TheLastHullIsReachable()
        {
            // Nine hulls, seven tiles: stepping to the ninth (index 8, the Stoat on the Regatta card) slides by two.
            Assert.AreEqual(2, ArenaLaunchPanel.StripOffset(0, 8, 9, 7));
            Assert.AreEqual(1, ArenaLaunchPanel.StripOffset(0, 7, 9, 7));
            // Stepping back inside the window does not move it; past its left edge slides it just enough.
            Assert.AreEqual(2, ArenaLaunchPanel.StripOffset(2, 4, 9, 7));
            Assert.AreEqual(1, ArenaLaunchPanel.StripOffset(2, 1, 9, 7));
            // Wrapping from the last hull to the first (the arrows wrap) brings the window home.
            Assert.AreEqual(0, ArenaLaunchPanel.StripOffset(2, 0, 9, 7));
        }

        [Test]
        public void NeverPastEitherEnd()
        {
            Assert.AreEqual(2, ArenaLaunchPanel.StripOffset(5, 8, 9, 7));
            Assert.AreEqual(0, ArenaLaunchPanel.StripOffset(-3, -1, 9, 7));
            Assert.AreEqual(0, ArenaLaunchPanel.StripOffset(4, 3, 9, 0), "no tiles: nothing to slide");
        }

        [Test]
        public void EveryIndexOfANineHullCard_LandsOnATile()
        {
            int offset = 0;
            for (int pick = 0; pick < 9; pick++)
            {
                offset = ArenaLaunchPanel.StripOffset(offset, pick, 9, 7);
                Assert.That(pick - offset, Is.InRange(0, 6), $"hull {pick} has no tile at offset {offset}");
            }
        }
    }
}
