using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;
using DG.Tweening;

namespace CosmicShore.Core
{
    /// <summary>
    /// Lives on an app-shell element (nav button, game card) and shows/hides its
    /// <c>ActiveIndicator</c> when the breadcrumb system lights or dismisses this target.
    ///
    /// Continuity law (C3): the indicator never pops in or out. It blooms (scale-from-near-zero
    /// + fade-in) on activate and withers (shrink + fade-out) on dismiss. Tweens use
    /// <c>SetUpdate(true)</c> so they still play while the menu pauses time.
    /// </summary>
    public class CallToActionTarget : MonoBehaviour
    {
        [SerializeField] public CallToActionTargetType TargetID;
        [SerializeField] GameObject ActiveIndicator;

        [Header("Continuity (C3) — bloom in / wither out")]
        [SerializeField, Min(0f)] float bloomDuration = 0.35f;
        [SerializeField, Min(0f)] float fadeDuration = 0.25f;
        [SerializeField] Ease bloomEase = Ease.OutBack;

        const float HiddenScale = 0.0001f;

        CanvasGroup _indicatorGroup;
        Vector3 _baseScale = Vector3.one;
        bool _baseScaleCached;
        Tween _tween;

        void Start()
        {
            CacheIndicator();

            if (CallToActionSystem.Instance == null)
            {
                CSDebug.LogWarning($"{nameof(CallToActionTarget)}: CallToActionSystem.Instance is null — skipping registration. GameObject: {gameObject.name}");
                return;
            }

            CallToActionSystem.Instance.RegisterCallToActionTarget(TargetID, WhenDutyCalls, WhenTheCallHasBeenAnswered);

            // Reflect the current state on (re)spawn WITHOUT animating — the breadcrumb persists
            // across scene loads, so re-entering the menu shouldn't replay the bloom.
            bool active = AmIActive();
            if (ActiveIndicator != null)
            {
                ActiveIndicator.SetActive(active);
                if (active) ShowImmediate();
            }
        }

        void OnDisable()
        {
            _tween?.Kill();
            _tween = null;
        }

        void CacheIndicator()
        {
            if (ActiveIndicator == null) return;
            if (!_baseScaleCached)
            {
                _baseScale = ActiveIndicator.transform.localScale;
                if (_baseScale == Vector3.zero) _baseScale = Vector3.one;
                _baseScaleCached = true;
            }
            if (_indicatorGroup == null)
                ActiveIndicator.TryGetComponent(out _indicatorGroup);
        }

        void WhenDutyCalls()
        {
            if (ActiveIndicator == null)
            {
                CSDebug.LogWarning($"CallToActionTarget does not have an ActiveIndicator set. GameObject Name: {gameObject.name}");
                return;
            }

            CacheIndicator();
            _tween?.Kill();

            ActiveIndicator.SetActive(true);
            ActiveIndicator.transform.localScale = _baseScale * HiddenScale;
            if (_indicatorGroup != null) _indicatorGroup.alpha = 0f;

            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(ActiveIndicator.transform.DOScale(_baseScale, bloomDuration).SetEase(bloomEase));
            if (_indicatorGroup != null)
                seq.Join(_indicatorGroup.DOFade(1f, bloomDuration * 0.7f));
            _tween = seq;
        }

        void WhenTheCallHasBeenAnswered()
        {
            if (ActiveIndicator == null) return;

            CacheIndicator();
            _tween?.Kill();

            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(ActiveIndicator.transform.DOScale(_baseScale * HiddenScale, fadeDuration).SetEase(Ease.InBack));
            if (_indicatorGroup != null)
                seq.Join(_indicatorGroup.DOFade(0f, fadeDuration));
            seq.OnComplete(ShowImmediateHidden);
            _tween = seq;
        }

        // Restores the indicator to its rest pose, then hides the GameObject — so the next
        // bloom starts clean and layout isn't left at near-zero scale.
        void ShowImmediateHidden()
        {
            ActiveIndicator.SetActive(false);
            ActiveIndicator.transform.localScale = _baseScale;
            if (_indicatorGroup != null) _indicatorGroup.alpha = 1f;
        }

        void ShowImmediate()
        {
            ActiveIndicator.transform.localScale = _baseScale;
            if (_indicatorGroup != null) _indicatorGroup.alpha = 1f;
        }

        bool AmIActive()
        {
            return CallToActionSystem.Instance != null && CallToActionSystem.Instance.IsCallToActionTargetActive(TargetID);
        }
    }
}
