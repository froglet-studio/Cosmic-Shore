using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class BrittleStarPrismController : VesselPrismController
    {
        [Space(5)]
        [Header("BrittleStar Specific")]

        [SerializeField]
        float nonBoostingScaleMultiplier = 2f;

        [SerializeField]
        float nonBoostingHalfGapMultiplier = 3f;

        [SerializeField]
        float nonBoostSpawnDelayMultiplier = 2f;

        protected override Vector3 ApplyBoostScale(Vector3 scale)
        {
            if (vesselStatus is { IsBoosting: true })
                return scale;

            return scale * nonBoostingScaleMultiplier;
        }

        protected override float ApplyBoostGap(float halfGap)
        {
            if (vesselStatus is { IsBoosting: true })
                return halfGap;

            return halfGap * nonBoostingHalfGapMultiplier;
        }

        protected override float ApplyBoostSpawnDelay(float delay)
        {
            if (vesselStatus is { IsBoosting: true })
                return delay;

            return delay * nonBoostSpawnDelayMultiplier;
        }
    }
}
