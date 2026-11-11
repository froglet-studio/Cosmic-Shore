namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A contact weapon's own petal take: how many petals a landed skimmer hit moves off an
    /// opposing pilot, and where they go. Implemented by the skimmer effects whose take is
    /// authored per weapon (the Squirrel's joust and the Rhino's sword through
    /// <see cref="VesselOvertakeBySkimmerEffectSO"/>, the Butterfly's dust through
    /// <see cref="VesselElementalDebuffBySkimmerEffectSO"/>).
    ///
    /// <para><b>Only <see cref="VesselCombatHitBySkimmerEffectSO"/> calls it</b>, and only for a
    /// hit it has just admitted (<see cref="CombatHitDrain.TryAdmit"/>), on the machine that owns
    /// the attacker. It finds the take among its SIBLINGS in the same skimmer container, so the
    /// container's list order does not matter and no asset has to name another. A take's own
    /// <c>Execute</c> never touches an opposing pilot's petals: a scored hit and a petal theft are
    /// one event (Garrett, 2026-10-10), and two effects that each decided for themselves had
    /// different cooldowns and different contact rules, so either could land without the
    /// other.</para>
    /// </summary>
    public interface IContactPetalTake
    {
        /// <summary>Move this weapon's petals off <paramref name="victim"/> for one landed hit.
        /// The victim's owner settles it (ward, clamp, whole petals), so this is a request, not a
        /// guarantee.</summary>
        void TakeFrom(IVesselStatus victim, IVesselStatus attacker, VesselImpactor impactor,
                      SkimmerImpactor impactee);
    }

    /// <summary>
    /// A contact weapon's own rule for WHETHER a contact counts at all, asked by
    /// <see cref="VesselCombatHitBySkimmerEffectSO"/> of every sibling in its container before it admits, scores or takes
    /// anything. The Squirrel's joust says no below its minimum joust speed (<see cref="JoustSpeedConfigSO"/>, the
    /// Squirrel's CHARGE since 2026-11-11), so a slow contact is neither scored nor robbed. The reporter asset is shared
    /// by five hulls (VesselCombatHitByJoust), so the rule lives on the hull's own take, not on the reporter.
    /// </summary>
    public interface IContactAdmission
    {
        /// <summary>False when this contact by <paramref name="attacker"/> on <paramref name="victim"/> must not
        /// count. Asked on the machine that decides the hit.</summary>
        bool AdmitsContact(IVesselStatus attacker, IVesselStatus victim);
    }

    /// <summary>
    /// A contact take that sets its OWN pace and its OWN price: the Tiger Moth's shred (one petal off
    /// one random element, every <see cref="SameVictimCooldownSeconds"/>, which its Charge shortens).
    /// The reporter then uses this window instead of its own <c>sameVictimCooldownSeconds</c>, silences
    /// the victim for <see cref="PetalsPerTake"/> petals instead of the hit class's points, and scores
    /// one hit of its class per <see cref="CombatHitDrain.PetalsFor"/> petals, so score and silence
    /// both stay proportional to the petals taken (Garrett, 2026-10-31: "score and silence should be
    /// proportional to petals, but this is one petal not 4").
    /// </summary>
    public interface IPacedContactPetalTake : IContactPetalTake
    {
        /// <summary>Seconds between takes on one victim by <paramref name="attacker"/>. Read at the
        /// attacker's replicated level, on the machine that decides the hit.</summary>
        float SameVictimCooldownSeconds(IVesselStatus attacker);

        /// <summary>Petals one take moves: what it silences for and what its score is priced on.</summary>
        float PetalsPerTake { get; }

        /// <summary>False when <paramref name="victim"/> holds nothing this take could move. The hit is
        /// then not admitted at all: no petal, no silence, no score.</summary>
        bool CanTakeFrom(IVesselStatus victim);
    }
}
