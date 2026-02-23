using CosmicShore.Utilities;
using System;
using UnityEngine;
using CosmicShore.Utility;


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
                CSDebug.Log("NetworkMonitor - Error. Check internet connection");
            }
        }
        else
        {
            if (!_connected)
            {
                _connected = true;
                OnNetworkConnectionFound?.Invoke();
                CSDebug.Log("NetworkMonitor Success. Internet connection established");
            }
        }
    }
}