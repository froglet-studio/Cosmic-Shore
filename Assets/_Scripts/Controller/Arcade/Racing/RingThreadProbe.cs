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
    /// A probe that also trips the Toybox's switches (<see cref="Toy"/>): the Thresher's ball, the flail (Garrett,
    /// 2026-10-20: "lets flip things so the wrasse does not activate toys, but the thresher flail does"). A separate opt-in,
    /// so a probe that only threads race rings (the Wrasse's kite) does not fire every toy it swings past.
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

    /// <summary>
    /// A probe body flown on ANOTHER machine, replayed where the rings are tested: the path it flew, in the world, played
    /// back one step a frame (a crew seat a person rides from another machine - the Wrasse's kite, <c>KiteNavigator</c>).
    /// The rings are static and the same on every machine, so the segment a ring tests is one the body really flew. The
    /// replay never runs ahead of what has arrived, keeps each step under the ring systems' own guard
    /// (<see cref="RingThreadProbeSample"/>'s max step), catches up at that speed when it falls behind, and moves the
    /// epoch on a re-seed or a jump, which therefore threads nothing.
    /// </summary>
    public sealed class RingProbeReplay
    {
        /// <summary>The ring systems refuse a step longer than maxPlausibleSpeed x dt x 2 + 5; 400 u/s is their default
        /// (GateRaceController, CrystalRaceRing). The replay keeps to 90% of it.</summary>
        public const float PlausibleSpeed = 400f;
        /// <summary>A recorded step longer than this is the body being re-placed, not flying.</summary>
        public const float JumpDistance = 300f;
        /// <summary>The most points held; past it the oldest go (the body is then far ahead, and the jump re-seeds).</summary>
        public const int MaxQueued = 256;

        readonly System.Collections.Generic.Queue<Vector3> _path = new();
        int _sourceEpoch = int.MinValue, _frame = int.MinValue;
        Vector3 _at;
        bool _has;

        /// <summary>Where the replayed body is (valid while <see cref="Has"/>).</summary>
        public Vector3 Position => _at;
        /// <summary>True once the replay has a point to stand on.</summary>
        public bool Has => _has;
        /// <summary>Changes on every re-seed and every jump: a segment across it threads nothing.</summary>
        public int Epoch { get; private set; }
        /// <summary>Points received and not yet played.</summary>
        public int Queued => _path.Count;

        /// <summary>Start again with nothing (the body changed: a different rider, or the local one again).</summary>
        public void Reset()
        {
            _path.Clear();
            _has = false;
            _sourceEpoch = int.MinValue;
            Epoch++;
        }

        /// <summary>Points the body flew, in order. <paramref name="sourceEpoch"/> is the flying machine's own epoch: when it
        /// changes the body was seated afresh there, and the path starts again.</summary>
        public void Enqueue(int sourceEpoch, Vector3 point)
        {
            if (sourceEpoch != _sourceEpoch)
            {
                Reset();
                _sourceEpoch = sourceEpoch;
            }
            _path.Enqueue(point);
            while (_path.Count > MaxQueued) _path.Dequeue();
        }

        /// <summary>Play one frame (<paramref name="frame"/> is the frame count: a second call in one frame does
        /// nothing, so every ring system that samples the probe this frame sees the same point).</summary>
        public void Step(int frame, float dt)
        {
            if (frame == _frame) return;
            _frame = frame;
            if (!_has)
            {
                if (_path.Count == 0) return;
                _at = _path.Dequeue();
                _has = true;
                return;
            }
            float budget = 0.9f * (PlausibleSpeed * dt * 2f + 5f);
            while (budget > 0f && _path.Count > 0)
            {
                var next = _path.Peek();
                float d = Vector3.Distance(_at, next);
                if (d > JumpDistance) { _at = _path.Dequeue(); Epoch++; break; }
                if (d <= budget) { _at = _path.Dequeue(); budget -= d; }
                else { _at = Vector3.MoveTowards(_at, next, budget); budget = 0f; }
            }
        }
    }
}
