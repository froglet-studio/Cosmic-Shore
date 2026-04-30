using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "ExplosionImpactorDataContainer",
        menuName = "ScriptableObjects/Impact Effects/Explosion - Container/ExplosionImpactorDataContainerSO")]
    public class ExplosionImpactorDataContainerSO : ScriptableObject
    {
        public VesselExplosionEffectSO[] vesselExplosionEffects;
        
        public ExplosionPrismEffectSO[] explosionPrismEffects;
    }
}