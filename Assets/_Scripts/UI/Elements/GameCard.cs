using CosmicShore.App.Systems.Favorites;
using CosmicShore.App.UI.Views;
using CosmicShore.Events;
using CosmicShore.FTUE;
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
        [HideInInspector] public ArcadeExploreView ExploreView;

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

        GameModes gameMode;
        public GameModes GameMode
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
            if (gameMode == GameModes.Random)
                gameMode = GameModes.BlockBandit;

            UpdateCardView();
        }

        void UpdateCardView()
        {
            SO_ArcadeGame game = AllGames.Games.Where(x => x.Mode == gameMode).FirstOrDefault();
            GameTitle.text = game.DisplayName;
            BackgroundImage.sprite = game.CardBackground;
            StarImage.sprite = Favorited ? StarIconActive : StarIconInActive;

            FTUEEventManager.RaiseCTAClicked(game.CallToActionTargetType);

            if (game.CallToActionTargetType == Systems.CTA.CallToActionTargetType.PlayGameFreestyle)
            {
                GetComponent<Button>().onClick.AddListener(delegate { FTUEEventManager.RaiseCTAClicked(game.CallToActionTargetType); });
            }

        }

        public void ToggleFavorite()
        {
            Favorited = !Favorited;
            StarImage.sprite = Favorited ? StarIconActive : StarIconInActive;
            FavoriteSystem.ToggleFavorite(gameMode);
            ExploreView.PopulateGameSelectionList();
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