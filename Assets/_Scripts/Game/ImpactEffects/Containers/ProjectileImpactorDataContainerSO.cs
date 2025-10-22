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
        
        [SerializeField]
        VesselProjectileEffectSO[] projectileShipEffects;
        [SerializeField]
        ProjectilePrismEffectSO[]  projectilePrismEffects; 
        [SerializeField]
        ProjectileMineEffectSO[] projectileMineEffects;
    }
}