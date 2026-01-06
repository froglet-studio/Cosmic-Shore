using System.Collections.Generic;
using System.Linq;
using CosmicShore.Soap;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;


namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Single responsibility: own a list of TurnMonitor and expose a clean API.
    /// </summary>
    public class TurnMonitorController : NetworkBehaviour 
    {
        [FormerlySerializedAs("miniGameData")] [SerializeField]
        GameDataSO gameData;
        
        [SerializeField] 
        List<TurnMonitor> monitors;

        bool isRunning;
        
        protected virtual void OnEnable()
        {
            SubscribeToEvents();
        }

        void Update()
        {
            if (!isRunning)
                return;

            if (!CheckEndOfTurn())
                return;

            OnTurnEnded();
        }

        void OnTurnEnded()
        {
            isRunning = false;
            gameData.InvokeGameTurnConditionsMet();
        }

        protected virtual void OnDisable()
        {
            UnsubscribeFromEvents();
            StopMonitors();
        }
        
        protected void SubscribeToEvents()
        {
            gameData.OnMiniGameTurnStarted.OnRaised += StartMonitors;
            // gameData.OnMiniGameTurnEnd += PauseMonitors;
            gameData.OnMiniGameTurnEnd.OnRaised += StopMonitors;
            // gameData.OnMiniGameEnd += StopMonitors;
        }

        protected void UnsubscribeFromEvents()
        {
            gameData.OnMiniGameTurnStarted.OnRaised -= StartMonitors;
            // gameData.OnMiniGameTurnEnd -= PauseMonitors;
            gameData.OnMiniGameTurnEnd.OnRaised -= StopMonitors;
            // gameData.OnMiniGameEnd -= StopMonitors;
        }

        void StartMonitors()
        {
            isRunning = true;
            
            foreach(var m in monitors) 
                m.StartMonitor();
        }

        void StopMonitors()
        {
            isRunning = false;
            
            foreach(var m in monitors)
                m.StopMonitor();
        }

        bool CheckEndOfTurn() => monitors.Any(m => m.CheckForEndOfTurn());
    }
}