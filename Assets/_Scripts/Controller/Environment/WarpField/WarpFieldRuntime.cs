using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        // POLES (a crystal wormhole's attractor and repulsor, Docs/CRYSTAL_WORMHOLE.md). With any
        // registered the field is read around the POLES instead of its centre, and they compose as a
        // PRODUCT, each raised to its live amplitude: s = Π s_i^a_i — smooth everywhere (no crease where
        // one pole takes over from the other), and a pole at amplitude 0 is flat space. A pole keeps
        // its LAST position and amplitude once its transform is gone, so a pole retired with its world
        // goes on shaping the field until the field itself has eased out — no pop.
        static readonly List<Transform> _poles = new();
        static readonly List<Vector3> _poleLast = new();
        static readonly List<System.Func<float>> _poleAmplitude = new();
        static readonly List<float> _poleAmplitudeLast = new();

        // The eased weight is evaluated lazily from a start time, so the field needs no driver.
        static float _fromWeight;
        static float _toWeight;
        static float _startTime;
        static float _easeSeconds;

        /// <summary>The field in effect (possibly still easing out), or null.</summary>
        public static WarpFieldSO Field => _field;

        /// <summary>True from <see cref="Activate"/> until the field has fully eased out again.</summary>
        public static bool IsActive
        {
            get
            {
                _ = Weight;   // retires a field whose ease-out has finished
                return _field != null;
            }
        }

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
            float s;
            if (_poles.Count == 0)
            {
                s = _field.ScaleAt(worldPosition - Centre);
            }
            else
            {
                s = 1f;
                for (int i = 0; i < _poles.Count; i++)
                {
                    var pole = _poles[i];
                    if (pole)
                    {
                        _poleLast[i] = pole.position;
                        if (_poleAmplitude[i] != null) _poleAmplitudeLast[i] = Mathf.Max(0f, _poleAmplitude[i]());
                    }
                    float a = _poleAmplitudeLast[i];
                    if (a <= 0f) continue;
                    float si = Mathf.Max(1e-4f, _field.ScaleAt(worldPosition - _poleLast[i]));
                    s *= a == 1f ? si : Mathf.Pow(si, a);
                }
            }
            s = Mathf.Max(1e-4f, s);
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
            // A new world's field starts from its own centre: poles a previous world added (still
            // easing out with it) belong to a world that is gone.
            _poles.Clear();
            _poleLast.Clear();
            _poleAmplitude.Clear();
            _poleAmplitudeLast.Clear();
            _field = field;
            _centre = centre;
            _centreFallback = centre ? centre.position : Vector3.zero;
            _owner = owner;
            Ease(current, 1f, field.EaseSeconds);
            WarpFieldVesselScaler.EnsureRunning();
            if (!_sceneHooked)
            {
                // A field belongs to the world of one scene; a scene change ends it outright
                // (the load screen is the transition), never easing into the next scene's vessels.
                SceneManager.activeSceneChanged += OnActiveSceneChanged;
                _sceneHooked = true;
            }
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

        /// <summary>
        /// Read the live field around <paramref name="pole"/> (its position, and its amplitude if given,
        /// are read live). Once any pole is registered the field is read around its poles instead of its
        /// centre, composed as a product (see the field above). Lives until <see cref="RemovePole"/>, a
        /// new field, or the field's end — its transform's destruction only freezes it.
        /// </summary>
        public static void AddPole(Transform pole, System.Func<float> amplitude = null)
        {
            if (!pole || _field == null || _poles.Contains(pole)) return;
            _poles.Add(pole);
            _poleLast.Add(pole.position);
            _poleAmplitude.Add(amplitude);
            _poleAmplitudeLast.Add(amplitude != null ? Mathf.Max(0f, amplitude()) : 1f);
        }

        public static void RemovePole(Transform pole)
        {
            int i = _poles.IndexOf(pole);
            if (i < 0) return;
            _poles.RemoveAt(i);
            _poleLast.RemoveAt(i);
            _poleAmplitude.RemoveAt(i);
            _poleAmplitudeLast.RemoveAt(i);
        }

        /// <summary>How many poles the live field is read around (0 = its centre).</summary>
        public static int PoleCount => _poles.Count;

        static bool _sceneHooked;

        static void OnActiveSceneChanged(Scene from, Scene to) => Clear();

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
            _poles.Clear();
            _poleLast.Clear();
            _poleAmplitude.Clear();
            _poleAmplitudeLast.Clear();
            _fromWeight = _toWeight = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Clear();
            if (_sceneHooked) SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            _sceneHooked = false;
        }
    }
}
