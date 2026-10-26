using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Reports each match to the server from EVERY player's machine (fair play, Threat Board T4 and T9;
    /// Docs/AntiCheat/README.md §4.2, §4.4; server side Tools/UGS/CloudCode/match-report.js).
    ///
    /// <para><b>Quitting (T9).</b> <c>start</c> goes when the first turn begins, <c>end</c> when the winner is
    /// known. A player who quits never sends <c>end</c>; the server counts that match as left at their next
    /// <c>start</c>. Nothing here asks the leaver to admit it.</para>
    ///
    /// <para><b>Host results (T4).</b> <c>end</c> carries the final scores as this game received them from the
    /// host, keyed by UGS player id, and the winner. The server compares the players' reports of the same match
    /// (<see cref="GameDataSO.MatchId"/>, which every player receives from the host) and records a signal against
    /// the host when they disagree.</para>
    ///
    /// <para>AI pilots have no UGS id and are left out. Attached by <see cref="MultiplayerMiniGameControllerBase"/>
    /// on every peer, only while <see cref="MatchReportGate.Enabled"/>.</para>
    /// </summary>
    public sealed class MatchReporter : MonoBehaviour
    {
        GameDataSO _gameData;
        bool _subscribed;
        bool _open;
        string _matchId, _mode, _hostId;
        readonly List<string> _players = new();

        public static void Attach(GameObject host, GameDataSO gameData)
        {
            if (!host.TryGetComponent(out MatchReporter reporter))
                reporter = host.AddComponent<MatchReporter>();
            reporter.Bind(gameData);
        }

        void Bind(GameDataSO gameData)
        {
            _gameData = gameData;
            if (_subscribed) return;
            _gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
            _gameData.OnWinnerCalculated.OnRaised += HandleWinnerCalculated;
            _gameData.OnResetForReplay.OnRaised += HandleResetForReplay;
            _subscribed = true;
        }

        void OnDestroy()
        {
            if (!_subscribed) return;
            _gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
            _gameData.OnWinnerCalculated.OnRaised -= HandleWinnerCalculated;
            _gameData.OnResetForReplay.OnRaised -= HandleResetForReplay;
            _subscribed = false;
        }

        void HandleResetForReplay() => _open = false;

        void HandleTurnStarted()
        {
            if (_open) return; // later turns of the same match
            if (string.IsNullOrEmpty(_gameData.MatchId)) return;

            _hostId = HostUgsId();
            if (string.IsNullOrEmpty(_hostId)) return;

            _matchId = _gameData.MatchId;
            _mode = _gameData.GameMode.ToString();
            _players.Clear();
            foreach (var p in _gameData.Players)
                if (p is Player np && !string.IsNullOrEmpty(np.UgsPlayerId) && !np.NetIsAI.Value)
                    _players.Add(np.UgsPlayerId);

            _open = true;
            ServerMatchReports.StartAsync(_matchId, _mode, _hostId, new List<string>(_players)).Forget();
        }

        void HandleWinnerCalculated()
        {
            if (!_open) return;
            _open = false;

            var scores = new List<ServerMatchReports.Score>(_gameData.RoundStatsList.Count);
            foreach (var stats in _gameData.RoundStatsList)
            {
                string id = UgsIdForName(stats.Name);
                if (!string.IsNullOrEmpty(id))
                    scores.Add(new ServerMatchReports.Score { id = id, score = stats.Score });
            }

            ServerMatchReports.EndAsync(_matchId, _mode, _hostId, new List<string>(_players), scores,
                UgsIdForName(_gameData.WinnerName)).Forget();
        }

        string HostUgsId()
        {
            foreach (var p in _gameData.Players)
                if (p is Player np && np.OwnerClientId == NetworkManager.ServerClientId && !np.NetIsAI.Value)
                    return np.UgsPlayerId;
            return null;
        }

        string UgsIdForName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var p in _gameData.Players)
                if (p is Player np && !np.NetIsAI.Value && np.Name == name)
                    return np.UgsPlayerId;
            return null;
        }
    }
}
