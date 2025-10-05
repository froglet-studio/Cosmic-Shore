using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Object pool manager for prism explosion effects.
    /// </summary>
    public class PrismExplosionPoolManager : GenericPoolManager<PrismExplosion>
    {
        public override PrismExplosion Get(Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
        {
            var explosion = Get_(position, rotation, parent, worldPositionStays);
            explosion.OnFinished = Release_;
            return explosion;
        }
        
        public override void Release(PrismExplosion instance)
        {
      
        }
    }
}