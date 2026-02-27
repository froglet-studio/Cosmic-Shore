using CosmicShore.Game.ShapeDrawing;
using CosmicShore.Game.UI;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Freestyle controller — no lobby. After initial countdown, goes straight to freestyle
    /// with SegmentSpawner. Spawnable shapes in the SegmentSpawner rotation trigger
    /// shape drawing mode on vessel collision via ShapeSignEvents.
    /// </summary>
    public class SinglePlayerFreestyleController : SinglePlayerMiniGameControllerBase
    {
        [Header("Environment")]
        [SerializeField] SegmentSpawner segmentSpawner;
        [SerializeField] ShapeDrawingManager shapeDrawingManager;
        [SerializeField] LocalCrystalManager localCrystalManager;
        [SerializeField] Cell cellScript;

        [Header("HUD")]
        [SerializeField] MiniGameHUD miniGameHUD;

        protected override bool HasEndGame => false;
        protected override bool ShowEndGameSequence => false;

        bool _isShapePrep;        // true while waiting for Ready after shape cinematic
        bool _isInShapeMode;      // true while shape drawing is active

        protected override void Start()
        {
            base.Start();

            // Initialize environment immediately so shapes/segments are visible
            // before the player presses Ready. BeginFreestyle() will re-initialize later.
            if (segmentSpawner)
            {
                segmentSpawner.enabled = true;
                segmentSpawner.Initialize();
            }
            if (cellScript) cellScript.enabled = true;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ShapeSignEvents.OnShapeSelected += HandleShapeCollision;
            if (shapeDrawingManager)
            {
                shapeDrawingManager.OnFreestyleResumed.AddListener(OnShapeDrawingFinished);
                shapeDrawingManager.OnPreviewComplete.AddListener(OnShapePreviewComplete);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ShapeSignEvents.OnShapeSelected -= HandleShapeCollision;
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
        /// Ready button starts the countdown timer.
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
            else
            {
                // Initial game start or returning from shape mode — begin freestyle
                BeginFreestyle();
            }
        }

        // ── Shape Collision During Freestyle ─────────────────────────────────

        /// <summary>
        /// Vessel hit a spawnable shape's trigger collider during freestyle.
        /// Transition: freeze player → nuke freestyle → start shape mode.
        /// </summary>
        void HandleShapeCollision(ShapeDefinition shapeDef, Vector3 worldPos, Domains shapeDomain)
        {
            if (_isInShapeMode || _isShapePrep) return;
            if (!shapeDef) return;

            _isInShapeMode = true;

            // Change the player's domain to match the shape's domain color
            var player = gameData.LocalPlayer;
            if (player is Player p)
                p.SetDomain(shapeDomain);

            // Freeze the player
            var vessel = player?.Vessel;
            if (vessel?.VesselStatus != null)
                vessel.VesselStatus.IsStationary = true;

            // Nuke all freestyle objects
            if (segmentSpawner)
            {
                segmentSpawner.NukeTheTrails();
                segmentSpawner.enabled = false;
            }
            if (cellScript)
            {
                cellScript.SetLifeFormsActive(false);
                cellScript.enabled = false;
            }
            if (localCrystalManager)
            {
                localCrystalManager.ManualTurnEnded();
                localCrystalManager.enabled = false;
            }

            ClearPlayerTrails();

            // Start shape drawing sequence (places player, shows preview, fires OnPreviewComplete)
            gameData.StartTurn();
            shapeDrawingManager.StartShapeSequence(shapeDef, worldPos);
        }

        /// <summary>
        /// Shape drawing finished → show connecting panel → ready → countdown → BeginFreestyle.
        /// </summary>
        void OnShapeDrawingFinished()
        {
            _isInShapeMode = false;

            var vessel = gameData.LocalPlayer?.Vessel;

            if (CameraManager.Instance)
            {
                CameraManager.Instance.SetCloseCameraActive();
                CameraManager.Instance.SnapPlayerCameraToTarget();
            }

            if (vessel?.VesselStatus != null)
                vessel.VesselStatus.VesselHUDController?.ShowHUD();

            ClearPlayerTrails();

            // Show connecting panel → ready → countdown → BeginFreestyle
            if (miniGameHUD)
                miniGameHUD.ShowConnectingFlow();
            else
                RaiseToggleReadyButtonEvent(true);
        }

        // ── Freestyle ────────────────────────────────────────────────────────

        /// <summary>
        /// Starts actual freestyle gameplay. Called after initial countdown
        /// or when returning from shape mode.
        /// </summary>
        void BeginFreestyle()
        {
            var vessel = gameData.LocalPlayer?.Vessel;
            if (vessel?.VesselStatus != null)
            {
                vessel.VesselStatus.IsStationary = false;
                vessel.VesselStatus.VesselHUDController?.ShowHUD();
            }

            // Enable systems BEFORE firing the turn event so their OnEnable
            // subscriptions are active when OnMiniGameTurnStarted fires.
            if (cellScript)
            {
                cellScript.enabled = true;
                cellScript.SetLifeFormsActive(true);
            }
            if (localCrystalManager) localCrystalManager.enabled = true;
            if (segmentSpawner)
            {
                segmentSpawner.enabled = true;
                segmentSpawner.Initialize();
            }

            gameData.SetPlayersActive();
            gameData.StartTurn();
        }

        // ── Utilities ────────────────────────────────────────────────────────

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
