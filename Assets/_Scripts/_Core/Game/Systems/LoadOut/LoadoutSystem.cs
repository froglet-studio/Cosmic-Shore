using System.Collections.Generic;
using UnityEngine;

public struct Loadout
{
    public int Intensity;
    public int PlayerCount;
    public ShipTypes ShipType;
    public MiniGames GameMode;

    public Loadout(int intensity, int playerCount, ShipTypes shipType, MiniGames gameMode)
    {
        Intensity = intensity;
        PlayerCount = playerCount;
        ShipType = shipType;
        GameMode = gameMode;
    }
    public override readonly string ToString()
    {
        return Intensity + "_" + PlayerCount + "_" + ShipType + "_" + GameMode ;
    }

    public readonly bool Uninitialized()
    {
        return Intensity == 0 && PlayerCount == 0 && ShipType == ShipTypes.Random && GameMode == MiniGames.Random;
    }
}

namespace StarWriter.Core.LoadoutFavoriting
{
    public static class LoadoutSystem
    {
        static int ActiveLoadoutIndex = 0;

        static Loadout activeLoadout;

        static List<Loadout> loadouts;
 
        public static void Init()
        {
            var dataAccessor = new DataAccessor("loadouts.data");
            loadouts = dataAccessor.Load<List<Loadout>>();

            if (loadouts.Count == 0)
            {
                loadouts = new List<Loadout>()
                {
                    new Loadout() { Intensity=1, PlayerCount=1, GameMode= MiniGames.BlockBandit, ShipType= ShipTypes.Manta},
                    new Loadout() { Intensity=0, PlayerCount=0, GameMode= MiniGames.Random, ShipType= ShipTypes.Random},
                    new Loadout() { Intensity=0, PlayerCount=0, GameMode= MiniGames.Random, ShipType= ShipTypes.Random},
                    new Loadout() { Intensity=0, PlayerCount=0, GameMode= MiniGames.Random, ShipType= ShipTypes.Random},
                };
                dataAccessor.Save<List<Loadout>>(loadouts);
            }

            activeLoadout = loadouts[0];
        }
        public static bool CheckLoadoutsExist(int idx)
        {
            return loadouts.Count > idx;
        }

        public static Loadout GetActiveLoadout()
        {
            return activeLoadout;
        }

        public static Loadout GetLoadout(int idx)
        {
            return loadouts[idx];
        }

        public static List<Loadout> GetFullListOfLoadouts()
        {
            return loadouts;
        }

        public static int GetActiveLoadoutIndex()
        {
            return ActiveLoadoutIndex;
        }

        public static void SetCurrentlySelectedLoadout(Loadout loadout)
        {
            SetLoadout(loadout, ActiveLoadoutIndex);
        }

        public static void SetLoadout(Loadout loadout, int index)
        {
            index = Mathf.Clamp(index, 0, loadouts.Count-1);
            loadouts[index] = loadout;
            if (index == ActiveLoadoutIndex)
                activeLoadout = loadouts[index];

            var dataAccessor = new DataAccessor("loadouts.data");
            dataAccessor.Save<List<Loadout>>(loadouts);
        }
        
        public static void SetActiveLoadoutIndex(int index) 
        {
            Debug.Log("Loadout Index changed to " + index);

            index = Mathf.Clamp(index, 0, loadouts.Count-1);
            ActiveLoadoutIndex = index;
            activeLoadout = loadouts[index];
        }
    }
}