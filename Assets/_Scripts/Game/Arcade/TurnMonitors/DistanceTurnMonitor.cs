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

        public override void StartMonitor()
        {
            distanceTraveled = 0;
            UpdateUI();
            base.StartMonitor();
        }

        protected override void RestrictedUpdate()
        { 
            float speed = 0f; // game.ActivePlayer.Vessel.VesselStatus.Speed;
            distanceTraveled += speed * Time.deltaTime;
            UpdateUI();
        }

        void UpdateUI()
        {
            string message = ((int)(distance - distanceTraveled)).ToString();
            onUpdateTurnMonitorDisplay.Raise(message);
        }
    }
}
