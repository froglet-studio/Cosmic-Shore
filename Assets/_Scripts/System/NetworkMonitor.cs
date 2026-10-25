using CosmicShore.Utility;
using CosmicShore.ScriptableObjects;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

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
        readonly Func<bool> _isReachable;

        /// <param name="isReachable">The reachability probe. Defaults to
        /// <c>Application.internetReachability</c>; a test passes its own to drive
        /// <see cref="Poll"/> through a network loss.</param>
        public NetworkMonitor(NetworkMonitorDataVariable networkMonitorDataVariable, Func<bool> isReachable = null)
        {
            _networkMonitorDataVariable = networkMonitorDataVariable;
            _isReachable = isReachable ?? IsCurrentlyReachable;
            _connected = _isReachable(); // initialize current state

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

        async UniTaskVoid MonitorLoopAsync(int intervalSeconds, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Poll();
                await UniTask.Delay(TimeSpan.FromSeconds(intervalSeconds), DelayType.UnscaledDeltaTime, cancellationToken: token);
            }
        }

        /// <summary>
        /// One reachability check: raises <c>OnNetworkLost</c> / <c>OnNetworkFound</c> on a
        /// transition and does nothing else. In particular it never touches the session - a network
        /// that dies mid-session gets a notice (DisconnectNotice), never an in-place switch to a
        /// loopback host (offline case 4, HARDENING_PLAN_STEAM_LAUNCH.md §4.1).
        /// </summary>
        internal void Poll()
        {
            bool reachable = _isReachable();

            if (!reachable && _connected)
            {
                _connected = false;
                _networkMonitorData.IsOnline = false;
                _networkMonitorData.LastTransitionUnscaledTime = Time.unscaledTime;
                _networkMonitorData.OnNetworkLost?.Raise();
                CSDebug.LogVerbose(CSLogChannel.Boot, $"[NetworkMonitor] Online -> Offline (reach={Application.internetReachability}, t={Time.unscaledTime:F1}s)");
            }
            else if (reachable && !_connected)
            {
                _connected = true;
                _networkMonitorData.IsOnline = true;
                _networkMonitorData.LastTransitionUnscaledTime = Time.unscaledTime;
                _networkMonitorData.OnNetworkFound?.Raise();
                CSDebug.LogVerbose(CSLogChannel.Boot, $"[NetworkMonitor] Offline -> Online (reach={Application.internetReachability}, t={Time.unscaledTime:F1}s)");
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
