using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Thresher's swim as a PURE function (design: <c>R_VesselActions/THRESHER.md</c> §5b): what
    /// <see cref="ThresherAnimation"/> poses the hull's parts to, with no scene in it, so
    /// <c>ThresherHullFormTests</c> and <c>Tools/Build/thresher_hull_harness</c> run it as-is.
    ///
    /// <para><b>The tail is a CHAIN, posed as one.</b> The scythe's segments are siblings under the hull
    /// (so each can carry its own mesh and the builder can re-seat them), but they are posed as a chain:
    /// segment k is hinged at the END of segment k−1 as that segment is actually rotated, so a curl never
    /// opens a gap between segments. <see cref="PoseChain"/> is that composition.</para>
    /// </summary>
    public static class ThresherSwimMath
    {
        /// <summary>The shipped swim — <see cref="ThresherAnimation"/>'s field initializers name these, so the
        /// offline harness measures the same numbers the prefab starts from.</summary>
        public const float DefaultBeatRate = 0.55f, DefaultBeatRateAtCruise = 1.1f, DefaultBaseSwing = 12f,
            DefaultLobeSwing = 22f, DefaultWaveLag = 0.7f, DefaultTurnSwing = 34f, DefaultWhipCurl = 48f, DefaultCrack = 55f;

        public static Settings Defaults => new()
        {
            BeatRate = DefaultBeatRate, BeatRateAtCruise = DefaultBeatRateAtCruise, BaseSwingDegrees = DefaultBaseSwing,
            LobeSwingDegrees = DefaultLobeSwing, WaveLag = DefaultWaveLag, TurnSwingDegrees = DefaultTurnSwing,
            WhipCurlDegrees = DefaultWhipCurl, CrackDegrees = DefaultCrack,
        };

        public struct Settings
        {
            public float BeatRate;            // tail beats per second at a standstill
            public float BeatRateAtCruise;    // ... and at cruise speed
            public float BaseSwingDegrees;    // the peduncle's share of a beat
            public float LobeSwingDegrees;    // each scythe segment's share of a beat (the wave grows to the tip)
            public float WaveLag;             // radians of phase each segment trails the one before it
            public float TurnSwingDegrees;    // the whole tail swings OUT of a full-stick turn by this much
            public float WhipCurlDegrees;     // planted, the tail curls toward the ball by up to this much
            public float CrackDegrees;        // a crack (a smash, a launch) flicks the tip by this much
        }

        /// <summary>The yaw of each joint (tail base first, then every scythe segment), in degrees about the
        /// hull's UP axis, for one frame.</summary>
        /// <param name="phase">The beat's phase, radians.</param>
        /// <param name="beat01">0..1: how hard the tail beats (speed and effort).</param>
        /// <param name="turn">-1..1: the stick's yaw. The tail swings OUT of the turn.</param>
        /// <param name="whip">-1..1: the side the planted ball is on (+1 = right), times how hard it pulls.</param>
        /// <param name="crack">0..1: a decaying flick toward <paramref name="crackSide"/>.</param>
        public static void JointYaws(in Settings s, float phase, float beat01, float turn, float whip, float crack,
            float crackSide, float[] into)
        {
            int n = into.Length;
            if (n == 0) return;
            float amp = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(beat01));
            for (int j = 0; j < n; j++)
            {
                // Joint 0 is the peduncle; joints 1.. are the scythe, root first. Each trails the last by
                // WaveLag, so a beat travels down the tail and the tip lands last.
                float swing = j == 0 ? s.BaseSwingDegrees : s.LobeSwingDegrees;
                float wave = swing * amp * Mathf.Sin(phase - j * s.WaveLag);
                float share = 1f / n;   // the steady terms are spread evenly so the TIP reaches the full angle
                float steer = -Mathf.Clamp(turn, -1f, 1f) * s.TurnSwingDegrees * share;
                float curl = Mathf.Clamp(whip, -1f, 1f) * s.WhipCurlDegrees * share;
                // The crack lives in the outer half of the tail: the tip snaps, the root barely moves.
                float tipWeight = n > 1 ? j / (float)(n - 1) : 1f;
                float flick = Mathf.Clamp01(crack) * crackSide * s.CrackDegrees * tipWeight * tipWeight * (2f / n);
                into[j] = wave + steer + curl + flick;
            }
        }

        /// <summary>Beats per second at <paramref name="speed01"/> of cruise.</summary>
        public static float BeatRate(in Settings s, float speed01) =>
            Mathf.Lerp(s.BeatRate, s.BeatRateAtCruise, Mathf.Clamp01(speed01));

        /// <summary>
        /// Compose a chain of hinged joints: joint 0 sits at <paramref name="restPivots"/>[0] and turns by
        /// <paramref name="localRotations"/>[0]; joint k's pivot is joint k−1's pivot plus joint k−1's
        /// ACCUMULATED rotation applied to the rest offset between them, and its rotation is the accumulated
        /// product. All in the parent's space (the hull's), which is where the sibling parts live.
        /// </summary>
        public static void PoseChain(Vector3[] restPivots, Quaternion[] localRotations, Vector3[] outPositions,
            Quaternion[] outRotations)
        {
            int n = restPivots.Length;
            if (n == 0) return;
            outPositions[0] = restPivots[0];
            outRotations[0] = localRotations[0];
            for (int k = 1; k < n; k++)
            {
                outPositions[k] = outPositions[k - 1] + outRotations[k - 1] * (restPivots[k] - restPivots[k - 1]);
                outRotations[k] = outRotations[k - 1] * localRotations[k];
            }
        }
    }
}
