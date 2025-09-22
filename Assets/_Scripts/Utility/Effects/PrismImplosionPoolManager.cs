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
        public PrismImplosion Spawn(Vector3 spawnPosition, Quaternion rotation, Transform targetTransform)
        {
            var implosion = Get_(spawnPosition, rotation);
            implosion.OnFinished = Release_; // auto return when done
            implosion.StartImplosion(targetTransform);
            return implosion;
        }
    }
}