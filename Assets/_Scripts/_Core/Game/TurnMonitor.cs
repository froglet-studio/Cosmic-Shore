using UnityEngine;

public abstract class TurnMonitor : MonoBehaviour
{
    protected bool paused = false;
    public abstract bool CheckForEndOfTurn();
    public abstract void NewTurn(string playerName);
    public void PauseTurn() { paused = true; }
    public void ResumeTurn() { paused = false; }
}