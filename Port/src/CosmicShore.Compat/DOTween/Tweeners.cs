using System;
using CosmicShore.Engine;

namespace DG.Tweening
{
    /// <summary>
    /// Value arithmetic for one tweenable type. Evaluation is UNCLAMPED so Back / Elastic
    /// overshoot reaches the target (DOTween behavior).
    /// </summary>
    internal abstract class ValuePlugin<T>
    {
        public abstract T Add(T a, T b);
        public abstract T Sub(T a, T b);
        public abstract T Scale(T a, float s);
        public virtual T Evaluate(T start, T change, float eased) => Add(start, Scale(change, eased));
        public virtual T Change(T start, T end) => Sub(end, start);
        public abstract float Distance(T a, T b);
        public virtual T Snap(T value) => value;
    }

    internal static class Plugins
    {
        public static readonly ValuePlugin<float> Float = new FloatPlugin();
        public static readonly ValuePlugin<double> Double = new DoublePlugin();
        public static readonly ValuePlugin<int> Int = new IntPlugin();
        public static readonly ValuePlugin<uint> UInt = new UIntPlugin();
        public static readonly ValuePlugin<long> Long = new LongPlugin();
        public static readonly ValuePlugin<ulong> ULong = new ULongPlugin();
        public static readonly ValuePlugin<Vector2> Vector2 = new Vector2Plugin();
        public static readonly ValuePlugin<Vector3> Vector3 = new Vector3Plugin();
        public static readonly ValuePlugin<Vector4> Vector4 = new Vector4Plugin();
        public static readonly ValuePlugin<Color> Color = new ColorPlugin();
        public static readonly ValuePlugin<Rect> Rect = new RectPlugin();

        sealed class FloatPlugin : ValuePlugin<float>
        {
            public override float Add(float a, float b) => a + b;
            public override float Sub(float a, float b) => a - b;
            public override float Scale(float a, float s) => a * s;
            public override float Distance(float a, float b) => MathF.Abs(b - a);
            public override float Snap(float v) => MathF.Round(v);
        }

        sealed class DoublePlugin : ValuePlugin<double>
        {
            public override double Add(double a, double b) => a + b;
            public override double Sub(double a, double b) => a - b;
            public override double Scale(double a, float s) => a * s;
            public override float Distance(double a, double b) => (float)Math.Abs(b - a);
            public override double Snap(double v) => Math.Round(v);
        }

        // Integer plugins interpolate in double and round (DOTween rounds int tweens).
        sealed class IntPlugin : ValuePlugin<int>
        {
            public override int Add(int a, int b) => a + b;
            public override int Sub(int a, int b) => a - b;
            public override int Scale(int a, float s) => (int)Math.Round(a * (double)s);
            public override int Evaluate(int start, int change, float eased) => (int)Math.Round(start + change * (double)eased);
            public override float Distance(int a, int b) => Math.Abs(b - a);
        }

        sealed class UIntPlugin : ValuePlugin<uint>
        {
            public override uint Add(uint a, uint b) => a + b;
            public override uint Sub(uint a, uint b) => a - b;
            public override uint Scale(uint a, float s) => (uint)Math.Max(0, Math.Round(a * (double)s));
            public override uint Change(uint start, uint end) => unchecked(end - start);
            public override uint Evaluate(uint start, uint change, float eased)
                => (uint)Math.Max(0d, Math.Round(start + (double)(int)change * eased));
            public override float Distance(uint a, uint b) => Math.Abs((long)b - a);
        }

        sealed class LongPlugin : ValuePlugin<long>
        {
            public override long Add(long a, long b) => a + b;
            public override long Sub(long a, long b) => a - b;
            public override long Scale(long a, float s) => (long)Math.Round(a * (double)s);
            public override long Evaluate(long start, long change, float eased) => (long)Math.Round(start + change * (double)eased);
            public override float Distance(long a, long b) => Math.Abs(b - a);
        }

        sealed class ULongPlugin : ValuePlugin<ulong>
        {
            public override ulong Add(ulong a, ulong b) => a + b;
            public override ulong Sub(ulong a, ulong b) => a - b;
            public override ulong Scale(ulong a, float s) => (ulong)Math.Max(0d, Math.Round(a * (double)s));
            public override ulong Evaluate(ulong start, ulong change, float eased)
                => (ulong)Math.Max(0d, Math.Round(start + (double)(long)change * eased));
            public override float Distance(ulong a, ulong b) => MathF.Abs((float)b - a);
        }

