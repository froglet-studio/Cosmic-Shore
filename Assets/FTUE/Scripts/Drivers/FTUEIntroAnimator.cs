using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening;

namespace CosmicShore.FTUE
{
    public class FTUEIntroAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image blackOverlay; // Fullscreen black panel
        [SerializeField] private RectTransform captainImage;
        [SerializeField] private RectTransform textBoxPanel;
        [SerializeField] private TutorialUIView tutorialUIView;

        [Header("Timing")]
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float moveDuration = 0.5f;
        [SerializeField] private float delayBeforeStart = 0.5f;

        public IEnumerator PlayIntro(System.Action onComplete)
        {
            tutorialUIView.ToggleFTUECanvas(true);
            yield return new WaitForSecondsRealtime(delayBeforeStart);

            yield return blackOverlay
                .DOFade(0.7f, fadeDuration)
                .SetUpdate(true)
                .WaitForCompletion();

            yield return captainImage
                .DOLocalMoveX(791f, moveDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .WaitForCompletion();

            yield return textBoxPanel
                .GetComponent<CanvasGroup>()
                .DOFade(1f, fadeDuration)
                .SetUpdate(true)
                .WaitForCompletion();

            onComplete?.Invoke();
        }

        public IEnumerator PlayOutro(System.Action onComplete)
        {
            yield return textBoxPanel.GetComponent<CanvasGroup>().DOFade(0f, 0.3f).SetUpdate(true)
                .WaitForCompletion();

            yield return captainImage.DOLocalMoveX(1201f, moveDuration).SetEase(Ease.InBack).SetUpdate(true).WaitForCompletion(); ;

            yield return blackOverlay.DOFade(0.7f, 0.7f).SetUpdate(true)
                .WaitForCompletion();
            tutorialUIView.textDisplay.text = "";
            tutorialUIView.ToggleFTUECanvas(false);

            onComplete?.Invoke();
        }

    }
}
