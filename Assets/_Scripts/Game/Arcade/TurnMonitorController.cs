using System.Collections.Generic;
using System.Linq;
using CosmicShore.SOAP;
using UnityEngine;


namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Single responsibility: own a list of TurnMonitor and expose a clean API.
    /// </summary>
    public class TurnMonitorController : MonoBehaviour 
    {
        [SerializeField]
        MiniGameDataSO miniGameData;
        
        [SerializeField] 
        List<TurnMonitor> monitors;

        bool isRunning;
        
        void OnEnable()
        {
            miniGameData.OnGameStarted += StartMonitors;
            miniGameData.OnMiniGameTurnEnd += PauseMonitors;
            miniGameData.OnMiniGameEnd += StopMonitors;
        }

        void Update()
        {
            if (!isRunning)
                return;

            if (!CheckEndOfTurn())
                return;

            miniGameData.InvokeGameTurnConditionsMet();
        }

        void OnDisable()
        {
            miniGameData.OnGameStarted -= StartMonitors;
            miniGameData.OnMiniGameTurnEnd -= PauseMonitors;
            miniGameData.OnMiniGameEnd -= StopMonitors;
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