using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ProjectileImpactorDataContainer",
        menuName = "ScriptableObjects/Impact Effects/Mine - Container/ProjectileImpactorDataContainerSO")]
    public class ProjectileImpactorDataContainerSO : ScriptableObject
    {
        public VesselProjectileEffectSO[] ProjectileShipEffects => projectileShipEffects;

        public ProjectilePrismEffectSO[] ProjectilePrismEffects => projectilePrismEffects;

        public ProjectileMineEffectSO[] ProjectileMineEffect => projectileMineEffects;
        public ProjectileEndEffectSO[] ProjectileEndEffects => projectileEndEffects;
        
        [SerializeField]
        VesselProjectileEffectSO[] projectileShipEffects;
        [SerializeField]
        ProjectilePrismEffectSO[]  projectilePrismEffects; 
        [SerializeField]
        ProjectileMineEffectSO[] projectileMineEffects;
        [SerializeField]
        ProjectileEndEffectSO[]  projectileEndEffects;
    }
}