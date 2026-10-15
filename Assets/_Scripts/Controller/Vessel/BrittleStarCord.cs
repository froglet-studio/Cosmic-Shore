using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One of Destruction Mode's five CORDS (Garrett, 2026-10-15: "instead of a ton of projectiles lets create 5 cords
    /// similar to the thresher's cord but without the weight at the end"). Pure C#, so it is tested rather than
    /// eyeballed (<c>BrittleStarTests</c>), and the Vessel Studio's Brittle Star page steps the same model.
    ///
    /// <para><b>The Thresher's rope with no ball.</b> The Thresher's chain (<see cref="ThresherChainLinks"/>) hangs between
    /// two pins, the hull and the ball. A cord has ONE pin — its turret's muzzle — and a free tip, so with nothing at the
    /// end to pull it straight it is held out by a <b>pose</b>: every link is drawn toward its place on the straight line
    /// from the muzzle along the arm's direction (<see cref="BrittleStarRules.ArmDirection"/>, which the right stick sets).
    /// The pull is stiff at the root and soft at the tip (<see cref="Settings.RootRate"/> → <see cref="Settings.TipRate"/>),
    /// so a turn of the hull or a flick of the ring bends the cord and its tip whips round after the root, the way a
    /// brittle star's arm follows the disc.</para>
    ///
    /// <para><b>The model</b>, per sub-step h (at most <see cref="MaxSubStep"/>, so 30 and 144 fps bend it alike):
    /// <list type="number">
    /// <item>Each link's velocity relaxes toward the velocity of its own target point at <see cref="Settings.Drag"/> —
    /// the cord rides along with the hull (no lag in a straight line, however fast) and keeps only the swing its
    /// targets do not share.</item>
    /// <item>It moves by that velocity, then eases toward its target by <c>1 − e^(−rate·h)</c>.</item>
    /// <item>A follow-the-leader pass from the muzzle outward caps every link at <c>length / links</c> — exact in one
    /// pass, so a cord never reads stretched however hard it whips.</item>
    /// <item>The velocity is re-read from the move (position-based dynamics), which the cut uses for its plane.</item>
    /// </list></para>
    /// </summary>
    public sealed class BrittleStarCord
    {
        /// <summary>The longest step the cord takes; a longer frame is cut into equal sub-steps.</summary>
        public const float MaxSubStep = 1f / 120f;
        /// <summary>Most sub-steps one frame may take (a hitch is stepped coarser, never in a burst).</summary>
        public const int MaxSubSteps = 8;

        public struct Settings
        {
            /// <summary>1/s: how hard the link at the muzzle is drawn to its place on the pose line.</summary>
            public float RootRate;
            /// <summary>1/s: the same at the free tip (softer: the tip whips).</summary>
            public float TipRate;
            /// <summary>1/s: how fast a link's own swing dies against the motion of its place.</summary>
            public float Drag;
        }

        /// <summary>The links, muzzle first. Point 0 is always the muzzle.</summary>
        public readonly Vector3[] Points;
        readonly Vector3[] _velocity;
        readonly Vector3[] _target;   // where each link's place on the pose line was at the last step
        readonly Vector3[] _start;
        bool _seeded;
        Vector3 _lastAnchor, _lastDirection;

        public BrittleStarCord(int links)
        {
            int n = Mathf.Max(2, links) + 1;
            Points = new Vector3[n];
            _velocity = new Vector3[n];
            _target = new Vector3[n];
            _start = new Vector3[n];
        }

        public int Count => Points.Length;
        public bool Seeded => _seeded;
        /// <summary>Velocity of link <paramref name="i"/> over the last step.</summary>
        public Vector3 NodeVelocity(int i) => _velocity[i];
        public Vector3 Tip => Points[Points.Length - 1];

        /// <summary>Forget the cord: the next <see cref="Step"/> lays it straight along its pose.</summary>
        public void Clear() => _seeded = false;

        /// <summary>Lay the cord straight along its pose, every link moving at <paramref name="velocity"/>.</summary>
        public void Reset(Vector3 anchor, Vector3 direction, float length, Vector3 velocity)
        {
            Vector3 dir = Unit(direction);
            int n = Points.Length;
            for (int i = 0; i < n; i++)
            {
                Points[i] = _target[i] = anchor + dir * (Mathf.Max(0f, length) * i / (n - 1));
                _velocity[i] = velocity;
            }
            _lastAnchor = anchor;
            _lastDirection = dir;
            _seeded = true;
        }

        /// <summary>
        /// Advance the cord by <paramref name="dt"/> to its muzzle at <paramref name="anchor"/>, posed along
        /// <paramref name="direction"/> at <paramref name="length"/>. The muzzle and the pose are interpolated across the
        /// sub-steps from where they were at the last call, so a long frame does not snap the root. An unseeded cord is
        /// laid straight, moving at <paramref name="velocity"/>.
        /// </summary>
        public void Step(Vector3 anchor, Vector3 direction, float length, in Settings settings, float dt, Vector3 velocity)
        {
            Vector3 dir = Unit(direction);
            if (!_seeded || dt <= 0f)
            {
                if (!_seeded) Reset(anchor, dir, length, velocity);
                return;
            }

            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / MaxSubStep - 1e-4f), 1, MaxSubSteps);
            float h = dt / steps;
            Vector3 fromAnchor = _lastAnchor, fromDir = _lastDirection;
            for (int k = 1; k <= steps; k++)
            {
                float f = k / (float)steps;
                Vector3 d = Vector3.Lerp(fromDir, dir, f);   // normalised below: a frame's turn is small
                SubStep(Vector3.Lerp(fromAnchor, anchor, f), d.sqrMagnitude > 1e-8f ? d.normalized : dir,
                        Mathf.Max(0f, length), settings, h);
            }
            _lastAnchor = anchor;
            _lastDirection = dir;
        }

        void SubStep(Vector3 anchor, Vector3 dir, float length, in Settings s, float h)
        {
            int n = Points.Length;
            float seg = length / (n - 1);
            float keep = Mathf.Exp(-Mathf.Max(0f, s.Drag) * h);
            for (int i = 0; i < n; i++) _start[i] = Points[i];

            Points[0] = anchor;
            for (int i = 1; i < n; i++)
            {
                Vector3 place = anchor + dir * (seg * i);
                Vector3 placeVelocity = (place - _target[i]) / h;
                _target[i] = place;
                Vector3 v = placeVelocity + (_velocity[i] - placeVelocity) * keep;
                Vector3 p = Points[i] + v * h;
                float rate = Mathf.Lerp(s.RootRate, s.TipRate, i / (float)(n - 1));
                Points[i] = p + (place - p) * (1f - Mathf.Exp(-Mathf.Max(0f, rate) * h));
            }

            // Follow the leader: each link at most one segment from the one before it, from the muzzle out.
            for (int i = 1; i < n; i++)
            {
                Vector3 d = Points[i] - Points[i - 1];
                float len = d.magnitude;
                if (len > seg && len > 1e-5f) Points[i] = Points[i - 1] + d * (seg / len);
            }

            for (int i = 0; i < n; i++) _velocity[i] = (Points[i] - _start[i]) / h;
        }

        static Vector3 Unit(Vector3 v) => v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.forward;
    }
}
