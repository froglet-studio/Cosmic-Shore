using CosmicShore.Game.AI;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class CellularDuelMiniGame : MiniGame 
    {
        [SerializeField] R_Player hostilePilot;
        protected override void Start()
        {
            base.Start();
            // hostilePilot.Ship.ShipStatus.AIPilot.SkillLevel = .4f + IntensityLevel*.15f;
        }
    }
}