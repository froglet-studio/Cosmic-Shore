using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>Every number the long tether's swing needs for one step, scaled to game units.</summary>
    public struct LongTetherTuning
    {
        /// <summary>Stick-up reel-in rate, units/s (and stick-down pay-out rate).</summary>
        public float ReelRate;
        /// <summary>Constant reel-in with no input, units/s.</summary>
        public float AutoReel;
        /// <summary>Shortest the line may be reeled to, units.</summary>
        public float MinLength;
        /// <summary>Longest the line may be paid out to, units.</summary>
        public float MaxLength;
        /// <summary>Spin limit the reel stops at, radians/s.</summary>
        public float MaxSpinRadPerSec;
        /// <summary>Speed cap, units/s.</summary>
        public float MaxSpeed;
    }

    /// <summary>
    /// One RIGID long tether: the state of a swing round a distant anchor. Plain C# so the
    /// executor and <c>TetherMathTests</c> fly the same object.
    ///
    /// The hull is always ON the circle while hooked: <see cref="Step"/> reels (conserving
    /// angular momentum) and returns the velocity to fly this frame along the arc's chord, and
    /// the transformer then puts the position back on the rope with
    /// <see cref="TetherMath.ConstrainToRope"/>. Speed is therefore changed by exactly two
    /// things — the reel and the cap — and nothing about the rope itself costs speed.
    ///
    /// <see cref="Track"/> (swept angle) is separate from <see cref="Step"/> (force) because
    /// it must run on every peer — it drives the swing indicator and the release boost — while
    /// the step runs only where the flight model does.
    /// </summary>
    public sealed class LongTetherRope
    {
        public bool Hooked { get; private set; }
        public Vector3 Anchor { get; private set; }
        public float Length { get; private set; }
        /// <summary>Total angle swept about the anchor since the hook, radians.</summary>
        public float Swept { get; private set; }
        /// <summary>+1 right trigger, −1 left.</summary>
        public int Side { get; private set; }
        /// <summary>How hard the hook yanked, 0..1 (<see cref="TetherMath.HookYank01"/>).</summary>
        public float Yank01 { get; private set; }

        Vector3 _lastRelative;

        /// <summary>
        /// Go taut on <paramref name="anchor"/>. The line takes the current distance as its
        /// length, and the returned velocity is the SAME speed redirected onto the tangent.
        /// </summary>
        public Vector3 Hook(Vector3 hull, Vector3 velocity, Vector3 anchor, Vector3 fallbackForward, int side)
        {
            Hooked = true;
            Anchor = anchor;
            Side = side >= 0 ? 1 : -1;
            Length = Mathf.Max(TetherMath.Epsilon, (hull - anchor).magnitude);
            Swept = 0f;
            _lastRelative = hull - anchor;
            Yank01 = TetherMath.HookYank01(hull, velocity, anchor);
            return TetherMath.HookVelocity(hull, velocity, anchor, fallbackForward);
        }

        /// <summary>Follow an anchor that moves (a drifting crystal). Length is kept.</summary>
        public void MoveAnchor(Vector3 anchor)
        {
            _lastRelative += Anchor - anchor;
            Anchor = anchor;
        }

        /// <summary>Accumulate the angle swept since the last call. Every peer, every frame.</summary>
        public void Track(Vector3 hull)
        {
            if (!Hooked) return;
            Vector3 rel = hull - Anchor;
            Swept += TetherMath.SweptAngle(_lastRelative, rel);
            _lastRelative = rel;
        }

        /// <summary>
        /// One frame of swing: reel the line (angular momentum conserved, stopped at the spin
        /// limit and the speed cap), then return the velocity to fly along this frame's arc.
        /// </summary>
        /// <param name="reelInput">Pitch command, −1..1: positive reels in.</param>
        public Vector3 Step(Vector3 hull, Vector3 velocity, float reelInput, in LongTetherTuning t, float dt)
        {
            if (!Hooked || dt <= 0f) return velocity;

            float speed = velocity.magnitude;
            float next = TetherMath.ReelLength(
                Length, speed, reelInput, t.ReelRate, t.AutoReel, t.MinLength,
                Mathf.Max(t.MaxLength, t.MinLength), t.MaxSpinRadPerSec, t.MaxSpeed, dt);
            float spun = TetherMath.ConserveAngularMomentum(speed, Length, next);
            if (t.MaxSpeed > TetherMath.Epsilon) spun = Mathf.Min(spun, t.MaxSpeed);
            Length = next;

            Vector3 v = speed > TetherMath.Epsilon ? velocity * (spun / speed) : velocity;
            return TetherMath.ArcVelocity(hull, v, Anchor, Length, dt);
        }

        /// <summary>Current spin, radians/s, at <paramref name="speed"/>.</summary>
        public float SpinRate(float speed) => TetherMath.SpinRate(speed, Length);

        /// <summary>Let go. The swept angle is left readable for the caller's release maths.</summary>
        public void Release() => Hooked = false;
    }
}
