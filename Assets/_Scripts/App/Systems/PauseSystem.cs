using System;
using UnityEngine;

namespace CosmicShore.App.Systems
{
    public static class PauseSystem
    {
        public static bool Paused { get; private set; }

        public static event Action OnGamePaused;
        public static event Action OnGameResumed;

        public static void TogglePauseGame()
        {
            if (Paused)
            {
                //Time.timeScale = 1f;
                Paused = false;
                OnGameResumed?.Invoke();
            }
            else
            {
                //Time.timeScale = 0f;
                Paused = true;
                OnGamePaused?.Invoke();
            }
        }
    }
}