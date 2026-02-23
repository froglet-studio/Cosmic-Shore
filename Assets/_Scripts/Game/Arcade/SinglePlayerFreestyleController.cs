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

        protected override bool HasEndGame => false;
        protected override bool ShowEndGameSequence => false;

        bool _isInLobby;

        protected override void OnCountdownTimerEnded()
        {
            gameData.SetPlayersActive();
            EnterLobby();
        }

        void EnterLobby()
        {
            _isInLobby = true;
            if (cellScript) cellScript.enabled = false;
            if (segmentSpawner) segmentSpawner.NukeTheTrails();

            if (localCrystalManager)
            {
                localCrystalManager.ManualTurnEnded();
                localCrystalManager.enabled = false;
            }

            foreach (var trigger in modeTriggers.Where(trigger => trigger))
            {
                trigger.gameObject.SetActive(true);
                trigger.ResetTrigger();
                trigger.OnModeSelected.RemoveListener(HandleModeSelection); 
                trigger.OnModeSelected.AddListener(HandleModeSelection);
            }
        }

        void HandleModeSelection(ShapeDefinition shapeDef)
        {
            _isInLobby = false;

            // Hide triggers
            foreach (var trigger in modeTriggers.Where(trigger => trigger)) trigger.gameObject.SetActive(false);

            gameData.StartTurn();

            if (!shapeDef)
            {
                StartStandardFreestyle();
            }
            else
            {
                StartShapeMode(shapeDef);
            }
        }

        void StartStandardFreestyle()
        {
            Debug.Log("[Controller] Starting Standard Freestyle");
            
            if (cellScript) cellScript.enabled = true;

            if (localCrystalManager) localCrystalManager.enabled = true;
            if (segmentSpawner) segmentSpawner.Initialize();
            
            RaiseToggleReadyButtonEvent(true);
        }

        void StartShapeMode(ShapeDefinition shapeDef)
        {
            Debug.Log("[Controller] Starting Shape Mode");
            if (cellScript) cellScript.enabled = false;

            shapeDrawingManager.StartShapeSequence(shapeDef, lobbyOrigin.position);
        }

        public void ReturnToLobby()
        {
            shapeDrawingManager.ExitShapeMode();
            
            // Optional: End the "turn" when returning to lobby if you want to stop time tracking
            // gameData.EndTurn(); // (Only if your Base Controller supports this cleanly)

            EnterLobby();
        }
    }
}