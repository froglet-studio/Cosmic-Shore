using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Urchin spike sticking into the mass it just struck: halts the round where it landed,
    /// leaves it standing there for a beat, then fades it out and returns it to the pool.
    ///
    /// This is the modern form of the 2023 <c>TrailBlockImpactEffects.Stop</c>, and it is the
    /// half of that effect the original got wrong. Back then Stop was a bare
    /// <c>StopCoroutine(moveCoroutine)</c>, and the coroutine it killed owned the only cleanup
    /// call the projectile had — so every spike that successfully hit something never came
    /// back, first as a leaked GameObject and later as a permanent hole in a 1,500-deep pool.
    /// <see cref="Projectile.EmbedAndRetire"/> owns the retirement explicitly for that reason.
    ///
    /// Pair it with <c>ProjectileStealPrismEffect</c> and <c>ProjectileChainFirePrismEffect</c>,
    /// in that order, to reproduce the original <c>[Stop, Steal, Fire]</c> loop. Order matters:
    /// the steal must land before the volley, so the newly-fired children see the prism as
    /// already converted and fly through it. That is the cascade's primary brake.
    /// </summary>
    [CreateAssetMenu(fileName = "ProjectileEmbedPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Projectile - Prism/ProjectileEmbedPrismEffectSO")]
    public class ProjectileEmbedPrismEffectSO : ProjectilePrismEffectSO
    {
        [Tooltip("How long the spike stands in the prism before it begins to fade. Purely a " +
                 "look: the steal and the chain volley have both already happened.")]
        [SerializeField] float dwellSeconds = 1.25f;

        [Tooltip("Fade-out duration. Continuity of existence: a spike may not pop out of " +
                 "existence, so this must stay above zero.")]
        [SerializeField] float fadeSeconds = 0.35f;

        public override void Execute(ProjectileImpactor impactor, PrismImpactor prismImpactee)
        {
            impactor.Projectile.EmbedAndRetire(dwellSeconds, fadeSeconds);
        }
    }
}
