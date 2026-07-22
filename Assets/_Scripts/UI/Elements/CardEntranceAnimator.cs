using DG.Tweening;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Shared staggered scale + fade entrance for score widgets. Returns the
    /// DOTween <see cref="Sequence"/> so the caller can kill it on destroy - the
    /// only state lives with the caller, so this stays a stateless helper.
    /// Used by <see cref="PlayerScoreEntry"/> and <see cref="PlayerScoreCard"/>.
    /// </summary>
    public static class CardEntranceAnimator
    {
        /// <summary>
        /// Plays the entrance on <paramref name="target"/> (scale) and
        /// <paramref name="canvasGroup"/> (fade). Caller ensures both are non-null.
        /// </summary>
        public static Sequence Play(Transform target, CanvasGroup canvasGroup,
            HUDAnimationSettingsSO settings, int staggerIndex)
        {
            float duration = settings ? settings.cardEntranceDuration : 0.3f;
            float startScale = settings ? settings.cardEntranceStartScale : 0.6f;
            var ease = settings ? settings.cardEntranceEase : Ease.OutBack;
            float stagger = settings ? settings.cardEntranceStagger : 0.08f;
            bool unscaled = settings == null || settings.useUnscaledTime;

            canvasGroup.alpha = 0f;
            target.localScale = Vector3.one * startScale;

            return DOTween.Sequence()
                .AppendInterval(stagger * staggerIndex)
                .Append(target.DOScale(1f, duration).SetEase(ease))
                .Join(canvasGroup.DOFade(1f, duration))
                .SetUpdate(unscaled);
        }
    }
}
