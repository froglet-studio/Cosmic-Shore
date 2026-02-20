using System;
using UnityEngine;

namespace CosmicShore.Systems
{
    public static class PauseSystem
    {
        public static bool Paused { get; private set; }

        public static event Action OnGamePaused;
        public static event Action OnGameResumed;
        
        public static void TogglePauseGame(bool pause)
        {
            if (Paused && !pause)
            {
                Time.timeScale = 1f;
                Paused = false;
                OnGameResumed?.Invoke();
            }
            else if (!Paused && pause)
            {
                Time.timeScale = 0f;
                Paused = true;
                OnGamePaused?.Invoke();
            }
        }
    }
}