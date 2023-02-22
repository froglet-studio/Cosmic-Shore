using UnityEngine;
using StarWriter.Core;
using StarWriter.Core.HangerBuilder;

public class MainMenuActions : MonoBehaviour
{
    public void GameManagerOnClickTutorialButton()
    {
        GameManager.Instance.OnClickTutorialButton();
    }
    public void GameSettingChangeAudioEnabledSetting()
    {
        GameSetting.Instance.ChangeAudioEnabledStatus();
    }
    public void GameSettingInvertYEnabledStatus()
    {
        GameSetting.Instance.ChangeInvertYEnabledStatus();
    }

    private GameObject toggleObject;
    public void ToggleGameObject() //I think this is a better place for this to live
    {
        toggleObject = GetComponent<GameObject>();
        if (toggleObject.activeInHierarchy == true)
            toggleObject.SetActive(false);
        else
            toggleObject.SetActive(true);
    }
}