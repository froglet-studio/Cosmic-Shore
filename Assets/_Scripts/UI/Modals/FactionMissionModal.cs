using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
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
            audioSystem.PlayMenuAudio(MenuAudioCategory.LetsGo);
            Arcade.Instance.LaunchMission(Mission.Mode, SquadSystem.SquadLeader?.Vessel, Intensity);
        }

        public void SetIntensity(float intensity)
        {
            Intensity = (int)intensity;
            IntensityText.text = intensity.ToString();
        }
    }
}
