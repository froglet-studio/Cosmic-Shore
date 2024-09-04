using System;
using System.Collections.Generic;
using CosmicShore.Integrations.PlayFab.PlayerModels;
using CosmicShore.Integrations.PlayFab.PlayStream;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class AnalyticsView : MonoBehaviour
{
    [Header("User Data Operation")]
    [SerializeField] private Button getUserData;
    [SerializeField] private Button setUserData;
    [SerializeField] private Button deleteData;
    
    [Header("PlayStream Events")]
    [SerializeField] private Button generatePlayerEvent;
    
    // private field
    private Dictionary<string, string> userData;

    [Inject] private AnalyticsController _analyticsController;

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
        // var keyList = new List<string>(userData.Keys);
        var keyList = new List<string> { "hi" };
        Debug.Log("I did something right?");
        _analyticsController.GetUserData(keyList);
    }

    void OnSetUserData()
    {
        _analyticsController.SetUserData(userData);
    }

    void OnDeleteData()
    {
        // List<string> keysToRemove = new List<string>(userData.Keys);
        var keysToRemove = new List<string> { "hi" };
        _analyticsController.DeleteUserDataByKeys(keysToRemove);
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
        _analyticsController.SendPlayerEvent(playerEvent);
    }
}
