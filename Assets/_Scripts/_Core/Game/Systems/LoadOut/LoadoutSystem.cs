using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core.LoadoutFavoriting
{
    public static class LoadoutSystem
    {
        static int ActiveLoadoutIndex = 0;

        static Loadout activeLoadout;

        static List<Loadout> loadouts;

        static List<ArcadeGameLoadout> gameLoadouts;

        const string GameLoadoutsSaveFileName = "game_loadouts.data";


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

            dataAccessor = new DataAccessor(GameLoadoutsSaveFileName);
            gameLoadouts = dataAccessor.Load<List<ArcadeGameLoadout>>();

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

        public static ArcadeGameLoadout LoadGameLoadout(MiniGames mode)
        {
            for (var i = 0; i < gameLoadouts.Count; i++)
            {
                if (gameLoadouts[i].GameMode == mode)
                {
                    return gameLoadouts[i];
                }
            }

            return new ArcadeGameLoadout(mode, new Loadout(0, 0, 0, 0));
        }

        public static void SaveGameLoadOut(MiniGames mode, Loadout loadout)
        {
            var gameLoadout = new ArcadeGameLoadout(mode, loadout);
            var found = false;
            for (var i=0; i<gameLoadouts.Count; i++)
            {
                if (gameLoadouts[i].GameMode == mode)
                {
                    gameLoadouts[i] = gameLoadout;
                    found = true;
                    break;
                } 
            }

            if (!found)
                gameLoadouts.Add(gameLoadout);

            var dataAccessor = new DataAccessor(GameLoadoutsSaveFileName);
            dataAccessor.Save(gameLoadouts);
        }

        public static void SetLoadout(Loadout loadout, int index)
        {
            index = Mathf.Clamp(index, 0, loadouts.Count-1);
            loadouts[index] = loadout;
            if (index == ActiveLoadoutIndex)
                activeLoadout = loadouts[index];

            var dataAccessor = new DataAccessor("loadouts.data");
            dataAccessor.Save(loadouts);
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