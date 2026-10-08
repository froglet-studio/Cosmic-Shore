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
    /// dispatches that contact. <see cref="Apply"/> and <see cref="ApplyAll"/> settle against
    /// whatever copy of the victim THIS machine holds, and that copy is only the truth on the
    /// machine that OWNS the victim: element levels are owner state (<c>NetElementLevels</c> is
    /// owner-write), so a take from a remote copy changes nothing anyone else reads. A human's
    /// press is round-tripped through the server (<c>R_VesselActionHandler</c>), so every peer
    /// replays the round, but each replay flies from that peer's own lagged picture of the shooter
    /// and can hit or miss on its own. An AI's guns run on the SERVER ONLY (<c>AIPilot</c> starts
    /// its abilities locally), so a client victim never replays an AI's shot at all.</para>
    ///
    /// <para><b>So a networked EJECT goes through <see cref="ApplyAllAuthoritative"/>.</b> The
    /// machine that owns the ATTACKER decides the hit. That is the same machine whose hit
    /// <c>StatsManager.CombatHitLanded</c> scores. It hands the take to the victim's owner through
    /// <see cref="IElementalLossRelay"/> (<c>NetworkVesselImpactor</c>). The owner settles it with
    /// <see cref="SettleEjectAll"/>, which is still <c>AccrueElementalLoss</c>, so the ward and the
    /// clamp are the owner's. It then mints the crystals and publishes the settled count, so every
    /// other peer mints the same number (<see cref="EjectSettled"/>). Every other peer's replay of
    /// the contact moves nothing. Offline nothing changes, because the route is
    /// <see cref="ElementalTransferRoute.Local"/>. The crystals stay per-peer local objects, like
    /// the food web's crystals - see <see cref="ElementalCrystalEjector"/>.</para>
    /// </summary>
    public static class ElementalTransfer
    {
        static readonly Element[] AllElements =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

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
        /// Which machine settles a transfer, from four facts about this machine. Pure, so it is
        /// tested directly. See the type doc for why the ATTACKER's owner decides.
        /// </summary>
        /// <param name="form">Only an <see cref="ElementalTransferForm.Eject"/> is relayed. A steal
        /// would also have to pay the attacker on the attacker's owner, and a burn has no attacker
        /// to own it, so both settle locally as before.</param>
        /// <param name="attackerNetworked">The attacker's hull is a spawned network object.</param>
        /// <param name="attackerOwnedHere">This machine owns the attacker: a human's own client, or
        /// the server for an AI.</param>
        /// <param name="victimNetworked">The victim's hull is a spawned network object with a relay.</param>
        public static ElementalTransferRoute RouteFor(ElementalTransferForm form, bool attackerNetworked,
                                                      bool attackerOwnedHere, bool victimNetworked)
        {
            if (form != ElementalTransferForm.Eject) return ElementalTransferRoute.Local;
            if (!attackerNetworked || !victimNetworked) return ElementalTransferRoute.Local;
            return attackerOwnedHere ? ElementalTransferRoute.Relay : ElementalTransferRoute.NotOurs;
        }

        /// <summary>
        /// <see cref="ApplyAll"/> for a hit that other machines also replay. It is settled once, on
        /// the victim's owner, as the attacker's owner saw it (see the type doc). Offline, or for a
        /// hull with no relay, this IS <see cref="ApplyAll"/>.
        /// </summary>
        /// <returns>The petals settled on THIS machine. A take handed to a remote owner returns 0
        /// here, because how many came loose is known only when that owner settles it.</returns>
        public static int ApplyAllAuthoritative(ElementalTransferForm form, IVesselStatus victim,
                                                IVesselStatus attacker, float normalizedAmountPerElement,
                                                Vector3 impactVelocity, ElementalDebuffSources source)
        {
            var victimRelay = RelayOf(victim);
            var attackerRelay = RelayOf(attacker);

            var route = RouteFor(form,
                                 attackerRelay != null && attackerRelay.IsNetworked,
                                 attackerRelay != null && attackerRelay.IsOwnedHere,
                                 victimRelay != null && victimRelay.IsNetworked);

            switch (route)
            {
                case ElementalTransferRoute.NotOurs:
                    // This machine's replay of somebody else's shot. The shooter's owner decides
                    // whether it landed; settling here too would take twice, or take on a replay
                    // the scorer saw miss.
                    return 0;

                case ElementalTransferRoute.Relay:
                    if (normalizedAmountPerElement <= 0f) return 0;
                    return victimRelay.RelayEjectToOwner(normalizedAmountPerElement, impactVelocity, source);

                default:
                    return ApplyAll(form, victim, attacker, normalizedAmountPerElement, impactVelocity, source);
            }
        }

        /// <summary>
        /// The OWNER's half of a relayed eject. Takes <paramref name="normalizedAmountPerElement"/>
        /// off each of the four elements through <c>AccrueElementalLoss</c> (ward, clamp, whole
        /// petals) and reports what came loose, packed by <see cref="PackPetals"/>. It mints
        /// nothing: the caller ejects with <see cref="EjectSettled"/> and publishes the same packed
        /// value to every other peer.
        /// </summary>
        public static uint SettleEjectAll(IVesselStatus victim, float normalizedAmountPerElement,
                                          ElementalDebuffSources source)
        {
            var resources = victim?.ResourceSystem;
            if (resources == null || normalizedAmountPerElement <= 0f) return 0u;

            uint packed = 0u;
            for (int i = 0; i < AllElements.Length; i++)
            {
                int petals = resources.AccrueElementalLoss(AllElements[i], normalizedAmountPerElement, source);
                packed = WithPetals(packed, AllElements[i], petals);
            }
            return packed;
        }

        /// <summary>Mints the crystals a settled take (<see cref="SettleEjectAll"/>) knocked loose,
        /// on whichever machine calls it. Levels are NOT touched: on the owner they already moved,
        /// and on every other peer they are the owner's to publish.</summary>
        public static void EjectSettled(IVesselStatus victim, uint packedPetals, Vector3 impactVelocity)
        {
            if (victim == null || packedPetals == 0u) return;
            for (int i = 0; i < AllElements.Length; i++)
            {
                int petals = PetalsIn(packedPetals, AllElements[i]);
                if (petals > 0) ElementalCrystalEjector.Eject(victim, AllElements[i], petals, impactVelocity);
            }
        }

        /// <summary>Four per-element petal counts in one <c>uint</c>, eight bits each (Charge in the
        /// lowest byte), each clamped to 0..255. One hit settles a few petals per element at most,
        /// and an element holds fifteen at most, so play never reaches the clamp.</summary>
        public static uint PackPetals(int charge, int mass, int space, int time)
        {
            uint packed = 0u;
            packed = WithPetals(packed, Element.Charge, charge);
            packed = WithPetals(packed, Element.Mass, mass);
            packed = WithPetals(packed, Element.Space, space);
            packed = WithPetals(packed, Element.Time, time);
            return packed;
        }

        /// <summary>The petals of <paramref name="element"/> in a <see cref="PackPetals"/> value;
        /// 0 for an element outside the four.</summary>
        public static int PetalsIn(uint packedPetals, Element element)
        {
            int shift = ByteShift(element);
            return shift < 0 ? 0 : (int)((packedPetals >> shift) & 0xFFu);
        }

        /// <summary>All four counts in a <see cref="PackPetals"/> value, summed.</summary>
        public static int TotalPetals(uint packedPetals)
        {
            int total = 0;
            for (int i = 0; i < AllElements.Length; i++) total += PetalsIn(packedPetals, AllElements[i]);
            return total;
        }

        static uint WithPetals(uint packed, Element element, int petals)
        {
            int shift = ByteShift(element);
            if (shift < 0) return packed;
            uint value = (uint)(petals < 0 ? 0 : petals > 255 ? 255 : petals);
            return (packed & ~(0xFFu << shift)) | (value << shift);
        }

        static int ByteShift(Element element)
        {
            int i = (int)element - 1;   // Charge=1 -> byte 0 ... Time=4 -> byte 3
            return i is >= 0 and < 4 ? i * 8 : -1;
        }

        /// <summary>The relay on a vessel's hull, or null for a hull without one (a toybox mini
        /// hull, a test double). A null relay routes <see cref="ElementalTransferRoute.Local"/>.</summary>
        static IElementalLossRelay RelayOf(IVesselStatus status)
        {
            // NOT status.Transform: see ElementalCrystalEjector. That property throws rather than
            // answering null on a status whose vessel is gone.
            var hull = status?.Vessel?.Transform;
            return hull && hull.TryGetComponent(out IElementalLossRelay relay) ? relay : null;
        }

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

    /// <summary>Which machine settles a transfer. See <see cref="ElementalTransfer.RouteFor"/>.</summary>
    public enum ElementalTransferRoute
    {
        /// <summary>Not a networked contact (offline, or a hull with no relay): settle here, as always.</summary>
        Local = 0,

        /// <summary>Networked, and this machine does not own the attacker: its replay of the
        /// contact moves nothing.</summary>
        NotOurs = 1,

        /// <summary>Networked, and this machine owns the attacker: hand the take to the victim's owner.</summary>
        Relay = 2,
    }

    /// <summary>
    /// The network half of a relayed eject, implemented by <c>NetworkVesselImpactor</c> on every
    /// vessel hull. It is declared here, rather than the transfer naming that class, so the transfer
    /// stays free of Netcode types and keeps compiling in <c>Tools/Build/elemental_transfer_harness</c>.
    /// </summary>
    public interface IElementalLossRelay
    {
        /// <summary>This hull is a spawned network object.</summary>
        bool IsNetworked { get; }

        /// <summary>This machine owns this hull.</summary>
        bool IsOwnedHere { get; }

        /// <summary>
        /// Settles an eject of <paramref name="normalizedAmountPerElement"/> per element on this
        /// hull's owner (here if this machine is the owner, otherwise by RPC through the server)
        /// and has every peer mint the settled crystals. Returns the petals settled HERE, which is
        /// 0 when the take was sent to a remote owner.
        /// </summary>
        int RelayEjectToOwner(float normalizedAmountPerElement, Vector3 impactVelocity,
                              ElementalDebuffSources source);
    }
}
