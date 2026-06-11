using TMPro;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Displays shape drawing results on the reveal camera screen.
    /// Attach to a World Space Canvas parented under the reveal camera, or to
    /// a Screen Space Overlay canvas that only activates during reveal.
    ///
    /// Wire ShapeDrawingManager.OnScoreCalculated to ShowScore().
    /// </summary>
    public class ShapeScoreDisplay : MonoBehaviour
    {
        [Header("Text Fields")]
        [SerializeField] TMP_Text shapeNameText;
        [SerializeField] TMP_Text timeText;
        [SerializeField] TMP_Text accuracyText;
        [SerializeField] TMP_Text starRatingText;

        [Header("Optional")]
        [SerializeField] GameObject panelRoot;

        void Awake()
        {
            if (panelRoot) panelRoot.SetActive(false);
        }

        public void ShowScore(ShapeScoreData score)
        {
            if (panelRoot) panelRoot.SetActive(true);

            if (shapeNameText)
                shapeNameText.text = score.ShapeName;

            if (timeText)
                timeText.text = $"{score.ElapsedTime:F1}s";

            if (accuracyText)
                accuracyText.text = $"{score.AccuracyPercent:F0}%";

            if (starRatingText)
                starRatingText.text = new string('*', score.StarRating);
        }

        public void Hide()
        {
            if (panelRoot) panelRoot.SetActive(false);
        }
    }
}
