using CosmicShore.Game.ShapeDrawing;
using CosmicShore.Game.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade
{
    public class SinglePlayerFreestyleController : SinglePlayerMiniGameControllerBase
    {
        [Header("Environment")]
        [SerializeField] SegmentSpawner segmentSpawner;
        [SerializeField] ShapeDrawingManager shapeDrawingManager;
        [SerializeField] LocalCrystalManager localCrystalManager;
        [SerializeField] Cell cellScript;

        [Header("Lobby Configuration")]
        [SerializeField] Transform lobbyOrigin;
        [SerializeField] List<ModeSelectTrigger> modeTriggers;

        [Header("HUD")]
        [SerializeField] MiniGameHUD miniGameHUD;

        protected override bool HasEndGame => false;
        protected override bool ShowEndGameSequence => false;

        bool _isInLobby;
        bool _isShapePrep;        // true while waiting for Ready after shape cinematic
        bool _isFreestylePrep;    // true while waiting for Ready after freestyle sign

        protected override void OnEnable()
        {
            base.OnEnable();
            ShapeSignEvents.OnShapeSelected += HandleShapeSignSelected;
            FreestyleSignEvents.OnFreestyleSelected += HandleFreestyleSignSelected;
            if (shapeDrawingManager)
            {
                shapeDrawingManager.OnFreestyleResumed.AddListener(OnShapeDrawingFinished);
                shapeDrawingManager.OnPreviewComplete.AddListener(OnShapePreviewComplete);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ShapeSignEvents.OnShapeSelected -= HandleShapeSignSelected;
            FreestyleSignEvents.OnFreestyleSelected -= HandleFreestyleSignSelected;
            if (shapeDrawingManager)
            {
                shapeDrawingManager.OnFreestyleResumed.RemoveListener(OnShapeDrawingFinished);
                shapeDrawingManager.OnPreviewComplete.RemoveListener(OnShapePreviewComplete);
            }
        }

        // ── Shape Drawing Callbacks ──────────────────────────────────────────

        /// <summary>Preview cinematic finished — show the Ready button.</summary>
        void OnShapePreviewComplete()
        {
            _isShapePrep = true;
            RaiseToggleReadyButtonEvent(true);
        }

        /// <summary>
        /// Ready button always starts the countdown timer.
        /// OnCountdownTimerEnded checks state to decide what happens next.
        /// </summary>
        protected override void OnReadyClicked_()
        {
            RaiseToggleReadyButtonEvent(false);
            StartCountdownTimer();
        }

        /// <summary>
        /// Countdown finished. Route to the correct flow based on current state.
        /// </summary>
        protected override void OnCountdownTimerEnded()
        {
            if (_isShapePrep)
            {
                _isShapePrep = false;
                shapeDrawingManager.BeginDrawing();
            }
            else if (_isFreestylePrep)
            {
                _isFreestylePrep = false;
                BeginFreestyle();
            }
            else
            {
                // Initial game start
                gameData.SetPlayersActive();
                ClearPlayerTrails();
                EnterLobby();
            }
        }

        void OnShapeDrawingFinished()
        {
            var vessel = gameData.LocalPlayer?.Vessel;

            if (CameraManager.Instance)
            {
                CameraManager.Instance.SetCloseCameraActive();
                CameraManager.Instance.SnapPlayerCameraToTarget();
            }

            if (vessel?.VesselStatus != null)
                vessel.VesselStatus.VesselHUDController?.ShowHUD();

            TeleportPlayerToLobby();
            ClearPlayerTrails();
            EnterLobby();
        }

        void TeleportPlayerToLobby()
        {
            var vessel = gameData.LocalPlayer?.Vessel;
            if (vessel?.Transform && lobbyOrigin)
                vessel.Transform.SetPositionAndRotation(lobbyOrigin.position, lobbyOrigin.rotation);
        }

        // ── Lobby ───────────────────────────────────────────────────────────

        void EnterLobby()
        {
            _isInLobby = true;

            if (cellScript) cellScript.enabled = false;

            if (segmentSpawner)
            {
                segmentSpawner.NukeTheTrails();
                segmentSpawner.enabled = false;
            }

            if (localCrystalManager)
            {
                localCrystalManager.ManualTurnEnded();
                localCrystalManager.enabled = false;
            }

            ClearPlayerTrails();

            // Enable and reset mode triggers — positions/rotations/scales
            // are set in the editor, never overridden at runtime.
            foreach (var trigger in modeTriggers.Where(trigger => trigger))
            {
                trigger.gameObject.SetActive(true);
                trigger.ResetTrigger();
                trigger.OnModeSelected.RemoveListener(HandleModeSelection);
                trigger.OnModeSelected.AddListener(HandleModeSelection);
            }
        }

        void ExitLobby()
        {
            _isInLobby = false;

            foreach (var trigger in modeTriggers.Where(trigger => trigger)) trigger.gameObject.SetActive(false);
        }

        // ── Mode Selection Handlers ─────────────────────────────────────────

        void HandleModeSelection(ShapeDefinition shapeDef)
        {
            if (!_isInLobby) return;
            ExitLobby();

            if (!shapeDef)
                StartFreestylePrep();
            else
                StartShapeMode(shapeDef);
        }

        void HandleShapeSignSelected(ShapeDefinition shapeDef, Vector3 signWorldPos)
        {
            if (!_isInLobby) return;
            ExitLobby();

            if (!shapeDef)
                StartFreestylePrep();
            else
                StartShapeMode(shapeDef);
        }

        void HandleFreestyleSignSelected()
        {
            if (!_isInLobby) return;
            ExitLobby();
            StartFreestylePrep();
        }

        // ── Game Modes ──────────────────────────────────────────────────────

        /// <summary>
        /// Freeze player, teleport to spawn, show connecting panel → ready → countdown → BeginFreestyle.
        /// Same flow as shape mode prep.
        /// </summary>
        void StartFreestylePrep()
        {
            Debug.Log("[Controller] Starting Freestyle Prep");

            var vessel = gameData.LocalPlayer?.Vessel;
            if (vessel?.VesselStatus != null)
                vessel.VesselStatus.IsStationary = true;

            TeleportPlayerToLobby();
            ClearPlayerTrails();

            _isFreestylePrep = true;

            if (miniGameHUD)
                miniGameHUD.ShowConnectingFlow();
            else
                RaiseToggleReadyButtonEvent(true);
        }

        /// <summary>
        /// Called after countdown ends in freestyle prep state.
        /// Releases player and starts actual freestyle gameplay.
        /// </summary>
        void BeginFreestyle()
        {
            Debug.Log("[Controller] Beginning Freestyle");

            var vessel = gameData.LocalPlayer?.Vessel;
            if (vessel?.VesselStatus != null)
            {
                vessel.VesselStatus.IsStationary = false;
                vessel.VesselStatus.VesselHUDController?.ShowHUD();
            }

            // Enable systems BEFORE firing the turn event so their OnEnable
            // subscriptions are active when OnMiniGameTurnStarted fires.
            if (cellScript) cellScript.enabled = true;
            if (localCrystalManager) localCrystalManager.enabled = true;
            if (segmentSpawner)
            {
                segmentSpawner.enabled = true;
                segmentSpawner.Initialize();
            }

            gameData.StartTurn();
        }

        void StartShapeMode(ShapeDefinition shapeDef)
        {
            Debug.Log($"[Controller] Starting Shape Mode: {shapeDef.shapeName}");
            if (cellScript) cellScript.enabled = false;

            ClearPlayerTrails();

            gameData.StartTurn();

            // Phase 1: setup + cinematic. OnPreviewComplete → show Ready button.
            shapeDrawingManager.StartShapeSequence(shapeDef, lobbyOrigin.position);
        }

        public void ReturnToLobby()
        {
            _isShapePrep = false;
            _isFreestylePrep = false;
            shapeDrawingManager.ExitShapeMode();
            EnterLobby();
        }

        void ClearPlayerTrails()
        {
            var vessel = gameData.LocalPlayer?.Vessel;
            if (vessel?.VesselStatus == null) return;

            var prismController = vessel.VesselStatus.VesselPrismController;
            if (prismController)
            {
                prismController.StopSpawn();
                prismController.ClearTrails();
            }
        }
    }
}
