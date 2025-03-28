using UnityEngine;
using TMPro;

namespace CosmicShore.Game.Arcade
{
    public abstract class TurnMonitor : MonoBehaviour
    {
        [HideInInspector] public TMP_Text Display;
        protected bool paused = false;
        
        // Controls whether this monitor should eliminate the player when ending their turn
        [SerializeField] protected bool eliminatesPlayer = false;
        
        public abstract bool CheckForEndOfTurn();
        public abstract void NewTurn(string playerName);
        public void PauseTurn() { paused = true; }
        public void ResumeTurn() { paused = false; }
        
        public bool ShouldEliminatePlayer() { return eliminatesPlayer; }
    }
}
