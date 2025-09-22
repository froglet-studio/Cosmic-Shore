using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Game.IO;
using CosmicShore.Game;
using CosmicShore.Utilities;
using Obvious.Soap;

namespace CosmicShore
{
    public class BoidController : BoidManager
    {
        [SerializeField] IVessel vessel;
        GameObject container;

        [SerializeField]
        ScriptableEventInt onMoundDroneSpawned;

        [SerializeField]
        ScriptableEventInt onQueenDroneSpawned;

        List<GameObject> queenDrones = new List<GameObject>();
        List<GameObject> moundDrones = new List<GameObject>();

        protected override void  Start()
        {
            container = new GameObject("BoidContainer");
            container.transform.SetParent(vessel.VesselStatus.Player.Transform);
        }

        public void SpawnDrone(Transform goal, bool isQueenDrone)
        {
            GameObject drone = Instantiate(boidPrefab.gameObject, vessel.Transform.position, Quaternion.identity);
            drone.transform.SetParent(container.transform);
            drone.transform.position = vessel.Transform.position;
            var boid = drone.GetComponent<Boid>();
            boid.DefaultGoal = goal;
            boid.domain = vessel.VesselStatus.Domain;
            boid.Population = this;
            if (isQueenDrone)
            {
                queenDrones.Add(drone);

                // TODO - Remove MiniGameHUD dependency
                // vessel.VesselStatus.Player.GameCanvas.MiniGameHUD.SetRightNumberDisplay(queenDrones.Count);
                onQueenDroneSpawned.Raise(queenDrones.Count);
            }
            else
            {
                moundDrones.Add(drone);

                // TODO - Remove MiniGameHUD dependency
                // vessel.VesselStatus.Player.GameCanvas.MiniGameHUD.SetLeftNumberDisplay(moundDrones.Count);
                onMoundDroneSpawned.Raise(moundDrones.Count);
            }
        }

        public void TransferDrone(bool toQueen)
        {
            if (toQueen && moundDrones.Count > 0)
            {
                var drone = moundDrones[0];
                drone.GetComponent<Boid>().DefaultGoal = vessel.Transform;
                moundDrones.Remove(drone);
                queenDrones.Add(drone);

                // TODO - Remove MiniGameHUD dependency
                onMoundDroneSpawned.Raise(moundDrones.Count);
                // vessel.VesselStatus.Player.GameCanvas.MiniGameHUD.SetLeftNumberDisplay(moundDrones.Count);
                onQueenDroneSpawned.Raise(queenDrones.Count);
                // vessel.VesselStatus.Player.GameCanvas.MiniGameHUD.SetRightNumberDisplay(queenDrones.Count);
            }
            else if (!toQueen && queenDrones.Count > 0)
            {
                var drone = queenDrones[0];
                //drone.GetComponent<Boid>().DefaultGoal = Goal;
                queenDrones.Remove(drone);
                moundDrones.Add(drone);

                // TODO - Remove MiniGameHUD dependency
                onQueenDroneSpawned.Raise(queenDrones.Count);
                // vessel.VesselStatus.Player.GameCanvas.MiniGameHUD.SetRightNumberDisplay(queenDrones.Count);
                onMoundDroneSpawned.Raise(moundDrones.Count);
                // vessel.VesselStatus.Player.GameCanvas.MiniGameHUD.SetLeftNumberDisplay(moundDrones.Count);
            }
            else
            {
                Debug.Log("No drones to transfer");
            }
        }
    }
}
