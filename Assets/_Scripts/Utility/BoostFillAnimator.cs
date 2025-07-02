using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class BoostFillAnimator : MonoBehaviour
{
    [SerializeField] private Image boostFillImage;
    private Tween currentTween;

    /// Instantly set the fill amount (0–1).
    public void SetFill(float amount)
    {
        KillTween();
        boostFillImage.fillAmount = Mathf.Clamp01(amount);
    }

    /// Animate to a target fill over duration, with a custom ease and optional callback.
    public void AnimateFillTo(float targetFill, float duration, Ease ease = Ease.Linear, System.Action onComplete = null)
    {
        KillTween();
        targetFill = Mathf.Clamp01(targetFill);
        currentTween = boostFillImage
            .DOFillAmount(targetFill, duration)
            .SetEase(ease)
            .OnComplete(() => onComplete?.Invoke());
    }

    /// Drains from `from` down to zero.
    public void AnimateFillDown(float duration, float from, Ease ease = Ease.OutQuad, System.Action onComplete = null)
    {
        SetFill(from);
        AnimateFillTo(0f, duration, ease, onComplete);
    }

    /// Fills from current (or 0) up to `to`.
    public void AnimateFillUp(float duration, float to = 1f, Ease ease = Ease.InOutSine, System.Action onComplete = null)
    {
        AnimateFillTo(to, duration, ease, onComplete);
    }

    private void KillTween()
    {
        if (currentTween != null && currentTween.IsActive())
            currentTween.Kill();
    }
}
