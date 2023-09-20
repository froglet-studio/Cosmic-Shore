using StarWriter.Utility.Singleton;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Favorite
{
    public int Intensity;
    public int PlayerCount;
    public int ShipTypeEnumVaule;
    public int MiniGameEnumVal;

    public Favorite(int intensity, int playerCount, int shipTypeEnumVaule, int miniGameEnumVal)
    {
        Intensity = intensity;
        PlayerCount = playerCount;
        ShipTypeEnumVaule = shipTypeEnumVaule;
        MiniGameEnumVal = miniGameEnumVal;
    }
    public override string ToString()
    {
        return Intensity + "_" + PlayerCount + "_" + ShipTypeEnumVaule + "_" + MiniGameEnumVal;
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
            //Testing 
            AddFavorite(1, 2, 3, 1);
            AddFavorite(3, 2, 1, 3);
            AddFavorite(1, 2, 3, 2);
            FavoriteExists(1, 2, 3, 1);
            RemoveFavorite(1, 2, 3, 2);
            FavoriteExists(1, 2, 3, 1);
            Debug.Log("Favorite - default: " + default(Favorite).ToString());
            ClearAllFavorites();
            FavoriteExists(1,0,0,0);
            FavoriteExists(0,0,0,0);
            Debug.Log("Favorite - default: " + default(Favorite).ToString());
            AddFavorite(3, 2, 1, 3);
            FavoriteExists(3, 2, 1, 3);
        }

        Favorite GetActiveFavorite()
        {
            return activeFavorite;
        }
        public void AttemptToAddFavorite(int intensity, int playerCount, int shipTypeEnumVaule, int miniGameEnumVal)
        {
            var newFavorite = new Favorite(intensity, playerCount, shipTypeEnumVaule, miniGameEnumVal);
            if (!FavoriteExists(newFavorite))
                {
                AddFavorite(intensity, playerCount, shipTypeEnumVaule, miniGameEnumVal);
                }
            else { FavoriteExists(newFavorite); }
        }
        public void SetActiveFavorite(int intensity, int playerCount, int shipTypeEnumVaule, int miniGameEnumVal)
        {
            var newFavorite = new Favorite(intensity, playerCount, shipTypeEnumVaule, miniGameEnumVal);
            favorites.Add(newFavorite);
            activeFavorite = newFavorite;
        }

        public void AddFavorite(int intensity, int playerCount, int shipTypeEnumVaule, int miniGameEnumVal)
        {
            var newFavorite = new Favorite(intensity, playerCount, shipTypeEnumVaule, miniGameEnumVal);
            favorites.Add(newFavorite);
            if (favorites.Count == 1)
                activeFavorite = newFavorite;
            Debug.Log("Favorites - My Favorite Count : " + favorites.Count);
            Debug.Log("Favorites - My Zero Element : " + new List<Favorite>(favorites)[0].ToString());
        }

        public bool FavoriteExists(int intensity, int playerCount, int shipTypeEnumVaule, int miniGameEnumVal)
        {
            Debug.Log("Favorite - favorite exists : " + favorites.Contains(new Favorite(intensity, playerCount, shipTypeEnumVaule, miniGameEnumVal)));
            return favorites.Contains(new Favorite(intensity, playerCount, shipTypeEnumVaule, miniGameEnumVal));
        }

        public bool FavoriteExists(Favorite favorite)
        {
            Debug.Log("Favorite - favorite exists: " + favorites.Contains(favorite));
            return favorites.Contains(favorite);
        }

        public void RemoveFavorite(int intensity, int playerCount, int shipTypeEnumVaule, int miniGameEnumVal)
        {
            favorites.Remove(new Favorite(intensity, playerCount, shipTypeEnumVaule, miniGameEnumVal));
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

    }
}



