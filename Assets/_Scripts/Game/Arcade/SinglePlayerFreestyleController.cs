using CosmicShore.Game.ShapeDrawing;
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
        [SerializeField] ShapeSignSpawner shapeSignSpawner;

        [Header("Trigger Placement")]
        [SerializeField] float triggerRingRadius = 40f;
        [SerializeField] float triggerScale = 0.4f;
        [SerializeField] float triggerForwardOffset = 50f;

        protected override bool HasEndGame => false;
        protected override bool ShowEndGameSequence => false;

        bool _isInLobby;
        bool _isShapePrep; // true while waiting for Ready after shape cinematic

        protected override void OnEnable()
        {
            base.OnEnable();
            ShapeSignEvents.OnShapeSelected += HandleShapeSignSelected;
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
        /// Countdown finished. If in shape-prep, begin drawing.
        /// Otherwise, do initial game start (SetPlayersActive + EnterLobby).
        /// </summary>
        protected override void OnCountdownTimerEnded()
        {
            if (_isShapePrep)
            {
                _isShapePrep = false;
                shapeDrawingManager.BeginDrawing();
            }
            else
            {
                gameData.SetPlayersActive();
                ClearPlayerTrails();
                EnterLobby();
            }
        }

        void OnShapeDrawingFinished()
        {
            TeleportPlayerToLobby();

            if (CameraManager.Instance)
            {
                CameraManager.Instance.SetCloseCameraActive();
                CameraManager.Instance.SnapPlayerCameraToTarget();
            }

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

            var playerTransform = gameData.LocalPlayer?.Vessel?.Transform;
            var center = playerTransform ? playerTransform.position : lobbyOrigin.position;
            var forward = playerTransform ? playerTransform.forward : Vector3.forward;
            var triggerCenter = center + forward * triggerForwardOffset;

            var activeTriggers = modeTriggers.Where(trigger => trigger).ToList();
            for (int i = 0; i < activeTriggers.Count; i++)
            {
                var trigger = activeTriggers[i];
                trigger.gameObject.SetActive(true);
                trigger.ResetTrigger();
                trigger.OnModeSelected.RemoveListener(HandleModeSelection);
                trigger.OnModeSelected.AddListener(HandleModeSelection);

                float angle = ((i - (activeTriggers.Count - 1) * 0.5f) / Mathf.Max(1, activeTriggers.Count - 1)) * Mathf.PI * 0.5f;
                var offset = new Vector3(Mathf.Sin(angle) * triggerRingRadius, 0f, Mathf.Cos(angle) * triggerRingRadius);
                trigger.transform.position = triggerCenter + offset;
                trigger.transform.localScale = Vector3.one * triggerScale;

                var dirToPlayer = (center - trigger.transform.position).normalized;
                if (dirToPlayer != Vector3.zero)
                    trigger.transform.rotation = Quaternion.LookRotation(dirToPlayer, Vector3.up);
            }

            if (shapeSignSpawner)
                shapeSignSpawner.ShowSigns();
        }

        void ExitLobby()
        {
            _isInLobby = false;

            foreach (var trigger in modeTriggers.Where(trigger => trigger)) trigger.gameObject.SetActive(false);

            if (shapeSignSpawner) shapeSignSpawner.HideSigns();

            gameData.StartTurn();
        }

        // ── Mode Selection Handlers ─────────────────────────────────────────

        void HandleModeSelection(ShapeDefinition shapeDef)
        {
            if (!_isInLobby) return;
            ExitLobby();

            if (!shapeDef)
                StartStandardFreestyle();
            else
                StartShapeMode(shapeDef);
        }

        void HandleShapeSignSelected(ShapeDefinition shapeDef, Vector3 signWorldPos)
        {
            if (!_isInLobby) return;
            ExitLobby();

            if (!shapeDef)
                StartStandardFreestyle();
            else
                StartShapeMode(shapeDef);
        }

        // ── Game Modes ──────────────────────────────────────────────────────

        void StartStandardFreestyle()
        {
            Debug.Log("[Controller] Starting Standard Freestyle");

            if (cellScript) cellScript.enabled = true;

            if (localCrystalManager) localCrystalManager.enabled = true;
            if (segmentSpawner)
            {
                segmentSpawner.enabled = true;
                segmentSpawner.Initialize();
            }

            RaiseToggleReadyButtonEvent(true);
        }

        void StartShapeMode(ShapeDefinition shapeDef)
        {
            Debug.Log($"[Controller] Starting Shape Mode: {shapeDef.shapeName}");
            if (cellScript) cellScript.enabled = false;

            ClearPlayerTrails();

            // Phase 1: setup + cinematic. OnPreviewComplete → show Ready button.
            shapeDrawingManager.StartShapeSequence(shapeDef, lobbyOrigin.position);
        }

        public void ReturnToLobby()
        {
            _isShapePrep = false;
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
