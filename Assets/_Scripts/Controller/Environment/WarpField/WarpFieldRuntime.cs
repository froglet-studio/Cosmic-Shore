using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The one live warp field (Docs/WARP_FIELD.md): which <see cref="WarpFieldSO"/> is on, where
    /// its centre is, and how far it has eased in. Every consumer of a local length scale — a
    /// vessel's size and speed, its camera, the prisms it lays — asks <see cref="ScaleAt"/> for the
    /// scale at a world position and multiplies its own authored length by it. With no field live
    /// the answer is exactly 1, so every consumer is a no-op outside a warped world.
    ///
    /// <para><b>A pure function of position.</b> Nothing about the field is replicated: every
    /// client reads the same authored field at the positions it already has, so each vessel's
    /// scale agrees across the session without a byte of traffic.</para>
    ///
    /// <para><b>Continuity.</b> A field never switches on or off: its weight eases 0→1 on
    /// <see cref="Activate"/> and 1→0 on <see cref="Release"/> over
    /// <see cref="WarpFieldSO.EaseSeconds"/>, and the scale is interpolated GEOMETRICALLY
    /// (<c>s^w</c>), the way a length the eye reads proportionally should move.</para>
    ///
    /// <para>Owned by the scene's <see cref="Cell"/> (a config's <c>WarpField</c>); one field at a
    /// time — a second <see cref="Activate"/> takes over from the first, easing from wherever the
    /// weight currently is.</para>
    /// </summary>
    public static class WarpFieldRuntime
    {
        static WarpFieldSO _field;
        static Transform _centre;
        static Vector3 _centreFallback;
        static object _owner;

        // The eased weight is evaluated lazily from a start time, so the field needs no driver.
        static float _fromWeight;
        static float _toWeight;
        static float _startTime;
        static float _easeSeconds;

        /// <summary>The field in effect (possibly still easing out), or null.</summary>
        public static WarpFieldSO Field => _field;

        /// <summary>True while any field contributes (weight above zero).</summary>
        public static bool IsActive => _field != null && Weight > 0f;

        /// <summary>0..1: how far the field is in.</summary>
        public static float Weight
        {
            get
            {
                if (_field == null) return 0f;
                float t = _easeSeconds > 0f ? Mathf.Clamp01((Time.time - _startTime) / _easeSeconds) : 1f;
                t = t * t * (3f - 2f * t);
                float w = Mathf.Lerp(_fromWeight, _toWeight, t);
                if (w <= 0f && _toWeight <= 0f && t >= 1f) Clear();
                return w;
            }
        }

        /// <summary>The field's centre in world space.</summary>
        public static Vector3 Centre => _centre ? _centre.position : _centreFallback;

        /// <summary>
        /// The local length scale at <paramref name="worldPosition"/>: 1 with no field, else the
        /// field's value there raised to the current weight.
        /// </summary>
        public static float ScaleAt(Vector3 worldPosition)
        {
            if (_field == null) return 1f;
            float w = Weight;
            if (w <= 0f || _field == null) return 1f;
            float s = Mathf.Max(1e-4f, _field.ScaleAt(worldPosition - Centre));
            return w >= 1f ? s : Mathf.Pow(s, w);
        }

        /// <summary>
        /// Switch <paramref name="field"/> on, centred on <paramref name="centre"/> (its position is
        /// read live, so a moving centre carries the field). Eases in from the current weight.
        /// </summary>
        public static void Activate(object owner, WarpFieldSO field, Transform centre)
        {
            if (field == null || owner == null) return;
            float current = Weight;
            _field = field;
            _centre = centre;
            _centreFallback = centre ? centre.position : Vector3.zero;
            _owner = owner;
            Ease(current, 1f, field.EaseSeconds);
            CSDebug.LogVerbose(CSLogChannel.Ecology,
                $"[WarpField] {field.name} on, centred at {_centreFallback}, easing in over {field.EaseSeconds:F1}s.");
        }

        /// <summary>
        /// Ease the field out, if <paramref name="owner"/> is the one that switched it on (a stale
        /// owner releasing late never cancels the field that replaced it).
        /// </summary>
        public static void Release(object owner)
        {
            if (_field == null || owner == null || !ReferenceEquals(owner, _owner)) return;
            if (_centre) _centreFallback = _centre.position;
            _centre = null;   // the centre may be destroyed with the world it belonged to
            Ease(Weight, 0f, _field ? _field.EaseSeconds : 0f);
            CSDebug.LogVerbose(CSLogChannel.Ecology, "[WarpField] released, easing out.");
        }

        static void Ease(float from, float to, float seconds)
        {
            _fromWeight = from;
            _toWeight = to;
            _startTime = Time.time;
            _easeSeconds = Mathf.Max(0f, seconds);
        }

        static void Clear()
        {
            _field = null;
            _centre = null;
            _owner = null;
            _fromWeight = _toWeight = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Clear();
    }
}
