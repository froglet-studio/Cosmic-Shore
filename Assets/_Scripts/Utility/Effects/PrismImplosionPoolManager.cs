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
        public PrismImplosion Spawn(Vector3 spawnPosition, Quaternion rotation)
        {
            var implosion = Get_(spawnPosition, rotation);
            implosion.OnFinished += OnFinished; // auto return when done
            return implosion;
        }

        void OnFinished(PrismImplosion implosion)
        {
            implosion.OnFinished -= OnFinished;
            Release_(implosion);
        }
    }
}