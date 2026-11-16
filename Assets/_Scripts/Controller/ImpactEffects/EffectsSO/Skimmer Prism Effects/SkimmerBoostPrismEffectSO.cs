using UnityEngine;
using CosmicShore.Soap;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
using CosmicShore.UI;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine.Serialization;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "SkimmerBoostPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerBoostPrismEffectSO")]
    public class SkimmerBoostPrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField] private float addPerHit = 0.1f;

        [Tooltip("Multiplies the energy gained per hit when the skimmed prism is dangerous. " +
                 "LOCKED behind the level-5 elemental upgrade named by dangerBonusElement " +
                 "(map-gated via IsUpgradeActive) - below the unlock, danger prisms grant only " +
                 "the base energy.")]
        [SerializeField] private float dangerEnergyMultiplier = 10f;

        [Tooltip("Which element's level-5 upgrade unlocks the danger bonus. AUTHORED rather than " +
                 "hardcoded, because the element that owns skimming is a per-vessel map decision: " +
                 "the Squirrel moved it Charge -> Time when Time took its speed abilities. The " +
                 "sibling SkimmerChangeResourceByPrismEffectSO._dangerBonusElement is the same " +
                 "shape. Charge is the historical default so an unauthored asset is unchanged.")]
        [SerializeField] private Element dangerBonusElement = Element.Charge;

        /// <summary>Skim energy per hit: x1 at the resting level, x2 at level 10, floored at x0.5 (half the rest value at level -5).
        /// The ELEMENT is authored on this field, not fixed in code - the Squirrel maps it to Time.
        /// Migrated verbatim from the retired ElementalAbilityMapSO generic
        /// multiplier (atFull 2, minMultiplier 0.25) — see
        /// Docs/ElementalAbilitySystem/ELEMENT_SCALING_UNIFICATION.md. Lives here, on the
        /// asset that owns the parameter, so it can only ever scale this one number.
        /// Never bound (this is a ScriptableObject, and BindElementalFloats reflects only
        /// over ElementalShipComponent MonoBehaviours), so it holds no per-vessel state.
        /// </summary>
        [FormerlySerializedAs("chargeEnergyMultiplier")]
        [SerializeField] ElementalFloat energyMultiplier =
            ElementalFloat.Multiplier(1f, 2f, Element.Charge, 0.5f);

        [Header("Skim streak")]
        [Tooltip("Skims landed within this many seconds of the last one build a streak, and each step of the streak " +
                 "multiplies the energy of the next skim. A gap longer than this resets it. 0 turns the streak off.")]
        [SerializeField, Min(0f)] float streakWindowSeconds;
        [Tooltip("How much each step of the streak adds to the energy multiplier (0.25 = the fifth skim in a row pays x2).")]
        [SerializeField, Min(0f)] float streakStep = 0.25f;
        [Tooltip("The streak's ceiling, as an energy multiplier.")]
        [SerializeField, Min(1f)] float streakCap = 2.5f;

        /// <summary>Per-vessel streak, held weakly so a destroyed vessel takes its entry with it. Not serialized
        /// and not shared across vessels: the SO stays a config, each hull keeps its own chain.</summary>
        sealed class Streak { public float Last = float.NegativeInfinity; public int Steps; }
        readonly ConditionalWeakTable<IVesselStatus, Streak> _streaks = new();

        /// <summary>The streak multiplier this skim pays, advancing the vessel's chain (1 when the streak is off).</summary>
        float AdvanceStreak(IVesselStatus status)
        {
            if (streakWindowSeconds <= 0f || status == null) return 1f;
            var streak = _streaks.GetOrCreateValue(status);
            float now = Time.time;
            streak.Steps = now - streak.Last <= streakWindowSeconds ? streak.Steps + 1 : 0;
            streak.Last = now;
            return Mathf.Min(streakCap, 1f + streakStep * streak.Steps);
        }

        /// <summary>The vessel's current streak multiplier without advancing it (1 once the window has lapsed).</summary>
        public float StreakMultiplier(IVesselStatus status)
        {
            if (streakWindowSeconds <= 0f || status == null || !_streaks.TryGetValue(status, out var streak)) return 1f;
            if (Time.time - streak.Last > streakWindowSeconds) return 1f;
            return Mathf.Min(streakCap, 1f + streakStep * streak.Steps);
        }

        [Header("Shared Config (single source of truth)")]
        [SerializeField] private ScriptableVariable<float> boostBaseMultiplier; // initial/base
        [SerializeField] private ScriptableVariable<float> boostMaxMultiplier;  // max

        [Header("Events")]
        [SerializeField] private ScriptableEventBoostChanged boostChanged;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Skimmer.VesselStatus;

            float baseMult = boostBaseMultiplier != null ? boostBaseMultiplier.Value : 1f;
            float maxMult  = boostMaxMultiplier  != null ? boostMaxMultiplier.Value  : 5f;

            // HARD GUARDS (prevents “stuck forever”)
            baseMult = Mathf.Max(0.0001f, baseMult);
            maxMult  = Mathf.Max(baseMult, maxMult);

            status.IsBoosting = true;

            // Skim energy: the energy gained per prism-skimmer collision scales with the vessel's
            // live level in whichever element this asset's ElementalFloat names (1x at the resting
            // level, 1x for a vessel with no map entry for it). Per-hit snapshot; stateless SO.
            float add = addPerHit * energyMultiplier.EvaluateLive(status);

            // The streak: skims chained inside the window pay more each step, so threading a wall of prisms is a
            // rising surge rather than a flat trickle, and a pilot who breaks the line feels the reset.
            add *= AdvanceStreak(status);

            // Level-5 'Live Wire': danger prisms grant the bonus energy multiplier only once the
            // skimming vessel's dangerBonusElement upgrade is active (below it, danger prisms pay
            // base energy - the risk stays, the 10x reward is earned).
            if (prismImpactee.Prism.prismProperties.IsDangerous
                && status?.ElementalAbilityHandler?.IsUpgradeActive(dangerBonusElement) == true)
                add *= dangerEnergyMultiplier;

            float next = status.BoostMultiplier + add;
            next = Mathf.Clamp(next, baseMult, maxMult);

            status.BoostMultiplier = next;

            boostChanged?.Raise(new BoostChangedPayload
            {
                BoostMultiplier = status.BoostMultiplier,
                MaxMultiplier = maxMult,
                SourceDomain = prismImpactee.OwnDomain,
                VesselStatus = status
            });
        }
    }
}