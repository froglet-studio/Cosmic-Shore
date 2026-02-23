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

        [Header("Trigger Placement")]
        [SerializeField] float triggerRingRadius = 40f;
        [SerializeField] float triggerScale = 0.4f;
        [SerializeField] float triggerForwardOffset = 50f;

        protected override bool HasEndGame => false;
        protected override bool ShowEndGameSequence => false;

        bool _isInLobby;

        protected override void OnEnable()
        {
            base.OnEnable();
            ShapeSignEvents.OnShapeSelected += HandleShapeSignSelected;
            if (shapeDrawingManager) shapeDrawingManager.OnFreestyleResumed.AddListener(OnShapeDrawingFinished);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ShapeSignEvents.OnShapeSelected -= HandleShapeSignSelected;
            if (shapeDrawingManager) shapeDrawingManager.OnFreestyleResumed.RemoveListener(OnShapeDrawingFinished);
        }

        void OnShapeDrawingFinished()
        {
            EnterLobby();
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

            // Position mode triggers in front of the player
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

                // Arrange in a small arc in front of the player
                float angle = ((i - (activeTriggers.Count - 1) * 0.5f) / Mathf.Max(1, activeTriggers.Count - 1)) * Mathf.PI * 0.5f;
                var offset = new Vector3(Mathf.Sin(angle) * triggerRingRadius, 0f, Mathf.Cos(angle) * triggerRingRadius);
                trigger.transform.position = triggerCenter + offset;
                trigger.transform.localScale = Vector3.one * triggerScale;

                // Face the player
                var dirToPlayer = (center - trigger.transform.position).normalized;
                if (dirToPlayer != Vector3.zero)
                    trigger.transform.rotation = Quaternion.LookRotation(dirToPlayer, Vector3.up);
            }

            // Show shape sign spawner (button-based signs) near the player
            if (shapeSignSpawner)
                shapeSignSpawner.ShowSigns(triggerCenter);
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
