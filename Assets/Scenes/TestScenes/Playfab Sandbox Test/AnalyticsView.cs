using System;
using System.Collections;
using System.Collections.Generic;
using _Scripts._Core.Playfab_Models;
using UnityEngine;
using UnityEngine.UI;

public class AnalyticsView : MonoBehaviour
{
    // Start is called before the first frame update
    [Header("User Data Operation")]
    [SerializeField] private Button getUserData;
    [SerializeField] private Button setUserData;
    [SerializeField] private Button deleteData;
    
    [Header("PlayStream Events")]
    [SerializeField] private Button generatePlayerEvent;
    
    // private field
    private Dictionary<string, string> userData;

    private void Start()
    {
        getUserData.onClick.AddListener(OnGetUserData);
        setUserData.onClick.AddListener(OnSetUserData);
        deleteData.onClick.AddListener(OnDeleteData);
        generatePlayerEvent.onClick.AddListener(OnGeneratingPlayerEvent);
        userData = new Dictionary<string, string>()
        {
            { "Ship", "Manta" },
            { "Playtime", "60" }
        };
    }

    void OnGetUserData()
    {
        List<string> keyList = new List<string>(userData.Keys);
        AnalyticsController.Instance.GetUserData(keyList);
    }

    void OnSetUserData()
    {
        AnalyticsController.Instance.SetUserData(userData);
    }

    void OnDeleteData()
    {
        List<string> keysToRemove = new List<string>(userData.Keys);
        AnalyticsController.Instance.DeleteUserDataByKeys(keysToRemove);
    }

    void OnGeneratingPlayerEvent()
    {
        var playerEvent = new PlayerEvent()
        {
            Body = new Dictionary<string, object> { { "Level Complete", "true" } },
            EventName = "player_level_complete_event",
            CustomTags = null,
            Timestamp = DateTime.Now
        };
        AnalyticsController.Instance.SendPlayerEvent(playerEvent);
    }
}
