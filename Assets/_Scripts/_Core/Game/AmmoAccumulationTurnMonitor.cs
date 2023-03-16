using UnityEngine;

public class AmmoAccumulationTurnMonitor : TurnMonitor
{
    [SerializeField] float percent;
    [SerializeField] MiniGame Game;

    public override bool CheckForEndOfTurn()
    {
        return Game.ActivePlayer.Ship.ResourceSystem.CurrentAmmo / Game.ActivePlayer.Ship.ResourceSystem.MaxAmmo > percent / 100;
    }

    public override void NewTurn(string playerName)
    {
    }
}