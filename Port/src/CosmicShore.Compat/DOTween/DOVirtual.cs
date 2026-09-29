using CosmicShore.Engine;
using DG.Tweening.Core.Easing;

namespace DG.Tweening
{
    /// <summary>
    /// Virtual tweens: tween a value that lives nowhere but the callback, plus eased-value
    /// helpers and delayed calls. The callback runs every time the value is applied.
    /// </summary>
    public static class DOVirtual
    {
        public static Tweener Float(float from, float to, float duration, TweenCallback<float> onVirtualUpdate)
        {
            float v = from;
            var t = DOTween.To(() => v, x => { v = x; onVirtualUpdate?.Invoke(x); }, to, duration);
            t.startValue = from;
            t.startValueSet = true;
            return t;
        }

        public static Tweener Int(int from, int to, float duration, TweenCallback<int> onVirtualUpdate)
        {
            int v = from;
            var t = DOTween.To(() => v, x => { v = x; onVirtualUpdate?.Invoke(x); }, to, duration);
            t.startValue = from;
            t.startValueSet = true;
            return t;
        }

        public static Tweener Vector2(Vector2 from, Vector2 to, float duration, TweenCallback<Vector2> onVirtualUpdate)
        {
            var v = from;
            var t = DOTween.To(() => v, x => { v = x; onVirtualUpdate?.Invoke(x); }, to, duration);
            t.startValue = from;
            t.startValueSet = true;
            return t;
        }

        public static Tweener Vector3(Vector3 from, Vector3 to, float duration, TweenCallback<Vector3> onVirtualUpdate)
        {
            var v = from;
            var t = DOTween.To(() => v, x => { v = x; onVirtualUpdate?.Invoke(x); }, to, duration);
            t.startValue = from;
            t.startValueSet = true;
            return t;
        }

        public static Tweener Color(Color from, Color to, float duration, TweenCallback<Color> onVirtualUpdate)
        {
            var v = from;
            var t = DOTween.To(() => v, x => { v = x; onVirtualUpdate?.Invoke(x); }, to, duration);
            t.startValue = from;
            t.startValueSet = true;
            return t;
        }

        /// <summary>Value between <paramref name="from"/> and <paramref name="to"/> at an eased 0..1 percentage.</summary>
        public static float EasedValue(float from, float to, float lifetimePercentage, Ease easeType)
            => from + (to - from) * EaseManager.Evaluate(easeType, null, lifetimePercentage, 1f,
                                                         DOTween.defaultEaseOvershootOrAmplitude, DOTween.defaultEasePeriod);

        public static float EasedValue(float from, float to, float lifetimePercentage, Ease easeType, float overshoot)
            => from + (to - from) * EaseManager.Evaluate(easeType, null, lifetimePercentage, 1f, overshoot, DOTween.defaultEasePeriod);

        public static float EasedValue(float from, float to, float lifetimePercentage, Ease easeType, float amplitude, float period)
            => from + (to - from) * EaseManager.Evaluate(easeType, null, lifetimePercentage, 1f, amplitude, period);

        public static float EasedValue(float from, float to, float lifetimePercentage, AnimationCurve easeCurve)
            => from + (to - from) * EaseManager.ToEaseFunction(easeCurve)(lifetimePercentage, 1f, 0f, 0f);

        public static Vector3 EasedValue(Vector3 from, Vector3 to, float lifetimePercentage, Ease easeType)
        {
            float e = EaseManager.Evaluate(easeType, null, lifetimePercentage, 1f,
                                           DOTween.defaultEaseOvershootOrAmplitude, DOTween.defaultEasePeriod);
            return new Vector3(from.x + (to.x - from.x) * e, from.y + (to.y - from.y) * e, from.z + (to.z - from.z) * e);
        }

        /// <summary>Calls <paramref name="callback"/> after <paramref name="delay"/> seconds (unscaled by default).</summary>
        public static Tween DelayedCall(float delay, TweenCallback callback, bool ignoreTimeScale = true)
            => DOTween.Sequence()
                .AppendInterval(delay)
                .OnStepComplete(callback)
                .SetUpdate(UpdateType.Normal, ignoreTimeScale)
                .SetAutoKill(true);
    }
}
