using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Core
{
    public static class LoadoutSystem
    {
        static int ActiveLoadoutIndex = 0;

        static Loadout activeLoadout;

        static List<Loadout> loadouts;

        static List<ArcadeGameLoadout> gameLoadouts;

        static bool _initialized;

        /// <summary>
        /// Loadout configurations automatically saved as the last game play configuration
        /// </summary>
        const string GameLoadoutsSaveFileName = "game_loadouts.data";
        /// <summary>
        /// Loadout configurations explicitly created by the player
        /// </summary>
        const string PlayerLoadoutsSaveFileName = "loadouts.data";

        static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            gameLoadouts = DataAccessor.Load<List<ArcadeGameLoadout>>(GameLoadoutsSaveFileName);
            loadouts = DataAccessor.Load<List<Loadout>>(PlayerLoadoutsSaveFileName);

            // Save file doesn't exist yet, let's make it
            if (loadouts == null || loadouts.Count == 0)
            {
                loadouts = new List<Loadout>()
                {
                    new() { Intensity=1, PlayerCount=1, GameMode= GameModes.BlockBandit, VesselType= VesselClassType.Manta},
                    new() { Intensity=0, PlayerCount=0, GameMode= GameModes.Random, VesselType= VesselClassType.Random},
                    new() { Intensity=0, PlayerCount=0, GameMode= GameModes.Random, VesselType= VesselClassType.Random},
                    new() { Intensity=0, PlayerCount=0, GameMode= GameModes.Random, VesselType= VesselClassType.Random},
                };
                DataAccessor.Save(PlayerLoadoutsSaveFileName, loadouts);
            }

            if (gameLoadouts == null)
                gameLoadouts = new List<ArcadeGameLoadout>();

            activeLoadout = loadouts[0];
        }

        public static void Init()
        {
            _initialized = false;
            EnsureInitialized();
        }

        public static bool CheckLoadoutsExist(int idx)
        {
            EnsureInitialized();
            return loadouts.Count > idx;
        }

        public static Loadout GetActiveLoadout()
        {
            EnsureInitialized();
            return activeLoadout;
        }

        public static Loadout GetLoadout(int idx)
        {
            EnsureInitialized();
            return loadouts[idx];
        }

        public static List<Loadout> GetFullListOfLoadouts()
        {
            EnsureInitialized();
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

        public static ArcadeGameLoadout LoadGameLoadout(GameModes mode, bool isMultiplayer)
        {
            EnsureInitialized();

            for (var i = 0; i < gameLoadouts.Count; i++)
            {
                if (gameLoadouts[i].GameMode == mode)
                {
                    return gameLoadouts[i];
                }
            }

            return new ArcadeGameLoadout(mode, new Loadout(0, 0, 0, 0, isMultiplayer));
        }

        public static void SaveGameLoadOut(GameModes mode, Loadout loadout)
        {
            EnsureInitialized();

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
            EnsureInitialized();
            index = Mathf.Clamp(index, 0, loadouts.Count-1);
            loadouts[index] = loadout;
            if (index == ActiveLoadoutIndex)
                activeLoadout = loadouts[index];

            DataAccessor.Save(PlayerLoadoutsSaveFileName, loadouts);
        }

        public static void SetActiveLoadoutIndex(int index)
        {
            EnsureInitialized();
            CSDebug.Log("Loadout Index changed to " + index);

            index = Mathf.Clamp(index, 0, loadouts.Count-1);
            ActiveLoadoutIndex = index;
            activeLoadout = loadouts[index];
        }
    }
}
