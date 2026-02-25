using System.Collections.Generic;

namespace CosmicShore.Game.UI
{
    public interface IShipCatalog
    {
        IEnumerable<VesselClassType> GetAll();
    }
}