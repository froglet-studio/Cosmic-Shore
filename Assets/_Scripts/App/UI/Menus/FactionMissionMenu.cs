using CosmicShore.App.Systems.Squads;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.App.UI
{
    public class FactionMissionMenu : MonoBehaviour
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