using System.Collections.Generic;
using CosmicShore.Game.UI;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Subscribes to the VesselStatEventSO assets exposed by the local player's
    /// VesselTelemetry subclass, caches their latest values, and supplies them
    /// to the Scoreboard at game-end via GetStats().
    ///
    /// Works with any VesselTelemetry subclass — it discovers stat events via
    /// the base class's GetAllStats() regardless of vessel type.
    /// </summary>
    public class EventDrivenStatsProvider : ScoreboardStatsProvider
    {
        [Header("Data")]
        [SerializeField] private GameDataSO gameData;

        private readonly Dictionary<VesselStatEventSO, float> _latestValues = new();
        private readonly List<(VesselStatEventSO stat, System.Action<float> handler)> _subscriptions = new();

        // ── Unity Lifecycle ────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (gameData == null) return;
            gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
        }

        private void OnDisable()
        {
            if (gameData == null) return;
            gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
            Unsubscribe();
        }

        // ── Wiring ─────────────────────────────────────────────────────────────

        private void OnTurnStarted()
        {
            Unsubscribe();
            _latestValues.Clear();

            var vessel = gameData.LocalPlayer?.Vessel;
            if (vessel == null) return;

            VesselTelemetry telemetry = null;
            if (vessel is Component vesselComponent)
                telemetry = vesselComponent.GetComponent<VesselTelemetry>();

            if (telemetry == null)
            {
                Debug.LogWarning("[EventDrivenStatsProvider] No VesselTelemetry found on local vessel.");
                return;
            }

            foreach (var stat in telemetry.GetAllStats())
            {
                if (stat == null) continue;

                _latestValues[stat] = stat.CurrentValue;

                // Store named handler so we can unsubscribe cleanly
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

            return result;
        }
    }
}