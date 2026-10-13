using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The lobby AI difficulty for a GATE race: the Skim Race's two deliberate mistakes
    /// (<see cref="SkimRaceHandicap"/>, numbers from <see cref="SkimRaceDifficultySO"/>) applied to the next
    /// RING instead of the next crystal. Proven first in the Stoat Flight Studio
    /// (<c>Docs/Studios/StoatFlightStudio.html</c>, <c>aiBelief</c>), where it made Medium 27-38% and Easy
    /// 43-52% slower than Hard on every course (<c>Docs/Studios/VesselStudio/README.md</c>).
    ///
    /// <para><b>It changes what the pilot BELIEVES, never what it does with a belief.</b> The race's own aim
    /// (<c>GateRaceController.ArmRacers</c>: line up behind the ring, then fly through it) runs unchanged on the
    /// point this class hands it, so Hard (no handicap) is the shipped pilot byte for byte.</para>
    /// <list type="bullet">
    /// <item><b>Late notice.</b> After threading a ring the pilot notices the next one
    /// <see cref="SkimRaceHandicapLevel.ReactionSeconds"/> late (x 0.5-1.5 per ring). Until then it has no
    /// target and flies straight on (<see cref="Belief.Unnoticed"/>). The first ring is seen on the grid.</item>
    /// <item><b>Misjudged ring.</b> With <see cref="SkimRaceHandicapLevel.MistakeChance"/> per ring the pilot
    /// believes the mouth sits two ring-radii (plus a margin) off to one side, in the ring's own plane, so it
    /// flies past outside it. Once it is past the point it believed in (or reached it, or
    /// <see cref="SkimRaceHandicap.MistakeMaxSeconds"/> ran out) it sees the real ring, now behind or beside
    /// it, and turns back for it; a ring threads from either side.</item>
    /// </list>
    ///
    /// <para>Pure C#, seeded by the caller with <see cref="System.Random"/> (never <c>UnityEngine.Random</c>,
    /// whose global state course generators seed).</para>
    /// </summary>
    public sealed class GateRaceHandicap
    {
        /// <summary>A misjudged ring is believed this many ring radii, plus <see cref="MissMargin"/>, off its
        /// centre: always outside the mouth.</summary>
        public const float MissRadii = 2f;
        public const float MissMargin = 6f;

        /// <summary>Closer than this to the believed point, the pilot sees the real ring.</summary>
        public const float SeeTruthDistance = 12f;

        /// <summary>What the pilot believes about the ring it is racing for.</summary>
        public readonly struct Belief
        {
            /// <summary>True while the ring has not been noticed yet: there is no target to aim at.</summary>
            public readonly bool Unnoticed;
            /// <summary>True while the ring is misjudged: <see cref="Point"/> is where it is BELIEVED to be.</summary>
            public readonly bool Misjudged;
            /// <summary>The ring's centre as believed (the real centre unless <see cref="Misjudged"/>).</summary>
            public readonly Vector3 Point;

            public Belief(bool unnoticed, bool misjudged, Vector3 point)
            {
                Unnoticed = unnoticed;
                Misjudged = misjudged;
                Point = point;
            }
        }

        readonly SkimRaceHandicapLevel _level;
        readonly System.Random _rng;

        int _tracking = -1;
        float _noticeAt;
        bool _misjudged;
        float _misjudgedUntil;
        Vector3 _offset;

        public GateRaceHandicap(SkimRaceHandicapLevel level, int seed)
        {
            _level = level;
            _rng = new System.Random(seed);
        }

        public SkimRaceHandicapLevel Level => _level;

        /// <summary>Rings misjudged this race (diagnostics).</summary>
        public int Mistakes { get; private set; }

        /// <summary>Forget the current ring and the race's count (a new race).</summary>
        public void Reset()
        {
            _tracking = -1;
            _misjudged = false;
            Mistakes = 0;
        }

        /// <summary>
        /// The ring <paramref name="ringIndex"/> as this pilot believes it, at time <paramref name="now"/>.
        /// <paramref name="ringIndex"/> is the RACE index (a lap's rings count again), so every leg is a new
        /// decision. <paramref name="self"/> and <paramref name="forward"/> are the pilot's hull.
        /// </summary>
        public Belief Believe(int ringIndex, Vector3 ringCentre, Vector3 ringAxis, float ringRadius,
                              Vector3 self, Vector3 forward, float now)
        {
            if (_level.IsNone) return new Belief(false, false, ringCentre);

            if (ringIndex != _tracking)
            {
                _tracking = ringIndex;
                float spread = SkimRaceHandicap.ReactionSpreadMin +
                               (float)_rng.NextDouble() * (SkimRaceHandicap.ReactionSpreadMax - SkimRaceHandicap.ReactionSpreadMin);
                _noticeAt = ringIndex > 0 && _level.ReactionSeconds > 0f ? now + _level.ReactionSeconds * spread : now;
                _misjudged = _level.MistakeChance > 0f && _rng.NextDouble() < _level.MistakeChance;
                if (_misjudged)
                {
                    Mistakes++;
                    _misjudgedUntil = now + SkimRaceHandicap.MistakeMaxSeconds;
                    _offset = InPlaneOffset(ringAxis, (float)(_rng.NextDouble() * Mathf.PI * 2.0),
                                            MissRadii * Mathf.Max(1f, ringRadius) + MissMargin);
                }
            }

            if (now < _noticeAt) return new Belief(true, false, ringCentre);

            if (_misjudged)
            {
                Vector3 believed = ringCentre + _offset;
                Vector3 to = believed - self;
                if (now > _misjudgedUntil || to.sqrMagnitude < SeeTruthDistance * SeeTruthDistance || Vector3.Dot(to, forward) < 0f)
                    _misjudged = false;   // past the point it believed in: now it sees the ring
                else
                    return new Belief(false, true, believed);
            }
            return new Belief(false, false, ringCentre);
        }

        /// <summary>A vector of length <paramref name="length"/> in the plane whose normal is
        /// <paramref name="axis"/>, at <paramref name="angle"/> round it.</summary>
        public static Vector3 InPlaneOffset(Vector3 axis, float angle, float length)
        {
            Vector3 n = axis.sqrMagnitude > 1e-8f ? axis.normalized : Vector3.forward;
            Vector3 u = Vector3.Cross(n, Mathf.Abs(n.x) > 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 w = Vector3.Cross(n, u);
            return (u * Mathf.Cos(angle) + w * Mathf.Sin(angle)) * length;
        }
    }
}
