using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A round striking a prism destroys it. The generic DIRECT-fire path: the Sparrow's
    /// full-auto bullets and its turret-stance prism rounds both run through this asset.
    ///
    /// <para>A missile does NOT — the skyburst carries its own
    /// <see cref="SkyBurstProjectileDamagePrismEffectSO"/>, which is what lets the two be told
    /// apart downstream. That distinction is load-bearing for exactly one reader: the Sparrow's
    /// tank refills from what its GUNS destroy and from nothing its rockets do
    /// (<c>VesselRearmOnPrismDestruction</c>), so <see cref="countsAsGunfire"/> travels with the
    /// kill on the destroyed channel.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "ProjectileDamagePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Projectile - Prism/ProjectileDamagePrismEffectSO")]
    public class ProjectileDamagePrismEffectSO : ProjectilePrismEffectSO
    {
        [SerializeField] float inertia = 1f;   // global scalar you can tune per effect
        [SerializeField] private Vector3 overrideCourse;
        [SerializeField] private float overrideSpeed;

        [Tooltip("Mark the kill as DIRECT GUNFIRE on the destroyed channel. On by default - " +
                 "this asset IS the direct-fire damage path - so the shipped assets, which " +
                 "author no key for it, keep that meaning. It affects no damage, only what " +
                 "PrismStats.DestroyedByGunfire says, which is what a reload-by-destroying-mass " +
                 "weapon reads. Turn it off for a round that damages prisms without being a gun.")]
        [SerializeField] private bool countsAsGunfire = true;

        public override void Execute(ProjectileImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Projectile.VesselStatus;
            PrismEffectHelper.Damage(status, prismImpactee, inertia, impactor.Projectile.Velocity,
                                     byGunfire: countsAsGunfire);
        }
    }
}
