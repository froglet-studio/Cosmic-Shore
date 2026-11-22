using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Plays ONE Brood Rush match in the real Unity game with the AI on every seat, a hull asked for every seat, and
    /// writes what each seat did: the match's own volume books (VolumeLedger VC, VD, lost, held), the broods each team
    /// scored, which pilot components were actually flying the hull, and the hull's live trail volume and element
    /// levels at the start. The flow is <see cref="ModeSmokeDriver"/>'s (boot, launch through
    /// <c>GameDataSO.InvokeGameLaunch</c>, press Ready, hand the host to the AI), with the seats picked the way the
    /// Amoebius Brood judge picks them (<c>Port/src/CosmicShore.Player/BroodJudge.cs</c>), so the two can be compared.
    ///
    /// <para>A READER: it never writes a vessel, prism, score, asset or end condition. Off-card hulls are let in by
    /// widening <c>GameDataSO.AllowedVesselClasses</c> in memory after the card is synced (the judge widens the card
    /// itself), so no asset is touched. Brood Rush audit, 2026-11-21.</para>
    /// </summary>
    public sealed class BroodAuditDriver
    {
        public enum Phase { Boot, Settle, SceneWait, StartTurn, Match, Done }

        const double RetryReadySeconds = 15.0;
        const float SampleEvery = 0.5f;
        const double BootTimeout = 1200, SceneTimeout = 900, StartTimeout = 300;

        readonly BroodAuditLauncher.Match _m;
        readonly SO_ArcadeGame _card;
        readonly double _startedAt;
        GameDataSO _gameData;
        Phase _phase = Phase.Boot;
        double _phaseStarted, _lastReady = double.NegativeInfinity, _lastBootPoke;
        bool _skippedName, _subscribed, _gameEnded, _snapped;
        float _matchStart, _nextSample;
        Cell _cell;
        readonly List<IPlayer> _seats = new();
        readonly Dictionary<IPlayer, BroodAuditLauncher.Seat> _rows = new();
        readonly Dictionary<IPlayer, float> _speedSum = new();
        readonly Dictionary<string, float> _claimS = new();
        readonly List<Domains> _teamDomains = new();
        readonly StringBuilder _wallRows = new();
        bool _clientSeen;
        int _rehost;
        double _menuSince = -1, _rehostAt;
        int _samples;
        readonly List<string> _notes = new();
        readonly BroodAuditActionCounter _actions = new();

        public Phase Current => _phase;

        public BroodAuditDriver(BroodAuditLauncher.Match match, SO_ArcadeGame card, double now)
        {
            _m = match;
            _card = card;
            _startedAt = now;
            _phaseStarted = now;
        }

        public bool Tick(double now)
        {
            if (_phase == Phase.Done) return true;
            if (_gameData == null) _gameData = FindGameData();
            if (_gameData == null) { Finish(now, "GameDataSO not found"); return true; }
            switch (_phase)
            {
                case Phase.Boot: TickBoot(now); break;
                case Phase.Settle: TickSettle(now); break;
                case Phase.SceneWait: TickSceneWait(now); break;
                case Phase.StartTurn: TickStartTurn(now); break;
                case Phase.Match: TickMatch(now); break;
            }
            return _phase == Phase.Done;
        }

        public void Abort(double now, string why) { if (_phase != Phase.Done) Finish(now, why); }

        void Enter(Phase p, double now) { _phase = p; _phaseStarted = now; _m.phaseReached = p.ToString(); }

        // ── Boot (ModeSmokeDriver's gates) ──
        void TickBoot(double now)
        {
            if (IsMenu() && _gameData.LocalPlayer?.Vessel != null) { Enter(Phase.Settle, now); return; }
            if (now - _lastBootPoke >= 0.25) { _lastBootPoke = now; PokeBootGates(); }
            TickRehost(now);
            if (now - _phaseStarted > BootTimeout) Finish(now, "menu never gave a local pilot");
        }

        /// <summary>
        /// Several audit editors on one machine: the offline host always binds UDP 7777, so only the first editor gets a
        /// pilot and the rest sit in the menu with none. Once the menu has been up a while with no local vessel, this
        /// peer leaves its host and starts it again on its own port (<c>-broodPort</c>, else 7778 + a hash of the
        /// project path), the way an MPPM clone does (<c>MultiplayerSetup</c>). The editor that holds 7777 never needs it.
        /// </summary>
        void TickRehost(double now)
        {
            if (!IsMenu()) { _menuSince = -1; return; }
            if (_menuSince < 0) _menuSince = now;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null) return;
            switch (_rehost)
            {
                case 0 when now - _menuSince > 40 && _gameData.LocalPlayer?.Vessel == null:
                    Note("no pilot in the menu: re-hosting on a free port");
                    nm.Shutdown();
                    _rehost = 1;
                    _rehostAt = now;
                    break;
                case 1 when !nm.ShutdownInProgress && !nm.IsListening && now - _rehostAt > 2:
                    ushort port = ushort.TryParse(Environment.GetCommandLineArgs().SkipWhile(a => a != "-broodPort").Skip(1).FirstOrDefault(), out var p)
                        ? p : (ushort)(7778 + Math.Abs(Application.dataPath.GetHashCode()) % 200);
                    if (nm.TryGetComponent(out Unity.Netcode.Transports.UTP.UnityTransport t)) t.SetConnectionData("127.0.0.1", port, "0.0.0.0");
                    bool ok = nm.StartHost();
                    Note($"re-hosted on {port}: {ok}");
                    _rehost = 2;
                    break;
            }
        }

        void PokeBootGates()
        {
            var privacy = PrivacyConsentOverlay.Current;
            if (privacy) UnityEngine.Object.Destroy(privacy.gameObject);
            if (_skippedName) return;
            var auth = UnityEngine.Object.FindAnyObjectByType<AuthenticationSceneController>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            if (auth && auth.GetType().GetField("_awaitingPlayerInput", flags)?.GetValue(auth) is true)
            {
                _skippedName = true;
                auth.GetType().GetMethod("NavigateToMainMenu", flags)?.Invoke(auth, null);
            }
        }

        // ── Launch ──
        void TickSettle(double now)
        {
            if (now - _phaseStarted < 1.5) return;
            if (_m.waitClient)
            {
                var nm = Unity.Netcode.NetworkManager.Singleton;
                int clients = nm != null && nm.IsServer ? nm.ConnectedClientsIds.Count : 0;
                if (clients < 2)
                {
                    if (now - _phaseStarted > 900) { Finish(now, "the client never connected"); return; }
                    return;
                }
                if (!_clientSeen) { _clientSeen = true; _phaseStarted = now; Note($"client connected ({clients} connections)"); return; }
                if (now - _phaseStarted < 5) return;
            }
            Configure();
            Subscribe();
            UnityEngine.Random.InitState(_m.seed);
            _gameData.InvokeGameLaunch();
            Enter(Phase.SceneWait, now);
        }

        /// <summary>The judge's launch: card synced, off-card hulls let in, host hull, AI hulls and teams placed.</summary>
        void Configure()
        {
            _gameData.SyncFromArcadeGame(_card);
            _gameData.IsTraining = false;
            _gameData.IsWeeklyChallenge = false;
            var hulls = _m.AllHulls().Select(Parse).ToList();
            if (_m.parkHost && _m.hostHull != null) hulls.Add(Parse(_m.hostHull));
            if (!_m.stockCard)
                foreach (var h in hulls)
                    if (!_gameData.AllowedVesselClasses.Contains(h))
                    {
                        _gameData.AllowedVesselClasses.Add(h);
                        Note($"let in off the card: {h}");
                    }

            var host = Parse(_m.HostHull());
            if (_gameData.selectedVesselClass != null) _gameData.selectedVesselClass.Value = host;
            if (_gameData.LocalPlayer is Player hp && hp.NetDefaultVesselType.Value != host) hp.NetDefaultVesselType.Value = host;
            if (_gameData.SelectedIntensity != null) _gameData.SelectedIntensity.Value = Mathf.Clamp(_m.intensity, 1, 4);

            var ai = _m.AiHulls().Select(Parse).ToList();
            _gameData.ConfigurePlayerCounts(1 + ai.Count, 1);
            int teams = Mathf.Clamp(_m.TeamCount, 2, GameDataSO.ActiveDomains.Length);
            _gameData.RequestedDomainCount = teams;
            _gameData.RequestedAIDifficulty = AIDifficultyRules.Resolve(_m.Difficulty());
            _gameData.SetRequestedAIDifficulties(null);
            _gameData.SetRequestedAIVessels(ai);

            // Teams: team A is the host's domain, the others the next active domains in order. Seat i of AiHulls() is on
            // _m.AiTeams()[i].
            var hostDomain = _gameData.LocalPlayer != null ? _gameData.LocalPlayer.Domain : GameDataSO.ActiveDomains[0];
            if (Array.IndexOf(GameDataSO.ActiveDomains, hostDomain) < 0) hostDomain = GameDataSO.ActiveDomains[0];
            _teamDomains.Clear();
            _teamDomains.Add(hostDomain);
            _teamDomains.AddRange(GameDataSO.ActiveDomains.Where(d => d != hostDomain).Take(teams - 1));
            _gameData.SetRequestedAIDomains(_m.AiTeams().Select(t => _teamDomains[Mathf.Min(t, _teamDomains.Count - 1)]).ToList());
            _m.hostDomainAtLaunch = hostDomain.ToString();
            _m.allowed = string.Join(",", _gameData.AllowedVesselClasses);
        }

        void TickSceneWait(double now)
        {
            if (SceneManager.GetActiveScene().name == _gameData.SceneName)
            {
                var controller = UnityEngine.Object.FindAnyObjectByType<MiniGameControllerBase>();
                _cell = UnityEngine.Object.FindAnyObjectByType<Cell>();
                int ready = _gameData.Players.Count(p => p != null && Alive(p.Vessel));
                if (controller != null && _cell != null && ready >= 1 + _m.AiHulls().Count() && now - _phaseStarted > 2.0)
                {
                    Enter(Phase.StartTurn, now);
                    return;
                }
            }
            if (now - _phaseStarted > SceneTimeout) Finish(now, "scene or seats not ready");
        }

        void TickStartTurn(double now)
        {
            if (_gameData.IsTurnRunning)
            {
                OnTurnBegan();
                _matchStart = Time.time;
                _nextSample = 0f;
                Enter(Phase.Match, now);
                return;
            }
            if (now - _lastReady >= RetryReadySeconds)
            {
                _lastReady = now;
                UnityEngine.Object.FindAnyObjectByType<MiniGameControllerBase>()?.OnReadyClicked();
            }
            if (now - _phaseStarted > StartTimeout) Finish(now, "no turn started");
        }

        void OnTurnBegan()
        {
            foreach (var p in _gameData.Players)
            {
                if (p == null || !Alive(p.Vessel) || p.IsInitializedAsAI || _m.parkHost) continue;
                if (p is Player human && human && !PilotControl.TrySetAIFlies(human, true, _gameData, out string refusal))
                    Note($"host not handed to the AI: {refusal}");
            }
            if (_m.timeScale > 1f) Time.timeScale = _m.timeScale;
        }

        // ── Match ──
        void TickMatch(double now)
        {
            if (_m.parkHost) ParkHost();
            if (_gameData.IsTurnRunning && _m.timeScale > 1f && Mathf.Approximately(Time.timeScale, 1f)) Time.timeScale = _m.timeScale;
            float t = Time.time - _matchStart;
            if (!_snapped && t > 1f) Snapshot();
            if (_snapped && t >= _nextSample)
            {
                _nextSample = t + SampleEvery;
                Sample();
            }
            if (_gameEnded) { Finish(now, null, "ended"); return; }
            if (t > _m.limitS) Finish(now, null, "timeout");
            else if (now - _startedAt > _m.limitS * 3 + 900) Finish(now, "wall clock budget spent", "wall");
        }

        void ParkHost()
        {
            var host = _gameData.LocalPlayer;
            if (host?.Vessel?.Transform is { } ht && _cell) ht.position = _cell.transform.position + new Vector3(0f, 40000f, 0f);
        }

        /// <summary>Once, a second into the turn: who flies each seat and with what.</summary>
        void Snapshot()
        {
            _snapped = true;
            _actions.Ensure();
            _seats.Clear();
            foreach (var p in _gameData.Players)
                if (p != null && Alive(p.Vessel) && !(_m.parkHost && !p.IsInitializedAsAI)) _seats.Add(p);
            var asked = _m.AllHulls().Select(h => h.ToString()).ToList();
            foreach (var p in _seats)
            {
                var vs = p.Vessel.VesselStatus;
                var row = new BroodAuditLauncher.Seat
                {
                    name = p.Name,
                    hull = vs != null ? vs.VesselType.ToString() : "?",
                    domain = p.Domain.ToString(),
                    isHost = !p.IsInitializedAsAI,
                    startLevels = vs?.ResourceSystem is { } rs
                        ? $"M{rs.GetLevel(Element.Mass)} C{rs.GetLevel(Element.Charge)} S{rs.GetLevel(Element.Space)} T{rs.GetLevel(Element.Time)}"
                        : "?",
                    trailVolume = TrailVolume(vs),
                    pilots = Pilots(p),
                    aiPilot = AIPilotFields(p),
                    difficulty = _gameData.AIDifficultyFor(p).ToString(),
                };
                row.requestedOnSeat = asked.Contains(row.hull);
                _rows[p] = row;
                _speedSum[p] = 0f;
            }
        }

        void Sample()
        {
            _samples++;
            _actions.Ensure();
            foreach (var p in _seats)
            {
                if (!_rows.TryGetValue(p, out var row) || !Alive(p.Vessel)) continue;
                _speedSum[p] += p.Vessel.VesselStatus?.Speed ?? 0f;
                if (_cell && p.Vessel.Transform is { } tr && _cell.IsInsideNucleus(tr.position)) row.nucleusS += SampleEvery;
            }
            string claim = _cell && _cell.TryGetNucleusClaim(out var c) ? c.ToString() : "none";
            _claimS[claim] = (_claimS.TryGetValue(claim, out var s) ? s : 0f) + SampleEvery;
            SampleSerpentWalls();
        }

        /// <summary>The Serpent walls this peer (the host, the server) has built: count, bricks, active.</summary>
        void SampleSerpentWalls()
        {
            int walls = 0, bricks = 0, active = 0;
            var membersField = typeof(SerpentWallAssembler).GetField("_members", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var w in SerpentWallAssembler.Live)
            {
                if (!w) continue;
                walls++;
                if (w.IsActive) active++;
                if (membersField?.GetValue(w) is System.Collections.ICollection m) bricks += m.Count;
            }
            if (_m.waitClient && BroodAuditLauncher.OutDir is { } dir)
            {
                if (_wallRows.Length == 0) _wallRows.Append("t,walls,bricks,active\n");
                _wallRows.Append(string.Format(CultureInfo.InvariantCulture, "{0:0.0},{1},{2},{3}\n", Time.time - _matchStart, walls, bricks, active));
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, $"walls_host_{_m.index}.csv"), _wallRows.ToString());
            }
            _m.serpentWallsMax = Mathf.Max(_m.serpentWallsMax, walls);
            _m.serpentBricksMax = Mathf.Max(_m.serpentBricksMax, bricks);
            _m.serpentActiveMax = Mathf.Max(_m.serpentActiveMax, active);
        }

        // ── Events ──
        void Subscribe()
        {
            if (_subscribed) return;
            if (_gameData.OnMiniGameEnd != null) _gameData.OnMiniGameEnd.OnRaised += OnGameEnd;
            _subscribed = true;
        }

        public void Unsubscribe()
        {
            if (!_subscribed || _gameData == null) return;
            if (_gameData.OnMiniGameEnd != null) _gameData.OnMiniGameEnd.OnRaised -= OnGameEnd;
            _subscribed = false;
        }

        void OnGameEnd() => _gameEnded = true;

        // ── Finish ──
        void Finish(double now, string fault, string outcome = "fault")
        {
            if (fault != null) _m.fault = fault;
            _m.outcome = outcome;
            if (_phase == Phase.Match && _gameData != null)
            {
                try { Read(); }
                catch (Exception e) { Note($"read failed: {e.GetType().Name}: {e.Message}"); }
            }
            _m.wallS = (float)(now - _startedAt);
            _m.notes = string.Join("; ", _notes);
            Time.timeScale = 1f;
            _actions.Remove();
            Unsubscribe();
            _phase = Phase.Done;
        }

        void Read()
        {
            _m.raceS = Time.time - _matchStart;
            var roster = _gameData.RoundStatsList.Where(s => s != null).Select(s => s.Name).ToList();
            // The controller counts at its own end; a timeout is counted here the same way.
            _gameData.VolumeLedger.CountLive(PrismSpatialIndex.Instance, roster);
            var books = _gameData.VolumeLedger;
            var broods = new StringBuilder();
            foreach (var d in _teamDomains)
                broods.Append(d).Append('=').Append(ScoringMetrics.SumByDomain(_gameData, ScoringMetric.Goals, d)).Append(' ');
            _m.broods = broods.ToString().Trim();
            _m.winner = _gameData.WinnerDomain.ToString();
            _m.claim = string.Join(" ", _claimS.Select(kv => $"{kv.Key}={kv.Value:0}"));
            _m.seats.Clear();
            foreach (var p in _seats)
            {
                if (!_rows.TryGetValue(p, out var row)) continue;
                var line = books.Get(row.name);
                row.vc = line.Created;
                row.vd = line.Reduced;
                row.lost = line.Lost;
                row.held = line.HasCount ? line.Counted : float.NaN;
                row.superShields = line.SuperShieldsBroken;
                row.teamBroods = ScoringMetrics.SumByDomain(_gameData, ScoringMetric.Goals, p.Domain);
                row.speed = _samples > 0 ? _speedSum[p] / _samples : 0f;
                if (p.RoundStats is { } rs)
                {
                    row.blocks = rs.BlocksCreated;
                    row.volumeCreated = rs.VolumeCreated;
                    row.stolen = rs.VolumeStolen;
                    row.destroyed = rs.HostileVolumeDestroyed;
                    row.crystals = rs.CrystalsCollected;
                    row.abilityS = string.Join("/", new[] { rs.Button1AbilityActiveTime, rs.Button2AbilityActiveTime,
                        rs.Button3AbilityActiveTime, rs.LeftStickAbilityActiveTime, rs.RightStickAbilityActiveTime,
                        rs.FullSpeedStraightAbilityActiveTime }.Select(v => v.ToString("0.#", CultureInfo.InvariantCulture)));
                }
                row.pilotsAtEnd = Alive(p.Vessel) ? Pilots(p) : "gone";
                row.abilityStarts = _actions.Total(p.Vessel?.VesselStatus);
                row.abilities = _actions.For(p.Vessel?.VesselStatus);
                _m.seats.Add(row);
            }
            if (_m.parkHost && _gameData.LocalPlayer is { } host)
            {
                var hl = books.Get(host.Name);
                _m.hostBooks = $"VC {hl.Created:0} VD {hl.Reduced:0}";
            }
        }

        // ── What flies a seat ──
        static float TrailVolume(IVesselStatus vs)
        {
            var pc = vs?.VesselPrismController;
            if (pc == null) return float.NaN;
            var f = typeof(VesselPrismController).GetField("trailVolume", BindingFlags.Instance | BindingFlags.NonPublic);
            return f?.GetValue(pc) is ElementalFloat ef ? ef.EvaluateLive(vs) : float.NaN;
        }

        static readonly string[] PilotWords = { "Pilot", "AI", "Autopilot", "Policy", "Brain", "Steer" };

        /// <summary>Every enabled behaviour on the hull whose type name says it flies or decides: name, enabled.</summary>
        static string Pilots(IPlayer p)
        {
            if (p.Vessel is not Component c || !c) return "?";
            var found = new List<string>();
            foreach (var b in c.GetComponentsInChildren<Behaviour>(true))
            {
                if (!b) continue;
                string n = b.GetType().Name;
                if (!PilotWords.Any(w => n.Contains(w, StringComparison.Ordinal))) continue;
                found.Add(b.isActiveAndEnabled ? n : n + "(off)");
            }
            var vs = p.Vessel.VesselStatus;
            if (vs != null) found.Add(vs.AutoPilotEnabled ? "autopilot=on" : "autopilot=off");
            return string.Join("+", found.Distinct());
        }

        /// <summary>The AIPilot's simple fields (numbers, flags, enums) as key=value, the levers it flies with.</summary>
        static string AIPilotFields(IPlayer p)
        {
            if (p.Vessel is not Component c || !c || !c.TryGetComponent(out AIPilot pilot)) return "";
            var sb = new StringBuilder();
            for (var t = pilot.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    var ft = f.FieldType;
                    if (!(ft.IsPrimitive || ft.IsEnum)) { if (typeof(Delegate).IsAssignableFrom(ft)) sb.Append(f.Name).Append('=').Append(f.GetValue(pilot) != null ? "set" : "null").Append(' '); continue; }
                    object v = f.GetValue(pilot);
                    sb.Append(f.Name).Append('=').Append(v is float fl ? fl.ToString("0.###", CultureInfo.InvariantCulture) : v).Append(' ');
                }
            return sb.ToString().Trim();
        }

        void Note(string line) { if (!_notes.Contains(line)) _notes.Add(line); }

        static VesselClassType Parse(string h) => Enum.TryParse(h, out VesselClassType v) ? v : VesselClassType.Random;

        static bool Alive(IVessel v) => v is UnityEngine.Object o ? o : v != null;

        static bool IsMenu() => SceneManager.GetActiveScene().name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0;

        static GameDataSO FindGameData()
        {
            var loaded = Resources.FindObjectsOfTypeAll<GameDataSO>();
            if (loaded is { Length: > 0 }) return loaded[0];
            var guids = AssetDatabase.FindAssets("t:GameDataSO");
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<GameDataSO>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }
    }
}
