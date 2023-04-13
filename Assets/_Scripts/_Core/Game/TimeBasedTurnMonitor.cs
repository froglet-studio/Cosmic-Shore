using TMPro;
using UnityEngine;

public class TimeBasedTurnMonitor : TurnMonitor
{
    [SerializeField] float duration;
    [HideInInspector] public TMP_Text display;
    float elapsedTime;

    public override bool CheckForEndOfTurn()
    {
        return elapsedTime > duration;
    }

    public override void NewTurn(string playerName)
    {
        elapsedTime = 0;
    }

    void Update() {
        elapsedTime += Time.deltaTime;

        if (display!= null)
            display.text = ((int)duration - (int)elapsedTime).ToString();
    }
}