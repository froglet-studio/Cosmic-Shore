using System;
using System.Collections.Generic;

namespace CosmicShore
{
    [Serializable]
    public class Inventory
    {
        public const string GameContentType = "Game";
        public const string ShipContentType = "ShipClass";
        public const string VesselContentType = "Vessel";
        public const string ShardContentType = "VesselShard";
        public const string UpgradeContentType = "VesselUpgrade";

        public List<VirtualItem> Vessels;
        public List<VirtualItem> Ships;
        public List<VirtualItem> Games;
        public List<VirtualItem> Shards;
    }
}