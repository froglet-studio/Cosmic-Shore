using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The arcade grid is AUTHORED at a fixed size (3 rows x 4 = 12 slots in Menu_Main) and the
    /// populate loop is bounded by it, so a roster that outgrows it truncates SILENTLY - the
    /// alphabetically-last modes simply stop existing in the arcade, with no error and no gap in
    /// the grid to notice. Shipping Switchback is what crossed the line (13 renderable cards into
    /// 12 slots), which is why the arithmetic that grows the grid is asserted here rather than
    /// eyeballed in a scene.
    /// </summary>
    public class ArcadeGridCapacityTests
    {
        [Test]
        public void FittingRoster_AddsNoRows()
        {
            Assert.AreEqual(0, ArcadeExploreView.RowsNeeded(12, 4, 12));
            Assert.AreEqual(0, ArcadeExploreView.RowsNeeded(12, 4, 5));
            Assert.AreEqual(0, ArcadeExploreView.RowsNeeded(12, 4, 0));
        }

        [Test]
        public void OneCardOverflow_AddsExactlyOneRow()
        {
            // The shipped case: 12 authored slots, 13 renderable cards.
            Assert.AreEqual(1, ArcadeExploreView.RowsNeeded(12, 4, 13));
        }

        [Test]
        public void PartialRowStillCountsAsAWholeRow()
        {
            Assert.AreEqual(1, ArcadeExploreView.RowsNeeded(12, 4, 16));
            Assert.AreEqual(2, ArcadeExploreView.RowsNeeded(12, 4, 17));
            Assert.AreEqual(2, ArcadeExploreView.RowsNeeded(12, 4, 20));
            Assert.AreEqual(3, ArcadeExploreView.RowsNeeded(12, 4, 21));
        }

        [Test]
        public void ResultAlwaysClosesTheGap()
        {
            for (int perRow = 1; perRow <= 6; perRow++)
            for (int capacity = 0; capacity <= 24; capacity++)
            for (int required = 0; required <= 40; required++)
            {
                int rows = ArcadeExploreView.RowsNeeded(capacity, perRow, required);
                Assert.GreaterOrEqual(capacity + rows * perRow, required,
                    $"capacity {capacity}, perRow {perRow}, required {required} left a deficit");
                // And never adds a row it did not need.
                if (rows > 0)
                    Assert.Less(capacity + (rows - 1) * perRow, required,
                        $"capacity {capacity}, perRow {perRow}, required {required} over-allocated");
            }
        }

        [Test]
        public void EmptyTemplateRowCanNeverCloseTheGap_AndIsNotAskedTo()
        {
            // Guards the infinite loop the earlier while-form would have run on a 0-child row.
            Assert.AreEqual(0, ArcadeExploreView.RowsNeeded(0, 0, 13));
        }
    }
}
