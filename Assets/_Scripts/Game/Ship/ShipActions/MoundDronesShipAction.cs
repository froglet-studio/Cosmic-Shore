using CosmicShore.Game.Environment;
using CosmicShore.Utility.DataContainers;
using UnityEngine;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
namespace CosmicShore.Game.Ship
{
    public class MoundDronesShipAction : ShipAction
    {
        [SerializeField]
        CellRuntimeDataSO cellData;
        [SerializeField] GameObject dronePrefab;
        [SerializeField] int Drones = 5;
        [SerializeField] BoidController boidController;

        public override void StartAction()
        {
            for (int i = 0; i < Drones; i++)
            {
                // crystal = FindAnyObjectByType<Crystal>().transform;
                boidController.SpawnDrone(cellData.CrystalTransform, false);
            }
        }

        public override void StopAction()
        {
            // do nothing
        }
    }
}
