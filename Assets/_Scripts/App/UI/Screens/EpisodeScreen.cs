using System;
using System.Collections.Generic;
using CosmicShore.Models;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Screens
{
    /// <summary>
    /// Displays episodes as horizontally scrollable cards.
    /// All episode buttons are non-interactable with a "Coming Soon" overlay.
    /// Episode view state is saved to UGS CloudSave.
    /// </summary>
    public class EpisodeScreen : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private SO_EpisodeList episodeList;

        [Header("UI References")]
        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject episodeCardPrefab;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private GameObject comingSoonOverlay;

        [Header("Support Us")]
        [SerializeField] private Button supportUsButton;
        [SerializeField] private TMP_Text supportUsText;

        private const string CLOUD_SAVE_KEY = "EPISODE_VIEW_STATE";
        private readonly List<GameObject> _spawnedCards = new();

        void Start()
        {
            if (supportUsButton != null)
                supportUsButton.onClick.AddListener(OnSupportUsClicked);
        }

        public void LoadView()
        {
            PopulateEpisodeCards();
            SaveEpisodeViewState();
        }

        void PopulateEpisodeCards()
        {
            // Clear existing cards
            foreach (var card in _spawnedCards)
            {
                if (card != null)
                    Destroy(card);
            }
            _spawnedCards.Clear();

            if (episodeList == null || episodeList.episodes == null)
                return;

            if (cardContainer == null || episodeCardPrefab == null)
            {
                Debug.LogWarning("[EpisodeScreen] Card container or prefab not assigned.");
                return;
            }

            foreach (var episode in episodeList.episodes)
            {
                var cardGO = Instantiate(episodeCardPrefab, cardContainer);
                _spawnedCards.Add(cardGO);

                // Set card image
                var cardImage = cardGO.GetComponentInChildren<Image>();
                if (cardImage != null && episode.cardImage != null)
                    cardImage.sprite = episode.cardImage;

                // Set card title
                var titleText = cardGO.GetComponentInChildren<TMP_Text>();
                if (titleText != null)
                    titleText.text = episode.title;

                // All buttons are non-interactable (Coming Soon)
                var button = cardGO.GetComponent<Button>();
                if (button == null)
                    button = cardGO.GetComponentInChildren<Button>();
                if (button != null)
                    button.interactable = false;
            }

            // Show coming soon overlay
            if (comingSoonOverlay != null)
                comingSoonOverlay.SetActive(true);
        }

        void OnSupportUsClicked()
        {
            // Unity IAP integration point
            // This will be implemented with Unity IAP when the store is set up.
            Debug.Log("[EpisodeScreen] Support Us clicked - IAP not yet configured.");
            IAPManager.Instance?.InitiateSupportPurchase();
        }

        /// <summary>
        /// Saves the episode view state to CloudSave for tracking.
        /// </summary>
        async void SaveEpisodeViewState()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    return;

                if (AuthenticationService.Instance == null || !AuthenticationService.Instance.IsSignedIn)
                    return;

                var data = new Dictionary<string, object>
                {
                    {
                        CLOUD_SAVE_KEY, new EpisodeViewState
                        {
                            lastViewedTimestamp = DateTime.UtcNow.Ticks,
                            totalEpisodes = episodeList != null ? episodeList.episodes.Count : 0
                        }
                    }
                };

                await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EpisodeScreen] CloudSave failed: {e.Message}");
            }
        }
    }

    [Serializable]
    public class EpisodeViewState
    {
        public long lastViewedTimestamp;
        public int totalEpisodes;
    }
}
