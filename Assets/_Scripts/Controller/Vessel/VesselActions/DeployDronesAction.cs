using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
{
    public class DeployDronesAction : ShipAction
    {
        [SerializeField] GameObject dronePrefab;
        [SerializeField] BoidController boidController;
        public override void StartAction()
        {
            boidController.TransferDrone(false);
        }

        public override void StopAction()
        {
            // do nothing
        }
    }
}
