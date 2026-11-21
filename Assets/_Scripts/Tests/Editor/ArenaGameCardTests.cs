#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The arena card's "2 - 6 pilots" / "2 domains" ranges (KitArenaGameCard.Range): one number when
    /// the card's bounds agree, "min - max" otherwise.
    /// </summary>
    [TestFixture]
    public class ArenaGameCardTests
    {
        [TestCase(2, 6, "2 - 6")]
        [TestCase(2, 2, "2")]
        [TestCase(1, 3, "1 - 3")]
        public void RangeReadsOneNumberOrMinToMax(int min, int max, string expected)
        {
            Assert.AreEqual(expected, KitArenaGameCard.Range(min, max));
        }
    }
}
#endif
