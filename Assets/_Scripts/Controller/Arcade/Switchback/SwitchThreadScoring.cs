using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The ONE place a threaded Switchback gate is credited, shared by the two paths that can
    /// report one - the server crediting a pilot whose vessel it simulates directly, and
    /// <see cref="Player.ReportSwitchThreaded_ServerRpc"/> forwarding a client's own crossing.
    /// Same shape and same reason as <see cref="CombatHitScoring"/>: two call sites that must
    /// agree about what a report means, so the rule lives once instead of twice.
    ///
    /// <para><b>The validation is the whole point.</b> A gate report carries the INDEX the
    /// reporter believes it just crossed, and it is credited only when that index equals the
    /// pilot's current progress - which, because the course is ordered and
    /// <see cref="IRoundStats.SwitchesThreaded"/> is both the count and the next-gate index, is
    /// exactly "this is the gate they were allowed to thread next". A stale duplicate (the same
    /// crossing reported twice while the replicated mirror catches up) fails it, and so does a
    /// client claiming gate 19 from the starting line. Neither needs a separate guard.</para>
    /// </summary>
    public static class SwitchThreadScoring
    {
        /// <summary>
        /// Credit one threaded gate. Returns true when the report was accepted, so a caller can
        /// tell a real advance from a rejected duplicate without re-reading the stat.
        /// </summary>
        public static bool Credit(IRoundStats stats, int gateIndex)
        {
            if (stats == null) return false;
            if (gateIndex != stats.SwitchesThreaded) return false;

            stats.SwitchesThreaded = gateIndex + 1;
            return true;
        }
    }
}
