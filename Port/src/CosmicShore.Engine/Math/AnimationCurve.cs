using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>Original-contract animation keyframe: time/value plus Hermite tangents.</summary>
    [Serializable]
    public struct Keyframe
    {
        public float time;
        public float value;
        public float inTangent;
        public float outTangent;

        public Keyframe(float time, float value)
        {
            this.time = time;
            this.value = value;
            inTangent = 0f;
            outTangent = 0f;
        }

        public Keyframe(float time, float value, float inTangent, float outTangent)
        {
            this.time = time;
            this.value = value;
            this.inTangent = inTangent;
            this.outTangent = outTangent;
        }
    }

    /// <summary>
    /// Original-contract animation curve: sorted keyframes evaluated with cubic Hermite
    /// interpolation between keys, clamped to the end values outside the key range
    /// (the original's default WrapMode.ClampForever behavior — the only mode ported
    /// code uses). Factories: <see cref="EaseInOut"/> (zero tangents — smooth-step
    /// feel), <see cref="Linear"/>, <see cref="Constant"/>.
    /// </summary>
    [Serializable]
    public class AnimationCurve
    {
        readonly List<Keyframe> _keys = new();

        public AnimationCurve() { }

        public AnimationCurve(params Keyframe[] keys)
        {
            if (keys == null) return;
            _keys.AddRange(keys);
            SortKeys();
        }

        /// <summary>Keys in time order (copy on get, replace on set — original contract).</summary>
        public Keyframe[] keys
        {
            get => _keys.ToArray();
            set
            {
                _keys.Clear();
                if (value != null) _keys.AddRange(value);
                SortKeys();
            }
        }

        public int length => _keys.Count;

        public Keyframe this[int index] => _keys[index];

        /// <summary>Insert a key, keeping keys time-sorted. Returns the key's index.</summary>
        public int AddKey(float time, float value) => AddKey(new Keyframe(time, value));

        public int AddKey(Keyframe key)
        {
            _keys.Add(key);
            SortKeys();
            return _keys.IndexOf(key);
        }

        public void RemoveKey(int index) => _keys.RemoveAt(index);

        /// <summary>
        /// Evaluate the curve at <paramref name="time"/>. Before the first key /
        /// after the last, the end value is held (clamp).
        /// </summary>
        public float Evaluate(float time)
        {
            int count = _keys.Count;
            if (count == 0) return 0f;
            if (count == 1 || time <= _keys[0].time) return _keys[0].value;
            if (time >= _keys[count - 1].time) return _keys[count - 1].value;

            // Find the bracketing segment (few keys in practice — linear walk).
            int i = 0;
            while (i < count - 2 && time >= _keys[i + 1].time) i++;

            Keyframe k0 = _keys[i], k1 = _keys[i + 1];
            float dt = k1.time - k0.time;
            if (dt <= 1e-12f) return k1.value;

            float t = (time - k0.time) / dt;
            float t2 = t * t;
            float t3 = t2 * t;

            // Cubic Hermite basis with tangents scaled by the segment length.
            float h00 = 2f * t3 - 3f * t2 + 1f;
            float h10 = t3 - 2f * t2 + t;
            float h01 = -2f * t3 + 3f * t2;
            float h11 = t3 - t2;

            return h00 * k0.value
                 + h10 * dt * k0.outTangent
                 + h01 * k1.value
                 + h11 * dt * k1.inTangent;
        }

        /// <summary>Two-key curve easing in and out (zero tangents at both ends — smooth-step feel).</summary>
        public static AnimationCurve EaseInOut(float timeStart, float valueStart, float timeEnd, float valueEnd)
            => new(new Keyframe(timeStart, valueStart, 0f, 0f), new Keyframe(timeEnd, valueEnd, 0f, 0f));

        /// <summary>Two-key straight line (tangents = slope).</summary>
        public static AnimationCurve Linear(float timeStart, float valueStart, float timeEnd, float valueEnd)
        {
            float dt = timeEnd - timeStart;
            float tangent = Mathf.Abs(dt) > 1e-12f ? (valueEnd - valueStart) / dt : 0f;
            return new AnimationCurve(
                new Keyframe(timeStart, valueStart, tangent, tangent),
                new Keyframe(timeEnd, valueEnd, tangent, tangent));
        }

        /// <summary>Two-key flat curve holding <paramref name="value"/>.</summary>
        public static AnimationCurve Constant(float timeStart, float timeEnd, float value)
            => new(new Keyframe(timeStart, value, 0f, 0f), new Keyframe(timeEnd, value, 0f, 0f));

        void SortKeys() => _keys.Sort(static (a, b) => a.time.CompareTo(b.time));
    }
}
