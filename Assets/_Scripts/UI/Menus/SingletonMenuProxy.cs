using UnityEngine;
using StarWriter.Core;

public class SingletonMenuProxy : MonoBehaviour
{
    public void GameManagerOnClickTutorialButton()
    {
        GameManager.Instance.OnClickTutorialToggleButton();
    }
    public void GameSettingChangeAudioEnabledSetting()
    {
        GameSetting.Instance.ChangeAudioEnabledStatus();
    }
    public void GameSettingChangeGyroEnabledStatus()
    {
        GameSetting.Instance.ChangeGyroEnabledStatus();
    }
}
