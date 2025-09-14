using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ProjectileDamagePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Projectile - Prism/ProjectileDamagePrismEffectSO")]
    public class ProjectileDamagePrismEffectSO : ProjectilePrismEffectSO
    {
        [SerializeField] float inertia = 70f;   // global scalar you can tune per effect
        [SerializeField] private Vector3 overrideCourse;
        [SerializeField] private float overrideSpeed;
        
        public override void Execute(ProjectileImpactor impactor, PrismImpactor prismImpactee)
        {
            Debug.Log("Executed");
            var status = impactor.Projectile.ShipStatus;
            PrismEffectHelper.Damage(status, prismImpactee, inertia, overrideCourse, overrideSpeed);
        }
    }
}