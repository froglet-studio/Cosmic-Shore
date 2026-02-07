using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.Cinematics
{
    /// <summary>
    /// Individual vessel display component for end-game screen.
    /// This script goes on the prefab that will be instantiated for each player.
    /// Handles vessel icon, player name, and ranking display with fade-in animation.
    /// </summary>
    public class EndGameVesselDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image vesselIconImage;
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private GameObject rankingIndicator;
        [SerializeField] private TMP_Text rankingText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Ranking Icons (Optional)")]
        [Tooltip("Optional: Different icons for 1st, 2nd, 3rd place")]
        [SerializeField] private GameObject firstPlaceIcon;
        [SerializeField] private GameObject secondPlaceIcon;
        [SerializeField] private GameObject thirdPlaceIcon;
        [SerializeField] private GameObject defaultRankIcon;

        [Header("Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float scaleInDuration = 0.4f;
        [SerializeField] private Ease fadeInEase = Ease.OutCubic;
        [SerializeField] private Vector3 startScale = new Vector3(0.8f, 0.8f, 0.8f);

        private RectTransform rectTransform;
        private bool isInitialized;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            
            // Start hidden
            if (canvasGroup)
                canvasGroup.alpha = 0f;
            
            // Hide all ranking icons initially
            HideAllRankingIcons();
        }

        /// <summary>
        /// Initialize vessel display with player data
        /// </summary>
        public void Initialize(VesselDisplayData data, VesselIconLibrarySO iconLibrary)
        {
            if (isInitialized)
            {
                Debug.LogWarning("VesselDisplay already initialized!");
                return;
            }

            // Set player name
            if (playerNameText)
                playerNameText.text = data.playerName;

            // Set vessel icon from library
            if (vesselIconImage && iconLibrary)
            {
                bool isWinner = data.ranking == 1;
                vesselIconImage.sprite = iconLibrary.GetVesselIcon(data.vesselType, isWinner);
                
                // Optional: Apply vessel color tint
                vesselIconImage.color = iconLibrary.GetVesselColor(data.vesselType);
            }

            // Set ranking
            SetRanking(data.ranking);

            isInitialized = true;
        }

        /// <summary>
        /// Set ranking and show appropriate icon
        /// </summary>
        void SetRanking(int ranking)
        {
            HideAllRankingIcons();

            // Show ranking text
            if (rankingText)
                rankingText.text = GetRankingText(ranking);

            // Show appropriate ranking icon
            switch (ranking)
            {
                case 1:
                    if (firstPlaceIcon) firstPlaceIcon.SetActive(true);
                    else if (rankingIndicator) rankingIndicator.SetActive(true);
                    break;
                case 2:
                    if (secondPlaceIcon) secondPlaceIcon.SetActive(true);
                    else if (rankingIndicator) rankingIndicator.SetActive(true);
                    break;
                case 3:
                    if (thirdPlaceIcon) thirdPlaceIcon.SetActive(true);
                    else if (rankingIndicator) rankingIndicator.SetActive(true);
                    break;
                default:
                    if (defaultRankIcon) defaultRankIcon.SetActive(true);
                    else if (rankingIndicator) rankingIndicator.SetActive(true);
                    break;
            }
        }

        /// <summary>
        /// Get ranking display text (1st, 2nd, 3rd, etc.)
        /// </summary>
        string GetRankingText(int ranking)
        {
            switch (ranking)
            {
                case 1: return "1st";
                case 2: return "2nd";
                case 3: return "3rd";
                default: return $"{ranking}th";
            }
        }

        /// <summary>
        /// Hide all ranking icon options
        /// </summary>
        void HideAllRankingIcons()
        {
            if (firstPlaceIcon) firstPlaceIcon.SetActive(false);
            if (secondPlaceIcon) secondPlaceIcon.SetActive(false);
            if (thirdPlaceIcon) thirdPlaceIcon.SetActive(false);
            if (defaultRankIcon) defaultRankIcon.SetActive(false);
            if (rankingIndicator) rankingIndicator.SetActive(false);
        }

        /// <summary>
        /// Play fade-in animation
        /// </summary>
        public void FadeIn(float delay = 0f)
        {
            if (!canvasGroup)
            {
                Debug.LogWarning("CanvasGroup not assigned, cannot fade in!");
                return;
            }

            // Reset state
            canvasGroup.alpha = 0f;
            if (rectTransform)
                rectTransform.localScale = startScale;

            // Create animation sequence
            Sequence sequence = DOTween.Sequence();
            sequence.SetDelay(delay);
            
            // Fade in
            sequence.Append(canvasGroup.DOFade(1f, fadeInDuration).SetEase(fadeInEase));
            
            // Scale in (parallel with fade)
            if (rectTransform)
            {
                sequence.Join(rectTransform.DOScale(Vector3.one, scaleInDuration).SetEase(fadeInEase));
            }

            sequence.Play();
        }

        /// <summary>
        /// Instant show without animation
        /// </summary>
        public void ShowInstant()
        {
            if (canvasGroup)
                canvasGroup.alpha = 1f;
            
            if (rectTransform)
                rectTransform.localScale = Vector3.one;
        }

        /// <summary>
        /// Hide this display
        /// </summary>
        public void Hide()
        {
            if (canvasGroup)
                canvasGroup.alpha = 0f;
        }

        /// <summary>
        /// Cleanup and reset for reuse
        /// </summary>
        public void Reset()
        {
            Hide();
            HideAllRankingIcons();
            isInitialized = false;
            
            if (playerNameText)
                playerNameText.text = "";
            
            if (vesselIconImage)
            {
                vesselIconImage.sprite = null;
                vesselIconImage.color = Color.white;
            }
            
            if (rankingText)
                rankingText.text = "";
        }
    }
}