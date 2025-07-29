using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// This interface must be implemented by all impact effects.
    /// This interface is used to define the contract for 
    /// impact effects that can be applied to ships, crystals, trail blocks, and other game objects.
    /// </summary>
    public interface IImpactEffect {}

    public interface IBaseImpactEffect : IImpactEffect
    {
        void Execute(ImpactEffectData data);
    }

    public interface ICrystalImpactEffect : IImpactEffect
    {
        void Execute(ImpactEffectData data, CrystalProperties crystalProperties);
    }

    public interface ITrailBlockImpactEffect : IImpactEffect
    {
        void Execute(ImpactEffectData data, TrailBlockProperties trailBlockProperties);
    }

    public class ImpactEffectData
    {
        public IShipStatus ThisShipStatus { get; }
        public IShipStatus ImpactedShipStatus { get; }
        public Vector3 ImpactVector { get; }

        public ImpactEffectData(IShipStatus thisShipStatus, IShipStatus impactedShipStatus, Vector3 impactVector)
        {
            ThisShipStatus = thisShipStatus;
            ImpactedShipStatus = impactedShipStatus;
            ImpactVector = impactVector;
        }
    }
}
