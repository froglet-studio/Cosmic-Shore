using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class RhinoVesselHUDView : VesselHUDView
    {
        [Header("Rhino – Skimmer Size Icon")]
        [SerializeField] private RectTransform skimmerSizeIcon;
        [SerializeField] private float minIconSize = 50f;
        [SerializeField] private float maxIconSize = 100f;

        [Header("Rhino – Crystal Slow Icon")]
        [SerializeField] private Image crystalIcon;
        [SerializeField] private TextMeshProUGUI slowedCountText;

        [Header("Rhino – Slow Line Icon")]
        [SerializeField] private Image lineIcon;

        [Header("Default Colors")]
        [SerializeField] private Color crystalDefaultColor = Color.white;
        [SerializeField] private Color crystalActivatedColor = Color.green;
        [SerializeField] private Color lineDefaultColor = Color.white;
        [SerializeField] private Color lineActivatedColor = Color.red;

        Coroutine _crystalRoutine;
        Coroutine _lineRoutine;

        void OnEnable()
        {
            // Ensure starting state is "idle"
            if (slowedCountText)
                slowedCountText.gameObject.SetActive(false);

            ResetAllRhinoStates();
        }

        #region Skimmer Icon

        public void SetSkimmerIconScale01(float t)
        {
            if (!skimmerSizeIcon) return;
            t = Mathf.Clamp01(t);
            float size = Mathf.Lerp(minIconSize, maxIconSize, t);
            skimmerSizeIcon.sizeDelta = new Vector2(size, size);
        }

        public void ResetSkimmerIcon()
        {
            SetSkimmerIconScale01(0f);
        }

        #endregion

        #region Crystal Icon + Count

        /// <summary>
        /// Called when the crystal explosion is triggered.
        /// Turns crystal green and shows count text, then after duration
        /// restores default color and hides the text.
        /// </summary>
        public void FlashCrystalActivated(float duration)
        {
            if (!crystalIcon) return;

            if (_crystalRoutine != null)
                StopCoroutine(_crystalRoutine);

            if(!gameObject.activeInHierarchy) return;
            _crystalRoutine = StartCoroutine(CrystalRoutine(duration));
        }

        IEnumerator CrystalRoutine(float duration)
        {
            // Activate state
            crystalIcon.color = crystalActivatedColor;

            if (slowedCountText)
                slowedCountText.gameObject.SetActive(true);

            yield return new WaitForSeconds(duration);

            // Back to normal
            crystalIcon.color = crystalDefaultColor;

            if (slowedCountText)
                slowedCountText.gameObject.SetActive(false);

            _crystalRoutine = null;
        }

        public void SetSlowedCount(int count)
        {
            if (!slowedCountText) return;
            slowedCountText.text = count.ToString();
        }

        public void ResetCrystalIcon()
        {
            if (crystalIcon)
                crystalIcon.color = crystalDefaultColor;

            if (slowedCountText)
            {
                slowedCountText.text = "0";
                slowedCountText.gameObject.SetActive(false);
            }
        }

        #endregion

        #region Line Icon

        /// <summary>
        /// Called when a vessel is slowed by the explosion.
        /// Turns line icon red, then after duration returns to default color.
        /// </summary>
        public void FlashLineIconActive(float duration)
        {
            if (!lineIcon) return;

            if (_lineRoutine != null)
                StopCoroutine(_lineRoutine);

            _lineRoutine = StartCoroutine(LineRoutine(duration));
        }

        IEnumerator LineRoutine(float duration)
        {
            lineIcon.color = lineActivatedColor;

            yield return new WaitForSeconds(duration);

            lineIcon.color = lineDefaultColor;

            _lineRoutine = null;
        }

        public void ResetLineIcon()
        {
            if (lineIcon)
                lineIcon.color = lineDefaultColor;
        }

        #endregion

        public void ResetAllRhinoStates()
        {
            ResetSkimmerIcon();
            ResetCrystalIcon();
            ResetLineIcon();
        }
    }
}
