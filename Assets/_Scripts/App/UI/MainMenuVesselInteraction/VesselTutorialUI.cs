using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.MainMenuVesselInteraction
{
    /// <summary>
    /// Simple overlay UI for vessel tutorial prompts and the exit button.
    /// Should be attached to a Canvas with a CanvasGroup for fade control.
    /// </summary>
    public class VesselTutorialUI : MonoBehaviour
    {
        [Header("Prompt")]
        [SerializeField] private CanvasGroup promptCanvasGroup;
        [SerializeField] private TMP_Text promptText;

        [Header("Exit Button")]
        [SerializeField] private CanvasGroup exitButtonCanvasGroup;
        [SerializeField] private Button exitButton;

        [Header("Animation")]
        [SerializeField] private float fadeDuration = 0.3f;

        /// <summary>
        /// Fired when the exit button is tapped.
        /// </summary>
        public event Action OnExitRequested;

        private void Awake()
        {
            if (exitButton != null)
                exitButton.onClick.AddListener(OnExitButtonClicked);

            HideAll();
        }

        private void OnDestroy()
        {
            if (exitButton != null)
                exitButton.onClick.RemoveListener(OnExitButtonClicked);
        }

        public void ShowPrompt(string text)
        {
            if (promptText != null)
                promptText.text = text;

            SetCanvasGroupVisible(promptCanvasGroup, true);
        }

        public void HidePrompt()
        {
            SetCanvasGroupVisible(promptCanvasGroup, false);
        }

        public void ShowExitButton()
        {
            SetCanvasGroupVisible(exitButtonCanvasGroup, true);
        }

        public void HideExitButton()
        {
            SetCanvasGroupVisible(exitButtonCanvasGroup, false);
        }

        public void HideAll()
        {
            HidePrompt();
            HideExitButton();
        }

        private void OnExitButtonClicked()
        {
            OnExitRequested?.Invoke();
        }

        private void SetCanvasGroupVisible(CanvasGroup group, bool visible)
        {
            if (group == null) return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
