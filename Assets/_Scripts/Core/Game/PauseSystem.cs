using UnityEngine;

public class PauseSystem
{
    static bool isPaused = false;

    public static void TogglePauseGame()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0 : 1;
    }

    public static bool GetIsPaused()
    {
        return isPaused;
    }
}