using CosmicShore.Data;
using CosmicShore.Gameplay;
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
    /// Retry policy: the retry surface (<see cref="BootStatusMode.Retry"/>) is
    /// reserved for idle/boot contexts where no other recovery driver is
    /// active - auth-scene retry exhaustion, or lobby-refresh escalation while
    /// sitting in the menu. During expected transitions the splash is
    /// deliberately opaque and those flows own their own failure recovery
    /// (PartyInviteController bounces to a solo menu; a genuine session loss
    /// during a game launch routes through OnActiveSessionEnd back to the
    /// menu), so connection-lost raises in those windows are suppressed here
    /// rather than rendered as a misleading "tap retry".
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

        [Tooltip("Optional: when a Shuffle (Tournament meta) is mid-run, the between-game loading splash " +
                 "shows the running domain standings instead of clean branding. Leave null outside that mode.")]
        [SerializeField] private TournamentDataSO           tournamentData;

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

        // True between OnLaunchGame (loading splash went opaque) and the next
        // OnClientReady (vessel initialized in the loaded scene). Connection-lost
        // raises inside this window are not retry-worthy - see class doc.
        private bool _inLaunchTransition;

        void OnEnable()
        {
            if (connectionData != null)
            {
                if (connectionData.OnHostConnectionEstablished != null)
                    connectionData.OnHostConnectionEstablished.OnRaised += HandleHostConnectionEstablished;
                if (connectionData.OnHostConnectionLost != null)
                    connectionData.OnHostConnectionLost.OnRaised += HandleHostConnectionLost;
                if (connectionData.OnPartyJoinCompleted != null)
                    connectionData.OnPartyJoinCompleted.OnRaised += HandlePartyJoinCompleted;
            }

            if (gameData != null && gameData.OnClientReady != null)
                gameData.OnClientReady.OnRaised += HandleClientReady;

            if (gameData != null && gameData.OnLaunchGame != null)
                gameData.OnLaunchGame.OnRaised += HandleLaunchGame;

            TrySubscribeSignedIn();
        }

        void Start()
        {
            // Inspector-wired design: BootStatusPanel must exist on this same
            // GameObject with its references wired in Bootstrap.unity - there
            // is no runtime repair. Fail loud if the scene lost it (see B10 in
            // Docs/PartySystem/BUGS.md): without the panel, nothing drives the
            // status text and an authored-active retry button would be
            // orphaned on every splash.
            if (GetComponent<BootStatusPanel>() == null)
                Debug.LogError("[BootStatusBroadcaster] BootStatusPanel is missing from the splash canvas - " +
                               "the boot/retry surface is dead. Re-apply the Bootstrap.unity wiring (B10).", this);

            // Auth may have signed-in event wired in lazily; retry the
            // subscription once injectors and facades have settled.
            TrySubscribeSignedIn();

            // Initial state: the splash is opaque, no auth/HCS events have
            // fired yet - show "Connecting…" so the surface is informative
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
                if (connectionData.OnPartyJoinCompleted != null)
                    connectionData.OnPartyJoinCompleted.OnRaised -= HandlePartyJoinCompleted;
            }

            if (gameData != null && gameData.OnClientReady != null)
                gameData.OnClientReady.OnRaised -= HandleClientReady;

            if (gameData != null && gameData.OnLaunchGame != null)
                gameData.OnLaunchGame.OnRaised -= HandleLaunchGame;

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

            // Expected-transition windows - suppress the retry surface and let
            // the owning flow recover (see class doc). Without these guards a
            // connection-lost raised while the loading splash is opaque renders
            // a prominent, wrong "tap retry" over a transition that is
            // progressing normally.
            if (_inLaunchTransition)
                return;
            var partyTransition = PartyInviteController.Instance;
            if (partyTransition != null && partyTransition.IsTransitioning)
                return;

            requestEvent?.Raise(new BootStatusRequest(BootStatusMode.Retry, labelConnectionLost));
        }

        private void HandleLaunchGame()
        {
            // A game-launch splash just went opaque (SceneLoader.LaunchGame on
            // every instance - host and clients).
            _inLaunchTransition = true;

            // Keep the loading splash CLEAN on every game launch. The Maelstrom round reveal (mode /
            // intensity / leading domain) now lives on the dedicated in-game connecting panel
            // (ConnectingPanelController) rather than on the splash, so we just clear whatever status was
            // last shown - in particular a stale Retry left over from a connection blip the auto-recovery
            // already handled.
            requestEvent?.Raise(new BootStatusRequest(BootStatusMode.Hide));
        }

        private void HandlePartyJoinCompleted()
            => requestEvent?.Raise(new BootStatusRequest(BootStatusMode.Hide));

        private void HandleClientReady()
        {
            _inLaunchTransition = false;
            requestEvent?.Raise(new BootStatusRequest(BootStatusMode.Hide));
        }
    }
}
