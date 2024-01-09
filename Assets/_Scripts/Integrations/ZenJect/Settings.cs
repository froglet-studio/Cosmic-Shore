using System;

namespace CosmicShore.Integrations.ZenJect
{
    public enum ShipStates
    {
        Move,
        Boost,
        Drift,
        Idle
    }
    
    [Serializable] // Make sure [Serializable] is included on setting wrappers to be able to show
    public class Settings
    {
        public ShipStates ShipState;
    }
}