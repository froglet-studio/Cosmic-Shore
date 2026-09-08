using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Subscribes to VesselStatEventSO assets and caches their latest values
    /// for the end-game scoreboard.
    ///
    /// Resolves its stat list in priority order:
    /// 1. Explicit: stat SOs wired directly via <see cref="statsToTrack"/>. Subscription happens
    ///    on OnEnable - no timing dependency on vessel spawn.
    /// 2. Profile: when the explicit list is empty, the per-mode list from
    ///    <see cref="GameModeStatsProfileSO"/> (<c>Resources/GameModeStatsProfile</c>) keyed by
    ///    <c>GameDataSO.GameMode</c>. This is where the shared GameCanvas prefab gets its
    ///    per-mode stats without any scene override (`Docs/GAMECANVAS.md`).
    /// 3. Dynamic fallback: discovers stats from the local vessel's VesselTelemetry at
    ///    OnClientReady / OnMiniGameTurnStarted.
    ///
    /// Stats are only cleared on explicit reset - they persist across turn boundaries
    /// until the game ends, so the scoreboard always shows the final values.
    /// </summary>
    public class EventDrivenStatsProvider : ScoreboardStatsProvider
    {
        [Header("Data")]
        [Inject] private GameDataSO gameData;

        [Header("Stats (explicit override)")]
        [Tooltip("Leave EMPTY on the shared GameCanvas prefab so Resources/GameModeStatsProfile decides per mode. " +
                 "If populated, this list wins over the profile and over dynamic discovery.")]
        [SerializeField] private List<VesselStatEventSO> statsToTrack = new();

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        private readonly Dictionary<VesselStatEventSO, float> _latestValues = new();
        private readonly List<(VesselStatEventSO stat, System.Action<float> handler)> _subscriptions = new();

        // ── Unity Lifecycle ────────────────────────────────────────────────────

        private void OnEnable()
        {
            // Subscribe immediately to explicit stats - no vessel dependency
            if (statsToTrack != null && statsToTrack.Count > 0)
            {
                SubscribeToStats(statsToTrack);
            }
            else
            {
                TrySubscribeFromProfile();
            }

            if (gameData == null) return;
            gameData.OnClientReady.OnRaised         += TrySubscribeFromVessel;
            gameData.OnMiniGameTurnStarted.OnRaised += TrySubscribeFromVessel;
            gameData.OnResetForReplay.OnRaised      += HandleReplay;
        }

        private void OnDisable()
        {
            if (gameData != null)
            {
                gameData.OnClientReady.OnRaised         -= TrySubscribeFromVessel;
                gameData.OnMiniGameTurnStarted.OnRaised -= TrySubscribeFromVessel;
                gameData.OnResetForReplay.OnRaised      -= HandleReplay;
            }
            Unsubscribe();
        }

        // ── Wiring ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Fallback: discover stat SOs from the local vessel's telemetry and subscribe.
        /// Skips if already subscribed to anything (avoids duplicate handlers).
        /// </summary>
        private void TrySubscribeFromVessel()
        {
            // If explicit list is wired, we're already subscribed - skip dynamic discovery
            if (statsToTrack != null && statsToTrack.Count > 0) return;

            // Already subscribed via previous call? skip.
            if (_subscriptions.Count > 0) return;

            // The profile is asked again here because [Inject] lands after OnEnable on some
            // spawn paths, so gameData.GameMode may not have been readable the first time.
            if (TrySubscribeFromProfile()) return;

            var vessel = gameData?.LocalPlayer?.Vessel;
            if (vessel == null)
            {
                if (verboseLogging) Debug.Log("[StatsProvider] Local vessel not ready yet - will retry.");
                return;
            }

            VesselTelemetry telemetry = null;
            if (vessel is Component vesselComponent)
                telemetry = vesselComponent.GetComponent<VesselTelemetry>();

            if (telemetry == null)
            {
                CSDebug.LogWarning("[EventDrivenStatsProvider] No VesselTelemetry found on local vessel.");
                return;
            }

            var allStats = telemetry.GetAllStats();
            if (verboseLogging)
                Debug.Log($"[StatsProvider] Discovered {telemetry.GetType().Name} with {allStats.Count} stat(s)");

            if (allStats.Count == 0)
                CSDebug.LogWarning("[EventDrivenStatsProvider] Telemetry has zero registered stats.");

            SubscribeToStats(allStats);
        }

        /// <summary>
        /// The per-mode list from <see cref="GameModeStatsProfileSO"/>. Returns false when there is
        /// no profile, no readable game mode, or an empty list - the caller then falls through to
        /// telemetry discovery exactly as before the profile existed.
        /// </summary>
        private bool TrySubscribeFromProfile()
        {
            if (_subscriptions.Count > 0) return true;
            if (gameData == null) return false;

            var profile = GameModeStatsProfileSO.Load();
            if (profile == null) return false;

            var stats = profile.StatsFor(gameData.GameMode);
            if (stats == null || stats.Count == 0) return false;

            if (verboseLogging)
                Debug.Log($"[StatsProvider] Using profile list for {gameData.GameMode}: {stats.Count} stat(s)");
            SubscribeToStats(stats);
            return _subscriptions.Count > 0;
        }

        private void SubscribeToStats(IEnumerable<VesselStatEventSO> stats)
        {
            foreach (var stat in stats)
            {
                if (stat == null) continue;
                if (_subscriptions.Any(s => s.stat == stat)) continue; // already subscribed

                // Seed cache with current value (or 0)
                if (!_latestValues.ContainsKey(stat))
                    _latestValues[stat] = stat.CurrentValue;

                void Handler(float value) => _latestValues[stat] = value;
                stat.OnRaised += Handler;
                _subscriptions.Add((stat, Handler));

                if (verboseLogging) Debug.Log($"[StatsProvider] Subscribed to: '{stat.Label}'");
            }
        }

        private void Unsubscribe()
        {
            foreach (var (stat, handler) in _subscriptions)
                if (stat != null) stat.OnRaised -= handler;
            _subscriptions.Clear();
        }

        private void HandleReplay()
        {
            // Reset cache for replay, but keep subscriptions
            foreach (var key in _latestValues.Keys.ToList())
                _latestValues[key] = 0f;
        }

        // ── ScoreboardStatsProvider ────────────────────────────────────────────

        public override List<StatData> GetStats()
        {
            var result = new List<StatData>();

            foreach (var (stat, _) in _subscriptions)
            {
                if (stat == null) continue;
                _latestValues.TryGetValue(stat, out var value);
                result.Add(new StatData
                {
                    Label = stat.Label,
                    Value = stat.Format(value),
                    Icon  = stat.Icon
                });
            }

            if (verboseLogging)
            {
                Debug.Log($"[StatsProvider] GetStats returning {result.Count} stat(s)");
                foreach (var s in result)
                    Debug.Log($"[StatsProvider]   → {s.Label}: {s.Value}");
            }

            return result;
        }
    }
}