        sealed class Vector2Plugin : ValuePlugin<Vector2>
        {
            public override Vector2 Add(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
            public override Vector2 Sub(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
            public override Vector2 Scale(Vector2 a, float s) => new(a.x * s, a.y * s);
            public override float Distance(Vector2 a, Vector2 b) => MathF.Sqrt((b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y));
            public override Vector2 Snap(Vector2 v) => new(MathF.Round(v.x), MathF.Round(v.y));
        }

        sealed class Vector3Plugin : ValuePlugin<Vector3>
        {
            public override Vector3 Add(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
            public override Vector3 Sub(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
            public override Vector3 Scale(Vector3 a, float s) => new(a.x * s, a.y * s, a.z * s);
            public override float Distance(Vector3 a, Vector3 b)
            {
                float x = b.x - a.x, y = b.y - a.y, z = b.z - a.z;
                return MathF.Sqrt(x * x + y * y + z * z);
            }
            public override Vector3 Snap(Vector3 v) => new(MathF.Round(v.x), MathF.Round(v.y), MathF.Round(v.z));
        }

        sealed class Vector4Plugin : ValuePlugin<Vector4>
        {
            public override Vector4 Add(Vector4 a, Vector4 b) => new(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
            public override Vector4 Sub(Vector4 a, Vector4 b) => new(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
            public override Vector4 Scale(Vector4 a, float s) => new(a.x * s, a.y * s, a.z * s, a.w * s);
            public override float Distance(Vector4 a, Vector4 b)
            {
                float x = b.x - a.x, y = b.y - a.y, z = b.z - a.z, w = b.w - a.w;
                return MathF.Sqrt(x * x + y * y + z * z + w * w);
            }
            public override Vector4 Snap(Vector4 v) => new(MathF.Round(v.x), MathF.Round(v.y), MathF.Round(v.z), MathF.Round(v.w));
        }

        sealed class ColorPlugin : ValuePlugin<Color>
        {
            public override Color Add(Color a, Color b) => new(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
            public override Color Sub(Color a, Color b) => new(a.r - b.r, a.g - b.g, a.b - b.b, a.a - b.a);
            public override Color Scale(Color a, float s) => new(a.r * s, a.g * s, a.b * s, a.a * s);
            public override float Distance(Color a, Color b)
            {
                float r = b.r - a.r, g = b.g - a.g, bl = b.b - a.b, al = b.a - a.a;
                return MathF.Sqrt(r * r + g * g + bl * bl + al * al);
            }
        }

        sealed class RectPlugin : ValuePlugin<Rect>
        {
            public override Rect Add(Rect a, Rect b) => new(a.x + b.x, a.y + b.y, a.width + b.width, a.height + b.height);
            public override Rect Sub(Rect a, Rect b) => new(a.x - b.x, a.y - b.y, a.width - b.width, a.height - b.height);
            public override Rect Scale(Rect a, float s) => new(a.x * s, a.y * s, a.width * s, a.height * s);
            public override float Distance(Rect a, Rect b)
            {
                float x = b.x - a.x, y = b.y - a.y;
                return MathF.Sqrt(x * x + y * y);
            }
            public override Rect Snap(Rect v) => new(MathF.Round(v.x), MathF.Round(v.y), MathF.Round(v.width), MathF.Round(v.height));
        }
    }

    /// <summary>
    /// Euler-angle plugin for rotation tweens. <see cref="RotateMode.Fast"/> wraps each
    /// axis delta into [-180, 180] (never more than half a turn per axis);
    /// <see cref="RotateMode.FastBeyond360"/> keeps the raw delta.
    /// </summary>
    internal sealed class EulerPlugin : ValuePlugin<Vector3>
    {
        readonly bool _wrap;
        public EulerPlugin(bool wrap) => _wrap = wrap;
        public override Vector3 Add(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public override Vector3 Sub(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public override Vector3 Scale(Vector3 a, float s) => new(a.x * s, a.y * s, a.z * s);
        public override Vector3 Change(Vector3 start, Vector3 end)
        {
            var d = Sub(end, start);
            return _wrap ? new Vector3(Wrap(d.x), Wrap(d.y), Wrap(d.z)) : d;
        }
        static float Wrap(float a)
        {
            a %= 360f;
            if (a > 180f) a -= 360f;
            else if (a < -180f) a += 360f;
            return a;
        }
        public override float Distance(Vector3 a, Vector3 b) => Plugins.Vector3.Distance(a, Change(a, b));
        public override Vector3 Snap(Vector3 v) => Plugins.Vector3.Snap(v);
    }

    /// <summary>
    /// The generic value tweener: getter/setter pair driven from a captured start value to an
    /// end value. (DOTween's TweenerCore carries three type args; the port only needs one.)
    /// </summary>
    public sealed class TweenerCore<T> : Tweener
    {
        internal DOGetter<T> getter;
        internal DOSetter<T> setter;
        internal ValuePlugin<T> plugin;
        public T startValue;
        public T endValue;
        public T changeValue;
        internal bool snapping;
        internal bool startValueSet;   // From(): start value fixed before startup
        internal bool relativeApplied;
        internal Action<TweenerCore<T>> onStartupInternal;

        internal TweenerCore(DOGetter<T> getter, DOSetter<T> setter, T endValue, float duration, ValuePlugin<T> plugin)
        {
            this.getter = getter;
            this.setter = setter;
            this.endValue = endValue;
            this.duration = duration < 0f ? 0f : duration;
            this.plugin = plugin;
        }

        internal override void Startup()
        {
            if (!startValueSet) startValue = getter();
            onStartupInternal?.Invoke(this);
            if (isRelative && !isFrom && !relativeApplied)
            {
                endValue = plugin.Add(startValue, endValue);
                relativeApplied = true;
            }
            changeValue = plugin.Change(startValue, endValue);
            if (isSpeedBased)
            {
                float speed = duration;
                duration = speed > 0f ? plugin.Distance(startValue, endValue) / speed : 0f;
                isSpeedBased = false;
            }
        }

        internal override void ApplyAt(int loopIndex, float loopPosition)
        {
            float e = EasedProgress(loopIndex, loopPosition);
            T start = startValue;
            if (loopType == LoopType.Incremental && loopIndex > 0)
                start = plugin.Add(start, plugin.Scale(changeValue, loopIndex));
            T value = plugin.Evaluate(start, changeValue, e);
            if (snapping) value = plugin.Snap(value);
            setter(value);
        }

        /// <summary>Make this a FROM tween: jump to <paramref name="fromValue"/> now and tween back to the current value.</summary>
        internal void SetFrom(T fromValue, bool setImmediately, bool relative)
        {
            if (isSequenced || startupDone) return;
            T current = getter();
            T from = relative ? plugin.Add(current, fromValue) : fromValue;
            isFrom = true;
            startValue = from;
            endValue = current;
            startValueSet = true;
            if (setImmediately) setter(from);
        }

        public TweenerCore<T> ChangeEndValue(T newEndValue, bool snapStartValue = false)
            => ChangeEndValue(newEndValue, -1f, snapStartValue);

        public TweenerCore<T> ChangeEndValue(T newEndValue, float newDuration, bool snapStartValue = false)
        {
            if (!active) return this;
            if (snapStartValue && startupDone) { startValue = getter(); }
            endValue = newEndValue;
            if (newDuration > 0f) duration = newDuration;
            if (startupDone) changeValue = plugin.Change(startValue, endValue);
            return this;
        }

        public TweenerCore<T> ChangeStartValue(T newStartValue, float newDuration = -1f)
        {
            if (!active) return this;
            startValue = newStartValue;
            startValueSet = true;
            if (newDuration > 0f) duration = newDuration;
            if (startupDone) changeValue = plugin.Change(startValue, endValue);
            return this;
        }

        public TweenerCore<T> ChangeValues(T newStartValue, T newEndValue, float newDuration = -1f)
        {
            if (!active) return this;
            startValue = newStartValue;
            startValueSet = true;
            endValue = newEndValue;
            if (newDuration > 0f) duration = newDuration;
            if (startupDone) changeValue = plugin.Change(startValue, endValue);
            return this;
        }
    }

    /// <summary>
    /// Multi-segment Vector3 tweener used by the Punch and Shake families: a path of offsets
    /// (relative to the value captured at startup) visited over per-segment durations, the
    /// tween's ease applied within each segment. The path always ends at offset zero, so the
    /// target returns to its starting value.
    /// </summary>
    public sealed class WaypointTweener : Tweener
    {
        readonly DOGetter<Vector3> _getter;
        readonly DOSetter<Vector3> _setter;
        readonly Vector3[] _offsets;
        readonly float[] _segmentEnds; // cumulative, normalized to 0..1 of duration
        Vector3 _start;
        int _lastSegment = -1;
        internal bool snapping;

        internal WaypointTweener(DOGetter<Vector3> getter, DOSetter<Vector3> setter, Vector3[] offsets, float[] segmentDurations, float duration)
        {
            _getter = getter;
            _setter = setter;
            _offsets = offsets;
            this.duration = duration < 0f ? 0f : duration;
            _segmentEnds = new float[segmentDurations.Length];
            float total = 0f;
            foreach (var d in segmentDurations) total += d;
            if (total <= 0f) total = 1f;
            float acc = 0f;
            for (int i = 0; i < segmentDurations.Length; i++)
            {
                acc += segmentDurations[i] / total;
                _segmentEnds[i] = acc;
            }
            if (_segmentEnds.Length > 0) _segmentEnds[^1] = 1f;
        }

        internal override void Startup() => _start = _getter();

        internal override void ApplyAt(int loopIndex, float loopPosition)
        {
            bool mirrored = loopType == LoopType.Yoyo && (loopIndex & 1) == 1;
            float p = duration <= 0f ? (mirrored ? 0f : 1f) : Math.Clamp((mirrored ? duration - loopPosition : loopPosition) / duration, 0f, 1f);
            int seg = 0;
            while (seg < _segmentEnds.Length - 1 && p > _segmentEnds[seg]) seg++;
            float segStart = seg == 0 ? 0f : _segmentEnds[seg - 1];
            float segLen = _segmentEnds[seg] - segStart;
            float local = segLen <= 0f ? 1f : Math.Clamp((p - segStart) / segLen, 0f, 1f);
            float e = Core.Easing.EaseManager.Evaluate(easeType, customEase, local, 1f, easeOvershootOrAmplitude, easePeriod);
            Vector3 from = seg == 0 ? new Vector3(0f, 0f, 0f) : _offsets[seg - 1];
            Vector3 to = _offsets[seg];
            var off = new Vector3(from.x + (to.x - from.x) * e, from.y + (to.y - from.y) * e, from.z + (to.z - from.z) * e);
            var value = new Vector3(_start.x + off.x, _start.y + off.y, _start.z + off.z);
            if (snapping) value = Plugins.Vector3.Snap(value);
            _setter(value);
            if (seg != _lastSegment)
            {
                _lastSegment = seg;
                if (onWaypointChange != null && !isSequenced) onWaypointChange(seg);
            }
        }

        // ── Path generators (documented semantics, first-party formulation) ──

        /// <summary>
        /// Punch: the target is pushed by <paramref name="punch"/> then oscillates back to its
        /// start like a damped spring. <paramref name="vibrato"/> sets the oscillation count per
        /// second; <paramref name="elasticity"/> (0..1) scales how far each backward swing goes
        /// past the start (1 = full opposite swing, 0 = only back to start). Later swings are
        /// slower and smaller; the last segment settles at zero.
        /// </summary>
        internal static void BuildPunch(Vector3 punch, float duration, int vibrato, float elasticity,
                                        out Vector3[] offsets, out float[] segments)
        {
            elasticity = Math.Clamp(elasticity, 0f, 1f);
            int count = Math.Max(2, (int)(vibrato * duration));
            offsets = new Vector3[count];
            segments = new float[count];
            float strength = punch.magnitude;
            Vector3 dir = strength > 0f ? punch * (1f / strength) : new Vector3(0f, 0f, 0f);
            for (int i = 0; i < count; i++)
            {
                segments[i] = i + 1; // progressively longer swings
                if (i == count - 1) { offsets[i] = new Vector3(0f, 0f, 0f); continue; }
                float amp = strength * (1f - (float)i / count);
                offsets[i] = i == 0 ? punch
                    : (i & 1) == 1 ? dir * (-amp * elasticity)
                    : dir * amp;
            }
        }

        /// <summary>
        /// Shake: <paramref name="vibrato"/> random offsets per second within
        /// <paramref name="strength"/> (per axis), each successive direction roughly opposite
        /// the previous one, deviated by up to <paramref name="randomness"/> degrees. With
        /// <paramref name="fadeOut"/> the magnitude decays linearly to zero; the path always ends
        /// at zero. <paramref name="ignoreZ"/> keeps the shake in the XY plane.
        /// </summary>
        internal static void BuildShake(Vector3 strength, float duration, int vibrato, float randomness,
                                        bool fadeOut, bool ignoreZ, ShakeRandomnessMode mode,
                                        out Vector3[] offsets, out float[] segments)
        {
            int count = Math.Max(2, (int)(vibrato * duration));
            offsets = new Vector3[count];
            segments = new float[count];
            randomness = Math.Clamp(randomness, 0f, 180f);
            float angle = CosmicShore.Engine.Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                segments[i] = 1f;
                if (i == count - 1) { offsets[i] = new Vector3(0f, 0f, 0f); break; }
                float decay = fadeOut ? 1f - (float)i / count : 1f;
                float spread = mode == ShakeRandomnessMode.Harmonic ? randomness * 0.5f : randomness;
                if (i > 0) angle = angle + 180f + CosmicShore.Engine.Random.Range(-spread, spread);
                float rad = angle * (MathF.PI / 180f);
                Vector3 d;
                if (ignoreZ)
                {
                    d = new Vector3(MathF.Cos(rad), MathF.Sin(rad), 0f);
                }
                else
                {
                    float elev = CosmicShore.Engine.Random.Range(-spread, spread) * 0.5f * (MathF.PI / 180f);
                    d = new Vector3(MathF.Cos(rad) * MathF.Cos(elev), MathF.Sin(rad) * MathF.Cos(elev), MathF.Sin(elev));
                }
                offsets[i] = new Vector3(d.x * strength.x * decay, d.y * strength.y * decay, d.z * strength.z * decay);
            }
        }
    }
}
