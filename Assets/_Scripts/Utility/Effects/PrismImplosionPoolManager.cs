using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Pool manager for PrismImplosion VFX.
    /// </summary>
    public class PrismImplosionPoolManager : GenericPoolManager<PrismImplosion>
    {
        public PrismImplosion Spawn(Vector3 position, Quaternion rotation, Vector3 convergencePoint, float volume = 1f)
        {
            var implosion = Get(position, rotation);
            implosion.OnFinished = Release; // auto return when done
            implosion.StartImplosion(convergencePoint, volume);
            return implosion;
        }
    }
}