using StarWriter.Core;
using UnityEngine;

public class CrystalCollisionTurnMonitor : TurnMonitor
{
    [SerializeField] int CrystalCollisions;
    [SerializeField] MiniGame Game;

    public override bool CheckForEndOfTurn()
    {
        if (!StatsManager.Instance.playerStats.ContainsKey(Game.ActivePlayer.PlayerName))
            return false;

        return StatsManager.Instance.playerStats[Game.ActivePlayer.PlayerName].crystalsCollected >= CrystalCollisions;
    }

    public override void NewTurn(string playerName)
    {
        StatsManager.Instance.ResetStats();

        // TODO: perhaps coerce stats manager to create an entry for the player here
    }
}