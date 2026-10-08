using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The timing and shape of a crystal's flip wave (<see cref="CrystalFlipWave"/>): when each ring starts and
    /// how a plate moves once its ring has. Ring 0 - the plates round the start vertex - plays
    /// <see cref="leadRing"/>; every later ring plays <see cref="laterRings"/>, starting
    /// <see cref="ringStaggerSeconds"/> after the ring before it. The wave repeats every
    /// <see cref="loopSeconds"/>, from a new vertex each time.
    ///
    /// <c>TimeCrystalFlipWaveProfile.asset</c> is GENERATED from the artist's take in TimeCrystalExport.fbx by
    /// <c>Tools/Build/author_time_crystal_flip_wave.py</c> (one key per authored frame, linear between them),
    /// and its <c>--check</c> fails if the asset drifts from the take. Hand-tune a copy, not that asset.
    /// </summary>
    [CreateAssetMenu(fileName = "FlipWaveProfile", menuName = "ScriptableObjects/Crystals/Flip Wave Profile")]
    public class FlipWaveProfileSO : ScriptableObject
    {
        /// <summary>One ring's motion, each curve on that ring's own clock (seconds since the ring started).</summary>
        [System.Serializable]
        public struct FlipTrack
        {
            [Tooltip("Fraction of the 180° turn (0 = rest, 1 = flipped) against seconds since this ring started.")]
            [SerializeField] AnimationCurve flip;

            [Tooltip("Uniform squash about the plate's centroid (1 = none). Empty = no squash.")]
            [SerializeField] AnimationCurve scale;

            [Tooltip("Inward dip of the plate's centroid, in units of the plate's reach (half its long diagonal). Empty = none.")]
            [SerializeField] AnimationCurve dip;

            [Tooltip("Slide of the plate's centroid toward the start vertex, in units of the plate's reach. Empty = none.")]
            [SerializeField] AnimationCurve slide;

            public FlipSample Sample(float seconds) => new(
                Evaluate(flip, seconds, 0f),
                Evaluate(scale, seconds, 1f),
                Evaluate(dip, seconds, 0f),
                Evaluate(slide, seconds, 0f));

            static float Evaluate(AnimationCurve curve, float seconds, float empty) =>
                curve != null && curve.length > 0 ? curve.Evaluate(seconds) : empty;
        }

        [Header("Timing")]
        [Tooltip("Seconds per wave. At the end of each one every plate is flipped (the same shape as rest), so the next wave starts from rest at a new vertex.")]
        [SerializeField, Min(0.1f)] float loopSeconds = 2f;

        [Tooltip("Seconds into each wave that the lead ring (the plates round the start vertex) starts to turn.")]
        [SerializeField, Min(0f)] float firstRingStartSeconds = 0.2f;

        [Tooltip("Seconds between one ring starting and the next. Every ring must finish inside the loop.")]
        [SerializeField, Min(0f)] float ringStaggerSeconds = 0.2f;

        [Header("Motion")]
        [Tooltip("The lead ring's flip, squash and pinch (dip + slide toward the start vertex).")]
        [SerializeField] FlipTrack leadRing;

        [Tooltip("The motion every later ring shares.")]
        [SerializeField] FlipTrack laterRings;

        public float LoopSeconds => loopSeconds;

        /// <summary>Ring <paramref name="ring"/>'s pose <paramref name="loopSeconds"/> into a wave.</summary>
        public FlipSample Sample(int ring, float loopSeconds)
        {
            float seconds = loopSeconds - (firstRingStartSeconds + ring * ringStaggerSeconds);
            return ring == 0 ? leadRing.Sample(seconds) : laterRings.Sample(seconds);
        }
    }
}
