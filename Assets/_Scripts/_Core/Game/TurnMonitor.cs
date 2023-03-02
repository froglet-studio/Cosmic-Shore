using UnityEngine;

public abstract class TurnMonitor : MonoBehaviour
{
    public abstract bool CheckForEndOfTurn();
    public abstract void NewTurn(string playerName);
}