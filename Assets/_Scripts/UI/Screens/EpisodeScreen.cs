using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore.UI
{
    public class EpisodeScreen : MonoBehaviour
    {
        [Inject] UGSDataService _ugsDataService;

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

            // Support Us is this panel's real-money affordance, so it wears the shared locked look
            // for as long as episodes are de-scoped. Marked here as well as gated in
            // OnSupportUsClicked because a dimmed button that refuses with a reason is the
            // deliberate state; a live-looking one that opens a payment page is the defect.
            if (supportUsButton != null)
                SO_CommerceAvailability.Mark(supportUsButton.gameObject, CommerceSurface.Episodes);
        }

        /// <summary>
        /// Whether this panel may open at all. While episodes are de-scoped it may not: every card
        /// in it is a buy affordance (the shipped episodes' own <c>amount</c> text is "Support Us"
        /// and the card prefab's label is <c>BuyText</c>), so the honest treatment is that the panel
        /// does not open rather than that it opens full of refusals
        /// (<c>Docs/STEAM_RELEASE_TASKS.md</c> R4).
        ///
        /// <para>The refusal is the app shell's own — a denied sting and a toast carrying the
        /// config's reason — and it is raised against this object rather than against the button
        /// that pressed it, which costs nothing: both of those are global, and the DIMMING that is
        /// per-control was already applied by that button's own <see cref="CommerceAffordance"/>.
        /// This object could not reach that button anyway: it lives ON the panel it toggles, the
        /// panel ships inactive, so <c>Start</c> has not run and cannot run until the panel opens —
        /// which is the one thing this gate prevents. That is what the marker component is for.</para>
        /// </summary>
        bool TryOpenEpisodes()
        {
            if (SO_CommerceAvailability.Instance.IsAvailable(CommerceSurface.Episodes))
                return true;

            // UnityEvent invokes a method on an inactive target, so this runs with nothing of ours
            // on screen. Safe: the view guards every piece of presentation it has not captured yet.
            SO_CommerceAvailability.TryPress(gameObject, CommerceSurface.Episodes);
            return false;
        }

        public void TogglePanel()
        {
            if (episodePanel == null) return;

            bool show = !episodePanel.activeSelf;
            if (show && !TryOpenEpisodes()) return;

            episodePanel.SetActive(show);

            if (show && !_loaded)
                LoadView();
        }

        public void ShowPanel()
        {
            if (!TryOpenEpisodes()) return;

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

        EpisodeProgressCloudData GetCloudProgress()
        {
            return _ugsDataService is { IsInitialized: true } ? _ugsDataService.Episodes?.Data : null;
        }

        void PopulateEpisodeCards()
        {
            foreach (var card in _spawnedCards)
                if (card != null) Destroy(card);
            _spawnedCards.Clear();

            if (episodeList == null || episodeList.episodes == null) return;
            if (cardContainer == null || episodeCardPrefab == null) return;

            var cloudProgress = GetCloudProgress();

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

                // A non-zero priceUsd marks the episode as purchasable "support" - its card
                // button becomes a web-checkout buy button (always interactable). Otherwise the
                // button keeps its play semantics (gated by availability / cloud unlock).
                //
                // While episodes are de-scoped it is NOT purchasable, whatever it is priced at: no
                // price is rendered, because a price on screen IS the offer whether or not the
                // button works, and no checkout listener is wired. This branch is unreachable today
                // (TryOpenEpisodes keeps the panel shut) and is kept because it is the half that has
                // to be right the day the panel opens again - the gate and the rendering must flip
                // together or the first build after the conversion shows prices it cannot take.
                bool episodesAvailable = SO_CommerceAvailability.Instance.IsAvailable(CommerceSurface.Episodes);
                bool isPurchasable = episode.priceUsd > 0f && episodesAvailable;

                // Amount / ValueText - price (purchasable), completion, or the free-text amount.
                var valueTransform = cardGO.transform.Find("Button/ValueText");
                if (valueTransform != null)
                {
                    var valueTMP = valueTransform.GetComponent<TMP_Text>();
                    if (valueTMP != null)
                    {
                        if (isPurchasable)
                            valueTMP.text = FormatPrice(episode.priceUsd);
                        else if (cloudProgress != null && cloudProgress.IsCompleted(episode.episodeId))
                            valueTMP.text = "Completed";
                        else
                            valueTMP.text = episode.amount;
                    }
                }

                // BG image
                var bgTransform = cardGO.transform.Find("BG");
                if (bgTransform != null && episode.cardImage != null)
                {
                    var bgImage = bgTransform.GetComponent<Image>();
                    if (bgImage != null)
                        bgImage.sprite = episode.cardImage;
                }

                var button = cardGO.transform.Find("Button");
                if (button != null)
                {
                    var btn = button.GetComponent<Button>();
                    if (btn != null)
                    {
                        if (isPurchasable)
                        {
                            // Replace any prefab-wired play handler with the web-checkout open.
                            var purchasedEpisode = episode; // capture for the closure
                            btn.onClick.RemoveAllListeners();
                            btn.interactable = true;
                            btn.onClick.AddListener(() => IAPManager.Instance?.InitiateEpisodePurchase(purchasedEpisode));
                        }
                        else if (!episodesAvailable)
                        {
                            // Every card in this panel is a buy affordance while episodes are
                            // de-scoped - the card prefab's own label is BuyText and the shipped
                            // episodes' amount text is "Support Us" - so it carries the shared
                            // locked look and refuses with a reason. Without this the card is an
                            // interactable button with no listeners at all, which is exactly the
                            // dead-Buy-button state R4 exists to fix.
                            //
                            // The marker rather than a hand-rolled dim, so one asset edit undoes it.
                            var affordance = CommerceAffordance.Ensure(cardGO, CommerceSurface.Episodes);
                            btn.onClick.AddListener(() => affordance.TryPress());
                        }
                        else
                        {
                            // Play semantics (unchanged): interactable when available or cloud-unlocked.
                            // Prefab-wired onClick listeners are left intact.
                            bool isAvailable = episode.isAvailable;
                            if (cloudProgress != null)
                                isAvailable = isAvailable || cloudProgress.IsUnlocked(episode.episodeId);
                            btn.interactable = isAvailable;
                        }
                    }
                }
            }
        }

        string FormatPrice(float priceUsd)
        {
            return IAPManager.Instance != null
                ? IAPManager.Instance.FormatPrice(priceUsd)
                : $"${priceUsd.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}";
        }

        void OnSupportUsClicked()
        {
            // The action layer. IAPManager.OpenCheckout refuses as well, so this press cannot open a
            // payment page even if the marking above were lost - but the gate belongs here too,
            // because only here is there a control to refuse ON, with the shell's sting and reason.
            if (!SO_CommerceAvailability.TryPress(
                    supportUsButton ? supportUsButton.gameObject : gameObject,
                    CommerceSurface.Episodes))
                return;

            IAPManager.Instance?.InitiateSupportPurchase();
        }
    }
}
