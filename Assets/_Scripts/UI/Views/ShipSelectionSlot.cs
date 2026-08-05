using CosmicShore.Data;
using System;

namespace CosmicShore.UI
{
    [Serializable]
    public struct ShipSelectionSlot
    {
        public VesselClassType vesselType;
        public ShipSelectionItemView itemView;
    }
}
