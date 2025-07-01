using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    public class ImpactContext
    {
        public IShipStatus ShipStatus { get; set; }
        public TrailBlockProperties TrailBlockProperties { get; set; }
        public CrystalProperties CrystalProperties { get; set; }
        public Vector3 ImpactPoint { get; set; }
        public Vector3 ImpactVector { get; set; }
        public Teams OwnTeam { get; set; }
    }
}
