using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using CosmicShore.Utility;
using Reflex.Attributes;
using CosmicShore.ScriptableObjects;
#if UNITY_EDITOR
using Unity.Multiplayer.Playmode;
using Unity.Netcode.Transports.UTP;
#endif
namespace CosmicShore.Gameplay
{
    public class MultiplayerSetup : MonoBehaviour
    {

        [Inject] GameDataSO gameData;
        [Inject] AuthenticationDataVariable authenticationDataVariable;
        AuthenticationData authenticationData => authenticationDataVariable.Value;

        private NetworkManager networkManager;
        private bool _hostStartInProgress;


        private void Start()
        {
            if (authenticationDataVariable == null)
            {
                CSDebug.LogError("[MultiplayerSetup] authenticationDataVariable was not injected - check AppManager DI registration.");
                return;
            }

            authenticationData.OnSignedIn.OnRaised += OnAuthenticationSignedIn;

            // If already authenticated (e.g. Bootstrap auth completed before Start),
            // start the host immediately.
            if (authenticationData.IsSignedIn)
            {
                OnAuthenticationSignedIn();
            }
        }

        private void OnDisable()
        {
            if (authenticationDataVariable == null) return;

            authenticationData.OnSignedIn.OnRaised -= OnAuthenticationSignedIn;

            if (networkManager != null)
            {
                networkManager.ConnectionApprovalCallback -= OnConnectionApprovalCallback;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
                networkManager.OnTransportFailure         -= OnTransportFailure;
            }
        }

        // --------------------------
        // Session Bootstrapping
        // --------------------------

        // Synchronous by design: nothing here is awaited. ExecuteMultiplayerSetup is the only
        // async work and it is explicitly fire-and-forget, so wrapping this in an async
        // UniTaskVoid added a state machine without changing when any of it ran.
        void OnAuthenticationSignedIn()
        {
            // Host startup is all sign-in needs. The legacy matchmaking path that used to
            // hang off an IsMultiplayerMode gate here (query/join/create UGS sessions by
            // gameMode) is retired: the eager per-user Relay party session
            // (HostConnectionService) IS the session for every game, solo included.
            EnsureHostStarted();
        }

        /// <summary>
        /// Ensures the NetworkManager has Netcode callbacks registered and
        /// starts the host exactly once. The NetworkManager lives in the
        /// Bootstrap scene as DontDestroyOnLoad and must already exist.
        /// Subsequent calls are no-ops while the host is already listening.
        /// </summary>
        void EnsureHostStarted()
        {
            // Guard against concurrent calls (e.g. OnSignedIn event + IsSignedIn
            // check both firing before the first call completes).
            if (_hostStartInProgress)
            {
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#00FFFF>[FLOW-1] [MultiplayerSetup] EnsureHostStarted SKIPPED (already in progress)</color>");
                return;
            }
            _hostStartInProgress = true;
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#00FFFF>[FLOW-1] [MultiplayerSetup] EnsureHostStarted START</color>");

            try
            {
                // NetworkManager should already exist from Bootstrap (DontDestroyOnLoad).
                var nm = NetworkManager.Singleton;
                if (nm == null)
                {
                    Debug.LogError("<color=#FF0000>[FLOW-1] [MultiplayerSetup] NetworkManager.Singleton is NULL!</color>");
                    CSDebug.LogError("[MultiplayerSetup] NetworkManager.Singleton is null - it should exist from the Bootstrap scene.");
                    return;
                }

                // Re-cache and wire callbacks if the NetworkManager instance changed.
                if (networkManager != nm)
                {
                    if (networkManager != null)
                    {
                        networkManager.ConnectionApprovalCallback -= OnConnectionApprovalCallback;
                        networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
                        networkManager.OnTransportFailure         -= OnTransportFailure;
                    }

                    networkManager = nm;
                    nm.ConnectionApprovalCallback += OnConnectionApprovalCallback;
                    nm.OnClientDisconnectCallback += OnClientDisconnect;
                    nm.OnTransportFailure         += OnTransportFailure;
                    CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#00FFFF>[FLOW-1] [MultiplayerSetup] Wired Netcode callbacks to NetworkManager</color>");
                }

                if (nm.IsListening)
                {
                    CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#00FFFF>[FLOW-1] [MultiplayerSetup] Network already running (IsListening=true), skipping StartHost</color>");
                    CSDebug.Log("[MultiplayerSetup] Network already running.");
                    return;
                }

#if UNITY_EDITOR
                // MPPM clones run as separate editor processes on the same machine.
                // Each starts its own local host, so they need unique ports to avoid
                // bind conflicts. Relay transport handles actual multiplayer connections.
                if (!CurrentPlayer.IsMainEditor)
                {
                    var transport = nm.GetComponent<UnityTransport>();
                    if (transport != null)
                    {
                        var tags = CurrentPlayer.ReadOnlyTags();
                        var tagKey = tags != null && tags.Length > 0 ? string.Join("-", tags) : "clone";
                        ushort port = (ushort)(7778 + (ushort)(Math.Abs(tagKey.GetHashCode()) % 100));
                        transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
                        CSDebug.Log($"[MultiplayerSetup] MPPM clone '{tagKey}' - local host port {port}.");
                    }
                }
#endif

                // Host startup is delegated to HostConnectionService which creates a
                // Relay-backed party session (via CreateSessionAsync + WithRelayNetwork).
                // AuthenticationSceneController.EnsureHostStartedAsync provides a local
                // host fallback if the Relay allocation times out.
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#00FFFF>[FLOW-1] [MultiplayerSetup] Callbacks wired. Waiting for HostConnectionService to start Relay host.</color>");
                CSDebug.Log("[MultiplayerSetup] Callbacks wired. Waiting for HostConnectionService to start Relay host.");
            }
            finally
            {
                _hostStartInProgress = false;
            }
        }

