using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.App.Systems.Loadout
{
    public static class LoadoutSystem
    {
        static int ActiveLoadoutIndex = 0;

        static Loadout activeLoadout;

        static List<Loadout> loadouts;

        static List<ArcadeGameLoadout> gameLoadouts;

        /// <summary>
        /// Loadout configurations automatically saved as the last game play configuration
        /// </summary>
        const string GameLoadoutsSaveFileName = "game_loadouts.data";
        /// <summary>
        /// Loadout configurations explicitly created by the player
        /// </summary>
        const string PlayerLoadoutsSaveFileName = "loadouts.data";


        public static void Init()
        {
            gameLoadouts = DataAccessor.Load<List<ArcadeGameLoadout>>(GameLoadoutsSaveFileName);
            loadouts = DataAccessor.Load<List<Loadout>>(PlayerLoadoutsSaveFileName);

            // Save file doesn't exist yet, let's make it
            if (loadouts.Count == 0)
            {
                loadouts = new List<Loadout>()
                {
                    new() { Intensity=1, PlayerCount=1, GameMode= GameModes.BlockBandit, ShipType= ShipTypes.Manta},
                    new() { Intensity=0, PlayerCount=0, GameMode= GameModes.Random, ShipType= ShipTypes.Random},
                    new() { Intensity=0, PlayerCount=0, GameMode= GameModes.Random, ShipType= ShipTypes.Random},
                    new() { Intensity=0, PlayerCount=0, GameMode= GameModes.Random, ShipType= ShipTypes.Random},
                };
                DataAccessor.Save(PlayerLoadoutsSaveFileName, loadouts);
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

        public static ArcadeGameLoadout LoadGameLoadout(GameModes mode)
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

        public static void SaveGameLoadOut(GameModes mode, Loadout loadout)
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

            DataAccessor.Save(GameLoadoutsSaveFileName, gameLoadouts);
        }

        public static void SetLoadout(Loadout loadout, int index)
        {
            index = Mathf.Clamp(index, 0, loadouts.Count-1);
            loadouts[index] = loadout;
            if (index == ActiveLoadoutIndex)
                activeLoadout = loadouts[index];

            DataAccessor.Save(PlayerLoadoutsSaveFileName, loadouts);
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