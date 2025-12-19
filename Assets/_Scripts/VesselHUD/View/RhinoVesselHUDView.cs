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

        [Header("Rhino – Debuff Icon")]
        [SerializeField] private Image debuffIcon;
        [SerializeField] private TextMeshProUGUI debuffTimerText;

        [Header("Default Colors")]
        [SerializeField] private Color crystalDefaultColor = Color.white;
        [SerializeField] private Color crystalActivatedColor = Color.green;
        [SerializeField] private Color lineDefaultColor = Color.white;
        [SerializeField] private Color lineActivatedColor = Color.red;
        [SerializeField] private Color debuffDefaultColor = Color.white;
        [SerializeField] private Color debuffActiveColor = Color.cyan;

        Coroutine _crystalRoutine;
        Coroutine _lineRoutine;
        Coroutine _debuffRoutine;

        void OnEnable()
        {
            if (slowedCountText)
                slowedCountText.gameObject.SetActive(false);

            if (debuffIcon)
                debuffIcon.gameObject.SetActive(false);
            if (debuffTimerText)
                debuffTimerText.gameObject.SetActive(false);

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

        public void FlashCrystalActivated(float duration)
        {
            if (!crystalIcon) return;

            if (_crystalRoutine != null)
                StopCoroutine(_crystalRoutine);

            _crystalRoutine = StartCoroutine(CrystalRoutine(duration));
        }

        IEnumerator CrystalRoutine(float duration)
        {
            crystalIcon.color = crystalActivatedColor;

            if (slowedCountText)
                slowedCountText.gameObject.SetActive(true);

            yield return new WaitForSeconds(duration);

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

        private void ResetCrystalIcon()
        {
            if (crystalIcon)
                crystalIcon.color = crystalDefaultColor;

            if (!slowedCountText) return;
            slowedCountText.text = "0";
            slowedCountText.gameObject.SetActive(false);
        }

        #endregion

        #region Line Icon

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

        #region Debuff Icon + Timer

        public void ShowDebuffTimer(float duration)
        {
            if (!debuffIcon || !debuffTimerText) return;

            if (_debuffRoutine != null)
                StopCoroutine(_debuffRoutine);

            _debuffRoutine = StartCoroutine(DebuffRoutine(duration));
        }

        IEnumerator DebuffRoutine(float duration)
        {
            debuffIcon.gameObject.SetActive(true);
            debuffTimerText.gameObject.SetActive(true);

            debuffIcon.color = debuffActiveColor;

            var remaining = duration;

            while (remaining > 0f)
            {
                debuffTimerText.text = Mathf.CeilToInt(remaining).ToString();
                yield return null;
                remaining -= Time.deltaTime;
            }

            debuffIcon.color = debuffDefaultColor;
            debuffIcon.gameObject.SetActive(false);
            debuffTimerText.gameObject.SetActive(false);

            _debuffRoutine = null;
        }

        private void ResetDebuffIcon()
        {
            if (debuffIcon)
            {
                debuffIcon.color = debuffDefaultColor;
                debuffIcon.gameObject.SetActive(false);
            }

            if (!debuffTimerText) return;
            debuffTimerText.text = string.Empty;
            debuffTimerText.gameObject.SetActive(false);
        }

        #endregion

        private void ResetAllRhinoStates()
        {
            ResetSkimmerIcon();
            ResetCrystalIcon();
            ResetLineIcon();
            ResetDebuffIcon();
        }
    }
}
