// PORT (Deviation #8 pattern): extracted verbatim from
// Assets/_Scripts/Controller/Vessel/R_VesselActionHandler.cs — the host class ports
// at VESSEL_LAYER V17; ShipHelper (V9) needs these mapping types now. When
// R_VesselActionHandler ports, it uses these definitions instead of redeclaring.
using System;
using System.Collections.Generic;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    [Serializable]
    public struct InputEventShipActionMapping
    {
        public InputEvents InputEvent;
        public List<ShipActionSO> ShipActions;
    }

    [Serializable]
    public struct ResourceEventShipActionMapping
    {
        public ResourceEvents ResourceEvent;
        public List<ShipActionSO> ClassActions;
    }
}