        // --------------------------
        // NGO Connection Hooks
        // --------------------------

        private void OnConnectionApprovalCallback(NetworkManager.ConnectionApprovalRequest request,
                                                  NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved           = true;
            response.CreatePlayerObject = true;
            response.Position           = Vector3.zero;
            response.Rotation           = Quaternion.identity;
            response.PlayerPrefabHash   = null;
        }

        private void OnClientDisconnect(ulong clientId)
        {
            if (networkManager == null) return;

            if (networkManager.IsHost)
            {
                if (clientId != networkManager.LocalClientId)
                {
                    CSDebug.Log($"[MultiplayerSetup] Client {clientId} disconnected from host.");
                    // Netcode backstop for hard drops (client crash) that may beat the
                    // graceful UGS ISession.PlayerLeaving. Only the Netcode clientId is
                    // available here (no UGS PlayerId), so this reconciles the roster;
                    // invite cleanup is handled by the PlayerLeaving handler / poll.
                    HostConnectionService.Instance?.ReconcilePartyMembersNow();
                }
                return;
            }

            if (clientId == networkManager.LocalClientId)
            {
                CSDebug.Log("[MultiplayerSetup] Host left/disconnected - bouncing to solo menu.");
                // Host-loss recovery: re-establish our OWN solo host in Menu_Main (works
                // from the lava-lamp menu AND any game scene). Routed through the proven
                // self-rescue instead of gameData.InvokeOnSessionEnded() →
                // SceneLoader.HandleActiveSessionEnd, whose defer-to-server guard hangs the
                // client when the server is gone. See Docs/PartySystem/BUGS.md B10.
                if (PartyInviteController.Instance != null)
                    PartyInviteController.Instance.HandleHostLossAsync("Host disconnected").Forget();
                else
                    gameData.InvokeOnSessionEnded(); // fallback: legacy path
            }
        }

        // --------------------------
        // Transport Failure Handler
        // --------------------------
        private async void OnTransportFailure()
        {
            try
            {
                CSDebug.LogWarning("[Net] Transport failure - bouncing to solo menu.");

                // Same self-rescue as host-loss: tear down, shut down NM, reload Menu_Main,
                // and recreate our OWN solo host (EnsurePartySessionAsync). The legacy path
                // below shut NM down but never recreated the solo session, leaving a hostless
                // menu. See Docs/PartySystem/BUGS.md B10.
                if (PartyInviteController.Instance != null)
                {
                    await PartyInviteController.Instance.HandleHostLossAsync("Connection lost");
                    return;
                }

                // Fallback (PartyInviteController unavailable): legacy teardown.
                if (gameData.ActiveSession != null)
                {
                    if (gameData.ActiveSession.IsHost)
                        await gameData.ActiveSession.AsHost().DeleteAsync();
                    else
                        await gameData.ActiveSession.LeaveAsync();

                    gameData.ActiveSession = null;
                }

                if (networkManager != null)
                    networkManager.Shutdown();

                await UniTask.Delay(500);
                gameData.InvokeOnSessionEnded();
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[Net] Transport failure handling error: {e}");
            }
        }
    }
}
