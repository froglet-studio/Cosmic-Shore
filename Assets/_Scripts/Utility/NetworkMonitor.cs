using CosmicShore.Utility.Singleton;
using System;
using UnityEngine;

public class NetworkMonitor : SingletonPersistent<NetworkMonitor>
{
    public static Action OnNetworkConnectionLost;
    public static Action OnNetworkConnectionFound;

    [SerializeField] bool forceOffline;
    bool _connected;

    void Update()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable || forceOffline)
        {
            if (_connected)
            {
                _connected = false;
                OnNetworkConnectionLost?.Invoke();
                Debug.Log("NetworkMonitor - Error. Check internet connection");
            }
        }
        else
        {
            if (!_connected)
            {
                _connected = true;
                OnNetworkConnectionFound?.Invoke();
                Debug.Log("NetworkMonitor Success. Internet connection established");
            }
        }
    }
}