using CosmicShore.Systems.Squads;
using CosmicShore.UI.Views;
using CosmicShore.Game.Environment;
using TMPro;
using UnityEngine;
using CosmicShore.Game.Environment.Prisms;
using CosmicShore.Game.Managers;
using CosmicShore.Game.Settings;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility;
using CosmicShore.Utility.PoolsAndBuffers;
namespace CosmicShore.UI.Modals
{
    public class FactionMissionModal : ModalWindowManager
    {
        [SerializeField] FactionMissionGameView GameView;
        [SerializeField] SO_Mission Mission;
        [SerializeField] int Intensity;
        [SerializeField] TMP_Text IntensityText;

        protected override void Start()
        {
            GameView.AssignModel(Mission);
            base.Start();
        }

        public void Play()
        {
            // Arcade.Instance.LaunchMission(Mission.Mode, SquadSystem.SquadLeader, Intensity);
        }

        public void SetIntensity(float intensity)
        {
            Intensity = (int)intensity;
            IntensityText.text = intensity.ToString();
        }
    }
}