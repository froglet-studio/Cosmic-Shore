using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay.Audio
{
    /// <summary>
    /// Ship-mounted FMOD listener. Attach to the vessel root prefab.
    ///
    /// Problem solved:
    ///   The default <see cref="StudioListener"/> lives on the camera. 3D
    ///   spatialization (left/right panning, attenuation) is therefore
    ///   camera-relative — a crystal to your ship's right only pans right if
    ///   the camera is also facing your ship's forward direction.
    ///
    /// Solution:
    ///   This component owns a child GameObject with a <see cref="StudioListener"/>
    ///   (and matching Unity <see cref="AudioListener"/>). When the local player's
    ///   vessel is ready, it:
    ///     1. Disables the scene's existing camera-mounted listener.
    ///     2. Activates its own ship-mounted listener.
    ///   Because the child follows the ship's transform, all FMOD 3D sounds are
    ///   now panned and attenuated relative to the ship's position AND facing
    ///   direction. Skims from crystals on the right pan right; AI activity
    ///   behind you sounds behind you.
    ///
    ///   On destroy the camera listener is restored, so everything works
    ///   correctly in the menu, after game-over, and between scenes.
    ///
    /// Multiplayer:
    ///   Only the local user's vessel activates the listener. Remote ships and
    ///   AI ships have this component but it stays dormant. Because
    ///   <see cref="IVesselStatus.IsLocalUser"/> may not be set until after
    ///   <c>vessel.Initialize(player)</c> completes, activation is deferred in
    ///   <c>Update()</c> until the vessel knows its owner.
    ///
    /// Existing audio controllers (<see cref="ShipAudioController"/>,
    ///   <see cref="ProximityBoostAudioController"/>,
    ///   <see cref="DriftAudioController"/>) resolve the listener via
    ///   <c>FindFirstObjectByType&lt;StudioListener&gt;()</c>. Once the ship
    ///   listener is active, those calls return this ship-mounted one — no
    ///   changes to those controllers are needed.
    ///
    /// Offset:
    ///   <see cref="localOffset"/> lets you position the listener at a cockpit
    ///   or center-of-mass offset relative to the ship root, in local space.
    ///   Default (0, 0, 0) is fine for a symmetric vessel; adjust per-vessel
    ///   if needed.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShipAudioListenerController : MonoBehaviour
    {
        [Header("Listener Position")]
        [SerializeField, Tooltip(
            "Local-space offset from the vessel root where the listener sits. " +
            "Zero = ship origin. Use a small forward/up value (e.g. 0, 1, 2) " +
            "to simulate a cockpit position if desired.")]
        Vector3 localOffset = Vector3.zero;

        [Header("Debug")]
        [SerializeField, Tooltip("Log activation / deactivation to the console.")]
        bool debugLog = false;

        // ── Child GO that holds the FMOD + Unity listeners ──────────────────
        GameObject _listenerGO;
        StudioListener _shipListener;
        AudioListener _shipUnityListener;

        // ── Cached scene listeners that we displace ──────────────────────────
        StudioListener _cameraFmodListener;
        AudioListener _cameraUnityListener;

        // ── State ────────────────────────────────────────────────────────────
        IVesselStatus _status;
        bool _activated;
        bool _initResolved;   // true once IsLocalUser is known

        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            _status = GetComponent<IVesselStatus>();

            // Build the listener child immediately so it exists in the hierarchy,
            // but keep it disabled until we confirm this is the local player.
            _listenerGO = new GameObject("ShipAudioListener");
            _listenerGO.transform.SetParent(transform, worldPositionStays: false);
            _listenerGO.transform.localPosition = localOffset;
            _listenerGO.transform.localRotation = Quaternion.identity;

            _shipListener = _listenerGO.AddComponent<StudioListener>();
            _shipUnityListener = _listenerGO.AddComponent<AudioListener>();

            // Disabled until activation — prevents two active listeners.
            _listenerGO.SetActive(false);
        }

        void Update()
        {
            if (_initResolved) return;

            // IVesselStatus.Player is null until vessel.Initialize(player) runs.
            if (_status?.Player == null) return;

            _initResolved = true;

            if (_status.IsLocalUser)
                Activate();
            // else: remote / AI — stay dormant, never activate.
        }

        void OnDestroy()
        {
            if (_activated)
                Deactivate();

            if (_listenerGO != null)
                Destroy(_listenerGO);
        }

        void OnDisable()
        {
            // Guard against vessel being disabled mid-game (e.g. vessel swap).
            if (_activated)
                Deactivate();
        }

        void OnEnable()
        {
            // Re-activate if the vessel is re-enabled after a swap, but only
            // if we already resolved ownership as local.
            if (_initResolved && _status is { IsLocalUser: true } && !_activated)
                Activate();
        }

        // ── Activation / Deactivation ─────────────────────────────────────────

        void Activate()
        {
            if (_activated) return;

            // Find and disable the camera-mounted listeners.
            CacheAndDisableCameraListeners();

            // Enable our ship listener.
            _listenerGO.SetActive(true);
            _activated = true;

            if (debugLog)
                Debug.Log($"[ShipAudioListenerController] '{name}': ship listener ACTIVATED " +
                          $"(displaced camera listener: {(_cameraFmodListener != null ? _cameraFmodListener.gameObject.name : "none")}).");
        }

        void Deactivate()
        {
            if (!_activated) return;

            _listenerGO.SetActive(false);
            _activated = false;

            // Restore the camera listeners.
            RestoreCameraListeners();

            if (debugLog)
                Debug.Log($"[ShipAudioListenerController] '{name}': ship listener DEACTIVATED " +
                          $"(restored camera listener: {(_cameraFmodListener != null ? _cameraFmodListener.gameObject.name : "none")}).");
        }

        // ── Camera listener helpers ───────────────────────────────────────────

        void CacheAndDisableCameraListeners()
        {
            // Find FMOD listener. FMOD docs: only one StudioListener should be
            // active at a time. We disable the camera one before enabling ours.
#if UNITY_2023_1_OR_NEWER
            _cameraFmodListener = FindFirstObjectByType<StudioListener>();
#else
            _cameraFmodListener = FindObjectOfType<StudioListener>();
#endif
            if (_cameraFmodListener != null && _cameraFmodListener != _shipListener)
            {
                _cameraFmodListener.enabled = false;
                if (debugLog)
                    Debug.Log($"[ShipAudioListenerController] Disabled FMOD StudioListener on '{_cameraFmodListener.gameObject.name}'.");
            }
            else
            {
                _cameraFmodListener = null; // nothing to restore
            }

            // Also disable Unity's AudioListener so Unity doesn't warn about
            // two active listeners (the ship child will have one too).
#if UNITY_2023_1_OR_NEWER
            _cameraUnityListener = FindFirstObjectByType<AudioListener>();
#else
            _cameraUnityListener = FindObjectOfType<AudioListener>();
#endif
            if (_cameraUnityListener != null && _cameraUnityListener != _shipUnityListener)
            {
                _cameraUnityListener.enabled = false;
            }
            else
            {
                _cameraUnityListener = null;
            }
        }

        void RestoreCameraListeners()
        {
            if (_cameraFmodListener != null)
            {
                _cameraFmodListener.enabled = true;
                _cameraFmodListener = null;
            }

            if (_cameraUnityListener != null)
            {
                _cameraUnityListener.enabled = true;
                _cameraUnityListener = null;
            }
        }

        // ── Public surface ────────────────────────────────────────────────────

        /// <summary>
        /// The ship-mounted <see cref="StudioListener"/> — valid after
        /// <c>Awake</c>. Active only when this is the local player's vessel.
        /// </summary>
        public StudioListener ShipListener => _shipListener;

        /// <summary>
        /// True once the local-user handoff has completed and the ship listener
        /// is the active FMOD listener.
        /// </summary>
        public bool IsListenerActive => _activated;
    }
}
