using System;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Color-over-time ramp with separate color and alpha key tracks (E14), preserving
    /// the original engine's contract: defaults to a solid-white, fully-opaque ramp;
    /// <see cref="SetKeys"/> replaces both tracks; key array properties return copies
    /// so a captured snapshot (e.g. <c>VesselTrailCustomization</c>'s prefab-authored
    /// alpha curve) can't be mutated out from under the holder; <see cref="Evaluate"/>
    /// blends linearly between neighboring keys and clamps past the ends.
    /// </summary>
    public class Gradient
    {
        GradientColorKey[] _colorKeys =
        {
            new(Color.white, 0f),
            new(Color.white, 1f),
        };

        GradientAlphaKey[] _alphaKeys =
        {
            new(1f, 0f),
            new(1f, 1f),
        };

        public GradientColorKey[] colorKeys
        {
            get => (GradientColorKey[])_colorKeys.Clone();
            set => _colorKeys = SortedCopy(value, k => k.time);
        }

        public GradientAlphaKey[] alphaKeys
        {
            get => (GradientAlphaKey[])_alphaKeys.Clone();
            set => _alphaKeys = SortedCopy(value, k => k.time);
        }

        public void SetKeys(GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys)
        {
            this.colorKeys = colorKeys;
            this.alphaKeys = alphaKeys;
        }

        public Color Evaluate(float time)
        {
            time = Mathf.Clamp01(time);
            Color color = EvaluateColor(time);
            color.a = EvaluateAlpha(time);
            return color;
        }

        Color EvaluateColor(float time)
        {
            var keys = _colorKeys;
            if (keys.Length == 0) return Color.white;
            if (time <= keys[0].time) return keys[0].color;
            for (int i = 1; i < keys.Length; i++)
            {
                if (time > keys[i].time) continue;
                float span = keys[i].time - keys[i - 1].time;
                float t = span <= 0f ? 1f : (time - keys[i - 1].time) / span;
                return Color.Lerp(keys[i - 1].color, keys[i].color, t);
            }
            return keys[^1].color;
        }

        float EvaluateAlpha(float time)
        {
            var keys = _alphaKeys;
            if (keys.Length == 0) return 1f;
            if (time <= keys[0].time) return keys[0].alpha;
            for (int i = 1; i < keys.Length; i++)
            {
                if (time > keys[i].time) continue;
                float span = keys[i].time - keys[i - 1].time;
                float t = span <= 0f ? 1f : (time - keys[i - 1].time) / span;
                return Mathf.Lerp(keys[i - 1].alpha, keys[i].alpha, t);
            }
            return keys[^1].alpha;
        }

        static T[] SortedCopy<T>(T[] source, Func<T, float> time)
        {
            if (source is null || source.Length == 0) return Array.Empty<T>();
            var copy = (T[])source.Clone();
            Array.Sort(copy, (a, b) => time(a).CompareTo(time(b)));
            return copy;
        }
    }
}
