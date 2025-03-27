using CosmicShore.App.Systems.Squads;
using CosmicShore.App.UI.Views;
using CosmicShore.Core;
using TMPro;
using UnityEngine;

namespace CosmicShore.App.UI.Modals
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
            Arcade.Instance.LaunchMission(Mission.Mode, SquadSystem.SquadLeader, Intensity);
        }

        public void SetIntensity(float intensity)
        {
            Intensity = (int)intensity;
            IntensityText.text = intensity.ToString();
        }
    }
}