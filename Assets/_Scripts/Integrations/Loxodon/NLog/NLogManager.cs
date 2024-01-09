using Loxodon.Log;
using Loxodon.Log.NLogger;
using UnityEngine;
using Zenject;
using ILogger = NLog.ILogger;

namespace CosmicShore.Integrations.Loxodon.NLog
{
    public class NLogManager : MonoBehaviour
    {
        [Inject] private ILogger _nlog;
        private void Awake()
        {
            // var _nlogFactory = new DefaultLogFactory();
            LogManager.Registry(NLogFactory.LoadInResources("Config"));
            DontDestroyOnLoad(gameObject);
            if (_nlog.IsInfoEnabled)
            {
                _nlog.Info("NLogManager created.");
            }
        }

        private void OnDestroy()
        {
            NLogFactory.Shutdown();
        }
    }
}
