using UnityEngine;
using CosmicShore.Core;

public class MainMenuActions : MonoBehaviour
{
    public void GameSettingToggleMusicEnabledSetting()
    {
        GameSetting.Instance.ChangeMusicEnabledSetting();
    }
    public void GameSettingToggleSFXEnabledSetting()
    {
        GameSetting.Instance.ChangeSFXEnabledSetting();
    }
    public void GameSettingToggleHapticEnabledSetting()
    {
        GameSetting.Instance.ChangeHapticsEnabledSetting();
    }
    public void GameSettingToggleInvertYEnabledSetting()
    {
        GameSetting.Instance.ChangeInvertYEnabledStatus();
    }

    // TODO: P1 - i think this is deprecated and should be removed
    GameObject toggleObject;
    public void ToggleGameObject() //I think this is a better place for this to live
    {
        toggleObject = GetComponent<GameObject>();
        if (toggleObject.activeInHierarchy == true)
            toggleObject.SetActive(false);
        else
            toggleObject.SetActive(true);
    }
}