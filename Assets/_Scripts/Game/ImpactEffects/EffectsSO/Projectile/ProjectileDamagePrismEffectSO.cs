using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ProjectileDamagePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Projectile/ProjectileDamagePrismEffectSO")]
    public class ProjectileDamagePrismEffectSO : DamagePrismEffectBase<ProjectileImpactor>
    {
        protected override IShipStatus GetAttackerStatus(ProjectileImpactor impactor)
            => impactor.Projectile?.ShipStatus;
    }
}