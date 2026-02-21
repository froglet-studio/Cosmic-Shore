using CosmicShore.Core;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.App.UI.Modals
{
    public class SettingsModal : ModalWindowManager
    {
        [Inject] GameSetting gameSetting;

        public void ToggleMusicEnabledSetting()
        {
            gameSetting.ChangeMusicEnabledSetting();
        }
        public void AdjustMusicLevel(float level)
        {
            Debug.Log($"Music Level: {level}");
            gameSetting.SetMusicLevel(level);
        }
        public void AdjustSFXLevel(float level)
        {
            gameSetting.SetSFXLevel(level);
        }
        public void AdjustHapticsLevel(float level)
        {
            gameSetting.SetHapticsLevel(level);
        }
        public void ToggleSFXEnabledSetting()
        {
            gameSetting.ChangeSFXEnabledSetting();
        }
        public void ToggleHapticEnabledSetting()
        {
            gameSetting.ChangeHapticsEnabledSetting();
        }
        public void ToggleInvertYEnabledSetting()
        {
            gameSetting.ChangeInvertYEnabledStatus();
        }
        public void ToggleInvertThrottleEnabledSetting()
        {
            gameSetting.ChangeInvertThrottleEnabledStatus();
        }
        public void ToggleJoystickVisualsEnabledSetting()
        {
            gameSetting.ChangeJoystickVisualsStatus();
        }
    }
}
