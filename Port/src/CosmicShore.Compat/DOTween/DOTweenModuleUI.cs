using CosmicShore.Engine;
using CosmicShore.Engine.UI;

namespace DG.Tweening
{
    /// <summary>
    /// UI shortcut tweens (DOTween's UI module + the TextMeshPro shortcuts): CanvasGroup,
    /// Graphic (Image / Text / RawImage), Image fill, RectTransform anchors and size, Slider,
    /// ScrollRect, Outline/Shadow, TMP text. Each tween's target is the receiver.
    /// </summary>
    public static class DOTweenModuleUI
    {
        static T Target<T>(T t, object target) where T : Tween
        {
            t.target = target;
            return t;
        }

        // ── CanvasGroup ──

        public static TweenerCore<float> DOFade(this CanvasGroup target, float endValue, float duration)
            => Target(DOTween.To(() => target.alpha, a => target.alpha = a, endValue, duration), target);

        // ── Graphic (Image, Text, RawImage, …) ──

        public static TweenerCore<Color> DOColor(this Graphic target, Color endValue, float duration)
            => Target(DOTween.To(() => target.color, c => target.color = c, endValue, duration), target);

        public static TweenerCore<float> DOFade(this Graphic target, float endValue, float duration)
            => Target(DOTween.AlphaTween(() => target.color, c => target.color = c, endValue, duration), target);

        /// <summary>Tweens Image.fillAmount (end value clamped to 0..1).</summary>
        public static TweenerCore<float> DOFillAmount(this Image target, float endValue, float duration)
        {
            if (endValue > 1f) endValue = 1f; else if (endValue < 0f) endValue = 0f;
            return Target(DOTween.To(() => target.fillAmount, v => target.fillAmount = v, endValue, duration), target);
        }

        // ── Outline / Shadow ──

        public static TweenerCore<Color> DOColor(this Shadow target, Color endValue, float duration)
            => Target(DOTween.To(() => target.effectColor, c => target.effectColor = c, endValue, duration), target);

        public static TweenerCore<float> DOFade(this Shadow target, float endValue, float duration)
            => Target(DOTween.AlphaTween(() => target.effectColor, c => target.effectColor = c, endValue, duration), target);

        // ── TextMeshPro ──

        public static TweenerCore<Color> DOColor(this TMP_Text target, Color endValue, float duration)
            => Target(DOTween.To(() => target.color, c => target.color = c, endValue, duration), target);

        public static TweenerCore<float> DOFade(this TMP_Text target, float endValue, float duration)
            => Target(DOTween.AlphaTween(() => target.color, c => target.color = c, endValue, duration), target);

        // ── RectTransform ──

        public static TweenerCore<Vector2> DOAnchorPos(this RectTransform target, Vector2 endValue, float duration, bool snapping = false)
        {
            var t = DOTween.To(() => target.anchoredPosition, v => target.anchoredPosition = v, endValue, duration);
            t.snapping = snapping;
            return Target(t, target);
        }

        public static TweenerCore<float> DOAnchorPosX(this RectTransform target, float endValue, float duration, bool snapping = false)
        {
            var t = DOTween.To(() => target.anchoredPosition.x,
                               v => { var p = target.anchoredPosition; p.x = v; target.anchoredPosition = p; },
                               endValue, duration);
            t.snapping = snapping;
            return Target(t, target);
        }

        public static TweenerCore<float> DOAnchorPosY(this RectTransform target, float endValue, float duration, bool snapping = false)
        {
            var t = DOTween.To(() => target.anchoredPosition.y,
                               v => { var p = target.anchoredPosition; p.y = v; target.anchoredPosition = p; },
                               endValue, duration);
            t.snapping = snapping;
            return Target(t, target);
        }

        public static TweenerCore<Vector2> DOAnchorMax(this RectTransform target, Vector2 endValue, float duration, bool snapping = false)
        {
            var t = DOTween.To(() => target.anchorMax, v => target.anchorMax = v, endValue, duration);
            t.snapping = snapping;
            return Target(t, target);
        }

