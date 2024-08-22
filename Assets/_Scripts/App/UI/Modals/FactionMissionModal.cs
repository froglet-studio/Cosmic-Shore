using CosmicShore.App.Systems.Squads;
using CosmicShore.App.UI.Views;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.App.UI.Modals
{
    public class FactionMissionModal : ModalWindowManager
    {
        [SerializeField] FactionMissionGameView GameView;
        [SerializeField] SO_ArcadeGame MissionGame;
        [SerializeField] int Intensity;

        void Start()
        {
            GameView.AssignModel(MissionGame);
        }

        public void Play()
        {
            Arcade.Instance.LaunchFactionMission(MissionGame.Mode, SquadSystem.SquadLeader.Ship.Class, SquadSystem.SquadLeader.InitialResourceLevels, Intensity);
        }
    }
}