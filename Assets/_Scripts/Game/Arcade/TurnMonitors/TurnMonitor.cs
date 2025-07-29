using System;
using Obvious.Soap;
using UnityEngine;
using TMPro;

namespace CosmicShore.Game.Arcade
{
    public abstract class TurnMonitor : MonoBehaviour
    {
        [SerializeField] 
        float _updateInterval = 1f;
        
        // Controls whether this monitor should eliminate the player when ending their turn
        [SerializeField] 
        protected bool eliminatesPlayer = false;

        [SerializeField] 
        protected ScriptableEventString onUpdateTurnMonitorDisplay;
        
        protected bool paused = false;
        

        private void Start() => InvokeRepeating(nameof(RestrictedUpdate), 0f, _updateInterval);

        public abstract bool CheckForEndOfTurn();
        public abstract void NewTurn(string playerName);
        public void PauseTurn() { paused = true; }
        public void ResumeTurn() { paused = false; }
        
        public bool ShouldEliminatePlayer() { return eliminatesPlayer; }

        protected virtual void RestrictedUpdate() { }
    }
}
