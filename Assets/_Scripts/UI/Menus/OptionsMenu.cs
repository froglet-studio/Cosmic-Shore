using StarWriter.Core;
using UnityEngine;

public class OptionsMenu : MonoBehaviour
{
    public void ToggleMusicEnabledSetting()
    {
        GameSetting.Instance.ChangeMusicEnabledSetting();
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
}