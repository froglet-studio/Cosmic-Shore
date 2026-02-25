using System.Collections.Generic;
using CosmicShore.Models.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.IAP;
using CosmicShore.Utility.Recording;

namespace CosmicShore.UI.Screens
{
    public class EpisodeScreen : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private SO_EpisodeList episodeList;

        [Header("UI References")]
        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject episodeCardPrefab;
        [SerializeField] private ScrollRect scrollRect;

        [Header("Support Us")]
        [SerializeField] private Button supportUsButton;

        [Header("Panel Toggle")]
        [SerializeField] private GameObject episodePanel;

        private readonly List<GameObject> _spawnedCards = new();
        private bool _loaded;

        void Start()
        {
            if (supportUsButton != null)
                supportUsButton.onClick.AddListener(OnSupportUsClicked);
        }

        public void TogglePanel()
        {
            if (episodePanel == null) return;

            bool show = !episodePanel.activeSelf;
            episodePanel.SetActive(show);

            if (show && !_loaded)
                LoadView();
        }

        public void ShowPanel()
        {
            if (episodePanel != null)
                episodePanel.SetActive(true);

            if (!_loaded)
                LoadView();
        }

        public void HidePanel()
        {
            if (episodePanel != null)
                episodePanel.SetActive(false);
        }

        public void LoadView()
        {
            PopulateEpisodeCards();
            _loaded = true;
        }

        void PopulateEpisodeCards()
        {
            foreach (var card in _spawnedCards)
                if (card != null) Destroy(card);
            _spawnedCards.Clear();

            if (episodeList == null || episodeList.episodes == null) return;
            if (cardContainer == null || episodeCardPrefab == null) return;

            foreach (var episode in episodeList.episodes)
            {
                var cardGO = Instantiate(episodeCardPrefab, cardContainer);
                _spawnedCards.Add(cardGO);

                // EpisodeName
                var nameTransform = cardGO.transform.Find("EpisodeName");
                if (nameTransform != null)
                {
                    var nameTMP = nameTransform.GetComponent<TMP_Text>();
                    if (nameTMP != null)
                        nameTMP.text = episode.title;
                }

                // EpisodeDetail (description)
                var detailTransform = cardGO.transform.Find("EpisodeDetail");
                if (detailTransform != null)
                {
                    var detailTMP = detailTransform.GetComponent<TMP_Text>();
                    if (detailTMP != null)
                        detailTMP.text = episode.description;
                }

                // Amount / ValueText
                var valueTransform = cardGO.transform.Find("Button/ValueText");
                if (valueTransform != null)
                {
                    var valueTMP = valueTransform.GetComponent<TMP_Text>();
                    if (valueTMP != null)
                        valueTMP.text = episode.amount;
                }

                // BG image
                var bgTransform = cardGO.transform.Find("BG");
                if (bgTransform != null && episode.cardImage != null)
                {
                    var bgImage = bgTransform.GetComponent<Image>();
                    if (bgImage != null)
                        bgImage.sprite = episode.cardImage;
                }

                // ComingSoon - show if not available
                var button = cardGO.transform.Find("Button");
                if (button != null)
                {
                    var btn = button.GetComponent<Button>();
                    if (btn != null)
                        btn.interactable = episode.isAvailable;
                }
            }
        }

        void OnSupportUsClicked()
        {
            CSDebug.Log("[EpisodeScreen] Support Us clicked - IAP not yet configured.");
            IAPManager.Instance?.InitiateSupportPurchase();
        }
    }
}
