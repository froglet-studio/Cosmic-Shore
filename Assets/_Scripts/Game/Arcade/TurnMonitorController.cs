using System.Collections.Generic;
using System.Linq;
using CosmicShore.SOAP;
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

            gameData.InvokeGameTurnConditionsMet();
        }

        protected virtual void OnDisable()
        {
            UnsubscribeFromEvents();   
        }
        
        protected void SubscribeToEvents()
        {
            gameData.OnGameStarted += StartMonitors;
            gameData.OnMiniGameTurnEnd += PauseMonitors;
            gameData.OnMiniGameEnd += StopMonitors;
        }

        protected void UnsubscribeFromEvents()
        {
            gameData.OnGameStarted -= StartMonitors;
            gameData.OnMiniGameTurnEnd -= PauseMonitors;
            gameData.OnMiniGameEnd -= StopMonitors;
        }

        void StartMonitors()
        {
            isRunning = true;
            
            foreach(var m in monitors) 
                m.StartMonitor();
        }

        void PauseMonitors()
        {
            isRunning = false;
            
            foreach(var m in monitors) 
                m.Pause();
        }

        void StopMonitors()
        {
            foreach(var m in monitors)
                m.StopMonitor();
        }

        void ResumeMonitors()
        {
            foreach(var m in monitors) 
                m.Resume();
        }

        bool CheckEndOfTurn() => monitors.Any(m => m.CheckForEndOfTurn());
    }
}