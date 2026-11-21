namespace CosmicShore.Data
{
    // Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
    // Always assign a static numeric value to your enum types.

    /// <summary>
    /// WHERE a petal goes when a vessel loses one. Every elemental debuff in the game is now a
    /// TRANSFER rather than a decay, and there are exactly three destinations - which is the
    /// whole of the elemental economy stated as an enum.
    ///
    /// <para><b>Two of these conserve and one does not, deliberately.</b>
    /// <see cref="Steal"/> and <see cref="Eject"/> move a petal from one place to another and the
    /// total in the match is unchanged; <see cref="Burn"/> destroys it. A closed economy needs a
    /// sink or every element saturates at 10 and nothing is worth fighting over, so exactly one
    /// force burns - a hostile danger prism - and everything else trades.</para>
    ///
    /// <para><b>The form is a property of the VERB, not of the hull</b>
    /// (<see cref="CosmicShore.Gameplay.ElementalTransfer.FormFor"/>): a contact hit steals
    /// because you flew into them and took it off them, and a ranged hit ejects because you
    /// knocked it loose from a distance and it fell into the arena. That keeps one rule for a
    /// fleet whose weapons keep changing, instead of eight authored answers that can disagree
    /// about what the same weapon class means.</para>
    /// </summary>
    public enum ElementalTransferForm
    {
        /// <summary>The attacker GAINS what the victim lost. Contact verbs - the Squirrel's joust,
        /// the Rhino's sword. Conserving, and the attacker is paid immediately.</summary>
        Steal = 0,

        /// <summary>The petal is knocked loose as a free-for-all crystal launched out of the
        /// victim's hull at the impact velocity. Ranged verbs - guns, rockets, blasts. Conserving,
        /// but nobody is paid until somebody flies over and collects it.</summary>
        Eject = 1,

        /// <summary>The petal is DESTROYED, with no recipient. The economy's only sink, held by a
        /// hostile danger prism and nothing else.</summary>
        Burn = 2,
    }
}
