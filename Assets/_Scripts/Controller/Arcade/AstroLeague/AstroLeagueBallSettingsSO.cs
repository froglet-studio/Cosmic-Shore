using UnityEngine;

namespace CosmicShore.Game.Arcade.AstroLeague
{
    [CreateAssetMenu(
        fileName = "AstroLeagueBallSettings",
        menuName = "ScriptableObjects/AstroLeague/BallSettings")]
    public class AstroLeagueBallSettingsSO : ScriptableObject
    {
        [Header("Collision Response")]
        [Tooltip("Multiplier on ship speed when transferring momentum to the ball")]
        [SerializeField] float hitBoostMultiplier = 2.5f;

        [Tooltip("0 = pure billiard deflection (away from contact point), 1 = pure push (ship direction)")]
        [Range(0f, 1f)]
        [SerializeField] float directionalBias = 0.35f;

        [Tooltip("Fraction of existing ball velocity preserved on ship hit (gives redirect feel)")]
        [Range(0f, 1f)]
        [SerializeField] float velocityRetention = 0.15f;

        [Tooltip("Ship speed below this threshold is ignored (prevents ghost taps)")]
        [SerializeField] float minimumHitSpeed = 5f;

        [Header("Physics")]
        [SerializeField] float maxSpeed = 200f;
        [SerializeField] float mass = 3f;

        [Tooltip("Physics material bounciness for the ball collider")]
        [SerializeField] float ballBounciness = 0.98f;

        [Tooltip("Drag at max speed — low value keeps the ball coasting")]
        [SerializeField] float highSpeedDrag = 0.3f;

        [Tooltip("Drag near zero speed — high value stops creeping")]
        [SerializeField] float lowSpeedDrag = 3f;

        [Tooltip("Speed below which velocity snaps to zero")]
        [SerializeField] float stopThreshold = 2f;

        [Header("Impact Juice — Hitstop")]
        [Tooltip("Real-time duration of the time-scale dip")]
        [SerializeField] float hitstopDuration = 0.04f;

        [Tooltip("Time.timeScale during hitstop")]
        [SerializeField] float hitstopTimeScale = 0.1f;

        [Tooltip("Ball speed required to trigger hitstop")]
        [SerializeField] float hitstopSpeedThreshold = 60f;

        [Header("Impact Juice — Camera")]
        [SerializeField] float cameraShakeIntensity = 0.8f;
        [SerializeField] float cameraShakeDuration = 0.15f;

        [Header("Impact Juice — Flash")]
        [Tooltip("How long the emission spike lasts after a hit")]
        [SerializeField] float impactFlashDuration = 0.12f;

        [Tooltip("Emission multiplier at peak flash")]
        [SerializeField] float impactFlashIntensity = 15f;

        [Header("Impact Juice — Particles")]
        [SerializeField] int impactParticleBurstCount = 25;

        [Header("Speed-Reactive Visuals")]
        [SerializeField] float minTrailWidth = 0.5f;
        [SerializeField] float maxTrailWidth = 5f;
        [SerializeField] float minEmissionIntensity = 2f;
        [SerializeField] float maxEmissionIntensity = 12f;
        [SerializeField] float minLightRange = 20f;
        [SerializeField] float maxLightRange = 80f;

        [Tooltip("Ball speed at which all speed-reactive visuals are fully maxed")]
        [SerializeField] float speedForMaxVisuals = 150f;

        // --- Public accessors ---
        public float HitBoostMultiplier => hitBoostMultiplier;
        public float DirectionalBias => directionalBias;
        public float VelocityRetention => velocityRetention;
        public float MinimumHitSpeed => minimumHitSpeed;

        public float MaxSpeed => maxSpeed;
        public float Mass => mass;
        public float BallBounciness => ballBounciness;
        public float HighSpeedDrag => highSpeedDrag;
        public float LowSpeedDrag => lowSpeedDrag;
        public float StopThreshold => stopThreshold;

        public float HitstopDuration => hitstopDuration;
        public float HitstopTimeScale => hitstopTimeScale;
        public float HitstopSpeedThreshold => hitstopSpeedThreshold;
        public float CameraShakeIntensity => cameraShakeIntensity;
        public float CameraShakeDuration => cameraShakeDuration;
        public float ImpactFlashDuration => impactFlashDuration;
        public float ImpactFlashIntensity => impactFlashIntensity;
        public int ImpactParticleBurstCount => impactParticleBurstCount;

        public float MinTrailWidth => minTrailWidth;
        public float MaxTrailWidth => maxTrailWidth;
        public float MinEmissionIntensity => minEmissionIntensity;
        public float MaxEmissionIntensity => maxEmissionIntensity;
        public float MinLightRange => minLightRange;
        public float MaxLightRange => maxLightRange;
        public float SpeedForMaxVisuals => speedForMaxVisuals;
    }
}
