using System.Collections.Generic;

namespace CosmicShore.App.Systems.Clout
{
    /// <summary>
    /// Clout is a measurement of you succuss using a ship/element combo in a specific game
    /// </summary>
    public struct Clout
    {
        public int MasterCloutValue { get; set; }
        public Dictionary<ShipTypes, int> shipClouts { get; set; }
    }
}