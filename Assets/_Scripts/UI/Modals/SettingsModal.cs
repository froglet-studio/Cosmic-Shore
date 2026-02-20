using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.App.UI.Modals
{
    public class SettingsModal : ModalWindowManager
    {
        public void ToggleMusicEnabledSetting()
        {
            GameSetting.Instance.ChangeMusicEnabledSetting();
        }
        public void AdjustMusicLevel(float level)
        {
            Debug.Log($"Music Level: {level}");
            GameSetting.Instance.SetMusicLevel(level);
        }
        public void AdjustSFXLevel(float level)
        {
            GameSetting.Instance.SetSFXLevel(level);
        }
        public void AdjustHapticsLevel(float level)
        {
            GameSetting.Instance.SetHapticsLevel(level);
        }
        public void ToggleSFXEnabledSetting()
        {
            GameSetting.Instance.ChangeSFXEnabledSetting();
        }
        public void ToggleHapticEnabledSetting()
        {
            GameSetting.Instance.ChangeHapticsEnabledSetting();
        }
        public void ToggleInvertYEnabledSetting()
        {
            GameSetting.Instance.ChangeInvertYEnabledStatus();
        }
        public void ToggleInvertThrottleEnabledSetting()
        {
            GameSetting.Instance.ChangeInvertThrottleEnabledStatus();
        }
        public void ToggleJoystickVisualsEnabledSetting()
        {
            GameSetting.Instance.ChangeJoystickVisualsStatus();
        }
    }
}