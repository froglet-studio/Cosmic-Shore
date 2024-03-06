using CosmicShore.Environment.FlowField;
using System.Collections;
using System.Collections.Generic;
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
                crystal = FindObjectOfType<Crystal>().transform;
                boidController.SpawnDrone(crystal, false);
            }
        }

        public override void StopAction()
        {
            // do nothing
        }
    }
}
