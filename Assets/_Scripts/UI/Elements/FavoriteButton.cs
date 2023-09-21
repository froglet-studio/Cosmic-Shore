using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using StarWriter.Core.Favoriting;
using UnityEngine.UIElements;

namespace StarWriter.UI
{
    public class FavoriteButton : MonoBehaviour
    {
        UnityEngine.UI.Button favoriteStar;
        public Favorite currentFavoriteSelected;

        FavoriteSystem favoriteSystem = new FavoriteSystem();


        // Start is called before the first frame update
        void Start()
        {
            favoriteStar  = gameObject.GetComponent<UnityEngine.UI.Button>();
        }

        // Update is called once per frame
        void Update()
        {

        }
        public void OnFavoritesButtonPressed()
        {
            //favoriteSystem.FavoriteExists();
            //favoriteSystem.AddFavorite(currentFavoriteSelected);
        }

    }

    
}

