using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "ConsumeBoostAction", menuName = "ScriptableObjects/Vessel Actions/Consume Boost")]
    public class ConsumeBoostActionSO : ShipActionSO
    {
        [Header("Boost Effect")]
        [SerializeField] private float boostMultiplier = 3f;
        [SerializeField] private float boostDuration = 4f;

        /// <summary>TIME -> boost duration: x1 at the resting level, x1.6 at level 10, floored at x0.25.
        /// Migrated verbatim from the retired ElementalAbilityMapSO generic
        /// multiplier (atFull 1.6, minMultiplier 0.25) — see
        /// Docs/ElementalAbilitySystem/ELEMENT_SCALING_UNIFICATION.md. Lives here, on the
        /// asset that owns the parameter, so it can only ever scale this one number.
        /// Never bound (this is a ScriptableObject, and BindElementalFloats reflects only
        /// over ElementalShipComponent MonoBehaviours), so it holds no per-vessel state.
        /// </summary>
        /// <para>This is the boost's DURATION and deliberately not its SPEED. Until this
        /// migration the same element also multiplied the speed, through the fleet-wide boost
        /// read in VesselTransformer.CurrentBoostAmount, which the map declared nowhere.</para>
        [SerializeField] ElementalFloat timeDurationMultiplier =
            ElementalFloat.Multiplier(1f, 1.6f, Element.Time, 0.25f);

        [Header("Magazine (charges)")]
        [SerializeField, Range(1, 4)] private int maxCharges = 4;
        [SerializeField] private float reloadCooldown = 3f;  
        [SerializeField] private float reloadFillTime = 0.8f;  

        [Header("Optional resource gate (one-time spend per shot; set <=0 to ignore)")]
        [SerializeField] private int resourceIndex = 1;
        [SerializeField] private float resourceCost = 0f;

        public float BoostMultiplier => boostMultiplier;
        public float BoostDuration => boostDuration;

        /// <summary>The live TIME multiplier on one charge's duration.</summary>
        public float TimeDurationMultiplier(IVesselStatus status)
            => timeDurationMultiplier.EvaluateLive(status);
        public int MaxCharges => maxCharges;
        public float ReloadCooldown => reloadCooldown;
        public float ReloadFillTime => reloadFillTime;
        public int ResourceIndex => resourceIndex;
        public float ResourceCost => resourceCost;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<ConsumeBoostActionExecutor>()?.Consume(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<ConsumeBoostActionExecutor>()?.StopAllBoosts();
    }
}
