using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Thresholds for the host-side movement plausibility check (fair play, Threat Board T5;
    /// <c>Docs/AntiCheat/README.md</c> §4.2). Loaded from <c>Resources/MovementPlausibilityConfig</c>;
    /// when that asset does not exist the defaults below apply, and with no hull ceilings the check
    /// only OBSERVES: it records how fast every hull really flies so the ceilings can be set from
    /// measured play instead of guessed from the transformer's many speed terms.
    /// </summary>
    [CreateAssetMenu(fileName = "MovementPlausibilityConfig", menuName = "ScriptableObjects/Fair Play/Movement Plausibility Config")]
    public class MovementPlausibilityConfigSO : ScriptableObject
    {
        public const string ResourcePath = "MovementPlausibilityConfig";

        [Serializable]
        public struct HullCeiling
        {
            [Tooltip("The hull this ceiling applies to.")]
            public VesselClassType Hull;

            [Tooltip("Highest believable speed (world units per second) held over the whole sample window. " +
                     "Set it from the observed maxima this check logs for honest play (AI and humans), plus a margin. " +
                     "0 = observe only for this hull.")]
            [Min(0f)] public float MaxSustainedSpeed;
        }

        [Header("Sampling")]
        [Tooltip("How often each vessel's position is sampled on the host, in seconds.")]
        [SerializeField, Min(0.02f)] float sampleInterval = 0.1f;

        [Tooltip("Speed is measured as displacement over this window, so one late network update cannot fake a spike.")]
        [SerializeField, Min(0.2f)] float windowSeconds = 1f;

        [Tooltip("A single sample step faster than this (units per second) is a discontinuity: a respawn, a wormhole, " +
                 "a warp or a teleport hack. The window restarts and the step is counted as a jump, never as speed.")]
        [SerializeField, Min(1f)] float jumpSpeed = 2000f;

        [Header("Signals")]
        [Tooltip("Per-hull ceilings. A hull with no entry, or a 0 ceiling, is observed only.")]
        [SerializeField] List<HullCeiling> hullCeilings = new();

        [Tooltip("A pilot must stay above the ceiling this long before a signal is raised.")]
        [SerializeField, Min(0f)] float sustainSeconds = 2f;

        [Tooltip("Jumps per minute above which a pilot raises a signal. 0 = never (wormholes and warps are legitimate jumps).")]
        [SerializeField, Min(0f)] float maxJumpsPerMinute = 0f;

        [Tooltip("After a signal, the same pilot cannot raise the same kind of signal again for this many seconds.")]
        [SerializeField, Min(1f)] float signalCooldownSeconds = 30f;

        [Header("AI-like steering (Threat Board T6)")]
        [Tooltip("A turn rate (degrees per second) below this counts as holding the nose still. Humans hold still " +
                 "between inputs; the AI pilot corrects every frame.")]
        [SerializeField, Min(0f)] float steerIdleTurnRate = 2f;

        [Tooltip("A guest whose share of still samples (percent) stays at or below this, AND whose mean change in " +
                 "turn rate stays at or below the jerk limit, raises an AiLikeSteering signal. Set both from the " +
                 "per-turn log of AI and human values. 0 = observe only.")]
        [SerializeField, Range(0f, 100f)] float aiLikeMaxIdlePercent = 0f;

        [Tooltip("Mean change in turn rate (degrees per second, per second) at or below which steering reads as the " +
                 "AI's. 0 = observe only.")]
        [SerializeField, Min(0f)] float aiLikeMaxTurnJerk = 0f;

        [Tooltip("Seconds of steering a guest must fly in one turn before the AI-like check judges it.")]
        [SerializeField, Min(10f)] float aiLikeMinSeconds = 60f;

        public float SampleInterval => sampleInterval;
        public float SteerIdleTurnRate => steerIdleTurnRate;
        public float AiLikeMaxIdlePercent => aiLikeMaxIdlePercent;
        public float AiLikeMaxTurnJerk => aiLikeMaxTurnJerk;
        public float AiLikeMinSeconds => aiLikeMinSeconds;
        public bool AiLikeCheckOn => aiLikeMaxIdlePercent > 0f && aiLikeMaxTurnJerk > 0f;
        public float WindowSeconds => windowSeconds;
        public float JumpSpeed => jumpSpeed;
        public float SustainSeconds => sustainSeconds;
        public float MaxJumpsPerMinute => maxJumpsPerMinute;
        public float SignalCooldownSeconds => signalCooldownSeconds;

        /// <summary>The ceiling for <paramref name="hull"/>, or 0 when the hull is observed only.</summary>
        public float CeilingFor(VesselClassType hull)
        {
            for (int i = 0; i < hullCeilings.Count; i++)
                if (hullCeilings[i].Hull == hull) return hullCeilings[i].MaxSustainedSpeed;
            return 0f;
        }

        static MovementPlausibilityConfigSO s_loaded;

        /// <summary>The authored asset, or an in-memory instance with the defaults (observe only).</summary>
        public static MovementPlausibilityConfigSO LoadOrDefault()
        {
            if (s_loaded) return s_loaded;
            s_loaded = Resources.Load<MovementPlausibilityConfigSO>(ResourcePath);
            if (!s_loaded) s_loaded = CreateInstance<MovementPlausibilityConfigSO>();
            return s_loaded;
        }
    }
}
