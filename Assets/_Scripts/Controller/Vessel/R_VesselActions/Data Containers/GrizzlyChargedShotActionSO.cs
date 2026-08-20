using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Fire — Charged Cannon (right trigger). See GRIZZLY_CHARGED_CANNON.md.
    ///
    /// Fly-by-wire gesture, one input:
    ///   pull    → charge (energy builds while held)
    ///   release → fire (explosion size scales with the charge spent)
    ///   pull    → freeze the shot in flight
    ///   release → detonate it where it hangs
    ///
    /// Element link: Space scales explosion size; at Space 5 blasts spare the
    /// shooter's own domain (self-propulsion is preserved — see
    /// VesselImpulseByExplosionEffectSO).
    /// </summary>
    [CreateAssetMenu(fileName = "GrizzlyChargedShotAction", menuName = "ScriptableObjects/Vessel Actions/Grizzly Charged Shot")]
    public class GrizzlyChargedShotActionSO : ShipActionSO
    {
        [Header("Charge")]
        [SerializeField, Tooltip("Energy gained per second while the trigger is held.")]
        float chargePerSecond = 0.4f;
        [SerializeField, Tooltip("Index of the Grizzly's single Energy resource.")]
        int energyIndex = 0;
        [SerializeField, Tooltip("Minimum charge required to fire on release.")]
        float minChargeToFire = 0.05f;

        [Header("Projectile")]
        [SerializeField] float projectileScale = 30f;
        [SerializeField] float projectileSpeed = 400f;
        [SerializeField, Tooltip("Flight seconds. The shell flies until frozen/detonated or impact.")]
        float projectileTime = 9999f;

        [Header("Detonation")]
        [SerializeField] ProjectileDetonatorSO detonator;
        [SerializeField] AOEExplosion[] aoePrefabs;
        [SerializeField, Tooltip("Explosion scale at zero charge.")]
        float minExplosionScale = 20f;
        [SerializeField, Tooltip("Explosion scale at full charge, before Space scaling.")]
        float maxExplosionScale = 120f;
        [SerializeField, Tooltip("Space element multiplier on explosion scale at level 10.")]
        float spaceScaleAtFull = 2.5f;
        [SerializeField] float spaceScaleMinMul = 0.5f;
        [SerializeField] float explodeDelaySeconds = 0.05f;
        [SerializeField] float returnDelaySeconds = 1f;

        public float ChargePerSecond => chargePerSecond;
        public int EnergyIndex => energyIndex;
        public float MinChargeToFire => minChargeToFire;
        public float ProjectileScale => projectileScale;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileTime => projectileTime;
        public ProjectileDetonatorSO Detonator => detonator;
        public AOEExplosion[] AoePrefabs => aoePrefabs;
        public float MinExplosionScale => minExplosionScale;
        public float MaxExplosionScale => maxExplosionScale;
        public float SpaceScaleAtFull => spaceScaleAtFull;
        public float SpaceScaleMinMul => spaceScaleMinMul;
        public float ExplodeDelaySeconds => explodeDelaySeconds;
        public float ReturnDelaySeconds => returnDelaySeconds;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlyChargedShotActionExecutor>()?.OnPress(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlyChargedShotActionExecutor>()?.OnRelease(this, vesselStatus);
    }
}
