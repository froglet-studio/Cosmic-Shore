using System.Collections.Generic;
using CosmicShore.Models.Enums;

namespace CosmicShore.Game.UI
{
    public interface IShipCatalog
    {
        IEnumerable<VesselClassType> GetAll();
    }
}