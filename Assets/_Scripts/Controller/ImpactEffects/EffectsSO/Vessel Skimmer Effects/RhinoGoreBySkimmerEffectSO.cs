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
    /// <para>CHARGE is the gore's MINIMUM RAM SPEED, the Squirrel's joust rule (<see cref="JoustSpeedConfigSO"/>, Garrett
    /// 2026-11-12: "do what squirrel does and have charge reduce the minimum speed"): <see cref="goreSpeed"/> (asset
    /// RhinoGoreSpeed) is 150 u/s at rest, three times the 50 u/s cruise, so a gore needs a short ramp wind-up, down to
    /// 50 u/s at Charge 10, where a cruising Rhino gores. Below it the contact is neither scored nor robbed
    /// (<see cref="IContactAdmission"/>). The take itself is flat: the fleet price of a Strike (8 points, 0.08 a petal
    /// per element, the Swordfish sword's own number). Below Charge 5 the petals come loose as free crystals at the strike (Eject); with Charge's level-5 upgrade
    /// (<b>Gore Steal</b>) they land on the Rhino instead. During an omni-crystal <b>Stampede</b>
    /// (<see cref="RhinoStampede"/>) every gore is gated at its Charge 10 speed and steals, whatever the levels.</para>
    ///
    /// <para>PvP stays petals only (LOCKED 2026-10-10): the gore moves nobody. Decided once, on the Rhino's owner
    /// (<see cref="ElementalTransfer.ApplyAllAuthoritative"/>), like every contact take. Design:
    /// <c>R_VesselActions/RHINO_PLOUGH.md</c> § "The 2026-11-12 rework".</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "RhinoGoreBySkimmerEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/RhinoGoreBySkimmerEffectSO")]
    public sealed class RhinoGoreBySkimmerEffectSO : VesselSkimmerEffectsSO, IContactPetalTake, IContactAdmission
    {
        [Header("Charge")]
        [Tooltip("CHARGE: the least ram speed a gore needs (RhinoGoreSpeed: 150 u/s at rest down to 50 u/s at Charge 10), " +
                 "read at the Rhino's replicated level. Empty = no minimum beyond out-running the victim.")]
        [SerializeField] JoustSpeedConfigSO goreSpeed;

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

            float amount = StrikePerElement;
            var form = RhinoStampede.IsActive(attacker) || StealUnlocked(attacker) ? ElementalTransferForm.Steal : ElementalTransferForm.Eject;
            ElementalTransfer.ApplyAllAuthoritative(form, victim, attacker, amount,
                form == ElementalTransferForm.Eject ? attacker.Course * attacker.Speed : Vector3.zero,
                ElementalDebuffSources.VesselContact);
        }

        /// <summary>The reporter's question (<see cref="IContactAdmission"/>): is the Rhino ramming fast enough to gore?
        /// During a Stampede the minimum is the Charge 10 one.</summary>
        public bool AdmitsContact(IVesselStatus attacker, IVesselStatus victim)
        {
            if (!goreSpeed) return true;
            if (attacker == null) return false;
            return RhinoStampede.IsActive(attacker)
                ? attacker.Speed >= goreSpeed.MinimumSpeedAtLevel(1f)
                : goreSpeed.Admits(attacker);
        }

        bool StealUnlocked(IVesselStatus rhino)
        {
            if (stealUpgradeElement == Element.None) return false;
            var abilities = rhino.ElementalAbilityHandler;
            return abilities && abilities.IsUpgradeActive(stealUpgradeElement);
        }
    }
}
