using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.App.Systems.Favorites
{
    public static class FavoriteSystem
    { 
        static List<MiniGames> FavoriteGames;
        static bool Initialized = false;

        const string GameFavoritesSaveFileName = "game_favorites.data";

        public static void Init()
        {
            FavoriteGames = DataAccessor.Load<List<MiniGames>>(GameFavoritesSaveFileName);
            if (FavoriteGames == null )
            {
                FavoriteGames = new List<MiniGames>();
                DataAccessor.Save(GameFavoritesSaveFileName, FavoriteGames);
            }
        }

        public static bool IsFavorited(MiniGames game)
        {
            if (!Initialized)
                Init();

            return FavoriteGames.Contains(game);
        }

        public static void ToggleFavorite(MiniGames game)
        {
            if (!Initialized)
                Init();

            if (FavoriteGames.Contains(game))
            {
                FavoriteGames.Remove(game);
            }
            else
            {
                FavoriteGames.Add(game);
            }

            DataAccessor.Save(GameFavoritesSaveFileName, FavoriteGames);
        }
    }
}