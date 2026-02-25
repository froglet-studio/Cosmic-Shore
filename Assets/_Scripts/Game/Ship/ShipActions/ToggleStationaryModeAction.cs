using CosmicShore.Game.Ship;
using UnityEngine;
using CosmicShore.Game.Ship.R_ShipActions.DataContainers;
using CosmicShore.Game.Ship.R_ShipActions.Executors;
using CosmicShore.Models.Enums;
namespace CosmicShore.Game.Ship.ShipActions
{
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
                        Vessel.VesselStatus.VesselPrismController.StopSpawn();
                        seedAssembler.BeginBonding();
                    }
                    else
                    {
                        Vessel.VesselStatus.VesselPrismController.StopSpawn();
                    }
                }
                else
                {
                    Vessel.VesselStatus.VesselPrismController.StartSpawn();
                    seedAssembler.StopSeedCompletely(); 
                }
            }
            else
            {
                if (isOn) Vessel.VesselStatus.VesselPrismController.StopSpawn();
                else      Vessel.VesselStatus.VesselPrismController.StartSpawn();
            }
        }
    
        public override void StopAction()
        {

        }
    }
}
