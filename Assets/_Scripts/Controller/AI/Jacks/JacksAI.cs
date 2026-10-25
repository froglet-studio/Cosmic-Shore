using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The jacks' AI (the lab's <c>shAI</c>, its <c>dogs</c> half; <c>Docs/Studios/SwarmVesselLab/src/shepherd.js</c>): a
    /// Strömbom shepherd. In board coordinates, jack A COLLECTS the straggler while the herd has spread, and otherwise holds
    /// one flank of the drive point behind the herd (seen from the target); jack B DRIVES from that point on the other
    /// flank. Both go round the herd, never through it. Every few seconds B lifts for a moment, leaving the decoy behind the
    /// herd to hold that side. Near the target the drive stands off (1.6 rings instead of 0.55), so the rings stop breaking
    /// the herd's mouthfuls at its pasture.
    ///
    /// <para>Input only, as a thumb is: the plan is two aim points and a lift, which the executor runs at the jacks' own
    /// speed. Lengths are the lab's times <see cref="JacksConfigSO.LabScale"/>.</para>
    /// </summary>
    sealed class JacksAI
    {
        internal struct PlanOut
        {
            public Vector2 A, B;   // aim points on the board (u, along the board's right and forward)
            public bool LiftB;     // B's thumb is up this frame: the decoy is left where B stands
        }

        System.Random _rnd;
        float _noiseT, _nx, _nz, _liftT = 7f, _lift;

        internal JacksAI(int seed) => _rnd = new System.Random(seed);

        float Rnd() => (float)_rnd.NextDouble();

        internal PlanOut Plan(JacksExecutor ex, float dt, float ring, Vector3 targetWorld)
        {
            var cfg = ex.Config;
            float scale = cfg.LabScale, nz = cfg.AINoise;
            _noiseT -= dt;
            if (_noiseT <= 0f)
            {
                _noiseT = 0.5f;
                _nx = (Rnd() - 0.5f) * 2f * nz * 260f * scale;
                _nz = (Rnd() - 0.5f) * 2f * nz * 260f * scale;
            }

            Vector2 f = ex.ToBoard(ex.HerdCentre), t = ex.ToBoard(targetWorld), far = ex.HerdFar;
            float spread = ex.HerdSpread, rr = ring;
            bool graze = (t - f).magnitude < spread + 200f * scale;
            float back = spread + rr * (graze ? 1.6f : 0.55f);

            Vector2 u = t - f;
            float ul = u.magnitude;
            u = ul > 1e-4f ? u / ul : Vector2.up;
            Vector2 drive = f - u * back;

            Vector2 a;
            if (ex.HerdCount > 0 && spread > 150f * scale)
            {
                Vector2 o = far - f;
                float ol = o.magnitude;
                a = far + (ol > 1e-4f ? o / ol : Vector2.zero) * (rr * 0.55f);
            }
            else a = new Vector2(drive.x - u.y * rr * 0.7f, drive.y + u.x * rr * 0.7f);
            var b = new Vector2(drive.x + u.y * rr * 0.45f, drive.y - u.x * rr * 0.45f);

            var pa = Route(ex.JackOnBoard(0), a, f, spread, rr);
            var pb = Route(ex.JackOnBoard(1), b, f, spread, rr);

            _liftT -= dt;
            if (_liftT <= 0f) { _lift = 0.15f; _liftT = 7f + nz * 4f; }
            bool liftB = false;
            if (_lift > 0f) { _lift -= dt; liftB = true; }

            return new PlanOut
            {
                A = new Vector2(pa.x + _nx, pa.y + _nz),
                B = new Vector2(pb.x - _nz, pb.y + _nx),
                LiftB = liftB,
            };
        }

        /// <summary>Go round the herd, not through it: if the straight run from <paramref name="from"/> to <paramref name="to"/>
        /// passes within the herd's radius plus 0.6 rings of its centre, aim for the rim at 0.9 rings first.</summary>
        static Vector2 Route(Vector2 from, Vector2 to, Vector2 centre, float spread, float ring)
        {
            Vector2 v = to - from;
            float l2 = Mathf.Max(1e-6f, v.sqrMagnitude);
            float along = Mathf.Clamp01(Vector2.Dot(centre - from, v) / l2);
            Vector2 q = from + v * along - centre;
            float ql = q.magnitude;
            if (along > 0f && along < 1f && ql < spread + ring * 0.6f)
            {
                float s = ql > 1f ? 1f / ql : 1f;
                return centre + q * (s * (spread + ring * 0.9f));
            }
            return to;
        }
    }
}
