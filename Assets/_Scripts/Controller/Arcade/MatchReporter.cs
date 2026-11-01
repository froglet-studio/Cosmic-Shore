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
    /// <para><b>Host observations (T5, T6).</b> On the host, the fair-play signals its movement monitor raises during the
    /// match are sent with its <c>end</c> report, keyed by each guest's UGS id. The server files them on the guest and
    /// counts them only once a second host reported the same account, so one lying host cannot flag anyone.</para>
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
        readonly List<FairPlaySignal> _observed = new();
        bool _listening;

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
            StopListening();
            if (!_subscribed) return;
            _gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
            _gameData.OnWinnerCalculated.OnRaised -= HandleWinnerCalculated;
            _gameData.OnResetForReplay.OnRaised -= HandleResetForReplay;
            _subscribed = false;
        }

        void HandleResetForReplay()
        {
            _open = false;
            StopListening();
        }

        void StartListening()
        {
            _observed.Clear();
            var nm = NetworkManager.Singleton;
            if (_listening || nm == null || !nm.IsServer) return;     // only the host measures its guests
            FairPlaySignals.OnSignal += HandleSignal;
            _listening = true;
        }

        void StopListening()
        {
            if (!_listening) return;
            FairPlaySignals.OnSignal -= HandleSignal;
            _listening = false;
        }

        void HandleSignal(FairPlaySignal signal) => _observed.Add(signal);

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
            StartListening();
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

            var observed = BuildObserved();
            StopListening();
            ServerMatchReports.EndAsync(_matchId, _mode, _hostId, new List<string>(_players), scores,
                UgsIdForName(_gameData.WinnerName), observed).Forget();
        }

        /// <summary>The host's signals this match, as the server reads them: the guest's UGS id and a kind name.</summary>
        List<ServerMatchReports.Observed> BuildObserved()
        {
            if (_observed.Count == 0) return null;
            var list = new List<ServerMatchReports.Observed>(_observed.Count);
            foreach (var s in _observed)
            {
                string kind = s.Kind switch
                {
                    FairPlaySignalKind.SustainedSpeed => "speed",
                    FairPlaySignalKind.JumpRate => "jumps",
                    FairPlaySignalKind.AiLikeSteering => "steering",
                    _ => null,
                };
                string id = UgsIdForClient(s.OwnerClientId);
                if (kind == null || string.IsNullOrEmpty(id)) continue;
                list.Add(new ServerMatchReports.Observed { id = id, kind = kind, measured = s.Measured, limit = s.Limit });
            }
            return list;
        }

        string UgsIdForClient(ulong clientId)
        {
            foreach (var p in _gameData.Players)
                if (p is Player np && !np.NetIsAI.Value && np.OwnerClientId == clientId)
                    return np.UgsPlayerId;
            return null;
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
