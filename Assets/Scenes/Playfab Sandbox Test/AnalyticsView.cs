using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AnalyticsView : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private Button getUserData;
    [SerializeField] private Button setUserData;
    [SerializeField] private Button deleteData;
    private Dictionary<string, string> userData;

    private void Start()
    {
        getUserData.onClick.AddListener(OnGetUserData);
        setUserData.onClick.AddListener(OnSetUserData);
        deleteData.onClick.AddListener(OnDeleteData);
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
}
