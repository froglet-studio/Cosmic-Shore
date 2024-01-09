using System;
using UnityEngine;
using Zenject;
using ILogger = NLog.ILogger;
using Random = UnityEngine.Random;

namespace CosmicShore.Integrations.Loxodon.NLog
{
    public class NLogger : MonoBehaviour
    {
        [Inject] private ILogger _log;
        // Start is called before the first frame update
        private void Start()
        {
            _log.Debug("This is my NLogger debug test.");
        }

        // Update is called once per frame
        void Update()
        {
            var r = Random.Range(0, 3);
            switch (r)
            {
                case 0:
                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug("This is NLog debug test.frame count: {0}", Time.frameCount);
                    }
                    break;
                case 1:
                    if (_log.IsInfoEnabled)
                    {
                        _log.Info("This is a NLog info test.");
                    }
                    break;
                case 2:
                    if (_log.IsWarnEnabled)
                    {
                        _log.Warn("This is a NLog warn test.");
                    }
                    break;
                case 3:
                    if (_log.IsErrorEnabled)
                    {
                        _log.Error(new Exception("This is a NLog error test."));
                    }
                    break;
                case 4:
                    if (_log.IsFatalEnabled)
                    {
                        _log.Fatal(new Exception("This is a NLog fatal test."));
                    }
                    break;
            }
        }
    }
}
