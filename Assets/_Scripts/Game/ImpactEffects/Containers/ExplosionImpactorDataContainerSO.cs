using UnityEngine;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects.Containers
{
    [CreateAssetMenu(fileName = "ExplosionImpactorDataContainer",
        menuName = "ScriptableObjects/Impact Effects/Explosion - Container/ExplosionImpactorDataContainerSO")]
    public class ExplosionImpactorDataContainerSO : ScriptableObject
    {
        public VesselExplosionEffectSO[] vesselExplosionEffects;
        
        public ExplosionPrismEffectSO[] explosionPrismEffects;
    }
}