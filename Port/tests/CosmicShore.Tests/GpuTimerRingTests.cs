using CosmicShore.Render;
using Xunit;

namespace CosmicShore.Tests
{
    public class GpuTimerRingTests
    {
        [Fact]
        public void A_slot_is_read_only_after_the_ring_comes_round()
        {
            var ring = new GpuTimerRing(4);
            for (int frame = 0; frame < 4; frame++)
            {
                Assert.Equal(frame, ring.Slot);
                Assert.False(ring.PendingInSlot); // nothing issued here yet: no read, no stall
                ring.Issued();
            }
            Assert.Equal(0, ring.Slot);
            Assert.True(ring.PendingInSlot); // frame 0's queries, four frames late
            ring.Harvested(available: true);
            Assert.False(ring.PendingInSlot);
            Assert.Equal(0, ring.Dropped);
        }

        [Fact]
        public void A_result_still_in_flight_is_dropped_not_waited_on()
        {
            var ring = new GpuTimerRing(2);
            ring.Issued();
            ring.Issued();
            ring.Harvested(available: false);
            Assert.Equal(1, ring.Dropped);
            Assert.False(ring.PendingInSlot); // the slot is free for this frame's queries
        }

        [Fact]
        public void A_frame_that_draws_nothing_leaves_the_ring_where_it_was()
        {
            var ring = new GpuTimerRing(3);
            ring.Issued();
            int slot = ring.Slot;
            Assert.False(ring.PendingInSlot); // skipped render: no Issued call
            Assert.Equal(slot, ring.Slot);
        }
    }
}
