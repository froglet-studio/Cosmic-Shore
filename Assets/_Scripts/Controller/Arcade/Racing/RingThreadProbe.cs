using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A second body a vessel threads race rings with, beside its hull. The Thresher's wrecking ball is the
    /// one shipped: Garrett, 2026-10-19, "the thresher should activate rings like in its racing game with
    /// either the vessel or the ball". Implemented by the vessel's own executor and found under the vessel,
    /// so a ring system never names a hull class.
    /// </summary>
    public interface IRingThreadProbe
    {
        /// <summary>Where the probe is now, and its EPOCH: a count that changes whenever the body is
        /// re-placed rather than moved (the ball re-seeded beside a respawned hull). A segment across an
        /// epoch change is a jump and threads nothing, the same rule as
        /// <see cref="VesselTransformer.TeleportCount"/>. False while there is no body to test.</summary>
        bool TryGetRingProbe(out Vector3 position, out int epoch);
    }

    /// <summary>
    /// A probe that also trips the Toybox's switches (<see cref="Toy"/>): the Wrasse's kite (Garrett, 2026-10-20: "make it
    /// so the wrasse can hit switches"). A separate opt-in, so a probe that only threads race rings (the Thresher's ball)
    /// does not start firing every toy it swings past.
    /// </summary>
    public interface ISwitchThreadProbe : IRingThreadProbe { }

    /// <summary>
    /// One pilot's probe samples, held by each ring system beside its hull sample
    /// (<see cref="GateRaceController"/>, <see cref="CrystalRaceRing"/>, <see cref="ModePreviewGateCourse"/>).
    /// <see cref="Advance"/> is called once per frame for every pilot, whatever the hull test then does, so
    /// the next segment is always one frame long.
    /// </summary>
    public sealed class RingThreadProbeSample
    {
        IVessel _vessel;
        IRingThreadProbe _probe;
        Vector3 _last;
        int _epoch;
        bool _has;

        /// <summary>Drop the last sample: the next frame seeds instead of testing.</summary>
        public void Forget() => _has = false;

        /// <summary>True when the last <see cref="Advance(IVessel, float, out Vector3, out Vector3)"/> found a live probe:
        /// the vessel threads with a second body this frame.</summary>
        public bool Live => _has;

        /// <summary>
        /// Sample <paramref name="vessel"/>'s probe and return the segment it flew since the last frame.
        /// False on the first sample, after an epoch change, for a hull with no probe, and for a step longer
        /// than <paramref name="maxStepSqr"/> allows (a respawn or a hitch is not a flight).
        /// </summary>
        public bool Advance(IVessel vessel, float maxStepSqr, out Vector3 prev, out Vector3 cur)
        {
            if (!ReferenceEquals(vessel, _vessel))
            {
                // A swapped or respawned hull is a new body: resolved once, a hull without one cached as null.
                _vessel = vessel;
                _probe = vessel is { Transform: var t } && t ? t.GetComponentInChildren<IRingThreadProbe>(true) : null;
                _has = false;
            }
            return Advance(_probe, maxStepSqr, out prev, out cur);
        }

        /// <summary>The same, for a probe already in hand (the tests drive this one).</summary>
        public bool Advance(IRingThreadProbe probe, float maxStepSqr, out Vector3 prev, out Vector3 cur)
        {
            prev = cur = default;
            if (probe == null || probe is Object uo && !uo || !probe.TryGetRingProbe(out cur, out int epoch))
            {
                _has = false;
                return false;
            }

            bool flew = _has && epoch == _epoch && (cur - _last).sqrMagnitude <= maxStepSqr;
            prev = _last;
            _last = cur;
            _epoch = epoch;
            _has = true;
            return flew;
        }
    }
}
