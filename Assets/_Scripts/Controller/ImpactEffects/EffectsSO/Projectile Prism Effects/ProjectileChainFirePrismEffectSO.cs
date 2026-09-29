using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The engine of the Urchin's chain reaction: when a spike lands, the spike's own
    /// <see cref="LoadedGun"/> sprays the next generation of spikes out of the prism it just
    /// converted, and each of those does the same. This is the modern form of the 2023
    /// <c>TrailBlockImpactEffects.Fire</c>.
    ///
    /// **The whole mechanic is emergent and stays that way.** Nothing here decides how far the
    /// cascade travels or how many prisms it takes. It propagates for exactly as long as it
    /// keeps finding enemy mass within a spike's reach, and it dies because
    /// <see cref="Projectile.DisallowImpactOnPrism"/> refuses a prism that already wears the
    /// firing domain — so the wavefront eats its own frontier. A cascade through a dense
    /// enemy trail runs; the same cascade fired into open space or into your own territory
    /// stops on its first hop, with no code path anywhere deciding that.
    ///
    /// Two authored bounds sit UNDER that emergent brake rather than replacing it:
    /// <see cref="Projectile.ChainGeneration"/> (zero is terminal) and
    /// <see cref="ChainReactionBudget"/> (volleys per frame). The original had neither, which
    /// is why "toned down urchin chain reactions" is a real commit in this repository.
    ///
    /// List this AFTER <c>ProjectileStealPrismEffect</c> in the container. Effects run in list
    /// order, and if the volley goes out before the steal lands, the children are fired at a
    /// prism that is still hostile and immediately re-convert it, wasting a whole generation
    /// on ground already taken.
    /// </summary>
    [CreateAssetMenu(fileName = "ProjectileChainFirePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Projectile - Prism/ProjectileChainFirePrismEffectSO")]
    public class ProjectileChainFirePrismEffectSO : ProjectilePrismEffectSO
    {
        [Tooltip("Skip the volley when the prism only shed a shield rather than changing " +
                 "hands. Shielded mass costs the cascade a hop for no territory, so spending " +
                 "a whole generation on it drains the chain against a shielded wall.")]
        [SerializeField] bool requireDomainChange = true;

        public override void Execute(ProjectileImpactor impactor, PrismImpactor prismImpactee)
        {
            var projectile = impactor.Projectile;
            if (!projectile) return;

            // Terminal generation. Checked before anything else so a spent spike costs nothing.
            if (projectile.ChainGeneration <= 0) return;

            // Prism.Steal is a no-op on super-shielded mass and only sheds the shield on
            // shielded mass, so in both cases the prism is still hostile and a volley fired
            // here would be spent re-hitting ground the cascade cannot take.
            if (requireDomainChange)
            {
                var prism = prismImpactee ? prismImpactee.Prism : null;
                if (!prism || prism.Domain != projectile.OwnDomain) return;
            }

            if (!projectile.TryGetComponent(out LoadedGun gun)) return;

            // Load shedding, last: a claimed slot must not be wasted on a volley that one of
            // the checks above would have refused anyway.
            if (!ChainReactionBudget.TryReserveVolley()) return;

            gun.FireGun();
        }
    }
}
