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
    

    public override void Initialize(IVessel vessel)
    {
        base.Initialize(vessel);

        if (seedAssembler != null)
            seedAssembler.Initialize(Vessel);
    }

    public override void StartAction()
    {
        VesselStatus.IsStationary = !VesselStatus.IsStationary;
        var isOn = VesselStatus.IsStationary;

        if (mode == Mode.Serpent && seedAssembler != null)
        {
            if (isOn)
            {
                if (seedAssembler.StartSeed())
                {
                    Vessel.VesselStatus.TrailSpawner.PauseTrailSpawner();
                    seedAssembler.BeginBonding();
                }
                else
                {
                    Vessel.VesselStatus.TrailSpawner.PauseTrailSpawner();
                }
            }
            else
            {
                Vessel.VesselStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(0);
                seedAssembler.StopSeedCompletely(); 
            }
        }
        else
        {
            if (isOn) Vessel.VesselStatus.TrailSpawner.PauseTrailSpawner();
            else      Vessel.VesselStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(0);
        }
    }



    public override void StopAction()
    {

    }
}