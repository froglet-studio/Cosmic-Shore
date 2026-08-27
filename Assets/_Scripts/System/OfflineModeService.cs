using System;
using System.Threading;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Starts the OFFLINE LOCAL HOST session - the single-player fallback used when UGS
    /// auth / Relay cannot be reached (airplane mode, Steam offline mode, service outage).
    ///
    /// <para>
    /// The design (Docs/OFFLINE_MODE.md): offline is a plain <c>NetworkManager.StartHost()</c>
    /// on 127.0.0.1 with the default UnityTransport - host == server == client on one machine,
    /// so the entire Netcode spawn chain, scene management, RPCs and AI backfill run
    /// byte-identically to a solo online session. There is deliberately no "no-netcode" branch.
    /// This is NOT lazy Relay creation (the locked party design is untouched): offline mode is
    /// the absence of Relay, entered only when Relay is provably unreachable.
    /// </para>
    ///
    /// <para>
    /// Entry point: <see cref="AuthenticationSceneController"/>, after its bounded Relay
    /// attempts exhaust. Single writer of <see cref="GameDataSO.IsOfflineSession"/>.
    /// Pure C# lazy DI singleton (registered in <c>AppManager.InstallBindings</c>).
    /// </para>
    /// </summary>
    public class OfflineModeService
    {
        const ushort LOCAL_HOST_PORT = 7777;
        const float HOST_START_TIMEOUT_SECONDS = 10f;
        const float DATA_INIT_TIMEOUT_SECONDS = 5f;

        const string OFFLINE_PREFERENCE_KEY = "CosmicShore.OfflinePreferred";

        readonly GameDataSO _gameData;

        public OfflineModeService(GameDataSO gameData)
        {
            _gameData = gameData;
        }

        /// <summary>True once this session is running on the offline local host.</summary>
        public bool IsOfflineSession => _gameData != null && _gameData.IsOfflineSession;

        /// <summary>
        /// The player's DELIBERATE choice to stay offline (the menu's online/offline toggle), as
        /// opposed to the automatic fallback that fires when UGS is simply unreachable.
        ///
        /// <para>
        /// Persisted, because a deliberate choice that silently reverts on the next launch is not
        /// a choice - this is the Steam "go offline" contract. It is read at boot by
        /// <see cref="AuthenticationSceneController"/>, which skips the Relay attempts entirely
        /// when it is set, and cleared by <c>ReconnectService.ReconnectAsync</c> when the player
        /// asks to come back online.
        /// </para>
        ///
        /// <para>
        /// Distinct from <see cref="IsOfflineSession"/>: that is what the session IS RIGHT NOW,
        /// this is what the player ASKED FOR. A player who never touched the toggle can be in an
        /// offline session (no network) with this false - and going online then costs them
        /// nothing but a tap.
        /// </para>
        /// </summary>
        public bool OfflinePreferred
        {
            get => PlayerPrefs.GetInt(OFFLINE_PREFERENCE_KEY, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(OFFLINE_PREFERENCE_KEY, value ? 1 : 0);
                PlayerPrefs.Save();
                CSDebug.Log($"[OfflineModeService] Offline preference set to {value}.");
            }
        }

        /// <summary>
        /// Brings up the offline session: restores the player's last-known-good data from the
        /// local cloud-cache (name, unlocked vessels, episodes, progression - see
        /// <see cref="LocalCloudDataCache"/>), then starts NetworkManager as a loopback host so
        /// the ordinary spawn chain can run. Idempotent. Returns false only when no
        /// NetworkManager exists or the host refuses to start - the caller keeps its manual
        /// retry surface for that case.
        /// </summary>
        public async UniTask<bool> EnterOfflineSessionAsync(CancellationToken ct = default)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                CSDebug.LogError("[OfflineModeService] NetworkManager.Singleton is null - cannot start offline host.");
                return false;
            }

            if (_gameData.IsOfflineSession && nm.IsListening)
                return true;

            // 1. Stand the party layer down. An offline session has no lobby and no Relay, and
            //    a presence lobby left running keeps its refresh/converge loop hammering UGS for
            //    the whole offline session - a stream of join/query errors on a screen the
            //    player was just told is offline. It also releases our SERVER-side lobby
            //    membership, so coming back online later can re-join instead of being refused
            //    with "player is already a member of the lobby".
            //    No-op on a cold offline boot, which never joined anything.
            var hcs = HostConnectionService.Instance;
            if (hcs != null)
            {
                try
                {
                    await hcs.ResetPartyLayerAsync().AttachExternalCancellation(ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e)
                {
                    CSDebug.LogWarning($"[OfflineModeService] Party layer reset failed: {e.Message} - continuing offline.");
                }
            }

            // 2. Restore the player's basic details BEFORE the host starts, so the Player
            //    NetworkObject that spawns with the host resolves its display name from the
            //    cached profile (PlayerDataService syncs it into GameDataSO on OnInitialized)
            //    instead of minting a random Pilot#### identity.
            await InitializeLocalDataAsync(ct);

            // 3. If a host is somehow already listening (e.g. a Relay session came up while we
            //    were deciding), this is not an offline session - use it as-is.
            if (nm.IsListening)
            {
                CSDebug.Log("[OfflineModeService] NetworkManager already listening - not entering offline mode.");
                return true;
            }

            // 4. Wire the Netcode callbacks (connection approval most importantly - the
            //    NetworkManager prefab ships ConnectionApproval on, and a host with no
            //    approval callback times out its own local client). MultiplayerSetup owns
            //    that wiring online; reuse it so both paths share one callback set.
            var multiplayerSetup = UnityEngine.Object.FindAnyObjectByType<MultiplayerSetup>();
            if (multiplayerSetup != null)
            {
                multiplayerSetup.EnsureNetcodeCallbacksWired();
            }
            else if (nm.ConnectionApprovalCallback == null)
            {
                CSDebug.LogWarning("[OfflineModeService] MultiplayerSetup not found - wiring minimal approval callback.");
                nm.ConnectionApprovalCallback = static (request, response) =>
                {
                    response.Approved = true;
                    response.CreatePlayerObject = true;
                    response.Position = Vector3.zero;
                    response.Rotation = Quaternion.identity;
                    response.PlayerPrefabHash = null;
                };
            }

            // 5. Point the transport at loopback. At cold boot this is already the prefab's
            //    authored state; re-asserting it costs nothing and protects against any
            //    partially-applied Relay configuration from the failed attempts.
            if (nm.TryGetComponent<UnityTransport>(out var transport))
                transport.SetConnectionData("127.0.0.1", LOCAL_HOST_PORT, "0.0.0.0");

            // 6. Set the flag BEFORE StartHost so every callback that fires during host
            //    bring-up (connection approval, Player.OnNetworkSpawn) already sees an
            //    offline session. Reverted on failure.
            _gameData.IsOfflineSession = true;

            CSDebug.Log("[OfflineModeService] Starting offline local host (127.0.0.1) ...");
            bool started;
            try
            {
                started = nm.StartHost();
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[OfflineModeService] StartHost threw: {e.Message}");
                started = false;
            }

            if (!started)
            {
                _gameData.IsOfflineSession = false;
                CSDebug.LogError("[OfflineModeService] StartHost failed - offline session unavailable.");
                return false;
            }

            // 7. Wait for the host to report listening (near-instant on loopback; bounded
            //    defensively).
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(HOST_START_TIMEOUT_SECONDS));
            try
            {
                await UniTask.WaitUntil(
                    () => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening,
                    cancellationToken: timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // StartHost returned true, so NM is mid-bring-up. Clearing the flag alone would
                // leave the worst possible state: a host that finishes coming up a moment later,
                // with IsOfflineSession false - so HostConnectionService stops standing down and
                // tries to build a Relay session on top of a live local host, while the caller
                // has already been told offline failed. Tear it back down so "false" means
                // nothing is running.
                CSDebug.LogError($"[OfflineModeService] Host did not start listening within {HOST_START_TIMEOUT_SECONDS}s - shutting it back down.");
                try { NetworkManager.Singleton?.Shutdown(); }
                catch (Exception e) { CSDebug.LogWarning($"[OfflineModeService] Shutdown after failed start threw: {e.Message}"); }

                _gameData.IsOfflineSession = false;
                return false;
            }

            CSDebug.Log("[OfflineModeService] Offline local host running - session is offline until app restart.");
            return true;
        }

        /// <summary>
        /// Loads the data layer from local snapshots when the cloud never signed in. Runs the
        /// SAME <see cref="UGSDataService.InitializeAsync"/> the online path runs - the
        /// provider answers null for every key offline and each repository falls back to its
        /// <see cref="LocalCloudDataCache"/> snapshot, so <c>IsInitialized</c> flips true,
        /// <c>SyncHangarToVessels</c> restores unlocks, and PlayerDataService merges the cached
        /// profile exactly as it would a cloud one.
        /// </summary>
        async UniTask InitializeLocalDataAsync(CancellationToken ct)
        {
            var dataService = UGSDataService.Instance;
            if (dataService == null)
            {
                CSDebug.LogWarning("[OfflineModeService] UGSDataService not present - continuing without profile data.");
                return;
            }

            if (dataService.IsInitialized)
                return;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(DATA_INIT_TIMEOUT_SECONDS));
            try
            {
                await dataService.InitializeOfflineAsync(timeoutCts.Token)
                    .AsUniTask()
                    .AttachExternalCancellation(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                CSDebug.LogWarning("[OfflineModeService] Offline data init timed out - continuing with defaults.");
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[OfflineModeService] Offline data init failed: {e.Message} - continuing with defaults.");
            }
        }
    }
}
