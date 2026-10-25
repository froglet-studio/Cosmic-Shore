using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Thresher's tail as a PURE function (design: <c>R_VesselActions/THRESHER.md</c> §5b): what
    /// <see cref="ThresherAnimation"/> poses the hull's tail joints to, with no scene in it, so
    /// <c>ThresherHullFormTests</c> and <c>Tools/Build/thresher_hull_harness</c> run it as-is.
    ///
    /// <para><b>The tail is the LAST LINKS OF THE CORD, not a fin that beats</b> (Garrett, 2026-10-19:
    /// "remove the tail wiggle when flying. let the tail more naturally sway as though it is the last links
    /// of the cord"). The chain leaves from the whip's tip, so the tail hangs along it:
    /// <see cref="CordBend"/> aims the tail at a point a little way down the chain, and every joint follows
    /// its share of that bend on a damped spring (<see cref="Step"/>) that also lags behind the hull's own
    /// turns. Flying straight with the ball towed behind, the tail lies straight; a turn, a planted orbit or a
    /// swinging ball sways it, and it settles. Nothing in it is a function of time on its own, so a flying
    /// Thresher never wiggles. Until 2026-10-19 the tail beat side to side as a travelling sine wave.</para>
    ///
    /// <para><b>Angles are Unity's.</b> A joint's yaw is <c>Euler(0, yaw, 0)</c> about the hull's up axis
    /// (positive carries a part trailing on -Z to the pilot's LEFT); its pitch is <c>Euler(pitch, 0, 0)</c>
    /// about the hull's right axis (positive RAISES a trailing part). Each joint's angles are absolute in
    /// the hull's frame, not relative to the joint before it.</para>
    ///
    /// <para><b>Stable at any frame rate.</b> The springs integrate in sub-steps of at most
    /// <see cref="MaxSubStep"/> and a frame longer than <see cref="MaxFrame"/> is clamped, so a phone running
    /// at 30 fps or dropping a frame poses the same tail as a PC at 144.</para>
    /// </summary>
    public static class ThresherSwimMath
    {
        /// <summary>The shipped tail - <see cref="ThresherAnimation"/>'s field initializers name these, so the
        /// offline harness measures the same numbers the prefab starts from.</summary>
        public const float DefaultMaxYaw = 110f, DefaultMaxPitch = 50f, DefaultRootFrequency = 2.4f,
            DefaultTipFrequency = 1.2f, DefaultDamping = 0.45f, DefaultRootInertia = 0.2f, DefaultTipInertia = 0.85f,
            DefaultCrackKick = 600f;

        /// <summary>The longest spring sub-step, seconds.</summary>
        public const float MaxSubStep = 1f / 120f;

        /// <summary>The longest frame the springs integrate; a longer one (a hitch, a pause) is clamped.</summary>
        public const float MaxFrame = 0.1f;

        /// <summary>The most a single frame's hull turn may drag the tail, degrees: a respawn or a pose set
        /// turns the hull in one frame and is not a swing.</summary>
        public const float MaxInertiaDegrees = 25f;

        /// <summary>How far past its bend limit a swinging joint may overshoot before it is held, degrees.</summary>
        public const float OvershootDegrees = 30f;

        public static Settings Defaults => new()
        {
            MaxYawDegrees = DefaultMaxYaw, MaxPitchDegrees = DefaultMaxPitch, RootFrequency = DefaultRootFrequency,
            TipFrequency = DefaultTipFrequency, Damping = DefaultDamping, RootInertia = DefaultRootInertia,
            TipInertia = DefaultTipInertia, CrackKick = DefaultCrackKick,
        };

        public struct Settings
        {
            public float MaxYawDegrees;     // the most the tail bends sideways toward the cord, at the tip
            public float MaxPitchDegrees;   // ... and up or down
            public float RootFrequency;     // the peduncle's spring, Hz: stiff, it follows the cord quickly
            public float TipFrequency;      // the tip's spring, Hz: softer, so it lags and sways
            public float Damping;           // damping ratio: under 1 the tail overshoots and settles (a sway)
            public float RootInertia;       // 0..1: how much of the hull's turn the peduncle resists
            public float TipInertia;        // ... and the tip
            public float CrackKick;         // deg/s a smash or a launch kicks the tip with
        }

        /// <summary>One joint's state: its angles (degrees, absolute in the hull's frame) and their rates.</summary>
        public struct Joint
        {
            public float Yaw, Pitch, YawVelocity, PitchVelocity;
        }

        /// <summary>Yaw of a hull-space direction in the joint convention: 0 straight back (-Z), positive to the left.</summary>
        public static float YawOf(Vector3 d) => Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg;

        /// <summary>Pitch of a hull-space direction in the joint convention: positive up.</summary>
        public static float PitchOf(Vector3 d) => Mathf.Atan2(d.y, Mathf.Sqrt(d.x * d.x + d.z * d.z)) * Mathf.Rad2Deg;

        /// <summary>
        /// The bend that lays the tail along the cord: <paramref name="restDir"/> is the tail's rest direction
        /// (peduncle to tip) and <paramref name="toCord"/> the direction from the peduncle to a point down the
        /// chain, both in the hull's space. Returns how strongly the tail follows (0..1): fully while the cord
        /// leaves behind or beside the hull, fading to nothing as it swings ahead (a tail cannot point forward,
        /// and straight ahead is where the side of the bend flips), and fading when the point sits inside the
        /// tail's own length (a slack chain bunched at the hull has no direction worth following).
        /// </summary>
        public static float CordBend(in Settings s, Vector3 restDir, Vector3 toCord, float tailLength,
            out float yaw, out float pitch)
        {
            yaw = pitch = 0f;
            float len = toCord.magnitude, restLen = restDir.magnitude;
            if (len < 1e-4f || restLen < 1e-4f) return 0f;

            // 1 straight back along the rest tail, 0 square to it, -1 straight ahead.
            float behind = Vector3.Dot(toCord, restDir) / (len * restLen);
            float follow = Smooth01((behind + 0.6f) / 0.5f);
            if (tailLength > 1e-4f) follow *= Smooth01((len - 0.5f * tailLength) / tailLength);
            if (follow <= 0f) return 0f;

            float dYaw = YawOf(toCord) - YawOf(restDir);
            if (dYaw > 180f) dYaw -= 360f;
            else if (dYaw < -180f) dYaw += 360f;
            yaw = Mathf.Clamp(dYaw, -s.MaxYawDegrees, s.MaxYawDegrees) * follow;
            pitch = Mathf.Clamp(PitchOf(toCord) - PitchOf(restDir), -s.MaxPitchDegrees, s.MaxPitchDegrees) * follow;
            return follow;
        }

        /// <summary>Each joint's share of a bend, root first: joint j takes (j+1)/n of it, so the tail curves
        /// smoothly and its last segment - where the chain leaves - lies along the full bend.</summary>
        public static void JointTargets(float bendYaw, float bendPitch, float[] yawInto, float[] pitchInto)
        {
            int n = yawInto.Length;
            for (int j = 0; j < n; j++)
            {
                float share = (j + 1f) / n;
                yawInto[j] = bendYaw * share;
                pitchInto[j] = bendPitch * share;
            }
        }

        /// <summary>
        /// Advance the tail one frame. <paramref name="hullYawDelta"/> / <paramref name="hullPitchDelta"/> are
        /// how far the HULL itself turned this frame (degrees, nose right / nose down positive): each joint keeps
        /// its share of where it was pointing in the world (<see cref="Settings.RootInertia"/> at the peduncle up
        /// to <see cref="Settings.TipInertia"/> at the tip), so a turn drags the tail and its spring brings it
        /// round. <paramref name="kick"/> (deg/s of yaw, signed) flicks the tip - a crack.
        /// </summary>
        public static void Step(in Settings s, Joint[] joints, float[] yawTargets, float[] pitchTargets,
            float hullYawDelta, float hullPitchDelta, float kick, float dt)
        {
            int n = joints.Length;
            if (n == 0) return;
            hullYawDelta = Mathf.Clamp(hullYawDelta, -MaxInertiaDegrees, MaxInertiaDegrees);
            hullPitchDelta = Mathf.Clamp(hullPitchDelta, -MaxInertiaDegrees, MaxInertiaDegrees);
            for (int j = 0; j < n; j++)
            {
                float t = TipWeight(j, n);
                float inertia = Mathf.Lerp(s.RootInertia, s.TipInertia, t);
                joints[j].Yaw -= hullYawDelta * inertia;
                joints[j].Pitch -= hullPitchDelta * inertia;
                joints[j].YawVelocity += kick * t * t;
            }

            dt = Mathf.Clamp(dt, 0f, MaxFrame);
            if (dt > 0f)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(dt / MaxSubStep));
                float h = dt / steps;
                for (int j = 0; j < n; j++)
                {
                    float hz = Mathf.Lerp(s.RootFrequency, s.TipFrequency, TipWeight(j, n));
                    float omega = 2f * Mathf.PI * Mathf.Max(0.01f, hz);
                    for (int k = 0; k < steps; k++)
                    {
                        Spring(ref joints[j].Yaw, ref joints[j].YawVelocity, yawTargets[j], omega, s.Damping, h);
                        Spring(ref joints[j].Pitch, ref joints[j].PitchVelocity, pitchTargets[j], omega, s.Damping, h);
                    }
                }
            }

            float yawLimit = s.MaxYawDegrees + OvershootDegrees, pitchLimit = s.MaxPitchDegrees + OvershootDegrees;
            for (int j = 0; j < n; j++)
            {
                joints[j].Yaw = Mathf.Clamp(joints[j].Yaw, -yawLimit, yawLimit);
                joints[j].Pitch = Mathf.Clamp(joints[j].Pitch, -pitchLimit, pitchLimit);
            }
        }

        /// <summary>A joint's rotation in the hull's frame: pitched, then yawed.</summary>
        public static Quaternion JointRotation(in Joint joint) =>
            Quaternion.AngleAxis(joint.Yaw, Vector3.up) * Quaternion.AngleAxis(joint.Pitch, Vector3.right);

        /// <summary>
        /// Compose a chain of hinged joints from their ABSOLUTE rotations: joint 0 sits at
        /// <paramref name="restPivots"/>[0]; joint k's pivot is joint k-1's pivot plus joint k-1's rotation
        /// applied to the rest offset between them, so a bend never opens a gap between segments. All in the
        /// hull's space, where the parts live.
        /// </summary>
        public static void PoseChain(Vector3[] restPivots, Quaternion[] rotations, Vector3[] outPositions)
        {
            int n = restPivots.Length;
            if (n == 0) return;
            outPositions[0] = restPivots[0];
            for (int k = 1; k < n; k++)
                outPositions[k] = outPositions[k - 1] + rotations[k - 1] * (restPivots[k] - restPivots[k - 1]);
        }

        static float TipWeight(int j, int n) => n > 1 ? j / (float)(n - 1) : 1f;

        static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // Semi-implicit Euler: stable for omega * h well under 2, which MaxSubStep keeps for any sane frequency.
        static void Spring(ref float x, ref float v, float target, float omega, float zeta, float h)
        {
            v += (omega * omega * (target - x) - 2f * zeta * omega * v) * h;
            x += v * h;
        }
    }
}
