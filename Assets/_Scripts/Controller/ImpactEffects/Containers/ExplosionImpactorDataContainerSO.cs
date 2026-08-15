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

        /// <summary>
        /// What this blast does to a CRYSTAL it engulfs. Empty on every blast in the fleet except
        /// the Scarab's cavitation punch, which forges a ball out of an omni crystal — the point
        /// of authoring it per blast rather than on the crystal is that a Dolphin cone hitting
        /// the same crystal must not start minting Astro League balls.
        /// </summary>
        public ExplosionCrystalEffectSO[] explosionCrystalEffects;
    }
}