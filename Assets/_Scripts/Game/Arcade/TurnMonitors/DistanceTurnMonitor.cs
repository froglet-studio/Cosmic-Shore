using CosmicShore.Game.Arcade;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class DistanceTurnMonitor : TurnMonitor
    {
        [SerializeField] float distance;
        
        // Use MiniGameData instead to get player infos.
        // [SerializeField] MiniGame game;
        
        float distanceTraveled;

        public override bool CheckForEndOfTurn() => distanceTraveled > distance;

        protected override void StartTurn() => distanceTraveled = 0;

        protected override void RestrictedUpdate()
        { 
            float speed = 0f; // game.ActivePlayer.Ship.ShipStatus.Speed;
            distanceTraveled += speed * Time.deltaTime;

            string message = ((int)(distance - distanceTraveled)).ToString();
            onUpdateTurnMonitorDisplay.Raise(message);
        }
    }
}
