using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Child class controlling prism spawning only for Sparrow
    /// </summary>
    public class SparrowPrismController : VesselPrismController
    {
        [Space(5)]
        [Header("Sparrow Specific")]
        
        [SerializeField]
        float nonBoostingScaleMultiplier = 2f;
        
        [SerializeField]
        float nonBoostingHalfGapMultiplier = 3f;
        
        [SerializeField]
        float nonBoostSpawnDelayMultiplier = 2f;
        
        protected override Vector3 ApplyBoostScale(Vector3 scale)
        {
            // If boosting → normal
            if (vesselStatus is { IsBoosting: true })
                return scale;

            // If NOT boosting → double size
            return scale * nonBoostingScaleMultiplier;
        }
        
        protected override float ApplyBoostGap(float halfGap)
        {
            if (vesselStatus is { IsBoosting: true })
                return halfGap;

            // Double the gap (so left prism moves more left, right prism more right)
            return halfGap * nonBoostingHalfGapMultiplier;
        }
        
        protected override float ApplyBoostSpawnDelay(float delay)
        {
            // If boosting → keep normal spawn frequency
            if (vesselStatus is { IsBoosting: true })
                return delay;

            // Not boosting → increase time between spawns (double, or whatever you want)
            return delay * nonBoostSpawnDelayMultiplier;
        }
    }
}