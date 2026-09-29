using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Serpent's SOLID FUEL PELLETS (Time). One press burns one pellet - <see cref="ResourceCost"/>
    /// of the fuel resource at <see cref="ResourceIndex"/> - and burns overlap, each adding the same
    /// increment of speed for its own duration. The fuel resource IS the tank: its authored
    /// <c>resourceGainRate</c> is the refill rate and its <c>maxAmount / resourceCost</c> is the
    /// pellet capacity (4 on the shipped Serpent). Behaviour lives on
    /// <see cref="ConsumeBoostActionExecutor"/>; this asset is stateless and shared.
    /// See <c>R_VesselActions/SERPENT_FUEL_PELLETS.md</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "ConsumeBoostAction", menuName = "ScriptableObjects/Vessel Actions/Consume Boost")]
    public class ConsumeBoostActionSO : ShipActionSO
    {
        [Header("Pellet burn")]
        [Tooltip("The boost multiplier ONE burning pellet produces (3 = three times cruise). Each " +
                 "further pellet burning at the same time adds the same increment again, so n " +
                 "pellets give 1 + (this - 1) * n.")]
        [SerializeField] private float boostMultiplier = 3f;

        [Tooltip("Seconds one pellet burns at the resting Time level, before Time scales it.")]
        [SerializeField] private float boostDuration = 4f;

        /// <summary>TIME -> burn duration: x1 at the resting level, x1.6 at level 10, floored at x0.25.
        /// Migrated verbatim from the retired ElementalAbilityMapSO generic
        /// multiplier (atFull 1.6, minMultiplier 0.25) — see
        /// Docs/ElementalAbilitySystem/ELEMENT_SCALING_UNIFICATION.md. Lives here, on the
        /// asset that owns the parameter, so it can only ever scale this one number.
        /// Never bound (this is a ScriptableObject, and BindElementalFloats reflects only
        /// over ElementalShipComponent MonoBehaviours), so it holds no per-vessel state.
        /// </summary>
        /// <para>This is the burn's DURATION and deliberately not its SPEED.</para>
        [SerializeField] ElementalFloat timeDurationMultiplier =
            ElementalFloat.Multiplier(1f, 1.6f, Element.Time, 0.25f);

        [Header("Fuel")]
        [Tooltip("Index of the vessel ResourceSystem resource that holds the fuel.")]
        [SerializeField] private int resourceIndex = 1;

        [Tooltip("Fuel ONE pellet costs, as a fraction of that resource. The pellet capacity is " +
                 "the resource's maxAmount divided by this - 0.25 of a 1.0 tank is four pellets.")]
        [SerializeField] private float resourceCost = 0.25f;

        public float BoostMultiplier => boostMultiplier;
        public float BoostDuration => boostDuration;

        /// <summary>The live TIME multiplier on one pellet's burn duration.</summary>
        public float TimeDurationMultiplier(IVesselStatus status)
            => timeDurationMultiplier.EvaluateLive(status);
        public int ResourceIndex => resourceIndex;
        public float ResourceCost => resourceCost;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<ConsumeBoostActionExecutor>()?.Consume(this, vesselStatus);

        /// <summary>A release does not end a burn - a lit pellet burns out on its own clock.</summary>
        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<ConsumeBoostActionExecutor>()?.Release();
    }
}
