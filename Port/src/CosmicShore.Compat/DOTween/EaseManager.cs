using System;
using CosmicShore.Engine;

namespace DG.Tweening.Core.Easing
{
    /// <summary>
    /// Evaluates every <see cref="Ease"/> as the standard Robert Penner curve, with DOTween's
    /// documented parameterization: <c>overshootOrAmplitude</c> is the Back overshoot (default
    /// 1.70158) AND the Elastic amplitude (clamped to at least 1), <c>period</c> is the Elastic
    /// period (0 = the conventional 0.3·duration, 0.45·duration for InOut) and, for the Flash
    /// family, the flash power (-1..1) while overshoot is the flash count.
    /// Result is the eased progress: 0 at time 0, 1 at time == duration (may overshoot between).
    /// </summary>
    public static class EaseManager
    {
        const float PiOver2 = MathF.PI * 0.5f;
        const float TwoPi = MathF.PI * 2f;

        public static float Evaluate(Tween t, float time, float duration, float overshootOrAmplitude, float period)
            => Evaluate(t.easeType, t.customEase, time, duration, overshootOrAmplitude, period);

        public static float Evaluate(Ease easeType, EaseFunction customEase, float time, float duration,
                                     float overshootOrAmplitude, float period)
        {
            if (duration <= 0f) return 1f;
            switch (easeType)
            {
                case Ease.Unset:
                    return Evaluate(DOTween.defaultEaseType == Ease.Unset ? Ease.OutQuad : DOTween.defaultEaseType,
                                    customEase, time, duration, overshootOrAmplitude, period);
                case Ease.Linear:
                    return time / duration;
                case Ease.InSine:
                    return -MathF.Cos(time / duration * PiOver2) + 1f;
                case Ease.OutSine:
                    return MathF.Sin(time / duration * PiOver2);
                case Ease.InOutSine:
                    return -0.5f * (MathF.Cos(MathF.PI * time / duration) - 1f);
                case Ease.InQuad:
                    return (time /= duration) * time;
                case Ease.OutQuad:
                    return -(time /= duration) * (time - 2f);
                case Ease.InOutQuad:
                    if ((time /= duration * 0.5f) < 1f) return 0.5f * time * time;
                    return -0.5f * (--time * (time - 2f) - 1f);
                case Ease.InCubic:
                    return (time /= duration) * time * time;
                case Ease.OutCubic:
                    return (time = time / duration - 1f) * time * time + 1f;
                case Ease.InOutCubic:
                    if ((time /= duration * 0.5f) < 1f) return 0.5f * time * time * time;
                    return 0.5f * ((time -= 2f) * time * time + 2f);
                case Ease.InQuart:
                    return (time /= duration) * time * time * time;
                case Ease.OutQuart:
                    return -((time = time / duration - 1f) * time * time * time - 1f);
                case Ease.InOutQuart:
                    if ((time /= duration * 0.5f) < 1f) return 0.5f * time * time * time * time;
                    return -0.5f * ((time -= 2f) * time * time * time - 2f);
                case Ease.InQuint:
                    return (time /= duration) * time * time * time * time;
                case Ease.OutQuint:
                    return (time = time / duration - 1f) * time * time * time * time + 1f;
                case Ease.InOutQuint:
                    if ((time /= duration * 0.5f) < 1f) return 0.5f * time * time * time * time * time;
                    return 0.5f * ((time -= 2f) * time * time * time * time + 2f);
                case Ease.InExpo:
                    return time == 0f ? 0f : MathF.Pow(2f, 10f * (time / duration - 1f));
                case Ease.OutExpo:
                    return time >= duration ? 1f : -MathF.Pow(2f, -10f * time / duration) + 1f;
                case Ease.InOutExpo:
                    if (time == 0f) return 0f;
                    if (time >= duration) return 1f;
                    if ((time /= duration * 0.5f) < 1f) return 0.5f * MathF.Pow(2f, 10f * (time - 1f));
                    return 0.5f * (-MathF.Pow(2f, -10f * --time) + 2f);
                case Ease.InCirc:
                    return -(MathF.Sqrt(1f - (time /= duration) * time) - 1f);
                case Ease.OutCirc:
                    return MathF.Sqrt(1f - (time = time / duration - 1f) * time);
                case Ease.InOutCirc:
                    if ((time /= duration * 0.5f) < 1f) return -0.5f * (MathF.Sqrt(1f - time * time) - 1f);
                    return 0.5f * (MathF.Sqrt(1f - (time -= 2f) * time) + 1f);
                case Ease.InElastic:
                    return InElastic(time, duration, overshootOrAmplitude, period);
                case Ease.OutElastic:
                    return OutElastic(time, duration, overshootOrAmplitude, period);
                case Ease.InOutElastic:
                    return InOutElastic(time, duration, overshootOrAmplitude, period);
                case Ease.InBack:
                    return (time /= duration) * time * ((overshootOrAmplitude + 1f) * time - overshootOrAmplitude);
                case Ease.OutBack:
                    return (time = time / duration - 1f) * time * ((overshootOrAmplitude + 1f) * time + overshootOrAmplitude) + 1f;
                case Ease.InOutBack:
                {
                    float s = overshootOrAmplitude * 1.525f;
                    if ((time /= duration * 0.5f) < 1f) return 0.5f * (time * time * ((s + 1f) * time - s));
                    return 0.5f * ((time -= 2f) * time * ((s + 1f) * time + s) + 2f);
                }
                case Ease.InBounce:
                    return 1f - OutBounce(duration - time, duration);
                case Ease.OutBounce:
                    return OutBounce(time, duration);
                case Ease.InOutBounce:
                    if (time < duration * 0.5f) return (1f - OutBounce(duration - time * 2f, duration)) * 0.5f;
                    return OutBounce(time * 2f - duration, duration) * 0.5f + 0.5f;
                case Ease.Flash:
                    return Flash(time, duration, overshootOrAmplitude, period, 0);
                case Ease.InFlash:
                    return Flash(time, duration, overshootOrAmplitude, period, 1);
                case Ease.OutFlash:
                    return Flash(time, duration, overshootOrAmplitude, period, 2);
                case Ease.InOutFlash:
                    return Flash(time, duration, overshootOrAmplitude, period, 3);
                case Ease.INTERNAL_Zero:
                    return 1f;
                case Ease.INTERNAL_Custom:
                    return customEase != null ? customEase(time, duration, overshootOrAmplitude, period) : time / duration;
                default:
                    return -(time /= duration) * (time - 2f); // OutQuad
            }
        }

