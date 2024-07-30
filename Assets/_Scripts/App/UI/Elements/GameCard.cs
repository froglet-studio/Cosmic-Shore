using CosmicShore.App.Systems.Favorites;
using CosmicShore.App.UI.Menus;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements
{
    public class GameCard : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] SO_GameList AllGames;
        [SerializeField] Sprite StarIconActive;
        [SerializeField] Sprite StarIconInActive;
        [HideInInspector] public ExploreMenu ExploreMenu;

        [Header("Placeholder Locations")]
        [SerializeField] TMP_Text GameTitle;
        [SerializeField] Image BackgroundImage;
        [SerializeField] Image StarImage;
        [SerializeField] int Index;


        bool favorited;
        [SerializeField] public bool Favorited
        {
            get { return favorited; }
            set
            {
                favorited = value;
                UpdateCardView();
            }
        }

        MiniGames gameMode;
        public MiniGames GameMode
        {
            get { return gameMode; }
            set
            {
                gameMode = value;
                UpdateCardView();
            }
        }

        void Start()
        {
            if (gameMode == MiniGames.Random)
                gameMode = MiniGames.BlockBandit;

            UpdateCardView();
        }

        void UpdateCardView()
        {
            SO_ArcadeGame game = AllGames.GameList.Where(x => x.Mode == gameMode).FirstOrDefault();
            GameTitle.text = game.DisplayName;
            BackgroundImage.sprite = game.CardBackground;
            StarImage.sprite = Favorited ? StarIconActive : StarIconInActive;
        }

        public void ToggleFavorite()
        {
            Favorited = !Favorited;
            StarImage.sprite = Favorited ? StarIconActive : StarIconInActive;
            FavoriteSystem.ToggleFavorite(gameMode);
            ExploreMenu.PopulateGameSelectionList();
        }

        public void OnCardClicked()
        {
            // Add highlight boarder

            // Set active and show details
            //LoadoutView.ExpandLoadout(Index);

            Debug.Log($"GameCard - Clicked: Gamemode: {gameMode}");
        }
    }
}