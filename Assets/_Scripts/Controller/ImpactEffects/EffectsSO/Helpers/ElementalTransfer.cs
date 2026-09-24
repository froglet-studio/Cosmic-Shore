using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// THE ONE SEAM A VESSEL DEBUFF GOES THROUGH. An elemental debuff used to be a temporary
    /// modifier that decayed back to zero, so a match-long fight over elements changed nothing:
    /// what a pilot was drained they got back in four seconds, and what an attacker took they
    /// never had. Every debuff is now a TRANSFER out of the victim's persistent level, and it
    /// lands in exactly one of three places (<see cref="ElementalTransferForm"/>) - stolen,
    /// knocked loose, or burned.
    ///
    /// <para><b>Conservation is enforced by the VICTIM, not by the caller.</b> The take is
    /// <see cref="ResourceSystem.AccrueElementalLoss"/>, which honours debuff immunity, clamps
    /// to what the victim actually holds above resting level 0, settles only in WHOLE petals, and
    /// returns how many truly came loose. Everything here hands out exactly that number. So a
    /// caller cannot over-pay an attacker, cannot mint a crystal out of a pilot who had nothing,
    /// and cannot leak a fraction - and a warded pilot yields nothing at all, which is what keeps
    /// the scoring effects that ask <c>requireDebuffableVictim</c> agreeing with what happened.
    /// </para>
    ///
    /// <para><b>Why the form keys on the weapon CLASS.</b> Contact verbs steal and ranged verbs
    /// eject, because that is what the two actions physically are: you cannot take a petal off
    /// somebody you never touched, and you cannot hand yourself one you shot out of them from
    /// three hundred units away. Keying on the class rather than authoring it per hull means a
    /// fleet whose weapons keep being re-cut cannot end up with two hulls disagreeing about what
    /// the same verb means - the trap CLAUDE.md records for every other fact that got authored
    /// eight times.</para>
    ///
    /// <para><b>Where it runs.</b> In the effect's own <c>Execute</c>, on every peer that
    /// dispatches that contact - which is the same place, and the same moment, the drain it
    /// replaces ran. Nothing here is server-authoritative, and nothing here needed to be: a
    /// vessel ability's press is round-tripped through the server
    /// (<c>R_VesselActionHandler</c>), so each peer simulates its own copy of the round and
    /// resolves the same contact against its own copies of both vessels. The one thing that is
    /// per-peer rather than agreed is an EJECTED crystal, which is a local object exactly as the
    /// food web's crystals are - see <see cref="ElementalCrystalEjector"/>.</para>
    /// </summary>
    public static class ElementalTransfer
    {
        static readonly Element[] AllElements =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        /// <summary>Every element, in the fleet's canonical charge/mass/space/time order - so a
        /// caller that debuffs "all four" spells it the same way the HUD flowers do.</summary>
        public static Element[] Elements => AllElements;

        /// <summary>
        /// Which destination a hit class sends its petals to. <see cref="CombatHitClass.Strike"/>
        /// is the contact verb (the Squirrel's joust, the Rhino's sword) and STEALS; every other
        /// class is ranged and EJECTS. A class added later ejects by default, which is the safe
        /// side: ejecting still conserves, it just does not pay the attacker automatically.
        /// </summary>
        public static ElementalTransferForm FormFor(CombatHitClass hitClass) =>
            hitClass == CombatHitClass.Strike
                ? ElementalTransferForm.Steal
                : ElementalTransferForm.Eject;

        /// <summary>
        /// Move <paramref name="normalizedAmount"/> of <paramref name="element"/> off
        /// <paramref name="victim"/> and put it wherever <paramref name="form"/> says. Returns the
        /// whole petals that actually moved (0 if the victim was warded or had nothing to give).
        /// </summary>
        /// <param name="attacker">Paid on a <see cref="ElementalTransferForm.Steal"/>; ignored otherwise.</param>
        /// <param name="impactVelocity">The hit's own velocity, used by
        /// <see cref="ElementalTransferForm.Eject"/> to throw the crystals; ignored otherwise.</param>
        public static int Apply(ElementalTransferForm form, IVesselStatus victim, IVesselStatus attacker,
                                Element element, float normalizedAmount, Vector3 impactVelocity,
                                ElementalDebuffSources source)
        {
            var resources = victim?.ResourceSystem;
            if (resources == null || normalizedAmount <= 0f) return 0;

            int petals = resources.AccrueElementalLoss(element, normalizedAmount, source);
            if (petals <= 0) return 0;

            switch (form)
            {
                case ElementalTransferForm.Steal:
                    // A steal with no attacker would silently BURN the petal, which is the one
                    // thing only a danger prism may do - eject instead, so the petal stays in
                    // play and the economy still balances.
                    var taker = attacker?.ResourceSystem;
                    if (taker != null) taker.GrantPetals(element, petals);
                    else ElementalCrystalEjector.Eject(victim, element, petals, impactVelocity);
                    break;

                case ElementalTransferForm.Eject:
                    ElementalCrystalEjector.Eject(victim, element, petals, impactVelocity);
                    break;

                case ElementalTransferForm.Burn:
                    // Nothing to do: AccrueElementalLoss already removed it and there is no
                    // recipient. This is the sink, and it is the only case that does not conserve.
                    break;
            }

            return petals;
        }

        /// <summary>The attacker takes what the victim loses. See <see cref="Apply"/>.</summary>
        public static int Steal(IVesselStatus victim, IVesselStatus attacker, Element element,
                                float normalizedAmount, ElementalDebuffSources source) =>
            Apply(ElementalTransferForm.Steal, victim, attacker, element, normalizedAmount,
                  Vector3.zero, source);

        /// <summary>The petals are knocked loose into the arena. See <see cref="Apply"/>.</summary>
        public static int Eject(IVesselStatus victim, Element element, float normalizedAmount,
                                Vector3 impactVelocity, ElementalDebuffSources source) =>
            Apply(ElementalTransferForm.Eject, victim, null, element, normalizedAmount,
                  impactVelocity, source);

        /// <summary>The petals are destroyed. The economy's only sink. See <see cref="Apply"/>.</summary>
        public static int Burn(IVesselStatus victim, Element element, float normalizedAmount,
                               ElementalDebuffSources source) =>
            Apply(ElementalTransferForm.Burn, victim, null, element, normalizedAmount,
                  Vector3.zero, source);

        /// <summary>
        /// <see cref="Apply"/> over every element at once - the shape almost every debuff wants,
        /// since a hit that strips a pilot strips all four. Returns the TOTAL petals moved.
        /// </summary>
        public static int ApplyAll(ElementalTransferForm form, IVesselStatus victim, IVesselStatus attacker,
                                   float normalizedAmountPerElement, Vector3 impactVelocity,
                                   ElementalDebuffSources source)
        {
            int total = 0;
            for (int i = 0; i < AllElements.Length; i++)
                total += Apply(form, victim, attacker, AllElements[i], normalizedAmountPerElement,
                               impactVelocity, source);
            return total;
        }
    }
}
