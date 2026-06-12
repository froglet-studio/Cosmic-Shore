using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Single designer-facing config for Astro League: match rules, kickoff pacing,
    /// billiard ball physics, impact juice, AI striker tuning, and arena visuals.
    /// All gameplay feel lives here — the MonoBehaviours only execute it.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AstroLeagueSettings",
        menuName = "ScriptableObjects/Arcade/AstroLeagueSettings")]
    public class AstroLeagueSettingsSO : ScriptableObject
    {
        [Header("Match Rules")]
        [Tooltip("Regulation match length in seconds")]
        public float matchDurationSeconds = 180f;

        [Tooltip("Goals needed for a mercy-rule win before time expires")]
        public int goalLimit = 5;

        [Tooltip("If true and the score is tied at full time, play sudden-death golden goal")]
        public bool goldenGoalOvertime = true;

        [Header("Kickoff Pacing")]
        [Tooltip("Seconds of GOAL! celebration (real time) before the ball resets")]
        public float celebrationSeconds = 2.2f;

        [Tooltip("Time.timeScale during the goal celebration slow-mo")]
        [Range(0.05f, 1f)] public float celebrationTimeScale = 0.35f;

        [Tooltip("Seconds the ball stays frozen at center during a kickoff count-in")]
        public float kickoffFreezeSeconds = 2.4f;

        [Header("Ball — Collision Response")]
        [Tooltip("Multiplier on vessel speed when transferring momentum to the ball")]
        public float hitBoostMultiplier = 2.5f;

        [Tooltip("0 = pure billiard deflection (away from contact point), 1 = pure push (vessel heading)")]
        [Range(0f, 1f)] public float directionalBias = 0.35f;

        [Tooltip("Fraction of existing ball velocity preserved on vessel hit (redirect feel)")]
        [Range(0f, 1f)] public float velocityRetention = 0.15f;

        [Tooltip("Vessel speed below this threshold is ignored (prevents ghost taps)")]
        public float minimumHitSpeed = 5f;

        [Header("Ball — Physics")]
        public float maxSpeed = 220f;
        public float ballMass = 3f;
        [Range(0f, 1f)] public float ballBounciness = 0.98f;

        [Tooltip("Drag at max speed — low keeps the ball coasting like a billiard")]
        public float highSpeedDrag = 0.25f;

        [Tooltip("Drag near zero speed — high stops creeping")]
        public float lowSpeedDrag = 3f;

        [Tooltip("Speed below which ball velocity snaps to zero")]
        public float stopThreshold = 2f;

        [Header("Juice — Hitstop")]
        public float hitstopDuration = 0.045f;
        [Range(0.01f, 1f)] public float hitstopTimeScale = 0.1f;
        [Tooltip("Ball speed required to trigger hitstop on a strike")]
        public float hitstopSpeedThreshold = 70f;

        [Header("Juice — Camera Shake")]
        public float strikeShakeIntensity = 1.0f;
        public float strikeShakeDuration = 0.18f;
        public float goalShakeIntensity = 2.5f;
        public float goalShakeDuration = 0.5f;

        [Header("Juice — Flash & Particles")]
        [Tooltip("Seconds the ball emission spikes after a strike")]
        public float impactFlashDuration = 0.12f;
        [Tooltip("Emission multiplier at peak flash")]
        public float impactFlashIntensity = 14f;
        public int impactParticleBurst = 28;
        public int goalParticleBurst = 120;

        [Header("Ball — Speed-Reactive Visuals")]
        public float minTrailWidth = 0.6f;
        public float maxTrailWidth = 5f;
        public float minEmissionIntensity = 2.5f;
        public float maxEmissionIntensity = 11f;
        public float minLightRange = 25f;
        public float maxLightRange = 90f;
        [Tooltip("Ball speed at which speed-reactive visuals are fully maxed")]
        public float speedForMaxVisuals = 160f;

        [Header("AI Striker")]
        [Tooltip("How far behind the ball (along the shot line) the AI aims its approach")]
        public float strikerApproachLead = 18f;

        [Tooltip("When recovering position, how far past the ball the AI swings wide")]
        public float strikerRecoverDistance = 60f;

        [Tooltip("Within this distance of its own goal, the AI prioritizes defending")]
        public float strikerDefendRadius = 130f;

        [Header("Arena — Visuals")]
        public Color edgeColor = new(0.25f, 0.85f, 1f, 0.55f);
        public Color jadeGoalColor = new(0.15f, 1f, 0.55f, 0.5f);
        public Color rubyGoalColor = new(1f, 0.22f, 0.35f, 0.5f);
        public Color planktonColor = new(0.55f, 0.8f, 1f, 0.35f);
        [Tooltip("Drifting hypersea motes that give speed perception inside the arena")]
        public int planktonCount = 400;
    }
}
