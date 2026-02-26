using CosmicShore.Game.Arcade.Scoring;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Game.UI;
using CosmicShore.Models.ScriptableObjects;
using CosmicShore.Utility.DataContainers;
using CosmicShore.Game.Ship;
namespace CosmicShore.Game.Arcade.TurnMonitors
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
            float speed = 0f; // game.LocalPlayer.Vessel.VesselStatus.Speed;
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
