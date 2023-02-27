using StarWriter.Core;
using UnityEngine;

public class VolumeCreatedTurnMonitor : TurnMonitor
{
    [SerializeField] float Amount;

    public override bool CheckForEndOfTurn()
    {
        return Amount > StatsManager.Instance.playerStats[GameManager.Instance.player.PlayerName].volumeCreated;
    }

    public override void NewTurn()
    {
        StatsManager.Instance.ResetStats();
    }
}