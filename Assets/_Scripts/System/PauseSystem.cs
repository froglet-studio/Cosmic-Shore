using System;
using UnityEngine;

namespace CosmicShore.Core
{
    public static class PauseSystem
    {
        public static bool Paused { get; private set; }

        public static event Action OnGamePaused;
        public static event Action OnGameResumed;

        // Exiting play while paused must not make the next session's first pause a no-op.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Paused = false;
            OnGamePaused = null;
            OnGameResumed = null;
        }
        
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