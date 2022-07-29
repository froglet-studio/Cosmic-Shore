using UnityEngine;
using StarWriter.Core;

public class SingletonMenuProxy : MonoBehaviour
{
    public void GameManagerOnClickTutorialButton()
    {
        GameManager.Instance.OnClickTutorialButton();
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
