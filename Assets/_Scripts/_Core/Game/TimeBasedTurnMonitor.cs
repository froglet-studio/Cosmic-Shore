using UnityEngine;

public class TimeBasedTurnMonitor : TurnMonitor
{
    [SerializeField] float duration;
    float elapsedTime;

    public override bool CheckForEndOfTurn()
    {
        return elapsedTime > duration;
    }

    public override void NewTurn()
    {
        throw new System.NotImplementedException();
    }

    void Update() {
        elapsedTime += Time.deltaTime;
    }
}