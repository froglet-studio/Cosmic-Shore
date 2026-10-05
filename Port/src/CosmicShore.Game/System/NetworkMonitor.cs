// Ported verbatim from Assets/_Scripts/System/NetworkMonitor.cs
// (bootstrap arc 2026-07-08). Mechanical substitutions (README):
// Cysharp.Threading.Tasks → System.Threading.Tasks + CosmicShore.Engine.Tasks
// (UniTaskVoid → Task + .Forget(); UniTask.Delay(TimeSpan, ct) →
// GameTask.Delay(seconds, ct)); UnityEngine → CosmicShore.Engine.
// FULLY LIVE — the reachability poll runs against the engine's
// Application.internetReachability (a harness-settable mirror until a real
// probe lands), driving the NetworkMonitorData online flag + SOAP
// lost/found raises exactly as upstream.

using CosmicShore.Utility;
using CosmicShore.ScriptableObjects;
using System;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Engine.Tasks;
using CosmicShore.Engine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Plain C# class (no MonoBehaviour, no static).
    /// Create an instance, pass offlineMode in constructor, call StartMonitoring().
    /// </summary>
    public class NetworkMonitor
    {

        NetworkMonitorDataVariable  _networkMonitorDataVariable;
        NetworkMonitorData _networkMonitorData => _networkMonitorDataVariable.Value;

        bool _connected;
        bool _isRunning;
        CancellationTokenSource _cts;

        public NetworkMonitor(NetworkMonitorDataVariable networkMonitorDataVariable)
        {
            _networkMonitorDataVariable = networkMonitorDataVariable;
            _connected = IsCurrentlyReachable(); // initialize current state

            if (_networkMonitorData != null)
            {
                _networkMonitorData.IsOnline = _connected;
                _networkMonitorData.LastTransitionUnscaledTime = Time.unscaledTime;
            }
        }

        /// <summary>
        /// Starts a polling loop that checks reachability every <paramref name="intervalSeconds"/> seconds.
        /// Safe to call multiple times (won't start duplicates).
        /// </summary>
        public void StartMonitoring(int intervalSeconds = 5, bool fireInitialEvent = false)
        {
            if (_isRunning) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();

            if (fireInitialEvent)
                FireEventForCurrentState();

            MonitorLoopAsync(intervalSeconds, _cts.Token).Forget();
        }

        public void StopMonitoring()
        {
            if (!_isRunning) return;

            _isRunning = false;

            try { _cts?.Cancel(); } catch { /* ignore */ }
            _cts?.Dispose();
            _cts = null;
        }

        async Task MonitorLoopAsync(int intervalSeconds, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                bool reachable = IsCurrentlyReachable();

                if (!reachable && _connected)
                {
                    _connected = false;
                    _networkMonitorData.IsOnline = false;
                    _networkMonitorData.LastTransitionUnscaledTime = Time.unscaledTime;
                    _networkMonitorData.OnNetworkLost?.Raise();
                    CSDebug.Log($"[NetworkMonitor] Online → Offline (reach={Application.internetReachability}, t={Time.unscaledTime:F1}s)");
                }
                else if (reachable && !_connected)
                {
                    _connected = true;
                    _networkMonitorData.IsOnline = true;
                    _networkMonitorData.LastTransitionUnscaledTime = Time.unscaledTime;
                    _networkMonitorData.OnNetworkFound?.Raise();
                    CSDebug.Log($"[NetworkMonitor] Offline → Online (reach={Application.internetReachability}, t={Time.unscaledTime:F1}s)");
                }

                await GameTask.Delay((float)intervalSeconds, cancellationToken: token);
            }
        }

        bool IsCurrentlyReachable()
        {
            return Application.internetReachability != NetworkReachability.NotReachable;
        }

        void FireEventForCurrentState()
        {
            if (_connected) _networkMonitorData.OnNetworkFound?.Raise();
            else _networkMonitorData.OnNetworkLost?.Raise();
        }
    }
}
