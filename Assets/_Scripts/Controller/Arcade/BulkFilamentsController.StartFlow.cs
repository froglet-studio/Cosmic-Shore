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

        protected override void OnReadyClicked_()
        {
            TryBeginBulkCountdown();
        }

        void ConfigureNewTurnStartFlow()
        {
            _countdownStarted = false;
            _vesselStagedForStart = false;
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

            if (!StageVesselForStart())
                return;

            _countdownStarted = true;
            _autoStartTimer = -1f;
            RaiseToggleReadyButtonEvent(false);
            CSDebug.Log("[BulkFilaments] Starting countdown.");

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
                return true;

            if (!AcquireVessel())
            {
                _autoStartTimer = 0f;
                LogAwaitingVessel();
                return false;
            }

            _distanceOnFilament = 0f;
            _speed = minimumSpeed;
            UpdateVesselPose();
            UpdateLatchRig();
            _vesselStagedForStart = true;
            CSDebug.Log("[BulkFilaments] Local vessel staged on first filament.");
            return true;
        }

        bool AcquireVessel()
        {
            if (!IsUsableLocalVessel())
            {
                _vessel = gameData?.LocalPlayer?.Vessel;
                if (!IsUsableLocalVessel())
                {
                    _vessel = null;
                    return false;
                }

                CSDebug.Log($"[BulkFilaments] Acquired local vessel '{_vessel.Transform.name}'.");
            }

            ApplyBulkVesselOverrides();
            return true;
        }

        bool IsUsableLocalVessel()
        {
            if (_vessel == null || _vessel.Transform == null || _vessel.VesselStatus == null)
                return false;

            var localPlayer = gameData?.LocalPlayer;
            if (localPlayer == null || !localPlayer.IsLocalUser || localPlayer.Vessel != _vessel)
                return false;

            if (gameData.Players == null || !gameData.Players.Contains(localPlayer))
                return false;

            return _vessel.VesselStatus.Player == localPlayer;
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
            CSDebug.Log("[BulkFilaments] Waiting for local vessel before countdown.");
        }
    }
}
