using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore
{
    public class MoundDronesShipAction : ShipAction
    {
        [SerializeField] GameObject dronePrefab;
        Transform crystal;
        [SerializeField] int Drones = 5;
        [SerializeField] BoidController boidController;

        public override void StartAction()
        {
            for (int i = 0; i < Drones; i++)
            {
                // crystal = FindAnyObjectByType<Crystal>().transform;
                var crystalTransform = CrystalManager.Instance.GetCrystalTransform();
                boidController.SpawnDrone(crystalTransform, false);
            }
        }

        public override void StopAction()
        {
            // do nothing
        }
    }
}
