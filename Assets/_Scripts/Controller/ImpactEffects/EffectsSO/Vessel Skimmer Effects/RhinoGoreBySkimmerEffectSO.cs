using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Rhino's CHARGE, the <b>Gore</b> (Garrett, 2026-11-12: "charge has no pvp"): ramming a rival vessel is a scored
    /// Strike that knocks its petals loose. This is the TAKE; the score, the cooldown and the contact rule are the
    /// sibling <see cref="VesselCombatHitBySkimmerEffectSO"/> in the Rhino's skimmer container (VesselCombatHitByGore,
    /// <c>requireFasterThanVictim</c> ON, so a gore is a ram the Rhino drives, never a rival backing into it), which calls
    /// <see cref="TakeFrom"/> for the hit it has just admitted.
    ///
    /// <para>The base take is the fleet price of a Strike (8 points, 0.08 a petal per element, the Swordfish sword's own
    /// number), multiplied by <see cref="goreScale"/> at the Rhino's replicated Charge: x1 at rest, x3 at Charge 10.
    /// Below Charge 5 the petals come loose as free crystals at the strike (Eject); with Charge's level-5 upgrade
    /// (<b>Gore Steal</b>) they land on the Rhino instead. During an omni-crystal <b>Stampede</b>
    /// (<see cref="RhinoStampede"/>) every gore takes at full Charge and steals, whatever the levels.</para>
    ///
    /// <para>PvP stays petals only (LOCKED 2026-10-10): the gore moves nobody. Decided once, on the Rhino's owner
    /// (<see cref="ElementalTransfer.ApplyAllAuthoritative"/>), like every contact take. Design:
    /// <c>R_VesselActions/RHINO_PLOUGH.md</c> § "The 2026-11-12 rework".</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "RhinoGoreBySkimmerEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/RhinoGoreBySkimmerEffectSO")]
    public sealed class RhinoGoreBySkimmerEffectSO : VesselSkimmerEffectsSO, IContactPetalTake
    {
        [Header("Charge")]
        [Tooltip("CHARGE: a multiplier on the petals one gore takes, on top of the fleet price of a Strike. x1 at rest, " +
                 "x3 at Charge 10, floored at x1. Read at the Rhino's replicated level.")]
        [SerializeField] ElementalFloat goreScale = ElementalFloat.Multiplier(1f, 3f, Element.Charge, 1f);

        [Tooltip("CHARGE level-5 'Gore Steal': with this element's upgrade active the petals land on the Rhino instead of " +
                 "coming loose as crystals. None = always eject.")]
        [SerializeField] Element stealUpgradeElement = Element.Charge;

        /// <summary>The Strike's fleet price per element, in normalized level units (0.08).</summary>
        static float StrikePerElement =>
            CombatHitDrain.PointsFor(CombatHitClass.Strike) / CombatHitDrain.PointsPerLevel * CombatHitDrain.NormalizedPerLevel;

        // The take runs only through TakeFrom, for the hit the reporter admitted: a scored gore and its petals are one event.
        public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee) { }

        public void TakeFrom(IVesselStatus victim, IVesselStatus attacker, VesselImpactor impactor, SkimmerImpactor impactee)
        {
            if (victim == null || attacker == null) return;

            bool stampede = RhinoStampede.IsActive(attacker);
            float scale = stampede ? goreScale.EvaluateAtNormalizedLevel(1f) : goreScale.EvaluateReplicated(attacker);
            float amount = StrikePerElement * Mathf.Max(1f, scale);

            var form = stampede || StealUnlocked(attacker) ? ElementalTransferForm.Steal : ElementalTransferForm.Eject;
            ElementalTransfer.ApplyAllAuthoritative(form, victim, attacker, amount,
                form == ElementalTransferForm.Eject ? attacker.Course * attacker.Speed : Vector3.zero,
                ElementalDebuffSources.VesselContact);
        }

        bool StealUnlocked(IVesselStatus rhino)
        {
            if (stealUpgradeElement == Element.None) return false;
            var abilities = rhino.ElementalAbilityHandler;
            return abilities && abilities.IsUpgradeActive(stealUpgradeElement);
        }
    }
}
