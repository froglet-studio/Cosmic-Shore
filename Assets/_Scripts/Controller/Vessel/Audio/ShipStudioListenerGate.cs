using FMODUnity;
using CosmicShore.Utility;
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

        void Awake()
        {
            _listener = GetComponent<StudioListener>();
            _status = GetComponent<IVesselStatus>();

            // Stay silent until we confirm this is the local player's vessel.
            if (_listener != null)
                _listener.enabled = false;
        }

        /// <summary>
        /// Follows ownership every frame rather than resolving it once. Two paths hand a LIVE hull
        /// to a different pilot without respawning it - Cellular Duel's round swap and the arena
        /// PilotSwap (VesselController.ChangePlayer) - and a one-shot latch left the listener on
        /// the hull the player had LEFT: every 3D sound was then placed relative to the
        /// opponent's ship. The test is two reads; the toggle only fires on a change.
        /// </summary>
        void Update()
        {
            if (_listener == null) return;

            // IVesselStatus.Player is null until vessel.Initialize(player) runs.
            bool want = _status?.Player != null && _status.IsLocalUser;
            if (_listener.enabled == want) return;

            _listener.enabled = want;

            if (debugLog && CSDebug.IsVerbose(CSLogChannel.Audio))
                CSDebug.LogVerbose(CSLogChannel.Audio, $"[ShipStudioListenerGate] '{name}': FMOD StudioListener {(want ? "ACTIVATED (local player)" : "DEACTIVATED (no longer the local player's hull)")}.");
        }
    }
}
