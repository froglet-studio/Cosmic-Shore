using CosmicShore.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Pool manager for PrismImplosion VFX.
    /// </summary>
    public class PrismImplosionPoolManager : GenericPoolManager<PrismImplosion>
    {
        public override PrismImplosion Get(Vector3 spawnPosition, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
        {
            var implosion = Get_(spawnPosition, rotation, parent, worldPositionStays);
            implosion.OnFinished += OnFinished; // auto return when done
            return implosion;
        }

        public override void Release(PrismImplosion instance)
        {
            throw new System.NotImplementedException();
        }

        void OnFinished(PrismImplosion implosion)
        {
            implosion.OnFinished -= OnFinished;
            Release_(implosion);
        }
    }
}