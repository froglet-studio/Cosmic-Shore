using CosmicShore.Game.ShapeDrawing;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

        protected override bool HasEndGame => false;
        protected override bool ShowEndGameSequence => false;

        bool _isInLobby;

        protected override void OnEnable()
        {
            base.OnEnable();
            ShapeSignEvents.OnShapeSelected += HandleShapeSignSelected;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ShapeSignEvents.OnShapeSelected -= HandleShapeSignSelected;
        }

        protected override void OnCountdownTimerEnded()
        {
            gameData.SetPlayersActive();
            EnterLobby();
        }

        void EnterLobby()
        {
            _isInLobby = true;

            // Disable environment systems — player flies freely in the lobby
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

            // Clear existing player trails so the lobby is clean
            ClearPlayerTrails();

            // Show mode selection triggers (collider-based)
            foreach (var trigger in modeTriggers.Where(trigger => trigger))
            {
                trigger.gameObject.SetActive(true);
                trigger.ResetTrigger();
                trigger.OnModeSelected.RemoveListener(HandleModeSelection);
                trigger.OnModeSelected.AddListener(HandleModeSelection);
            }

            // Show shape sign spawner (button-based signs)
            if (shapeSignSpawner) shapeSignSpawner.ShowSigns();
        }

        void ExitLobby()
        {
            _isInLobby = false;

            // Hide triggers
            foreach (var trigger in modeTriggers.Where(trigger => trigger)) trigger.gameObject.SetActive(false);

            // Hide signs
            if (shapeSignSpawner) shapeSignSpawner.HideSigns();

            gameData.StartTurn();
        }

        // ── Mode Selection Handlers ─────────────────────────────────────────

        /// <summary>Called by ModeSelectTrigger (collider-based selection).</summary>
        void HandleModeSelection(ShapeDefinition shapeDef)
        {
            if (!_isInLobby) return;
            ExitLobby();

            if (!shapeDef)
                StartStandardFreestyle();
            else
                StartShapeMode(shapeDef);
        }

        /// <summary>Called by ShapeSignEvents (button-based sign selection).</summary>
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

            // Clear any leftover player trails before entering shape drawing
            ClearPlayerTrails();

            shapeDrawingManager.StartShapeSequence(shapeDef, lobbyOrigin.position);
        }

        public void ReturnToLobby()
        {
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
