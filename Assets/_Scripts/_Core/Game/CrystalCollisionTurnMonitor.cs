using StarWriter.Core;
using UnityEngine;

public class CrystalCollisionTurnMonitor : TurnMonitor
{
    [SerializeField] int CrystalCollisions;

    public override bool CheckForEndOfTurn()
    {
        return StatsManager.Instance.playerStats[GameManager.Instance.player.PlayerName].crystalsCollected >= CrystalCollisions;
    }

    public override void NewTurn()
    {
        StatsManager.Instance.ResetStats();
    }
}