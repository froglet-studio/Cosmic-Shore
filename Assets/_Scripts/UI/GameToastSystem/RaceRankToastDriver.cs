using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Watches the per-player crystal ranking during an active turn and posts the race
    /// situations: <see cref="GameToastSituation.Overtake"/> ("A overtook B") the moment one
    /// player's crystal count passes another's, and <see cref="GameToastSituation.NewRaceLeader"/>
    /// when the top spot changes hands. Lives on the toast panel prefab next to
    /// <see cref="GameToastController"/> and SELF-GATES on config: it only runs when the
    /// current mode's toast config authors either situation (Skim Race does; modes without
    /// those entries cost one resolve at turn start and nothing after). Runs on every peer
    /// against the replicated RoundStats - toasts are local, nothing extra crosses the wire.
    /// </summary>
    public class RaceRankToastDriver : MonoBehaviour
    {
        [Header("References (wire on the prefab)")]
        [SerializeField] private GameToastLibrarySO library;

        [Min(0.1f)]
        [Tooltip("Seconds between ranking evaluations.")]
        [SerializeField] private float pollInterval = 0.5f;

        [Inject] private GameDataSO gameData;

        private bool _active;
        private float _nextPollTime;
        private bool _watchOvertakes;
        private bool _watchLeader;
        private string _lastLeader;

        private readonly Dictionary<string, int> _previousRank = new();
        private readonly List<IRoundStats> _ranking = new();

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
            _watchOvertakes = library.TryResolve(gameData.GameMode, GameToastSituation.Overtake, out _);
            _watchLeader = library.TryResolve(gameData.GameMode, GameToastSituation.NewRaceLeader, out _);
            _active = _watchOvertakes || _watchLeader;

            _lastLeader = null;
            _previousRank.Clear();
            _nextPollTime = 0f;
        }

        private void HandleTurnEnded() => _active = false;

        private void Update()
        {
            if (!_active) return;
            if (Time.time < _nextPollTime) return;
            _nextPollTime = Time.time + pollInterval;

            EvaluateRanking();
        }

        private void EvaluateRanking()
        {
            BuildRanking();
            if (_ranking.Count == 0) return;

            if (_watchLeader)
                CheckLeaderChange();

            if (_watchOvertakes && _previousRank.Count > 0)
                CheckOvertakes();

            SnapshotRanking();
        }

        /// <summary>
        /// Ranking = crystals collected descending; ties keep their previous relative order
        /// (stable OrderBy) so nobody "passes" anyone by merely equalling their count.
        /// </summary>
        private void BuildRanking()
        {
            _ranking.Clear();
            var list = gameData.RoundStatsList;
            for (int i = 0, count = list.Count; i < count; i++)
            {
                var stats = list[i];
                if (stats != null && !string.IsNullOrEmpty(stats.Name))
                    _ranking.Add(stats);
            }

            var ordered = _ranking
                .OrderByDescending(s => s.CrystalsCollected)
                .ThenBy(PreviousRankOf)
                .ToList();
            _ranking.Clear();
            _ranking.AddRange(ordered);
        }

        private int PreviousRankOf(IRoundStats stats) =>
            _previousRank.TryGetValue(stats.Name, out var rank) ? rank : int.MaxValue;

        private void CheckLeaderChange()
        {
            var top = _ranking[0];

            // A tied top spot has no single leader - keep announcing nothing until it breaks.
            if (_ranking.Count > 1 && _ranking[1].CrystalsCollected == top.CrystalsCollected)
                return;

            if (top.CrystalsCollected <= 0 || top.Name == _lastLeader)
                return;

            _lastLeader = top.Name;
            GameToastAPI.Post(GameToastSituation.NewRaceLeader, top.Domain, top.Name);
        }

        private void CheckOvertakes()
        {
            // A pair counts as an overtake when their order flipped since the last poll AND
            // the overtaker is now STRICTLY ahead on crystals (ties never announce).
            for (int i = 0; i < _ranking.Count; i++)
            {
                var ahead = _ranking[i];
                if (!_previousRank.TryGetValue(ahead.Name, out var aheadPrevRank))
                    continue;

                for (int j = i + 1; j < _ranking.Count; j++)
                {
                    var behind = _ranking[j];
                    if (!_previousRank.TryGetValue(behind.Name, out var behindPrevRank))
                        continue;

                    if (aheadPrevRank > behindPrevRank &&
                        ahead.CrystalsCollected > behind.CrystalsCollected)
                    {
                        GameToastAPI.Post(GameToastSituation.Overtake,
                            ahead.Domain, behind.Domain, ahead.Name, behind.Name);
                    }
                }
            }
        }

        private void SnapshotRanking()
        {
            _previousRank.Clear();
            for (int i = 0; i < _ranking.Count; i++)
                _previousRank[_ranking[i].Name] = i;
        }
    }
}
