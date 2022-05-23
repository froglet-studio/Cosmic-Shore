using UnityEngine;
using StarWriter.Core;
using System;

namespace StarWriter.UI
{
    public class GyroToggleSynchronizer : MonoBehaviour
    {
        private bool isGyroEnabled = true;
        public SwitchToggle switchToggle;
  
        void Start()
        {
            GameSetting gameSettings = GameSetting.Instance;
            isGyroEnabled = gameSettings.IsGyroEnabled;

            switchToggle = GetComponent<SwitchToggle>();                           
        }

        private void OnEnable()
        {
            GameSetting.OnChangeGyroStatus += SyncGyroStatus;
        }

        private void OnDisable()
        {
            GameSetting.OnChangeGyroStatus -= SyncGyroStatus;
        }

        private void SyncGyroStatus(bool status)
        {
            isGyroEnabled = status;
            switchToggle.Toggled(isGyroEnabled);
        }
    }

}

