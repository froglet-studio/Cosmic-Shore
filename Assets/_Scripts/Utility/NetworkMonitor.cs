using CosmicShore.Utility.Singleton;
using UnityEngine;

public class NetworkMonitor : SingletonPersistent<NetworkMonitor>
{
    public delegate void NetworkConnectionLostEvent();
    public static event NetworkConnectionLostEvent NetworkConnectionLost;

    public delegate void NetworkConnectionFoundEvent();
    public static event NetworkConnectionFoundEvent NetworkConnectionFound;

    [SerializeField] bool forceOffline;
    bool _connected;

    void Update()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable || forceOffline)
        {
            if (_connected)
            {
                _connected = false;
                NetworkConnectionLost?.Invoke();
                Debug.Log("NetworkMonitor - Error. Check internet connection");
            }
        }
        else
        {
            if (!_connected)
            {
                _connected = true;
                NetworkConnectionFound?.Invoke();
                Debug.Log("NetworkMonitor Success. Internet connection established");
            }
        }
    }
}