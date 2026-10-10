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
}
