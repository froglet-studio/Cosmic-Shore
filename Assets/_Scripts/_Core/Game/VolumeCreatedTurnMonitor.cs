using StarWriter.Core;
using UnityEngine;

public class VolumeCreatedTurnMonitor : TurnMonitor
{
    [SerializeField] float Amount;
    [SerializeField] MiniGame Game;

    public override bool CheckForEndOfTurn()
    {
        return Amount >= StatsManager.Instance.playerStats[Game.ActivePlayer.PlayerName].volumeCreated;
    }

    public override void NewTurn(string playerName)
    {
        StatsManager.Instance.ResetStats();
    }
}