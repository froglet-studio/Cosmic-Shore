using System.Collections;
using CosmicShore.Game.Progression;
using CosmicShore.Models;
using TMPro;
using UnityEngine;

namespace CosmicShore.App.UI.Elements
{
    /// <summary>
    /// Controls one of the two intensity info panels (one for intensity 3, one for intensity 4).
    /// Shows the goal description and progress when the player taps a locked intensity button.
    /// Auto-hides after a configurable duration. Only one panel can be visible at a time —
    /// managed by setting a shared static reference.
    /// </summary>
    public class IntensityInfoPanel : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Which locked intensity this panel describes (3 or 4)")]
        [SerializeField] private int targetIntensity = 3;

        [Tooltip("How long the panel stays visible before auto-hiding")]
        [SerializeField] private float displayDuration = 2f;

        [Header("UI References")]
        [Tooltip("Text displaying the goal description and progress")]
        [SerializeField] private TMP_Text infoText;

        static IntensityInfoPanel _currentlyVisible;
        Coroutine _hideCoroutine;

        void Awake()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Shows this panel with the current goal info for the given game mode.
        /// Hides any other currently visible panel first.
        /// </summary>
        public void Show(GameModes mode)
        {
            // Hide the other panel if one is already showing
            if (_currentlyVisible != null && _currentlyVisible != this)
                _currentlyVisible.HideImmediate();

            var service = GameModeProgressionService.Instance;
            if (service == null) return;

            var quest = service.GetQuestForMode(mode);
            if (quest == null) return;

            // Already unlocked — show completed state
            if (service.IsIntensityUnlocked(mode, targetIntensity))
            {
                if (infoText)
                    infoText.text = "Unlocked";
            }
            else
            {
                string goalTemplate = targetIntensity == 3
                    ? quest.Intensity3GoalDescription
                    : quest.Intensity4GoalDescription;

                int required = service.GetPlaysRequiredForIntensity(mode, targetIntensity);
                int remaining = service.GetPlaysRemainingForIntensity(mode, targetIntensity);

                if (infoText)
                    infoText.text = $"{string.Format(goalTemplate, required)}\n{remaining} remaining";
            }

            gameObject.SetActive(true);
            _currentlyVisible = this;

            if (_hideCoroutine != null)
                StopCoroutine(_hideCoroutine);
            _hideCoroutine = StartCoroutine(HideAfterDelay());
        }

        IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(displayDuration);
            HideImmediate();
        }

        void HideImmediate()
        {
            if (_hideCoroutine != null)
            {
                StopCoroutine(_hideCoroutine);
                _hideCoroutine = null;
            }

            gameObject.SetActive(false);

            if (_currentlyVisible == this)
                _currentlyVisible = null;
        }

        void OnDisable()
        {
            if (_currentlyVisible == this)
                _currentlyVisible = null;

            _hideCoroutine = null;
        }
    }
}
