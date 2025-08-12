using CosmicShore.Game.Arcade;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class DistanceTurnMonitor : TurnMonitor
    {
        [SerializeField] float distance;
        [SerializeField] MiniGame game;
        float distanceTraveled;

        public override bool CheckForEndOfTurn()
        {
            if (paused) return false;

            return distanceTraveled > distance;
        }

        public override void NewTurn(string playerName)
        {
            distanceTraveled = 0;
        }

        protected override void RestrictedUpdate()
        { 
            if (paused) return;
            float speed = game.ActivePlayer.Ship.ShipStatus.Speed;
            distanceTraveled += speed * Time.deltaTime;

            string message = ((int)(distance - distanceTraveled)).ToString();
            onUpdateTurnMonitorDisplay.Raise(message);
        }
    }
}
