using System.Collections.Generic;

namespace CosmicShore.App.UI.Domain
{
    public interface IShipCatalog
    {
        IEnumerable<VesselClassType> GetAll();
    }
}