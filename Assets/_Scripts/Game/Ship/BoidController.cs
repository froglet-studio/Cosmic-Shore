using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game.IO;
using CosmicShore.Environment.FlowField;

namespace CosmicShore
{
    public class BoidController : BoidManager
    {
        protected InputController inputController;
        [SerializeField] Ship ship;
        GameObject container;

        List<GameObject> queenDrones = new List<GameObject>();
        List<GameObject> moundDrones = new List<GameObject>();

        protected override void  Start()
        {
            inputController = ship.InputController;
            container = new GameObject("BoidContainer");
            container.transform.SetParent(ship.Player.transform);
        }

        public void SpawnDrone(Transform goal, bool isQueenDrone)
        {
            GameObject drone = Instantiate(boidPrefab.gameObject, ship.transform.position, Quaternion.identity);
            drone.transform.SetParent(container.transform);
            drone.transform.position = ship.transform.position;
            var boid = drone.GetComponent<Boid>();
            boid.DefaultGoal = goal;
            boid.Team = ship.Team;
            boid.Population = this;
            if (isQueenDrone)
            {
                queenDrones.Add(drone);
                ship.Player.GameCanvas.MiniGameHUD.SetRightNumberDisplay(queenDrones.Count);
            }
            else
            {
                moundDrones.Add(drone);
                ship.Player.GameCanvas.MiniGameHUD.SetLeftNumberDisplay(moundDrones.Count);
            }
        }

        public void TransferDrone(bool toQueen)
        {
            if (toQueen && moundDrones.Count > 0)
            {
                var drone = moundDrones[0];
                drone.GetComponent<Boid>().DefaultGoal = ship.transform;
                moundDrones.Remove(drone);
                queenDrones.Add(drone);
                ship.Player.GameCanvas.MiniGameHUD.SetLeftNumberDisplay(moundDrones.Count);
                ship.Player.GameCanvas.MiniGameHUD.SetRightNumberDisplay(queenDrones.Count);
            }
            else if (!toQueen && queenDrones.Count > 0)
            {
                var drone = queenDrones[0];
                //drone.GetComponent<Boid>().DefaultGoal = Goal;
                queenDrones.Remove(drone);
                moundDrones.Add(drone);
                ship.Player.GameCanvas.MiniGameHUD.SetRightNumberDisplay(queenDrones.Count);
                ship.Player.GameCanvas.MiniGameHUD.SetLeftNumberDisplay(moundDrones.Count);
            }
            else
            {
                Debug.Log("No drones to transfer");
            }
        }
    }
}
