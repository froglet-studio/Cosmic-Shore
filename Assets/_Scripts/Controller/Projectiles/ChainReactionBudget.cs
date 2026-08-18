using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The frame-rate brake on the Urchin's chain reaction — the third and last of three, and
    /// the only one that exists to protect the machine rather than the design.
    ///
    /// The three brakes, in order of authority:
    ///
    /// 1. **Territory conversion (emergent, primary).** A spike is refused by a prism that
    ///    already wears its domain (<see cref="Projectile.DisallowImpactOnPrism"/>), and the
    ///    cascade converts every prism it touches — so the wavefront extinguishes as it eats
    ///    its own frontier. This was the ONLY brake in the 2023 original and it is the reason
    ///    that build shipped at all. It stays primary: the cascade must die because it ran out
    ///    of enemy territory, not because a counter said so.
    /// 2. **Generation depth (authored).** <see cref="Projectile.ChainGeneration"/>, scaled by
    ///    the pilot's CHARGE level. This is the tuning dial.
    /// 3. **This budget (load shedding).** A hard ceiling on volleys per frame, so a cascade
    ///    that meets a dense prismscape degrades into a slower cascade instead of a frame
    ///    spike. It bounds COST, never coverage.
    ///
    /// Excess volleys are **dropped, not queued** — the same rule the fleet learned from the
    /// gun fire loop: a backlog drained later discharges a hitch as a burst, which is worse
    /// than the hitch. A dropped volley simply ends that branch of the cascade, which the
    /// other two brakes were going to do shortly anyway.
    ///
    /// The ceiling is deliberately GLOBAL rather than per-vessel: it exists to protect the
    /// frame, and the frame does not care which Urchin filled it. Four Urchins cascading at
    /// once contend for one budget, and that is the intended behaviour.
    ///
    /// Drops are counted and reported (throttled) rather than silent — a cap that hides its
    /// own truncation reads as "the mechanic is weak" instead of "the mechanic was shedding".
    /// </summary>
    public static class ChainReactionBudget
    {
        /// <summary>
        /// Volleys allowed to fire across all cascades in one frame. Each volley is up to
        /// <c>2 * (generation + 3)</c> spikes, so at the shipped depth of 4 one frame can add
        /// at most <c>6 x 14 = 84</c> live trigger colliders from chaining.
        ///
        /// The depth ladder this bounds, worst case per SEEDED hit (every child finding fresh
        /// enemy mass): depth 1 -> 8 spikes, depth 2 -> 90, depth 3 -> 1,092, depth 4 -> 15,302.
        /// **Depth 4 is what ships** (round 6: "dial up the recursive explosions") — which is
        /// exactly why this ceiling exists: the cascade's TOTAL population is unbounded in
        /// theory and bounded in practice by territory conversion, while its FRAME cost is
        /// bounded here regardless. Raised 4 -> 6 alongside the depth change so a deep cascade
        /// reads as a rolling barrage rather than a trickle of dropped branches. Real counts
        /// run far below the worst case because a converted prism stops accepting spikes, but
        /// a collider budget has to survive the worst case rather than the average one.
        /// </summary>
        public static int VolleysPerFrame = 6;

        /// <summary>Total volleys dropped by the ceiling since load — a diagnostics read.</summary>
        public static int DroppedVolleys { get; private set; }

        static int _frame = -1;
        static int _volleysThisFrame;
        static float _nextReportTime;

        /// <summary>
        /// Claims one volley slot for this frame. False means the caller must NOT fire — that
        /// branch of the cascade ends here.
        /// </summary>
        public static bool TryReserveVolley()
        {
            if (Time.frameCount != _frame)
            {
                _frame = Time.frameCount;
                _volleysThisFrame = 0;
            }

            if (_volleysThisFrame >= VolleysPerFrame)
            {
                DroppedVolleys++;
                Report();
                return false;
            }

            _volleysThisFrame++;
            return true;
        }

        static void Report()
        {
            if (Time.unscaledTime < _nextReportTime) return;
            _nextReportTime = Time.unscaledTime + 5f;
            CSDebug.LogWarning(
                $"[ChainReactionBudget] Shed a chain volley - ceiling is {VolleysPerFrame}/frame " +
                $"({DroppedVolleys} dropped so far). Raise ChainReactionBudget.VolleysPerFrame " +
                "if the cascade should carry further, or lower the ability's generation count.");
        }

        /// <summary>Test / scene-teardown hook: forget the accumulated drop count.</summary>
        public static void ResetDiagnostics()
        {
            DroppedVolleys = 0;
            _nextReportTime = 0f;
        }
    }
}
