using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Posts the per-player STAT toasts - "X collected a crystal", "X landed a rocket",
    /// "X bent a rival" - by watching the replicated <see cref="IRoundStats"/> of every player
    /// during an active turn. Every stat it reads (crystals, missile hits, debuff hits, hostile
    /// prisms destroyed, lifeform kills) is a server-written NetworkVariable on the persistent
    /// Player object, so the SAME toast fires on every peer with nothing extra crossing the
    /// wire - the reason this is a poll over RoundStats rather than a post at the credit site,
    /// which runs on the owner's machine (and the host's) and nowhere else.
    ///
    /// Lives on the toast panel prefab next to <see cref="GameToastController"/> and, like
    /// <see cref="RaceRankToastDriver"/>, SELF-GATES on config: a stat is only watched while the
    /// current mode's <see cref="GameToastConfigSO"/> authors its situation, and the entry's
    /// <see cref="GameToastDefinition.everyN"/> decides how often the total has to cross a
    /// multiple before the toast fires. A player first seen mid-turn (a late joiner, a client
    /// whose roster is still replicating) is SEEDED silently - the backlog is never announced.
    /// </summary>
    public class StatToastDriver : MonoBehaviour
    {
        [Header("References (wire on the prefab)")]
        [SerializeField] private GameToastLibrarySO library;

        [Min(0.1f)]
        [Tooltip("Seconds between stat polls. A toast lands at most this long after the stat replicates.")]
        [SerializeField] private float pollInterval = 0.25f;

        [Inject] private GameDataSO gameData;

        private readonly struct Watch
        {
            public readonly GameToastSituation Situation;
            public readonly Func<IRoundStats, int> Read;
            public Watch(GameToastSituation situation, Func<IRoundStats, int> read)
            {
                Situation = situation;
                Read = read;
            }
        }

        /// <summary>Every stat a toast can be authored against. Adding one is one line here + one enum member.</summary>
        private static readonly Watch[] Watches =
        {
            new(GameToastSituation.CrystalCollected,         s => s.CrystalsCollected),
            new(GameToastSituation.RocketHit,                s => s.MissileHitsLanded),
            new(GameToastSituation.BendLanded,               s => s.DebuffHitsLanded),
            new(GameToastSituation.PrismsDestroyedMilestone, s => s.HostilePrismsDestroyed),
            new(GameToastSituation.LifeformKilled,           s => s.LifeformsKilled),
        };

        private readonly List<(Watch watch, int everyN)> _active = new();
        private readonly Dictionary<(string name, GameToastSituation situation), int> _last = new();
        private bool _turnActive;
        private float _nextPollTime;

        private void Start()
        {
            gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised += HandleTurnEnded;
        }

        private void OnDestroy()
        {
            if (gameData == null) return;
            gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised -= HandleTurnEnded;
        }

        private void HandleTurnStarted()
        {
            _active.Clear();
            _last.Clear();
            _nextPollTime = 0f;

            for (int i = 0; i < Watches.Length; i++)
            {
                if (library.TryResolve(gameData.GameMode, Watches[i].Situation, out var definition))
                    _active.Add((Watches[i], Mathf.Max(1, definition.everyN)));
            }

            _turnActive = _active.Count > 0;
        }

        private void HandleTurnEnded() => _turnActive = false;

        private void Update()
        {
            if (!_turnActive) return;
            if (Time.time < _nextPollTime) return;
            _nextPollTime = Time.time + pollInterval;

            Poll();
        }

        private void Poll()
        {
            var list = gameData.RoundStatsList;
            if (list == null) return;

            // Resolved once per poll: the objective target is a property of the mode, not the player.
            int target = gameData.ScoringRule != null ? gameData.ScoringRule.TargetFor(gameData) : 0;

            for (int i = 0, count = list.Count; i < count; i++)
            {
                var stats = list[i];
                if (stats == null || string.IsNullOrEmpty(stats.Name)) continue;

                for (int w = 0; w < _active.Count; w++)
                {
                    var (watch, everyN) = _active[w];
                    int current = watch.Read(stats);
                    var key = (stats.Name, watch.Situation);

                    if (!_last.TryGetValue(key, out int previous) || current < previous)
                    {
                        // First sight of this player (or a reset): seed silently, announce nothing.
                        _last[key] = current;
                        continue;
                    }
                    if (current == previous) continue;

                    _last[key] = current;

                    // Fire when the total crosses a multiple of everyN (everyN 1 = every increase).
                    if (current / everyN > previous / everyN)
                    {
                        GameToastAPI.Post(watch.Situation, stats.Domain, stats.Name,
                            current.ToString(), (current - previous).ToString(), target.ToString());
                    }
                }
            }
        }
    }
}
