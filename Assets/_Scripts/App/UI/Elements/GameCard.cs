using CosmicShore.App.Systems.Audio;
using CosmicShore.App.Systems.Favorites;
using CosmicShore.App.UI.Views;
using CosmicShore.Events;
using CosmicShore.FTUE;
using CosmicShore.Game.Progression;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Utility;

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

        [Header("Lock State")]
        [Tooltip("Overlay shown when the game mode is locked")]
        [SerializeField] private GameObject lockOverlay;
        [Tooltip("Tint color applied to the card background when locked")]
        [SerializeField] private Color lockedTintColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        private bool _isLocked;
        private Color _originalBgColor = Color.white;

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
            if (game == null) return;

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
            AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.OptionClick);
            FavoriteSystem.ToggleFavorite(gameMode);
            ExploreView.PopulateGameSelectionList();
        }

        public void OnCardClicked()
        {
            CSDebug.Log($"GameCard - Clicked: Gamemode: {gameMode}");
        }

        /// <summary>
        /// Sets the visual locked state of this card.
        /// Locked cards are greyed out with a lock icon overlay and non-interactable.
        /// </summary>
        public void SetLocked(bool locked)
        {
            if (lockOverlay != null)
                lockOverlay.SetActive(locked);

            if (BackgroundImage != null)
            {
                // Only save the original color when transitioning from unlocked → locked
                // to avoid overwriting it with the tinted color on repeated SetLocked(true) calls
                if (locked && !_isLocked)
                    _originalBgColor = BackgroundImage.color;

                BackgroundImage.color = locked ? lockedTintColor : _originalBgColor;
            }

            _isLocked = locked;

            if (TryGetComponent<Button>(out var btn))
                btn.interactable = !locked;
        }
    }
}