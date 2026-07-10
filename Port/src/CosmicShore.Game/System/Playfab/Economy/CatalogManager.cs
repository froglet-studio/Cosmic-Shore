// PORT Deviation — type-preserving SHELL of CatalogManager + Inventory (original:
// Assets/_Scripts/System/Playfab/Economy/CatalogManager.cs + Inventory.cs — the
// PlayFab economy layer, legacy/inert upstream per CLAUDE.md). Landed in Arc F 2b-ii
// because ArcadeExploreView consumes `OnLoadInventory` + `Inventory.ContainsGame`
// behind RespectInventoryForGameSelection (default FALSE — production filtering off),
// so an empty inventory shell reproduces the shipping default exactly. The economy
// port, if ever needed, follows the deprecated-PlayFab track.
using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    /// <summary>Shell of the PlayFab item inventory — the name-lookup surface only.</summary>
    public class Inventory
    {
        public class Item
        {
            public string Name;
        }

        readonly List<Item> games = new();
        readonly List<Item> shipClasses = new();

        public bool ContainsShipClass(string shipName)
        {
            return shipClasses.Where(item => item.Name == shipName).Count() > 0;
        }

        public bool ContainsGame(string gameName)
        {
            return games.Where(item => item.Name == gameName).Count() > 0;
        }
    }

    public class CatalogManager : SingletonPersistent<CatalogManager>
    {
        public static Inventory Inventory { get; private set; } = new();

        public static event Action OnLoadCatalogSuccess;
        public static event Action OnLoadInventory;
        public static event Action OnInventoryChange;

        protected static void RaiseLoadCatalogSuccess() => OnLoadCatalogSuccess?.Invoke();
        protected static void RaiseLoadInventory() => OnLoadInventory?.Invoke();
        protected static void RaiseInventoryChange() => OnInventoryChange?.Invoke();
    }
}
