using CosmicShore.Core;
using CosmicShore.Game.AI;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class BotDuelMiniGame : MiniGame 
    {
        [SerializeField] Player hostilePilot;
        protected override void Start()
        {
            base.Start();
            hostilePilot.Ship.GetComponent<AIPilot>().SkillLevel = .4f + (IntensityLevel*.15f);
            //ActivePlayer.GetComponent<AIPilot>().AutoPilotEnabled = true;
            //ActivePlayer.GetComponent<ShipStatus>().AutoPilotEnabled = true;
        }
    }
}