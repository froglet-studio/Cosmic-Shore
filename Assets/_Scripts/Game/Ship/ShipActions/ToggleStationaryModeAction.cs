using System;
using CosmicShore;
using CosmicShore.Game;
using UnityEngine;

public class ToggleStationaryModeAction : ShipAction
{
    private enum Mode { Sparrow, Serpent }

    [Header("Mode")]
    [SerializeField] private Mode mode;

    [Header("Seed (Serpent mode)")]
    [SerializeField] private SeedAssemblerAction seedAssembler;

    public event Action<bool> OnStationaryToggled;

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

        if (isOn)
            Ship.ShipStatus.TrailSpawner.PauseTrailSpawner();
        else
            Ship.ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(0);

        OnStationaryToggled?.Invoke(isOn);

        if (mode != Mode.Serpent || seedAssembler == null) return;
        
        if (isOn) 
            seedAssembler.StopSeed();
        else
            seedAssembler.StartSeed();
       
    }

    public override void StopAction()
    {

    }
}