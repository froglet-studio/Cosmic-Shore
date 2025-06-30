using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class BoostFillAnimator : MonoBehaviour
{
    [SerializeField] private Image boostFillImage;
    private Tween currentTween;

    /// <summary>
    /// Instantly set the fill amount (0 to 1).
    /// </summary>
    public void SetFill(float amount)
    {
        KillTween();
        boostFillImage.fillAmount = Mathf.Clamp01(amount);
    }

    /// <summary>
    /// Animate the fill to a specific target over duration.
    /// </summary>
    public void AnimateFillTo(float targetFill, float duration)
    {
        KillTween();
        targetFill = Mathf.Clamp01(targetFill);
        currentTween = boostFillImage.DOFillAmount(targetFill, duration).SetEase(Ease.Linear);
    }

    /// <summary>
    /// Animate draining from <paramref name="from"/> down to zero over <paramref name="duration"/>.
    /// </summary>
    public void AnimateFillDown(float duration, float from)
    {
        SetFill(from);
        AnimateFillTo(0f, duration);
    }

    /// <summary>
    /// Animate filling up from current (or a given start) up to <paramref name="to"/> over <paramref name="duration"/>.
    /// </summary>
    public void AnimateFillUp(float duration, float to = 1f)
    {
        AnimateFillTo(to, duration);
    }

    private void KillTween()
    {
        if (currentTween != null && currentTween.IsActive())
            currentTween.Kill();
    }
}
