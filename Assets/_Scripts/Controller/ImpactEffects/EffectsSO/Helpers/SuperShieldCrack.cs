using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// THE super-shield crack: the one code path by which a pilot's force strips a super shield
    /// off a prism. Super shields were designed to fall only to "targeted opt-in mechanics", and
    /// there are exactly two: the Grizzly sniper's round (<see cref="SniperCrackSuperShieldPrismEffectSO"/>)
    /// and the payload (<see cref="PayloadCrackPrismEffectSO"/>, Docs/Pilots/ARTIFACTS.md §2). Both
    /// call here, so the rules a crack obeys are written once:
    /// <list type="bullet">
    /// <item>it never touches the attacker's own domain's mass (the projectile route's own domain
    /// gate, <c>DisallowImpactOnPrism</c>, says the same for the sniper);</item>
    /// <item>a SILENCED attacker cracks nothing: the hit deflects (<see cref="Prism.DeflectsSilenced"/>,
    /// Docs/ElementalAbilitySystem/SILENCE.md);</item>
    /// <item>it cracks, it does not shatter: with <c>leaveRegularShield</c> the prism keeps a
    /// one-hit shield after its super shield falls.</item>
    /// </list>
    /// </summary>
    public static class SuperShieldCrack
    {
        /// <summary>What a crack did to one prism.</summary>
        public enum Outcome
        {
            Untouched = 0,
            Deflected = 1,
            CrackedSuperShield = 2,
            CrackedShield = 3,
        }

        /// <param name="spareDomain">The attacker's own domain, whose mass is never cracked. Null
        /// where the caller's route has already refused own-domain mass (the sniper's projectile).</param>
        /// <param name="alsoShielded">Also strip an ordinary (one-hit) shield. The sniper's round
        /// does not; the payload does.</param>
        /// <param name="leaveRegularShield">A cracked SUPER shield leaves a one-hit shield behind.</param>
        public static Outcome TryCrack(Prism prism, string attackerName, Domains? spareDomain,
            bool alsoShielded, bool leaveRegularShield)
        {
            if (prism == null || prism.destroyed || prism.prismProperties == null) return Outcome.Untouched;

            bool super = prism.prismProperties.IsSuperShielded;
            bool shielded = !super && prism.prismProperties.IsShielded;
            if (!super && !(alsoShielded && shielded)) return Outcome.Untouched;

            // Own mass is never the target (the sniper's projectile is refused on it before its
            // prism effects run; the payload's radius has no such gate, so it is stated here).
            if (spareDomain.HasValue && prism.Domain == spareDomain.Value) return Outcome.Untouched;

            if (attackerName != null && prism.DeflectsSilenced(attackerName, 0f)) return Outcome.Deflected;

            prism.DeactivateShields();
            if (super)
            {
                if (leaveRegularShield) prism.ActivateShield();
                return Outcome.CrackedSuperShield;
            }
            return Outcome.CrackedShield;
        }
    }
}
