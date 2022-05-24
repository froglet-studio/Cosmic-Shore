using UnityEngine;
using StarWriter.Core;
using System;

namespace StarWriter.UI
{
    public class MusicToggleSynchrornizer : MonoBehaviour
    {
        private bool isMuted = false;
        public SwitchToggle switchToggle;

        void Start()
        {
            GameSetting gameSettings = GameSetting.Instance;
            isMuted = gameSettings.IsMuted;

            switchToggle = GetComponent<SwitchToggle>();
            SyncMusicStatus(isMuted);
        }

        private void OnEnable()
        {
            GameSetting.OnChangeAudioMuteStatus += SyncMusicStatus;
        }

        private void OnDisable()
        {
            GameSetting.OnChangeAudioMuteStatus -= SyncMusicStatus;
        }

        private void SyncMusicStatus(bool status)
        {
            isMuted = status;
            switchToggle.SetToggleValue(isMuted);
        }
    }

}
