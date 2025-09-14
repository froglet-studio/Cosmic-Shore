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

            // TODO - set the hostile pilot's vessel and AI skill level based on the intensity level
            // hostilePilot.Vessel.VesselStatus.AIPilot.SkillLevel = .4f + IntensityLevel*.15f;
        }
    }
}