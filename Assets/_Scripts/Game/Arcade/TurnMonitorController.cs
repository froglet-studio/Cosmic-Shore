using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Single responsibility: own a list of TurnMonitor and expose a clean API.
    /// </summary>
    public class TurnMonitorController : MonoBehaviour 
    {
        [SerializeField] List<TurnMonitor> monitors;

        public void StartMonitors()
        {
            foreach(var m in monitors) 
                m.StartMonitor();
        }

        public void PauseMonitors()
        {
            foreach(var m in monitors) 
                m.Pause();
        }

        public void StopMonitors()
        {
            foreach(var m in monitors)
                m.StopMonitor();
        }

        public void ResumeMonitors()
        {
            foreach(var m in monitors) 
                m.Resume();
        }

        public bool CheckEndOfTurn() => monitors.Any(m => m.CheckForEndOfTurn());
    }
}