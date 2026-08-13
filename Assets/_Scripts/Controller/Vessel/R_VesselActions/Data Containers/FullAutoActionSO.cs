using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Sparrow's full-auto cannons. This asset is also the single authored home of the
    /// vessel's gun cadence, muzzle speed, flight time and ACCURACY: the Turret Stance
    /// (<see cref="FullAutoBlockShootActionSO"/>) fires prisms at the SAME rate, the SAME
    /// speed, along the SAME flight path and through the SAME spread cone, and it gets those
    /// numbers by pointing at this asset rather than copying them. Retune here and both fire
    /// modes move together.
    /// </summary>
    [CreateAssetMenu(fileName="FullAutoAction", menuName="ScriptableObjects/Vessel Actions/Full Auto")]
    public class FullAutoActionSO : ShipActionSO
    {
        [Header("Config")]
        [SerializeField] int   ammoIndex = 0;
        [SerializeField] float ammoCost  = 0.03f;
        [SerializeField] bool  inherit   = false;
        [SerializeField] float projectileScale = 1f;
        [SerializeField] float firingRate = 1f;
        [SerializeField] float projectileTime = 3f;
        [SerializeField] FiringPatterns firingPattern = FiringPatterns.Default;
        [SerializeField] int   energy = 0;
        [SerializeField] ElementalFloat speedValue;

        [Header("Accuracy")]
        [Tooltip("How the cone opens while the trigger is held, and the haptic ramp that " +
                 "reports it. Shared with the Turret Stance through bulletAction, exactly like " +
                 "cadence and speed — a turret shot IS a bullet, spread included.")]
        [SerializeField] GunSpreadProfile spread = new();

        public int AmmoIndex => ammoIndex;
        public float AmmoCost => ammoCost;
        public bool Inherit => inherit;
        public float ProjectileScale => projectileScale;
        public float FiringRate => firingRate;
        public float ProjectileTime => projectileTime;
        public FiringPatterns FiringPattern => firingPattern;
        public int Energy => energy;
        public ElementalFloat SpeedValue => speedValue;

        /// <summary>The accuracy-decay cone, shared by both fire modes. Never null — an
        /// all-zero profile is the sanctioned "no spread" opt-out.</summary>
        public GunSpreadProfile Spread => spread ??= new GunSpreadProfile();

        /// <summary>
        /// The live muzzle speed of one shot: the authored base scaled by the vessel's SPACE
        /// multiplier from its <c>ElementalAbilityMapSO</c>. Read per volley at fire time —
        /// never cached across a hold, and never bound as an ElementalFloat on this shared
        /// asset (per-vessel state on a shared SO is last-initializer-wins in multiplayer).
        /// </summary>
        public float ResolveSpeed(IVesselStatus status)
        {
            var abilities = status?.ElementalAbilityHandler;
            return speedValue.Value * (abilities ? abilities.Multiplier(Element.Space) : 1f);
        }

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            if (!execs) return;
            // The accuracy state is a per-vessel component, resolved here and handed down:
            // the SO is shared by every Sparrow in the match and must stay stateless.
            execs.Get<FullAutoActionExecutor>()?.Begin(this, execs.Get<GunSprayAccuracy>());
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<FullAutoActionExecutor>()?.End();
    }
}
