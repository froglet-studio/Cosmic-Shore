using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay.Audio
{
    /// <summary>
    /// Activates the vessel's FMOD <see cref="StudioListener"/> only on the
    /// local player's ship.
    ///
    /// Every vessel prefab carries a <see cref="StudioListener"/> so that FMOD
    /// 3D audio can be heard relative to the ship's position and facing. But
    /// FMOD treats every active <see cref="StudioListener"/> as a distinct
    /// listener (mixing by nearest, up to <c>FMOD.CONSTANTS.MAX_LISTENERS</c>),
    /// so in multiplayer / AI scenes the remote and AI ships' listeners would
    /// pollute the mix. This gate keeps the listener disabled on every vessel
    /// until ownership resolves, then enables it ONLY when this is the local
    /// user's vessel - leaving exactly one active FMOD listener: the player's.
    ///
    /// The prefab's <see cref="StudioListener"/> ships disabled, so there is
    /// never a frame where multiple listeners are live during spawn.
    /// <see cref="IVesselStatus.IsLocalUser"/> is not known until
    /// <c>vessel.Initialize(player)</c> has run, so activation is deferred in
    /// <see cref="Update"/> until <see cref="IVesselStatus.Player"/> is set.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StudioListener))]
    public class ShipStudioListenerGate : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField, Tooltip("Log activation to the console.")]
        bool debugLog = false;

        StudioListener _listener;
        IVesselStatus _status;
        bool _resolved;   // true once IsLocalUser is known

        void Awake()
        {
            _listener = GetComponent<StudioListener>();
            _status = GetComponent<IVesselStatus>();

            // Stay silent until we confirm this is the local player's vessel.
            if (_listener != null)
                _listener.enabled = false;
        }

        void Update()
        {
            if (_resolved) return;

            // IVesselStatus.Player is null until vessel.Initialize(player) runs.
            if (_status?.Player == null) return;

            _resolved = true;

            if (_status.IsLocalUser)
                Activate();
            // else: remote / AI - leave the listener disabled.
        }

        void OnEnable()
        {
            // Re-activate if the vessel is re-enabled after a swap, but only
            // if we already resolved ownership as local.
            if (_resolved && _status is { IsLocalUser: true })
                Activate();
        }

        void Activate()
        {
            if (_listener == null || _listener.enabled) return;

            _listener.enabled = true;

            if (debugLog)
                Debug.Log($"[ShipStudioListenerGate] '{name}': FMOD StudioListener ACTIVATED (local player).");
        }
    }
}
