using System;
using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Displays shape drawing results after the player completes a shape.
    /// Place this inside the GameCanvas. Wire the TMP_Text fields and buttons
    /// in the Inspector.
    /// </summary>
    public class EndShapeDetailHUD : MonoBehaviour
    {
        [Header("Stats Display")]
        [SerializeField] TMP_Text shapeNameText;
        [SerializeField] TMP_Text elapsedTimeText;
        [SerializeField] TMP_Text parTimeText;
        [SerializeField] TMP_Text accuracyText;
        [SerializeField] TMP_Text starRatingText;

        [Header("Buttons")]
        [SerializeField] Button screenshotButton;
        [SerializeField] Button exitButton;

        public event Action OnExitPressed;
        public event Action OnScreenshotPressed;

        void OnEnable()
        {
            if (screenshotButton) screenshotButton.onClick.AddListener(HandleScreenshot);
            if (exitButton) exitButton.onClick.AddListener(HandleExit);
        }

        void OnDisable()
        {
            if (screenshotButton) screenshotButton.onClick.RemoveListener(HandleScreenshot);
            if (exitButton) exitButton.onClick.RemoveListener(HandleExit);
        }

        /// <summary>
        /// Populate stat fields and activate the panel.
        /// </summary>
        public void Show(ShapeScoreData score)
        {
            if (shapeNameText) shapeNameText.text = score.ShapeName;
            if (elapsedTimeText) elapsedTimeText.text = $"{score.ElapsedTime:F1}s";
            if (parTimeText) parTimeText.text = $"Par {score.ParTime:F1}s";
            if (accuracyText) accuracyText.text = $"{score.AccuracyPercent:F1}%";
            if (starRatingText) starRatingText.text = FormatStars(score.StarRating);

            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hide the panel.
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        void HandleScreenshot() => OnScreenshotPressed?.Invoke();
        void HandleExit() => OnExitPressed?.Invoke();

        static string FormatStars(int rating)
        {
            return rating switch
            {
                >= 5 => "★★★★★",
                4 => "★★★★☆",
                3 => "★★★☆☆",
                2 => "★★☆☆☆",
                _ => "★☆☆☆☆"
            };
        }
    }
}
