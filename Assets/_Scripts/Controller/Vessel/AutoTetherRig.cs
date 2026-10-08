using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Tether's straight-line flight: a small set of short ELASTIC lines to anchors the
    /// vessel plants ahead of itself, alternating sides. Plain C# (no Unity object) so the
    /// executor and <c>TetherMathTests</c> run the exact same rig.
    ///
    /// Two halves, deliberately separable:
    /// <list type="bullet">
    /// <item><b>Bookkeeping</b> — the plant cadence, which side is next, attaching an anchor,
    /// letting go when the hull passes abeam, and each line's tension. Runs on EVERY peer (from
    /// replicated motion), because every peer draws the beams and cuts with them.</item>
    /// <item><b>Force</b> — <see cref="Solve"/>. Runs only where the flight model runs (the
    /// machine that flies the hull), called from inside the move step.</item>
    /// </list>
    ///
    /// Because the pulls alternate sides, their sideways parts cancel over a pair and the path
    /// stays straight with a rhythmic surge; the forward parts sum to a pull that the flight
    /// model's ordinary overspeed decay balances a little above cruise.
    /// </summary>
    public sealed class AutoTetherRig
    {
        /// <summary>Most lines alive at once. Lifetime ≈ lead time, so at the default 0.8 s lead
        /// and 0.35 s period about three are ever live; this is headroom, not a tuning value.</summary>
        public const int Capacity = 8;

        public struct Line
        {
            public bool Live;
            /// <summary>Monotonic per rig; never reused, so a visual keyed by it can tell a
            /// recycled slot from the line it was drawing.</summary>
            public int Id;
            public Vector3 Anchor;
            public float Rest;
            /// <summary>Initial length minus rest — the stretch at which tension reads 1.</summary>
            public float Span;
            public int Side;
            public float Tension01;
        }

        readonly Line[] _lines = new Line[Capacity];
        float _cadence;
        int _nextSide = 1;
        int _nextId = 1;

        public AutoTetherRig() => Reset();

        /// <summary>Slot <paramref name="i"/> of <see cref="Capacity"/>.</summary>
        public Line this[int i] => _lines[i];

        /// <summary>The side the next anchor goes to: +1 right, −1 left.</summary>
        public int NextSide => _nextSide;

        /// <summary>Any live line is past its rest length this frame.</summary>
        public bool AnyTaut
        {
            get
            {
                for (int i = 0; i < Capacity; i++)
                    if (_lines[i].Live && _lines[i].Tension01 > 0f) return true;
                return false;
            }
        }

        public bool AnyLive
        {
            get
            {
                for (int i = 0; i < Capacity; i++)
                    if (_lines[i].Live) return true;
                return false;
            }
        }

        public bool IsLive(int id)
        {
            for (int i = 0; i < Capacity; i++)
                if (_lines[i].Live && _lines[i].Id == id) return true;
            return false;
        }

        /// <summary>Drop every line and re-arm the cadence so the next tick plants at once.</summary>
        public void Reset()
        {
            for (int i = 0; i < Capacity; i++) _lines[i] = default;
            _cadence = float.PositiveInfinity;   // first tick plants immediately
        }

        /// <summary>Let go of every line without resetting the cadence (a long tether took over).</summary>
        public void ReleaseAll()
        {
            for (int i = 0; i < Capacity; i++)
            {
                _lines[i].Live = false;
                _lines[i].Tension01 = 0f;
            }
        }

        /// <summary>
        /// Advance the plant clock. True when an anchor is due this frame. Owed time beyond one
        /// period is dropped rather than paid as a burst, so a hitch (or a pause) never plants a
        /// clump of anchors on one frame.
        /// </summary>
        public bool TickCadence(float dt, float period)
        {
            if (period <= 0f) return false;
            if (float.IsPositiveInfinity(_cadence)) { _cadence = 0f; return true; }
            _cadence += Mathf.Max(0f, dt);
            if (_cadence < period) return false;
            _cadence = Mathf.Min(_cadence - period, period);
            return true;
        }

        /// <summary>
        /// Attach a new line to <paramref name="anchor"/> on <see cref="NextSide"/> and flip the
        /// side. The rest length is <paramref name="restFraction"/> of the planted distance, so a
        /// fresh line starts stretched and pulls at once. Reuses the oldest slot when full.
        /// </summary>
        /// <returns>The new line's id.</returns>
        public int Attach(Vector3 hull, Vector3 anchor, float restFraction)
        {
            int slot = -1, oldestId = int.MaxValue, oldestSlot = 0;
            for (int i = 0; i < Capacity; i++)
            {
                if (!_lines[i].Live) { slot = i; break; }
                if (_lines[i].Id < oldestId) { oldestId = _lines[i].Id; oldestSlot = i; }
            }
            if (slot < 0) slot = oldestSlot;

            float length = (anchor - hull).magnitude;
            float rest = length * Mathf.Clamp01(restFraction);
            _lines[slot] = new Line
            {
                Live = true,
                Id = _nextId++,
                Anchor = anchor,
                Rest = rest,
                Span = Mathf.Max(TetherMath.Epsilon, length - rest),
                Side = _nextSide,
                Tension01 = rest < length ? 1f : 0f,
            };
            _nextSide = -_nextSide;
            return _lines[slot].Id;
        }

        /// <summary>Let go of every line whose anchor the hull has drawn level with. Every peer.</summary>
        public void ReleasePassed(Vector3 hull, Vector3 forward)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (!_lines[i].Live) continue;
                if (!TetherMath.PassedAbeam(hull, forward, _lines[i].Anchor)) continue;
                _lines[i].Live = false;
                _lines[i].Tension01 = 0f;
            }
        }

        /// <summary>Recompute tension from geometry alone — what a peer that does not run the
        /// force step draws from.</summary>
        public void UpdateTension(Vector3 hull)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (!_lines[i].Live) continue;
                float stretch = (_lines[i].Anchor - hull).magnitude - _lines[i].Rest;
                _lines[i].Tension01 = stretch > 0f ? Mathf.Clamp01(stretch / _lines[i].Span) : 0f;
            }
        }

        /// <summary>
        /// Sum of every live line's pull this frame (already × dt), via
        /// <see cref="TetherMath.ElasticPull"/>. Updates tension as a side effect. The caller
        /// bounds the result's speed gain with <see cref="TetherMath.LimitSpeedGain"/>.
        /// </summary>
        public Vector3 Solve(Vector3 hull, Vector3 velocity, float stiffness, float damping, float dt)
        {
            Vector3 total = Vector3.zero;
            for (int i = 0; i < Capacity; i++)
            {
                if (!_lines[i].Live) continue;
                total += TetherMath.ElasticPull(
                    hull, velocity, _lines[i].Anchor, _lines[i].Rest, _lines[i].Span,
                    stiffness, damping, dt, out float tension);
                _lines[i].Tension01 = tension;
            }
            return total;
        }
    }
}
