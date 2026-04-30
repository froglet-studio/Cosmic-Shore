using System.Collections.Generic;
using CosmicShore.Data;

namespace CosmicShore.UI
{
    public interface IShipCatalog
    {
        IEnumerable<VesselClassType> GetAll();
    }
}