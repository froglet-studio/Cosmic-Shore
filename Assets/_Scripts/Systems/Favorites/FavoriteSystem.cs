using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.App.Systems.Favorites
{
    public static class FavoriteSystem
    { 
        static List<GameModes> FavoriteGames;
        static bool Initialized = false;

        const string GameFavoritesSaveFileName = "game_favorites.data";

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