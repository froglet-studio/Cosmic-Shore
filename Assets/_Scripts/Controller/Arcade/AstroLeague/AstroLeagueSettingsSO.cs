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

        [Tooltip("Domain goal sum that ends the match early (mercy rule). Published to GameDataSO.GoalTargetCount.")]
        public int goalLimit = 5;

        [Tooltip("If true and the score is tied at full time, play sudden-death golden goal")]
        public bool goldenGoalOvertime = true;

        [Header("Kickoff Pacing")]
        [Tooltip("Seconds of GOAL! celebration (real time) before the ball resets")]
        public float celebrationSeconds = 2.2f;

        [Tooltip("Time.timeScale during the goal celebration slow-mo. Solo sessions only — " +
                 "never applied with a second connected client (local timescale desyncs peers).")]
        [Range(0.05f, 1f)] public float celebrationTimeScale = 0.35f;

        [Tooltip("Seconds the ball stays frozen at center during a kickoff count-in")]
        public float kickoffFreezeSeconds = 2.4f;

        [Tooltip("Seconds the winner banner holds before the shared scoreboard flow takes over")]
        public float winnerBannerSeconds = 2.5f;

        [Header("Kickoff Parking")]
        [Tooltip("Distance from arena center toward a team's own goal where its vessels park for kickoff")]
        public float kickoffLineDistance = 110f;

        [Tooltip("Lateral spacing between teammates parked on the same kickoff line")]
        public float kickoffLateralSpacing = 30f;

        [Header("Ball — Collision Response")]
        [Tooltip("Multiplier on vessel speed when transferring momentum to the ball")]
        public float hitBoostMultiplier = 2.5f;

        [Tooltip("0 = pure billiard deflection (away from contact point), 1 = pure push (vessel heading). " +
                 "Higher gives players more aim control over where a struck ball goes.")]
        [Range(0f, 1f)] public float directionalBias = 0.45f;

        [Tooltip("Fraction of existing ball velocity preserved on vessel hit (redirect feel)")]
        [Range(0f, 1f)] public float velocityRetention = 0.15f;

        [Tooltip("Vessel speed below this threshold is ignored (prevents ghost taps)")]
        public float minimumHitSpeed = 5f;

        [Tooltip("On a strike, the ball is ejected so its center is at least (ball radius + this) " +
                 "from the vessel root — guarantees the vessel hull never clips through the ball, " +
                 "including the trigger-only ships (Serpent/Sparrow) that have no physical barrier. " +
                 "Roughly the vessel's visual hull reach.")]
        public float vesselClearRadius = 12f;

        [Header("Ball — Physics (zero friction)")]
        public float maxSpeed = 220f;
        public float ballMass = 3f;
        [Tooltip("1 = perfectly elastic reflection direction. The ball has ZERO passive friction/drag " +
                 "— it coasts at constant speed between collisions; the ONLY speed decay is the " +
                 "per-collision loss below.")]
        [Range(0f, 1f)] public float ballBounciness = 1f;

        [Tooltip("Fraction of speed KEPT on each wall/prism bounce — THE ONLY speed-decay mechanism " +
                 "(0.85 = lose 15% per collision). Vessel strikes re-energize the ball. The energy " +
                 "lost feeds the prism explosion.")]
        [Range(0f, 1f)] public float collisionSpeedRetention = 0.85f;

        [Header("Ball — Prism Explosion (per collision, scaled by speed)")]
        [Tooltip("Every collision explodes live prisms within a speed-scaled radius of the contact " +
                 "(the canonical animated Prism.Damage path — mass-conserving active force). This is " +
                 "the radius at/below minimum impact speed.")]
        public float prismDestroyRadius = 6f;

        [Tooltip("Explosion radius at max ball speed — a fast collision blasts a much wider crater")]
        public float prismDestroyRadiusAtMaxSpeed = 18f;

        [Tooltip("Impact speed below which a collision doesn't explode prisms (a faint tap just bounces)")]
        public float prismDestroyMinSpeed = 5f;

        [Tooltip("Minimum seconds between prism-explosion broadcasts (flood guard while plowing a wall)")]
        public float prismDestroyCooldown = 0.02f;

        [Header("Ball — Client Replication")]
        [Tooltip("How aggressively non-server peers blend toward the dead-reckoned ball position (higher = snappier)")]
        public float clientSmoothingRate = 12f;

        [Tooltip("Position error beyond which non-server peers snap instead of smoothing")]
        public float clientSnapDistance = 30f;

        [Header("Juice — Hitstop (solo sessions only)")]
        public float hitstopDuration = 0.045f;
        [Range(0.01f, 1f)] public float hitstopTimeScale = 0.1f;
        [Tooltip("Ball speed required to trigger hitstop on a strike")]
        public float hitstopSpeedThreshold = 70f;

        [Header("Juice — Camera Shake")]
        public float strikeShakeIntensity = 1.0f;
        public float strikeShakeDuration = 0.18f;
        public float goalShakeIntensity = 2.5f;
        public float goalShakeDuration = 0.5f;

        [Tooltip("Camera shake fades with distance from the impact, reaching zero at this radius")]
        public float shakeFalloffRadius = 180f;

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

        [Header("Arena — Visuals")]
        public Color edgeColor = new(0.25f, 0.85f, 1f, 0.55f);
        public Color jadeGoalColor = new(0.15f, 1f, 0.55f, 0.5f);
        public Color rubyGoalColor = new(1f, 0.22f, 0.35f, 0.5f);
        public Color planktonColor = new(0.55f, 0.8f, 1f, 0.35f);
        [Tooltip("Drifting hypersea motes that give speed perception inside the arena")]
        public int planktonCount = 400;
    }
}
