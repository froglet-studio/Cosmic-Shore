using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ExplosionImpactorDataContainer",
        menuName = "ScriptableObjects/Impact Effects/Explosion - Container/ExplosionImpactorDataContainerSO")]
    public class ExplosionImpactorDataContainerSO : ScriptableObject
    {
        public VesselExplosionEffectSO[] vesselExplosionEffects;
        
        public ExplosionPrismEffectSO[] explosionPrismEffects;
    }
}