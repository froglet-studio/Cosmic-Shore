using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Object pool manager for prism explosion effects.
    /// </summary>
    public class PrismExplosionPoolManager : GenericPoolManager<PrismExplosion>
    {
        public PrismExplosion Spawn(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            var explosion = Get(position, rotation);
            explosion.OnFinished = Release;
            explosion.TriggerExplosion(velocity);
            return explosion;
        }
    }
}