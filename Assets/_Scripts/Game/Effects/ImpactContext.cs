using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    public class ImpactContext
    {
        public IShipStatus ShipStatus { get; set; }
        public TrailBlockProperties TrailBlockProps { get; set; }
        public CrystalProperties CrystalProps { get; set; }
        public Vector3 ImpactPoint { get; set; }
        public Vector3 ImpactDirection { get; set; }
    }
}
