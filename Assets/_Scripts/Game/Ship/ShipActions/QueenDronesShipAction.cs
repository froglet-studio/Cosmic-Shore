using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class QueenDronesShipAction : ShipAction
    {
        [SerializeField] GameObject dronePrefab;
        [SerializeField] int Drones = 10;
        [SerializeField] BoidController boidController;


        public override void StartAction()
        {
            for (int i = 0; i < Drones; i++)
            {
                boidController.SpawnDrone(transform, true);
            }           
        }

        public override void StopAction()
        {
            // do nothing
        }
    }
}
