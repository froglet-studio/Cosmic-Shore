using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Grizzly sniper's signature: cracks SUPER SHIELDS. The first (and only)
    /// gameplay source of super-shield removal — every other path is scripted/arcade
    /// (super shields were designed to fall to "targeted opt-in mechanics"; this is one).
    ///
    /// Runs as a PRISM impact effect: ProjectileImpactor's prism dispatch executes
    /// these regardless of shield state (DisallowImpactOnPrism only checks domain and
    /// the immunity window), so the crack lands where Damage() would be refused.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SniperCrackSuperShieldPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Projectile - Prism/SniperCrackSuperShieldPrismEffectSO")]
    public class SniperCrackSuperShieldPrismEffectSO : ProjectilePrismEffectSO
    {
        [SerializeField, Tooltip("Crack, don't shatter: the prism keeps a regular one-hit shield after the super shield falls.")]
        bool leaveRegularShield = true;

        public override void Execute(ProjectileImpactor impactor, PrismImpactor prismImpactee)
        {
            var prism = prismImpactee ? prismImpactee.Prism : null;
            if (prism == null) return;

            if (!prism.prismProperties.IsSuperShielded) return;

            prism.DeactivateShields();
            if (leaveRegularShield)
                prism.ActivateShield();
        }
    }
}
