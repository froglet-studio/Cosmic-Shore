using _Scripts.Environment.FlowField;
using UnityEngine;

public class FlightSchoolMiniGame : MiniGame
{
    [SerializeField] Crystal Crystal;
    [SerializeField] Vector3 CrystalStartPosition;
    [SerializeField] Vector3 CrystalStartScale = Vector3.one;
    [SerializeField] SegmentSpawner SegmentSpawner;

    public static new ShipTypes PlayerShipType = ShipTypes.Manta;

    protected override void Start()
    {
        base.Start();

        Crystal.transform.position = CrystalStartPosition;
        Crystal.transform.localScale = CrystalStartScale;
        Crystal.SetOrigin(CrystalStartPosition);

        SegmentSpawner.Seed = new System.Random().Next();
        SegmentSpawner.numberOfSegments = IntensityLevel * 2 - 1;
        SegmentSpawner.origin.z = -(IntensityLevel - 1) * SegmentSpawner.StraightLineLength;
        SegmentSpawner.Initialize();
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

    protected override void EndTurn()
    {
        foreach (var turnMonitor in TurnMonitors)
        {
            if (turnMonitor is TimeBasedTurnMonitor timeMonitor)
            {
                if (timeMonitor.CheckForEndOfTurn()) 
                {
                    EliminateActivePlayer();
                    base.EndTurn();
                    return;
                }
            }
        }
        
        base.EndTurn();
    }

    protected override void SetupTurn()
    {
        base.SetupTurn();
        Crystal.transform.position = CrystalStartPosition;
        ActivePlayer.Ship.DisableSkimmer();

        StatsManager.Instance.ResetStats(); // TODO: this belongs in the EliminationMonitor
    }
}