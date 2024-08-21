 using UnityEngine;

namespace CosmicShore.App.Systems
{
    public class PauseSystem
    {
        public static bool Paused { get; private set; }

        public static void TogglePauseGame()
        {
            Paused = !Paused;
            Time.timeScale = Paused ? 0 : 1;
        }
    }
}