        /// <summary>Wraps an AnimationCurve as an ease: evaluated over the curve's own key span.</summary>
        public static EaseFunction ToEaseFunction(AnimationCurve curve)
        {
            if (curve == null) return null;
            return (time, duration, _, _) =>
            {
                float span = curve.length > 0 ? curve[curve.length - 1].time : 1f;
                float start = curve.length > 0 ? curve[0].time : 0f;
                float p = duration <= 0f ? 1f : time / duration;
                return curve.Evaluate(start + (span - start) * p);
            };
        }

        public static EaseFunction ToEaseFunction(Ease ease)
            => (time, duration, overshoot, period) => Evaluate(ease, null, time, duration, overshoot, period);

        public static bool IsFlashEase(Ease ease) => ease >= Ease.Flash && ease <= Ease.InOutFlash;

        static float ResolvePeriod(float period, float duration, float factor) => period == 0f ? duration * factor : period;

        static float InElastic(float time, float duration, float amplitude, float period)
        {
            if (time == 0f) return 0f;
            if ((time /= duration) >= 1f) return 1f;
            period = ResolvePeriod(period, duration, 0.3f);
            float s;
            if (amplitude < 1f) { amplitude = 1f; s = period / 4f; }
            else s = period / TwoPi * MathF.Asin(1f / amplitude);
            time -= 1f;
            return -(amplitude * MathF.Pow(2f, 10f * time) * MathF.Sin((time * duration - s) * TwoPi / period));
        }

        static float OutElastic(float time, float duration, float amplitude, float period)
        {
            if (time == 0f) return 0f;
            if ((time /= duration) >= 1f) return 1f;
            period = ResolvePeriod(period, duration, 0.3f);
            float s;
            if (amplitude < 1f) { amplitude = 1f; s = period / 4f; }
            else s = period / TwoPi * MathF.Asin(1f / amplitude);
            return amplitude * MathF.Pow(2f, -10f * time) * MathF.Sin((time * duration - s) * TwoPi / period) + 1f;
        }

        static float InOutElastic(float time, float duration, float amplitude, float period)
        {
            if (time == 0f) return 0f;
            if ((time /= duration * 0.5f) >= 2f) return 1f;
            period = ResolvePeriod(period, duration, 0.3f * 1.5f);
            float s;
            if (amplitude < 1f) { amplitude = 1f; s = period / 4f; }
            else s = period / TwoPi * MathF.Asin(1f / amplitude);
            if (time < 1f)
            {
                time -= 1f;
                return -0.5f * (amplitude * MathF.Pow(2f, 10f * time) * MathF.Sin((time * duration - s) * TwoPi / period));
            }
            time -= 1f;
            return amplitude * MathF.Pow(2f, -10f * time) * MathF.Sin((time * duration - s) * TwoPi / period) * 0.5f + 1f;
        }

        static float OutBounce(float time, float duration)
        {
            if ((time /= duration) < 1f / 2.75f) return 7.5625f * time * time;
            if (time < 2f / 2.75f) return 7.5625f * (time -= 1.5f / 2.75f) * time + 0.75f;
            if (time < 2.5f / 2.75f) return 7.5625f * (time -= 2.25f / 2.75f) * time + 0.9375f;
            return 7.5625f * (time -= 2.625f / 2.75f) * time + 0.984375f;
        }

        /// <summary>
        /// Flash family (approximation of the documented behavior): <paramref name="flashes"/>
        /// triangle flashes between start (0) and end (1) — an odd count ends on the end value,
        /// an even count back on the start value. <paramref name="power"/> in -1..1 shrinks
        /// (positive) or grows (negative) the flash amplitude toward the end of the tween.
        /// <paramref name="shape"/>: 0 linear, 1 in, 2 out, 3 in-out per flash.
        /// </summary>
        static float Flash(float time, float duration, float flashes, float power, int shape)
        {
            int steps = Math.Max(1, (int)MathF.Ceiling(flashes));
            float stepDur = duration / steps;
            int idx = Math.Min((int)(time / stepDur), steps - 1);
            float local = Math.Clamp((time - idx * stepDur) / stepDur, 0f, 1f);
            local = shape switch
            {
                1 => local * local,
                2 => 1f - (1f - local) * (1f - local),
                3 => local < 0.5f ? 2f * local * local : 1f - 2f * (1f - local) * (1f - local),
                _ => local,
            };
            float v = idx % 2 == 0 ? local : 1f - local;
            float final = steps % 2 == 1 ? 1f : 0f;
            power = Math.Clamp(power, -1f, 1f);
            if (power != 0f)
            {
                float p = Math.Clamp(time / duration, 0f, 1f);
                float envelope = power > 0f ? 1f - power * p : 1f + power * (1f - p);
                v = final + (v - final) * envelope;
            }
            return v;
        }
    }
}
