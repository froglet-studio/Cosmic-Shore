using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Translates boot-phase SOAP events from auth / party / game-data into
    /// <see cref="BootStatusRequest"/> raises on the inbound status channel.
    ///
    /// Open/Closed: adding a new boot phase = new label field + one new
    /// subscription here. The panel and event channels stay closed.
    ///
    /// Lives on the same GameObject as <see cref="BootStatusPanel"/> (child
    /// of the Bootstrap splash canvas, persisted via the canvas's DDOL root).
    /// </summary>
    public class BootStatusBroadcaster : MonoBehaviour
    {
        [Header("Inbound SOAP (existing events)")]
        [SerializeField] private AuthenticationDataVariable authData;
        [SerializeField] private HostConnectionDataSO       connectionData;
        [SerializeField] private GameDataSO                 gameData;

        [Header("Outbound SOAP (request channel → BootStatusPanel)")]
        [SerializeField] private ScriptableEventBootStatusRequest requestEvent;

        [Header("Labels")]
        [SerializeField] private string labelConnecting      = "Connecting…";
        [SerializeField] private string labelJoiningLobby    = "Joining lobby…";
        [SerializeField] private string labelCreatingSession = "Creating session…";
        [SerializeField] private string labelHostReady       = "Host ready…";
        [SerializeField] private string labelConnectionLost  = "Connection lost. Tap retry.";

        private bool _hostReadyReached;
        private bool _signedInSubscribed;

        void OnEnable()
        {
            if (connectionData != null)
            {
                if (connectionData.OnHostConnectionEstablished != null)
                    connectionData.OnHostConnectionEstablished.OnRaised += HandleHostConnectionEstablished;
                if (connectionData.OnHostConnectionLost != null)
                    connectionData.OnHostConnectionLost.OnRaised += HandleHostConnectionLost;
            }

            if (gameData != null && gameData.OnClientReady != null)
                gameData.OnClientReady.OnRaised += HandleClientReady;

            TrySubscribeSignedIn();
        }

        void Start()
        {
            // Auth may have signed-in event wired in lazily; retry the
            // subscription once injectors and facades have settled.
            TrySubscribeSignedIn();

            // Initial state: the splash is opaque, no auth/HCS events have
            // fired yet — show "Connecting…" so the surface is informative
            // from the very first frame.
            requestEvent?.Raise(new BootStatusRequest(BootStatusMode.Status, labelConnecting));
        }

        void OnDisable()
        {
            if (connectionData != null)
            {
                if (connectionData.OnHostConnectionEstablished != null)
                    connectionData.OnHostConnectionEstablished.OnRaised -= HandleHostConnectionEstablished;
                if (connectionData.OnHostConnectionLost != null)
                    connectionData.OnHostConnectionLost.OnRaised -= HandleHostConnectionLost;
            }

            if (gameData != null && gameData.OnClientReady != null)
                gameData.OnClientReady.OnRaised -= HandleClientReady;

            if (_signedInSubscribed && authData?.Value?.OnSignedIn != null)
            {
                authData.Value.OnSignedIn.OnRaised -= HandleSignedIn;
                _signedInSubscribed = false;
            }
        }

        private void TrySubscribeSignedIn()
        {
            if (_signedInSubscribed) return;
            var signedIn = authData?.Value?.OnSignedIn;
            if (signedIn == null) return;
            signedIn.OnRaised += HandleSignedIn;
            _signedInSubscribed = true;
        }

        private void HandleSignedIn()
            => requestEvent?.Raise(new BootStatusRequest(BootStatusMode.Status, labelJoiningLobby));

        private void HandleHostConnectionEstablished()
        {
            // Fires twice: lobby join (NM not listening) then Relay session
            // create (NM listening). Map the second fire to "Host ready…".
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (!_hostReadyReached && nm != null && nm.IsListening)
            {
                _hostReadyReached = true;
                requestEvent?.Raise(new BootStatusRequest(BootStatusMode.Status, labelHostReady));
            }
            else
            {
                requestEvent?.Raise(new BootStatusRequest(BootStatusMode.Status, labelCreatingSession));
            }
        }

        private void HandleHostConnectionLost()
        {
            _hostReadyReached = false;
            requestEvent?.Raise(new BootStatusRequest(BootStatusMode.Retry, labelConnectionLost));
        }

        private void HandleClientReady()
            => requestEvent?.Raise(new BootStatusRequest(BootStatusMode.Hide));
    }
}
