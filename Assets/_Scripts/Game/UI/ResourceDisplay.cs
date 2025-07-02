using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    public class ResourceDisplay : MonoBehaviour
    {
        [Header("Mode")]
        [Tooltip("When true, use segmented fill UI; when false, use legacy sprite-swap UI")]
        [SerializeField] private bool useSegmentedDisplay = false;

        [Header("Legacy Sprite-Swap UI (unchanged)")]
        [SerializeField] private bool verboseLogging;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private TMP_Text fuelLevelText;
        [SerializeField] private List<Sprite> fuelLevelImages;
        [SerializeField] private Image fuelLevelImage;

        [Header("Smooth Fill Animator (unchanged)")]
        [SerializeField] private BoostFillAnimator resourceFillAnimator;

        [Header("New Segmented Fill UI")]
        [Tooltip("One Image per segment; each should be set to Filled in the Inspector")]
        [SerializeField] private List<Image> segmentedLevelImages;

        private const float maxLevel = 1f;
        private float currentLevel;

        void Start()
        {
            // common initialization
            if (backgroundImage && backgroundSprite)
                backgroundImage.sprite = backgroundSprite;

            currentLevel = 0f;

            // initialize legacy
            if (!useSegmentedDisplay)
            {
                if (fuelLevelImages != null && fuelLevelImages.Count > 0)
                    fuelLevelImage.sprite = fuelLevelImages[0];
            }
            if (fuelLevelText)
                fuelLevelText.text = "0";

            // initialize segmented
            foreach (var img in segmentedLevelImages)
                img.fillAmount = 0f;
        }

        /// <summary>
        /// Instantly snap to the new level (0–1), choosing mode by flag.
        /// </summary>
        public void UpdateDisplay(float newResourceLevel)
        {
            currentLevel = Mathf.Clamp01(newResourceLevel);

            if (useSegmentedDisplay)
            {
                UpdateSegmentedDisplay(currentLevel);
                return;
            }

            // legacy sprite-swap path
            int maxIndex = fuelLevelImages.Count - 1;
            float pct = currentLevel / maxLevel;
            int idx = Mathf.FloorToInt(pct * maxIndex);

            if (verboseLogging)
                Debug.Log($"ResourceDisplay.UpdateDisplay – pct:{pct:F2}, idx:{idx}");

            fuelLevelImage.sprite = fuelLevelImages[Mathf.Clamp(idx, 0, maxIndex)];
            if (fuelLevelText)
                fuelLevelText.text = (currentLevel * 100f).ToString("F0");
        }

        /// <summary>
        /// NEW: segmented?bar update. Each Image.fillAmount is set to its segment’s fraction.
        /// </summary>
        private void UpdateSegmentedDisplay(float normalizedLevel)
        {
            int segCount = segmentedLevelImages.Count;
            if (segCount == 0) return;

            for (int i = 0; i < segCount; i++)
            {
                // compute per-segment fill (0–1)
                float fill = Mathf.Clamp01(currentLevel * segCount - i);
                segmentedLevelImages[i].fillAmount = fill;
            }

            // update the same text field
            if (fuelLevelText)
                fuelLevelText.text = Mathf.RoundToInt(currentLevel * 100f).ToString();
        }

        /// <summary>
        /// Animate draining from <paramref name="from"/>?0 over <paramref name="duration"/>s.
        /// </summary>
        public void AnimateFillDown(float duration, float from)
        {
            if (resourceFillAnimator == null)
            {
                Debug.LogWarning("ResourceDisplay: missing BoostFillAnimator!");
                return;
            }
            resourceFillAnimator.SetFill(from);
            resourceFillAnimator.AnimateFillDown(duration, from);
        }

        /// <summary>
        /// Animate refilling from current/0?<paramref name="to"/> over <paramref name="duration"/>s.
        /// </summary>
        public void AnimateFillUp(float duration, float to)
        {
            if (resourceFillAnimator == null)
            {
                Debug.LogWarning("ResourceDisplay: missing BoostFillAnimator!");
                return;
            }
            resourceFillAnimator.AnimateFillUp(duration, to);
        }
    }
}
