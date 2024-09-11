using CosmicShore.App.Systems.Squads;
using CosmicShore.App.UI.Views;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.App.UI.Modals
{
    public class FactionMissionModal : ModalWindowManager
    {
        [SerializeField] FactionMissionGameView GameView;
        [SerializeField] SO_Mission Mission;
        [SerializeField] int Intensity;

        protected override void Start()
        {
            GameView.AssignModel(Mission);
            base.Start();
        }

        public void Play()
        {
            Arcade.Instance.LaunchMission(Mission.Mode, SquadSystem.SquadLeader, Intensity);
        }
    }
}