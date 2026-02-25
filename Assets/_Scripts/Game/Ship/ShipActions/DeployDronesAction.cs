using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Game.Ship;
namespace CosmicShore.Game.Ship.ShipActions
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
