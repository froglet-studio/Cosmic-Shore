using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One of Charge Mode's five CORDS: a WHIP when the trigger is released, a SABER when it is pulled (Garrett,
    /// 2026-10-16: "make these melee cords behave like whips, so pulses send emergent low speed wave motions from a high
    /// inertia low elasticity cord that extends to 150% of its current length. pulling the trigger should shrink it to the
    /// current 100% and rigidify it so they are more like 5 straight sabers. this should be analog"). Pure C#, so it is
    /// tested rather than eyeballed (<c>BrittleStarTests</c>), and the Vessel Studio's Brittle Star page steps the same
    /// model.
    ///
    /// <para><b>The model: a discrete wave equation on the cord's LINK DIRECTIONS.</b> The cord is
    /// <see cref="Links"/> unit vectors laid end to end from its muzzle, so it cannot stretch at all (no elasticity: the
    /// links are its length). Each direction is pulled toward the average of its neighbours,
    /// <c>u'' = k (u[i-1] − 2u[i] + u[i+1])</c>, with the muzzle's own direction (the arm's line the right stick sets) as
    /// the fixed end and the tip free. That is a string's wave equation, so nothing is scripted: a turn, a flick of the
    /// ring or a sweep of the stick bends the root, and the bend TRAVELS down the cord at about √k links a second,
    /// reflects off the free tip and rings on, dying at <see cref="Settings.LooseDamping"/> — the whip. The directions
    /// live in world space, so the cord has inertia: the hull turns under it and the cord follows a moment later.</para>
    ///
    /// <para><b>The trigger</b> (<c>rigidity</c> 0..1, analog) moves every dial from whip to saber at once: the wave
    /// stiffness <see cref="Settings.LooseWave"/> → <see cref="Settings.RigidWave"/> (a fast, stiff string), the damping
    /// <see cref="Settings.LooseDamping"/> → <see cref="Settings.RigidDamping"/>, and a pose spring
    /// <c>rigidity² × </c><see cref="Settings.Pose"/> that pulls every link onto the straight arm's line. The length is the
    /// caller's (the executor shortens it from 150% to 100% with the same depth).</para>
    ///
    /// <para>Sub-stepped at <see cref="MaxSubStep"/> (semi-implicit Euler, stable for h·2√k &lt; 2), so 30 and 144 fps
    /// peers see the same whip.</para>
    /// </summary>
    public sealed class BrittleStarCord
    {
        /// <summary>The longest step the cord takes; a longer frame is cut into equal sub-steps.</summary>
        public const float MaxSubStep = 1f / 120f;
        /// <summary>Most sub-steps one frame may take (a hitch is stepped coarser, never in a burst).</summary>
        public const int MaxSubSteps = 8;

        public struct Settings
        {
            /// <summary>1/s²: the whip's wave stiffness. A root pulse travels about √k links a second.</summary>
            public float LooseWave;
            /// <summary>1/s²: the saber's (trigger fully pulled).</summary>
            public float RigidWave;
            /// <summary>1/s: how fast the whip's waves die (low: high inertia, it rings on).</summary>
            public float LooseDamping;
            /// <summary>1/s: the saber's.</summary>
            public float RigidDamping;
            /// <summary>1/s²: the pull of every link onto the straight arm's line at full rigidity (× rigidity²).</summary>
            public float Pose;
        }

        /// <summary>The joints, muzzle first: <see cref="Links"/> + 1 of them. Point 0 is always the muzzle.</summary>
        public readonly Vector3[] Points;
        readonly Vector3[] _direction;   // unit, world space: link i runs Points[i] -> Points[i + 1]
        readonly Vector3[] _spin;        // d(direction)/dt, kept perpendicular to its direction
        readonly Vector3[] _accel;
        readonly Vector3[] _velocity;
        readonly Vector3[] _previous;
        bool _seeded;
        Vector3 _lastDirection;

        public BrittleStarCord(int links)
        {
            int n = Mathf.Max(2, links);
            _direction = new Vector3[n];
            _spin = new Vector3[n];
            _accel = new Vector3[n];
            Points = new Vector3[n + 1];
            _velocity = new Vector3[n + 1];
            _previous = new Vector3[n + 1];
        }

        public int Links => _direction.Length;
        public int Count => Points.Length;
        public bool Seeded => _seeded;
        public Vector3 Tip => Points[Points.Length - 1];
        /// <summary>Velocity of joint <paramref name="i"/> over the last <see cref="Step"/>.</summary>
        public Vector3 NodeVelocity(int i) => _velocity[i];
        /// <summary>Unit direction of link <paramref name="i"/>.</summary>
        public Vector3 LinkDirection(int i) => _direction[i];
        /// <summary>How fast link <paramref name="i"/> is swinging, radians per second.</summary>
        public float LinkSwing(int i) => _spin[i].magnitude;

        /// <summary>Forget the cord: the next <see cref="Step"/> lays it straight along its pose.</summary>
        public void Clear() => _seeded = false;

        /// <summary>Lay the cord straight along <paramref name="direction"/>, still.</summary>
        public void Reset(Vector3 anchor, Vector3 direction, float length)
        {
            Vector3 dir = Unit(direction);
            for (int i = 0; i < _direction.Length; i++)
            {
                _direction[i] = dir;
                _spin[i] = Vector3.zero;
            }
            _lastDirection = dir;
            Lay(anchor, length);
            for (int i = 0; i < Points.Length; i++) _velocity[i] = Vector3.zero;
            _seeded = true;
        }

        /// <summary>
        /// Advance the cord by <paramref name="dt"/>: its muzzle at <paramref name="anchor"/>, its root along
        /// <paramref name="direction"/>, <paramref name="length"/> long, at <paramref name="rigidity"/> (0 = whip,
        /// 1 = saber). The root direction is interpolated across the sub-steps from where it was at the last call, so a
        /// long frame does not snap it. An unseeded cord is laid straight.
        /// </summary>
        public void Step(Vector3 anchor, Vector3 direction, float length, float rigidity, in Settings s, float dt)
        {
            Vector3 dir = Unit(direction);
            if (!_seeded)
            {
                Reset(anchor, dir, length);
                return;
            }
            if (dt <= 0f) return;

            float r = Mathf.Clamp01(rigidity);
            float wave = Mathf.Max(0f, Mathf.Lerp(s.LooseWave, s.RigidWave, r));
            float damping = Mathf.Max(0f, Mathf.Lerp(s.LooseDamping, s.RigidDamping, r));
            float pose = Mathf.Max(0f, r * r * s.Pose);

            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / MaxSubStep - 1e-4f), 1, MaxSubSteps);
            float h = dt / steps;
            float keep = Mathf.Exp(-damping * h);
            Vector3 from = _lastDirection;
            int n = _direction.Length;
            for (int k = 1; k <= steps; k++)
            {
                Vector3 root = Vector3.Lerp(from, dir, k / (float)steps);
                root = root.sqrMagnitude > 1e-8f ? root.normalized : dir;
                for (int i = 0; i < n; i++)
                {
                    Vector3 before = i == 0 ? root : _direction[i - 1];
                    Vector3 after = i == n - 1 ? _direction[i] : _direction[i + 1];   // the tip is free
                    Vector3 a = (before + after - 2f * _direction[i]) * wave + (root - _direction[i]) * pose;
                    _accel[i] = a - _direction[i] * Vector3.Dot(a, _direction[i]);
                }
                for (int i = 0; i < n; i++)
                {
                    Vector3 w = (_spin[i] + _accel[i] * h) * keep;
                    Vector3 u = _direction[i] + w * h;
                    u = u.sqrMagnitude > 1e-8f ? u.normalized : _direction[i];
                    _direction[i] = u;
                    _spin[i] = w - u * Vector3.Dot(w, u);
                }
            }
            _lastDirection = dir;

            for (int i = 0; i < Points.Length; i++) _previous[i] = Points[i];
            Lay(anchor, length);
            for (int i = 0; i < Points.Length; i++) _velocity[i] = (Points[i] - _previous[i]) / dt;
        }

        void Lay(Vector3 anchor, float length)
        {
            float seg = Mathf.Max(0f, length) / _direction.Length;
            Points[0] = anchor;
            for (int i = 0; i < _direction.Length; i++) Points[i + 1] = Points[i] + _direction[i] * seg;
        }

        static Vector3 Unit(Vector3 v) => v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.forward;
    }
}
