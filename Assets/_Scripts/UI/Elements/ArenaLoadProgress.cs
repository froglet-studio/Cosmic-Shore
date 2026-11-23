using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The connecting screen's 0..1 progress: three measured phases, each with a fixed share of the bar.
    ///
    /// <para><b>Every value it shows is a measurement</b> (Shombith, 2026-11-12: "Make the percentage real").
    /// Laying is prisms laid over prisms queued, growing is prisms settled over the most that were ever
    /// growing, and the last share is pilots who have reported their arena built over pilots in the match.
    /// Where nothing is measurable (the opening dwell, a gap between phases) the bar HOLDS: it used to creep
    /// on a timer there, which showed a number nothing had measured.</para>
    ///
    /// <para><b>It is MONOTONIC by construction.</b> A progress bar that goes backwards is worse
    /// than no progress bar - it turns "this is taking a while" into "something is wrong" - and the
    /// underlying signals genuinely do go backwards: <c>LayProgress</c> reads 1 while idle and
    /// drops to 0 the moment a batch starts, a second arena build re-queues the lay counters
    /// from scratch, and a pilot can leave mid-load. So the model never lowers its own output; a phase
    /// that would report less than what is already shown is simply not shown.</para>
    ///
    /// <para>Plain C# and pure per tick, so the behaviour that matters - monotonicity, the shares, and
    /// that it ends at exactly 1 - is provable without a play-mode session.</para>
    /// </summary>
    public class ArenaLoadProgress
    {
        /// <summary>Laying spans this band. It is the long phase, so it owns the most bar.</summary>
        public const float LayFloor = 0f;
        public const float LayCeiling = 0.60f;

        /// <summary>Growing spans this band - the placed prisms blooming in behind the veil.</summary>
        public const float GrowFloor = LayCeiling;
        public const float GrowCeiling = 0.90f;

        /// <summary>
        /// The pilots' share: this machine's arena is built, and the bar's last tenth is how many of the
        /// match's human pilots have reported theirs. A solo load has one pilot, so it finishes at once.
        /// </summary>
        public const float PilotsFloor = GrowCeiling;

        float _value;
        int _growPeak;

        /// <summary>The bar's value. Never decreases within a load.</summary>
        public float Value => _value;

        /// <summary>Start of a load. The ONLY place the value may go down.</summary>
        public void Reset()
        {
            _value = 0f;
            _growPeak = 0;
        }

        /// <summary>
        /// One frame. <paramref name="arenaReady"/> is this machine's arena-complete predicate; from then on
        /// the bar is the pilots' share, <paramref name="readyPilots"/> of <paramref name="totalPilots"/>
        /// (no pilots to wait on counts as all of them, so the bar still ends at exactly 1).
        /// </summary>
        public float Tick(bool laying, float layProgress, int growRemaining, bool arenaReady, int readyPilots, int totalPilots)
        {
            if (arenaReady)
            {
                float pilots = totalPilots > 0 ? Mathf.Clamp01((float)readyPilots / totalPilots) : 1f;
                return Raise(Mathf.Lerp(PilotsFloor, 1f, pilots));
            }

            if (laying)
                return Raise(Mathf.Lerp(LayFloor, LayCeiling, Mathf.Clamp01(layProgress)));

            if (growRemaining > 0)
            {
                // The denominator is the MOST still growing at any point this load: the count only
                // ever falls from its peak, and taking the first reading as the total would show
                // 0% forever on a build whose prisms are still being queued as others settle.
                if (growRemaining > _growPeak) _growPeak = growRemaining;
                float done = _growPeak > 0 ? 1f - (float)growRemaining / _growPeak : 1f;
                return Raise(Mathf.Lerp(GrowFloor, GrowCeiling, Mathf.Clamp01(done)));
            }

            // Nothing measurable: hold. The bar shows what was last measured and nothing more.
            return _value;
        }

        float Raise(float target)
        {
            if (target > _value) _value = target;
            return _value;
        }
    }
}
