using CosmicShore;
using CosmicShore.Game;
using UnityEngine;

public class ToggleStationaryModeAction : ShipAction
{
    private enum Mode { Sparrow, Serpent }

    [Header("Mode")]
    [SerializeField] private Mode mode;

    [Header("Seed (Serpent mode)")]
    [SerializeField] private SeedAssemblerConfigurator seedAssembler;
    

    public override void Initialize(IShip ship)
    {
        base.Initialize(ship);

        if (seedAssembler != null)
            seedAssembler.Initialize(Ship);
    }

    public override void StartAction()
    {
        ShipStatus.IsStationary = !ShipStatus.IsStationary;
        var isOn = ShipStatus.IsStationary;

        if (mode == Mode.Serpent && seedAssembler != null)
        {
            if (isOn)
            {
                if (seedAssembler.StartSeed())
                {
                    Ship.ShipStatus.TrailSpawner.PauseTrailSpawner();
                    seedAssembler.BeginBonding();
                }
                else
                {
                    Ship.ShipStatus.TrailSpawner.PauseTrailSpawner();
                }
            }
            else
            {
                Ship.ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(0);
                seedAssembler.StopSeedCompletely(); 
            }
        }
        else
        {
            if (isOn) Ship.ShipStatus.TrailSpawner.PauseTrailSpawner();
            else      Ship.ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(0);
        }
    }



    public override void StopAction()
    {

    }
}