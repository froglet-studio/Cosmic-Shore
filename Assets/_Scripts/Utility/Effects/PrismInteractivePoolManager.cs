using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    public class PrismInteractivePoolManager : GenericPoolManager<Prism>
    {
        public override Prism Get(Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
        {
            var instance = Get_(position, rotation, parent, worldPositionStays);
            instance.OnReturnToPool += Release;
            return instance;
        }

        public override void Release(Prism instance)
        {
            instance.OnReturnToPool -= Release;
            Release_(instance);
        }
    }
}