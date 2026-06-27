using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        [Header("Start Flow")]
        [SerializeField] bool autoStartCountdown = true;
        [SerializeField, Min(0f)] float autoStartDelay = 0.75f;

        float _autoStartTimer = -1f;
        float _awaitingVesselLogTimer;
        bool _countdownStarted;
        bool _vesselStagedForStart;
        bool _spawnFallbackAttempted;

        protected override void OnReadyClicked_()
        {
            TryBeginBulkCountdown();
        }

        void ConfigureNewTurnStartFlow()
        {
            _countdownStarted = false;
            _vesselStagedForStart = false;
            _spawnFallbackAttempted = false;
            _awaitingVesselLogTimer = 0f;
            _autoStartTimer = autoStartCountdown ? autoStartDelay : -1f;
            RaiseToggleReadyButtonEvent(!autoStartCountdown);
        }

        void TickAutoStartCountdown()
        {
            if (_isRunning || _turnFinished || _countdownStarted || _autoStartTimer < 0f)
                return;

            _autoStartTimer -= Time.deltaTime;
            if (_autoStartTimer <= 0f)
                TryBeginBulkCountdown();
        }

        void TryBeginBulkCountdown()
        {
            if (_countdownStarted)
                return;

            _countdownStarted = true;
            _autoStartTimer = -1f;
            RaiseToggleReadyButtonEvent(false);
            CSDebug.Log("[BulkFilaments] Starting countdown; vessel will stage after player activation.");

            if (countdownTimer != null)
            {
                StartCountdownTimer();
                return;
            }

            CSDebug.LogWarning("[BulkFilaments] Missing countdown timer; starting turn immediately.", this);
            OnCountdownTimerEnded();
        }

        bool StageVesselForStart()
        {
            if (_vesselStagedForStart)
                return AcquireVessel();

            if (!AcquireVessel())
            {
                LogAwaitingVessel();
                return false;
            }

            _distanceOnFilament = 0f;
            _speed = minimumSpeed;
            UpdateVesselPose();
            UpdateLatchRig();
            _bulkPlayer?.StartPlayer();
            _vesselStagedForStart = true;
            CSDebug.Log("[BulkFilaments] Vessel staged on first filament.");
            return true;
        }

        bool AcquireVessel()
        {
            if (!IsUsableBulkVessel(_bulkPlayer, _vessel))
            {
                if (!TryResolveBulkPlayer(out _bulkPlayer, out _vessel))
                {
                    TrySpawnFallbackPlayer();
                    if (!TryResolveBulkPlayer(out _bulkPlayer, out _vessel))
                    {
                        _bulkPlayer = null;
                        _vessel = null;
                        return false;
                    }
                }

                EnsureBulkPlayerRegistered();
                CSDebug.Log($"[BulkFilaments] Acquired vessel '{_vessel.Transform.name}' for player '{_bulkPlayer.Name}'.");
            }

            ApplyBulkVesselOverrides();
            return true;
        }

        bool TryResolveBulkPlayer(out IPlayer player, out IVessel vessel)
        {
            player = null;
            vessel = null;

            if (TryUseBulkPlayer(gameData?.LocalPlayer, out player, out vessel))
                return true;

            if (gameData?.Players != null)
            {
                for (int i = 0; i < gameData.Players.Count; i++)
                {
                    if (TryUseBulkPlayer(gameData.Players[i], out player, out vessel))
                        return true;
                }
            }

            var scenePlayers = Object.FindObjectsByType<Player>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < scenePlayers.Length; i++)
            {
                if (TryUseBulkPlayer(scenePlayers[i], out player, out vessel))
                    return true;
            }

            return false;
        }

        bool TryUseBulkPlayer(IPlayer candidate, out IPlayer player, out IVessel vessel)
        {
            player = null;
            vessel = null;

            if (!IsUsableBulkVessel(candidate, candidate?.Vessel))
                return false;

            player = candidate;
            vessel = candidate.Vessel;
            return true;
        }

        bool IsUsableBulkVessel(IPlayer player, IVessel vessel)
        {
            if (player == null || player.IsInitializedAsAI)
                return false;

            if (player is Object playerObject && !playerObject)
                return false;

            if (vessel == null || vessel.Transform == null || vessel.VesselStatus == null)
                return false;

            if (vessel is Object vesselObject && !vesselObject)
                return false;

            IPlayer vesselPlayer = vessel.VesselStatus.Player;
            return vesselPlayer == null || vesselPlayer == player;
        }

        void EnsureBulkPlayerRegistered()
        {
            if (gameData?.Players == null || _bulkPlayer == null)
                return;

            if (!gameData.Players.Contains(_bulkPlayer))
                gameData.AddPlayer(_bulkPlayer);
        }

        void TrySpawnFallbackPlayer()
        {
            if (_spawnFallbackAttempted)
                return;

            _spawnFallbackAttempted = true;
            var spawner = Object.FindFirstObjectByType<MiniGamePlayerSpawnerAdapter>(FindObjectsInactive.Include);
            if (!spawner)
                return;

            if (spawner.EnsureLocalPlayerSpawned())
                CSDebug.Log("[BulkFilaments] Recovered missing mini-game player spawn after countdown.");
        }

        void ApplyBulkVesselOverrides()
        {
            _vessel.VesselStatus.IsStationary = true;
            _vessel.VesselStatus.VesselPrismController?.StopSpawn();
        }

        void LogAwaitingVessel()
        {
            _awaitingVesselLogTimer -= Time.deltaTime;
            if (_awaitingVesselLogTimer > 0f)
                return;

            _awaitingVesselLogTimer = 2f;
            CSDebug.Log("[BulkFilaments] Waiting for local vessel to stage Bulk run.");
        }
    }
}
