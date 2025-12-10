using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class MantaVesselHUDView : VesselHUDView
    {
        [Header("Simple counter")]
        [SerializeField] private TMP_Text countText; // currently unused

        [Header("Overcharge")]
        [SerializeField] private Image            fillImage;
        [SerializeField] private TextMeshProUGUI  overchargePrismCount;
        [SerializeField] private GameObject       overchargeCountdownContainer;
        [SerializeField] private TextMeshProUGUI  overchargeCountdownText;
        [SerializeField] private Color            normalColor   = Color.white;
        [SerializeField] private Color            highlightColor = Color.yellow;

        public Color NormalColor    => normalColor;
        public Color HighLightColor => highlightColor;

        /// <summary>
        /// Called by controller once on Initialize.
        /// </summary>
        public void InitializeOvercharge(int maxBlockHits)
        {
            if (fillImage)
            {
                fillImage.fillAmount = 0f;
                fillImage.color      = normalColor;
            }

            SetOverchargeCount(0, maxBlockHits);

            if (overchargeCountdownContainer)
                overchargeCountdownContainer.SetActive(false);

            if (overchargeCountdownText)
                overchargeCountdownText.text = string.Empty;
        }

        /// <summary>
        /// Updates prism count text + radial fill + highlight color.
        /// </summary>
        public void SetOverchargeCount(int count, int max)
        {
            if (overchargePrismCount)
                overchargePrismCount.text = count.ToString();

            if (!fillImage || max <= 0) 
                return;

            float fill = Mathf.Clamp01((float)count / max);
            fillImage.fillAmount = fill;
            fillImage.color      = (count >= max) ? highlightColor : normalColor;
        }

        /// <summary>
        /// Resets visual state after cooldown starts.
        /// </summary>
        public void ResetOvercharge(int max)
        {
            SetOverchargeCount(0, max);

            if (fillImage)
            {
                fillImage.fillAmount = 0f;
                fillImage.color      = normalColor;
            }

            if (overchargeCountdownContainer)
                overchargeCountdownContainer.SetActive(false);

            if (overchargeCountdownText)
                overchargeCountdownText.text = string.Empty;
        }

        /// <summary>
        /// Optional HUD-side countdown
        /// </summary>
        public void ShowOverchargeCountdown(float seconds)
        {
            if (!overchargeCountdownContainer || !overchargeCountdownText)
                return;

            overchargeCountdownContainer.SetActive(true);
            overchargeCountdownText.text = Mathf.CeilToInt(seconds).ToString();
        }

        public void HideOverchargeCountdown()
        {
            if (overchargeCountdownContainer)
                overchargeCountdownContainer.SetActive(false);

            if (overchargeCountdownText)
                overchargeCountdownText.text = string.Empty;
        }
    }
}
