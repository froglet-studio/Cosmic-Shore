using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Ark's reactor rules (<see cref="CosmicShore.ScriptableObjects.ArkReactorSO"/>): the omni crystal aboard powers
    /// the hull's regrowth at a FIXED RATE, one prism a beat, into the empty slot nearest the reactor. Pure functions, so
    /// the rules are testable without a scene.
    /// </summary>
    public static class ArkReactor
    {
        /// <summary>
        /// The fixed-rate clock: true when a beat is due at <paramref name="now"/>, advancing <paramref name="next"/> by one
        /// period. A clock more than a period behind (a stall, a hitch, a hull that had nothing to regrow) snaps forward
        /// instead of paying the missed beats back in a burst: the rate is fixed, never banked.
        /// </summary>
        public static bool Beat(ref float next, float now, float period)
        {
            if (now < next) return false;
            period = Mathf.Max(0.01f, period);
            next = now - next >= period ? now + period : next + period;
            return true;
        }

        /// <summary>
        /// The empty slot nearest <paramref name="reactor"/> (the regrowth spreads out from the reactor well), or -1 when
        /// none is empty. <paramref name="positions"/> and <paramref name="empty"/> are parallel, in one space.
        /// </summary>
        public static int NearestEmpty(IReadOnlyList<Vector3> positions, IReadOnlyList<bool> empty, Vector3 reactor)
        {
            int best = -1;
            float bestSqr = float.PositiveInfinity;
            int n = Mathf.Min(positions.Count, empty.Count);
            for (int i = 0; i < n; i++)
            {
                if (!empty[i]) continue;
                float d = (positions[i] - reactor).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = i;
                }
            }
            return best;
        }
    }
}
