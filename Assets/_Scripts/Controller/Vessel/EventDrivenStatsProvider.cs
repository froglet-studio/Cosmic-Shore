using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
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
            WatchPilots(null);
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
                if (verboseLogging) CSDebug.LogVerbose(CSLogChannel.VesselTelemetry, "[StatsProvider] Local vessel not ready yet - will retry.");
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
            if (verboseLogging && CSDebug.IsVerbose(CSLogChannel.VesselTelemetry))
                CSDebug.LogVerbose(CSLogChannel.VesselTelemetry, $"[StatsProvider] Discovered {telemetry.GetType().Name} with {allStats.Count} stat(s)");

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

            if (verboseLogging && CSDebug.IsVerbose(CSLogChannel.VesselTelemetry))
                CSDebug.LogVerbose(CSLogChannel.VesselTelemetry, $"[StatsProvider] Using profile list for {gameData.GameMode}: {stats.Count} stat(s)");
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
            var pilots = HumanPilots();
            WatchPilots(pilots);

            foreach (var (stat, _) in _subscriptions)
            {
                if (stat == null) continue;
                _latestValues.TryGetValue(stat, out var localValue);
                string value;
                if (pilots.Count <= 1)
                {
                    // Solo (or the only human): this machine's own number, as it always was.
                    if (pilots.Count == 1 && pilots[0].TryGetVesselStat(stat.name, out var synced)) localValue = synced;
                    value = stat.Format(localValue);
                }
                else
                {
                    var row = new List<PilotValue>(pilots.Count);
                    foreach (var p in pilots)
                    {
                        bool has = p.TryGetVesselStat(stat.name, out var v);
                        // The pilot on this machine counts here even before the report lands.
                        if (!has && ReferenceEquals(p, gameData.LocalPlayer?.RoundStats)) { v = localValue; has = true; }
                        row.Add(new PilotValue(p.Name, has, v));
                    }
                    value = FormatPerPilot(stat, row);
                }

                result.Add(new StatData
                {
                    Label = stat.Label,
                    Value = value,
                    Icon  = stat.Icon
                });
            }

            if (verboseLogging && CSDebug.IsVerbose(CSLogChannel.VesselTelemetry))
                CSDebug.LogVerbose(CSLogChannel.VesselTelemetry, $"[StatsProvider] GetStats returning {result.Count} stat(s)");

            return result;
        }

        // ── Every pilot's numbers (Docs/ScoringSystem/BUGS.md B21) ─────────────

        /// <summary>One pilot's value for one stat; <see cref="Has"/> false when that pilot never reported it.</summary>
        public readonly struct PilotValue
        {
            public readonly string Name;
            public readonly bool Has;
            public readonly float Value;

            public PilotValue(string name, bool has, float value)
            {
                Name = name;
                Has = has;
                Value = value;
            }
        }

        /// <summary>"Yash 12 · Garrett 39": each pilot in order, a dash for one with no value for this stat.</summary>
        public static string FormatPerPilot(VesselStatEventSO stat, IReadOnlyList<PilotValue> pilots)
        {
            var parts = new List<string>(pilots.Count);
            foreach (var p in pilots)
                parts.Add($"{(string.IsNullOrEmpty(p.Name) ? "?" : p.Name)} {(p.Has ? stat.Format(p.Value) : "-")}");
            return string.Join("  ·  ", parts);
        }

        /// <summary>The human pilots' stats, this machine's pilot first. AI fly no telemetry.</summary>
        List<RoundStats> HumanPilots()
        {
            var list = new List<RoundStats>();
            if (gameData?.Players == null) return list;
            var local = gameData.LocalPlayer?.RoundStats as RoundStats;
            if (local) list.Add(local);
            foreach (var player in gameData.Players)
            {
                if (player == null || player.IsInitializedAsAI) continue;
                if (player.RoundStats is RoundStats rs && rs && !list.Contains(rs)) list.Add(rs);
            }
            return list;
        }

        readonly List<RoundStats> _watched = new();

        /// <summary>Listens to the pilots on the board, so a value landing after it was drawn redraws it.</summary>
        void WatchPilots(List<RoundStats> pilots)
        {
            foreach (var rs in _watched)
                if (rs) rs.OnVesselStatsChanged -= OnPilotStatsChanged;
            _watched.Clear();
            if (pilots == null) return;
            foreach (var rs in pilots)
            {
                rs.OnVesselStatsChanged += OnPilotStatsChanged;
                _watched.Add(rs);
            }
        }

        void OnPilotStatsChanged(RoundStats _) => RaiseChanged();
    }
}
