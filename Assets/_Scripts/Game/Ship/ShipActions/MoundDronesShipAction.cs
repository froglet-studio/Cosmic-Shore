using CosmicShore.Game;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore
{
    public class MoundDronesShipAction : ShipAction
    {
        [SerializeField]
        CellDataSO cellData;
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
