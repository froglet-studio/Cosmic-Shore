using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Sizes every vessel by the warp field at its own position (Docs/WARP_FIELD.md): root scale
    /// = the vessel's authored root scale × <see cref="WarpFieldRuntime.ScaleAt"/>. It runs on
    /// EVERY peer for EVERY vessel — local, remote and AI alike — because a vessel's scale is not
    /// replicated (no shipped vessel syncs scale over its NetworkTransform) and does not need to
    /// be: it is a function of the position every peer already has, so a vessel flying toward the
    /// centre shrinks on everybody's screen at once.
    ///
    /// <para>Lives only while a field does. <see cref="WarpFieldRuntime.Activate"/> starts the
    /// driver; when the field has fully eased out the driver writes every vessel back to its
    /// authored scale and destroys itself, so a field-free session never runs it. Vessels are
    /// enumerated a few times a second (there is no vessel registry), never per frame.</para>
    /// </summary>
    public static class WarpFieldVesselScaler
    {
        const float RefreshSeconds = 0.25f;

        static Driver _driver;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _driver = null;

        internal static void EnsureRunning()
        {
            if (_driver || !Application.isPlaying) return;
            var go = new GameObject("[WarpFieldVesselScaler]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            _driver = go.AddComponent<Driver>();
        }

        sealed class Driver : MonoBehaviour
        {
            readonly List<VesselStatus> _vessels = new();
            // Authored ROOT scale per vessel, captured the first time the field sees it.
            readonly Dictionary<VesselStatus, Vector3> _authored = new();
            readonly List<VesselStatus> _gone = new();
            float _nextRefresh;

            // After the vessels moved (Update), before anything renders them.
            void LateUpdate()
            {
                if (!WarpFieldRuntime.IsActive)
                {
                    RestoreAll();
                    Destroy(gameObject);
                    return;
                }

                if (Time.unscaledTime >= _nextRefresh)
                {
                    _nextRefresh = Time.unscaledTime + RefreshSeconds;
                    _vessels.Clear();
                    _vessels.AddRange(FindObjectsByType<VesselStatus>(FindObjectsSortMode.None));
                    Prune();
                }

                for (int i = 0; i < _vessels.Count; i++)
                {
                    var vessel = _vessels[i];
                    if (!vessel) continue;
                    var root = vessel.transform;
                    if (!_authored.TryGetValue(vessel, out var authored))
                    {
                        authored = root.localScale;
                        _authored[vessel] = authored;
                    }
                    root.localScale = authored * WarpFieldRuntime.ScaleAt(root.position);
                }
            }

            void Prune()
            {
                _gone.Clear();
                foreach (var kv in _authored)
                    if (!kv.Key) _gone.Add(kv.Key);
                for (int i = 0; i < _gone.Count; i++) _authored.Remove(_gone[i]);
            }

            void RestoreAll()
            {
                foreach (var kv in _authored)
                    if (kv.Key) kv.Key.transform.localScale = kv.Value;
                _authored.Clear();
                _vessels.Clear();
            }

            void OnDestroy()
            {
                RestoreAll();
                if (_driver == this) _driver = null;
            }
        }
    }
}
