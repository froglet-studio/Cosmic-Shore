using System;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    public static class FavoriteSystem
    {
        static List<GameModes> FavoriteGames;
        static bool Initialized = false;

        // Re-read the disk store each play session, and drop AnalyticsServiceFacade's
        // un-removable constructor-lambda subscription with it.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            FavoriteGames = null;
            Initialized = false;
            OnFavoriteChanged = null;
        }

        const string GameFavoritesSaveFileName = "game_favorites.data";

        /// <summary>Raised after a favorite toggle. Args: (game, isNowFavorited).
        /// AnalyticsServiceFacade subscribes - keeps this static utility decoupled from UI.</summary>
        public static event Action<GameModes, bool> OnFavoriteChanged;

        public static void Init()
        {
            FavoriteGames = DataAccessor.Load<List<GameModes>>(GameFavoritesSaveFileName);
            if (FavoriteGames == null )
            {
                FavoriteGames = new List<GameModes>();
                DataAccessor.Save(GameFavoritesSaveFileName, FavoriteGames);
            }
            Initialized = true;
        }

        public static bool IsFavorited(GameModes game)
        {
            if (!Initialized)
                Init();

            return FavoriteGames.Contains(game);
        }

        public static void ToggleFavorite(GameModes game)
        {
            if (!Initialized)
                Init();

            bool favorited;
            if (FavoriteGames.Contains(game))
            {
                FavoriteGames.Remove(game);
                favorited = false;
            }
            else
            {
                FavoriteGames.Add(game);
                favorited = true;
            }

            DataAccessor.Save(GameFavoritesSaveFileName, FavoriteGames);
            OnFavoriteChanged?.Invoke(game, favorited);
        }
    }
}