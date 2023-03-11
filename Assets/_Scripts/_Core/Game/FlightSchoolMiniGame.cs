using UnityEngine;

public class FlightSchoolMiniGame : MiniGame
{
    [SerializeField] Crystal Crystal;
    [SerializeField] Vector3 CrystalStartPosition;
    [SerializeField] Vector3 CrystalStartScale = Vector3.one;
    public static new ShipTypes PlayerShipType = ShipTypes.Manta;
    protected override void Start()
    {
        base.Start();
    }

    protected override void Update()
    {
        base.Update();

        if (!gameRunning) return;

        // TODO: pull this out into an "EliminationMonitor" class
        // if any volume was destroyed, there must have been a collision
        if (StatsManager.Instance.playerStats.ContainsKey(ActivePlayer.PlayerName) && StatsManager.Instance.playerStats[ActivePlayer.PlayerName].volumeDestroyed > 0)
        {
            EliminateActivePlayer();
            EndTurn();
        }
    }

    protected override void SetupTurn()
    {
        base.SetupTurn();
        
        ActivePlayer.Ship.DisableSkimmer();

        StatsManager.Instance.ResetStats(); // TODO: this belongs in the EliminationMonitor
        Crystal.transform.position = CrystalStartPosition;
        Crystal.transform.localScale = CrystalStartScale;
    }
}