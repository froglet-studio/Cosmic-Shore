using CosmicShore.Utility.PoolsAndBuffers;
using Unity.Cinemachine;
using UnityEngine;

namespace CosmicShore.Utility.Effects
{
    /// <summary>
    /// Pool manager for PrismImplosion VFX.
    /// </summary>
    public class PrismImplosionPoolManager : GenericPoolManager<PrismImplosion>
    {
        private const int MinPrewarm = 64;

        protected override void Awake()
        {
            base.Awake();
            EnsureBuffer(MinPrewarm);
        }

        public override PrismImplosion Get(Vector3 spawnPosition, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
        {
            var implosion = Get_(spawnPosition, rotation, parent, worldPositionStays);
            implosion.OnReturnToPool += Release; // auto return when done
            return implosion;
        }

        public override void Release(PrismImplosion instance)
        {
            instance.OnReturnToPool -= Release;
            Release_(instance);
        }
    }
}