using Loxodon.Log;
using Loxodon.Log.NLogger;
using UnityEngine;

namespace CosmicShore.Integrations.Loxodon.NLog
{
    public class NLogManager : MonoBehaviour
    {
        private void Awake()
        {
            // var _nlogFactory = new DefaultLogFactory();
            LogManager.Registry(NLogFactory.LoadInResources("Config"));
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            NLogFactory.Shutdown();
        }
    }
}
