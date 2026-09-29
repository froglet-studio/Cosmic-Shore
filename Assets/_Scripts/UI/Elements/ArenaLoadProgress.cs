using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The connecting screen's 0..1 progress, folded from the arena build's two measurable phases
    /// plus the stretches where nothing is measurable at all.
    ///
    /// <para><b>It is MONOTONIC by construction.</b> A progress bar that goes backwards is worse
    /// than no progress bar - it turns "this is taking a while" into "something is wrong" - and the
    /// underlying signals genuinely do go backwards: <c>LayProgress</c> reads 1 while idle and
    /// drops to 0 the moment a batch starts, and a second arena build re-queues the lay counters
    /// from scratch. So the model never lowers its own output; a phase that would report less than
    /// what is already shown is simply not shown.</para>
    ///
    /// <para><b>Where nothing is measurable it CREEPS.</b> Two spans have no denominator: the
    /// opening dwell before any build is announced, and any gap between phases. Sitting still there
    /// reads as a hang, and jumping ahead lies. It eases toward the current phase's ceiling at a
    /// rate that never reaches it, so the bar always moves and never overtakes real progress.</para>
    ///
    /// <para>Plain C# and pure per tick, so the behaviour that matters - monotonicity, and that it
    /// ends at exactly 1 - is provable without a play-mode session.</para>
    /// </summary>
    public class ArenaLoadProgress
    {
        /// <summary>Ceiling of the pre-build creep: the dwell can never look more than 5% done.</summary>
        public const float DwellCeiling = 0.05f;

        /// <summary>Laying spans this band. It is the long phase, so it owns the most bar.</summary>
        public const float LayFloor = DwellCeiling;
        public const float LayCeiling = 0.60f;

        /// <summary>Growing spans this band - the placed prisms blooming in behind the veil.</summary>
        public const float GrowFloor = LayCeiling;
        public const float GrowCeiling = 0.95f;

        /// <summary>Seconds the creep would take to close the remaining gap to a ceiling.</summary>
        const float CreepSeconds = 6f;

        /// <summary>
        /// The furthest phase this load has ENTERED. The creep's ceiling is a function of this
        /// rather than of the current value: inferring it from the value ("we are past the lay
        /// band, so creep toward the grow ceiling") relies on a lerp never quite reaching its
        /// target, which is a float accident standing in for a decision - and the day it does
        /// reach it, the dwell creeps all the way to 60% before a single prism is laid.
        /// </summary>
        enum Phase { Dwell = 0, Laying = 1, Growing = 2 }

        float _value;
        int _growPeak;
        Phase _phase;

        /// <summary>The bar's value. Never decreases within a load.</summary>
        public float Value => _value;

        /// <summary>Start of a load. The ONLY place the value may go down.</summary>
        public void Reset()
        {
            _value = 0f;
            _growPeak = 0;
            _phase = Phase.Dwell;
        }

        /// <summary>
        /// One frame. <paramref name="ready"/> is the arena-complete predicate: once it is true the
        /// bar finishes, because the panel is about to come down and a bar that vanishes at 0.9
        /// reads as an abandoned load.
        /// </summary>
        public float Tick(float deltaTime, bool laying, float layProgress, int growRemaining, bool ready)
        {
            if (ready) return Raise(1f);

            if (laying)
            {
                if (_phase < Phase.Laying) _phase = Phase.Laying;
                return Raise(Mathf.Lerp(LayFloor, LayCeiling, Mathf.Clamp01(layProgress)));
            }

            if (growRemaining > 0)
            {
                if (_phase < Phase.Growing) _phase = Phase.Growing;
                // The denominator is the MOST still growing at any point this load: the count only
                // ever falls from its peak, and taking the first reading as the total would show
                // 0% forever on a build whose prisms are still being queued as others settle.
                if (growRemaining > _growPeak) _growPeak = growRemaining;
                float done = _growPeak > 0 ? 1f - (float)growRemaining / _growPeak : 1f;
                return Raise(Mathf.Lerp(GrowFloor, GrowCeiling, Mathf.Clamp01(done)));
            }

            // Nothing measurable. Creep toward the ceiling of the furthest phase actually seen -
            // never past it, so the bar cannot claim a phase that has not started.
            float ceiling = _phase switch
            {
                Phase.Dwell => DwellCeiling,
                Phase.Laying => GrowFloor,
                _ => GrowCeiling,
            };

            _value = Mathf.Lerp(_value, ceiling, Mathf.Clamp01(deltaTime / CreepSeconds));
            return _value;
        }

        float Raise(float target)
        {
            if (target > _value) _value = target;
            return _value;
        }
    }
}
