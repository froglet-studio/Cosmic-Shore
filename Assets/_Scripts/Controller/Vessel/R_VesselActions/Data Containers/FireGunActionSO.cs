using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "FireGunAction", menuName = "ScriptableObjects/Vessel Actions/Fire Gun")]
    public class FireGunActionSO : ShipActionSO
    {
        [Header("Config")]
        [SerializeField] int ammoIndex = 0;
        [SerializeField] float ammoCost = 0.03f;
        [SerializeField] float projectileScale = 1f;
        [SerializeField] int energy = 0;
        [SerializeField] float speed = 90f;
        [SerializeField] ElementalFloat projectileTime;

        [Header("Bay Launch")]
        [Tooltip("Seconds between the fire input (which starts the missile-bay animation) and the " +
                 "projectile actually spawning. 0 = spawn immediately at the gun muzzle (legacy " +
                 "behavior). The Sparrow's skyburst uses this to let the bay open and the animated " +
                 "missile clear the hull before the live projectile takes over at the bay's pose.")]
        [SerializeField] float launchDelaySeconds = 0f;

        public int AmmoIndex => ammoIndex;
        public float AmmoCost => ammoCost;
        public float ProjectileScale => projectileScale;
        public int Energy => energy;
        public float Speed => speed;
        public ElementalFloat ProjectileTime => projectileTime;
        public float LaunchDelaySeconds => launchDelaySeconds;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<FireGunActionExecutor>()?.Fire(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
        }
    }
}
