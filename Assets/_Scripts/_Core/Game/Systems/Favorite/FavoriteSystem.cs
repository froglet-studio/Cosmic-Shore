using StarWriter.Core.CloutSystem;
using StarWriter.Core.HangerBuilder;
using StarWriter.Utility.Singleton;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct Favorite
{
    public int Intensity;
    public int PlayerCount;
    public ShipTypes ShipType;
    public MiniGames GameMode;

    public Favorite(int intensity, int playerCount, ShipTypes shipType, MiniGames gameMode)
    {
        Intensity = intensity;
        PlayerCount = playerCount;
        ShipType = shipType;
        GameMode = gameMode;
    }
    public override string ToString()
    {
        return Intensity + "_" + PlayerCount + "_" + ShipType + "_" + GameMode;
    }
}
namespace StarWriter.Core.Favoriting
{
    public class FavoriteSystem : SingletonPersistent<FavoriteSystem>
    {
        HashSet<Favorite> favorites = new();
        Favorite activeFavorite = new();

        void Start()
        {
            
        }

        Favorite GetActiveFavorite()
        {
            return activeFavorite;
        }
        public void AttemptToAddFavorite(int intensity, int playerCount, ShipTypes shipType, MiniGames gameMode)
        {
            var newFavorite = new Favorite(intensity, playerCount, shipType, gameMode);
            if (!FavoriteExists(newFavorite))
                {
                AddFavorite(intensity, playerCount, shipType, gameMode);
                }
            else { FavoriteExists(newFavorite); }
        }
        public void SetActiveFavorite(int intensity, int playerCount, ShipTypes shipType, MiniGames gameMode)
        {
            var newFavorite = new Favorite(intensity, playerCount, shipType, gameMode);
            favorites.Add(newFavorite);
            activeFavorite = newFavorite;
        }

        public void AddFavorite(int intensity, int playerCount, ShipTypes shiptype, MiniGames gameMode)
        {
            var newFavorite = new Favorite(intensity, playerCount, shiptype, gameMode);
            favorites.Add(newFavorite);
            if (favorites.Count == 1)
                activeFavorite = newFavorite;
            Debug.Log("Favorites - My Favorite Count : " + favorites.Count);
            Debug.Log("Favorites - My Zero Element : " + new List<Favorite>(favorites)[0].ToString());
        }

        public bool FavoriteExists(int intensity, int playerCount, ShipTypes shipType, MiniGames gameMode)
        {
            Debug.Log("Favorite - favorite exists : " + favorites.Contains(new Favorite(intensity, playerCount, shipType, gameMode)));
            return favorites.Contains(new Favorite(intensity, playerCount, shipType, gameMode));
        }

        public bool FavoriteExists(Favorite favorite)
        {
            Debug.Log("Favorite - favorite exists: " + favorites.Contains(favorite));
            return favorites.Contains(favorite);
        }

        public void RemoveFavorite(int intensity, int playerCount, ShipTypes shipType, MiniGames gameMode)
        {
            favorites.Remove(new Favorite(intensity, playerCount, shipType, gameMode));
            if (favorites.Count == 0)
                activeFavorite = default;
            Debug.Log("Favorite - My favorite Count: " + favorites.Count);
            Debug.Log("Favorite - My Zero Element: " + new List<Favorite>(favorites)[0].ToString());
        }

        public void RemoveFavorite(Favorite favorite)
        {         
            favorites.Remove(favorite);
            if (favorites.Count == 0)
                activeFavorite = default;
            Debug.Log("sampleStruct - My favorite Count: " + favorites.Count);
            Debug.Log("sampleStruct - My Zero Element: " + new List<Favorite>(favorites)[0].ToString());
        }

        public List<Favorite> GetFavorites()
        {
            return new List<Favorite>(favorites);
        }

        public int GetFavoriteCount()
        {
            return favorites.Count;
        }

        public void ClearAllFavorites()
        {
            favorites.Clear();
        }
        public void OnClickPlayTest()
        {
            MiniGame.PlayerShipType = (ShipTypes)activeFavorite.ShipType;
            MiniGame.PlayerPilot = Hangar.Instance.SoarPilot;
            MiniGame.IntensityLevel = (int)activeFavorite.Intensity;
            MiniGame.NumberOfPlayers = (int)activeFavorite.PlayerCount;

            SceneManager.LoadScene("MinigameFreestyle");
        }

        public void OnClickChangeShipType(int shipType)
        {
            activeFavorite.ShipType = (ShipTypes)shipType;
            Debug.Log(activeFavorite.ShipType.ToString());
        }
        public void OnClickChangeMiniGame(int gameMode)
        {
            activeFavorite.GameMode = (MiniGames)gameMode;
            Debug.Log(activeFavorite.GameMode.ToString());
        }
        public void OnClickChangeIntensity(int intensity)
        {
            activeFavorite.Intensity = intensity;
            Debug.Log(activeFavorite.Intensity.ToString());
        }
        public void OnClickChangePlayerCount(int playerCount)
        {
            activeFavorite.PlayerCount = playerCount;
            Debug.Log(activeFavorite.PlayerCount.ToString());
        }

    }
}