        public static TweenerCore<Vector2> DOAnchorMin(this RectTransform target, Vector2 endValue, float duration, bool snapping = false)
        {
            var t = DOTween.To(() => target.anchorMin, v => target.anchorMin = v, endValue, duration);
            t.snapping = snapping;
            return Target(t, target);
        }

        public static TweenerCore<Vector2> DOPivot(this RectTransform target, Vector2 endValue, float duration)
            => Target(DOTween.To(() => target.pivot, v => target.pivot = v, endValue, duration), target);

        public static TweenerCore<Vector2> DOSizeDelta(this RectTransform target, Vector2 endValue, float duration, bool snapping = false)
        {
            var t = DOTween.To(() => target.sizeDelta, v => target.sizeDelta = v, endValue, duration);
            t.snapping = snapping;
            return Target(t, target);
        }

        public static WaypointTweener DOPunchAnchorPos(this RectTransform target, Vector2 punch, float duration, int vibrato = 10, float elasticity = 1f, bool snapping = false)
        {
            var t = DOTween.Punch(() => { var p = target.anchoredPosition; return new Vector3(p.x, p.y, 0f); },
                                  v => target.anchoredPosition = new Vector2(v.x, v.y),
                                  new Vector3(punch.x, punch.y, 0f), duration, vibrato, elasticity);
            t.snapping = snapping;
            return Target(t, target);
        }

        public static WaypointTweener DOShakeAnchorPos(this RectTransform target, float duration, float strength = 100f, int vibrato = 10,
            float randomness = 90f, bool snapping = false, bool fadeOut = true, ShakeRandomnessMode randomnessMode = ShakeRandomnessMode.Full)
            => target.DOShakeAnchorPos(duration, new Vector2(strength, strength), vibrato, randomness, snapping, fadeOut, randomnessMode);

        public static WaypointTweener DOShakeAnchorPos(this RectTransform target, float duration, Vector2 strength, int vibrato = 10,
            float randomness = 90f, bool snapping = false, bool fadeOut = true, ShakeRandomnessMode randomnessMode = ShakeRandomnessMode.Full)
        {
            var t = DOTween.Shake(() => { var p = target.anchoredPosition; return new Vector3(p.x, p.y, 0f); },
                                  v => target.anchoredPosition = new Vector2(v.x, v.y),
                                  duration, new Vector3(strength.x, strength.y, 0f), vibrato, randomness, fadeOut, randomnessMode, true);
            t.snapping = snapping;
            return Target(t, target);
        }

        // ── Slider / ScrollRect ──

        public static TweenerCore<float> DOValue(this Slider target, float endValue, float duration, bool snapping = false)
        {
            var t = DOTween.To(() => target.value, v => target.value = v, endValue, duration);
            t.snapping = snapping;
            return Target(t, target);
        }

        public static TweenerCore<Vector2> DONormalizedPos(this ScrollRect target, Vector2 endValue, float duration, bool snapping = false)
        {
            var t = DOTween.To(() => new Vector2(target.horizontalNormalizedPosition, target.verticalNormalizedPosition),
                               v => { target.horizontalNormalizedPosition = v.x; target.verticalNormalizedPosition = v.y; },
                               endValue, duration);
            t.snapping = snapping;
            return Target(t, target);
        }

        public static TweenerCore<float> DOHorizontalNormalizedPos(this ScrollRect target, float endValue, float duration, bool snapping = false)
        {
            var t = DOTween.To(() => target.horizontalNormalizedPosition, v => target.horizontalNormalizedPosition = v, endValue, duration);
            t.snapping = snapping;
            return Target(t, target);
        }

        public static TweenerCore<float> DOVerticalNormalizedPos(this ScrollRect target, float endValue, float duration, bool snapping = false)
        {
            var t = DOTween.To(() => target.verticalNormalizedPosition, v => target.verticalNormalizedPosition = v, endValue, duration);
            t.snapping = snapping;
            return Target(t, target);
        }
    }
}
