using System;
using System.Collections.Generic;
using Obvious.Soap;
using TMPro;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>Single responsibility: own a list of TurnMonitor and expose a clean API.</summary>
    public class TurnMonitorController : MonoBehaviour {
        [SerializeField] List<TurnMonitor> monitors;
        
        public void NewTurn(string playerName){ foreach(var m in monitors) m.NewTurn(playerName); }
        public void PauseAll(){ foreach(var m in monitors) m.PauseTurn(); }
        public void ResumeAll(){ foreach(var m in monitors) m.ResumeTurn(); }
        public bool CheckEndOfTurn(){ foreach(var m in monitors) if(m.CheckForEndOfTurn()) return true; return false; }
    }
}