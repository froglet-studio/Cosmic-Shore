using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Game.Ship;
namespace CosmicShore.Game.Ship
{
    public class RecallDronesAction : ShipAction
    {
        [SerializeField] GameObject dronePrefab;
        [SerializeField] BoidController boidController;
        public override void StartAction()
        {
            boidController.TransferDrone(true);
        }

        public override void StopAction()
        {
            // do nothing
        }
    }
}
