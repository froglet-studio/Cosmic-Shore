using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ProjectileStealPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Projectile/ProjectileStealPrismEffectSO")]
    public class ProjectileStealPrismEffectSO : StealPrismEffectBaseSO<ProjectileImpactor>
    {
        protected override IShipStatus GetShipStatus(ProjectileImpactor impactor)
            => impactor.Projectile?.ShipStatus;
    }
}