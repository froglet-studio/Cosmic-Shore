using System;
using System.Collections.Generic;
using System.Reflection;
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
    /// Plays ONE mode for the smoke test, inside one Play session that started in the Bootstrap scene. It is the soak
    /// runners' flow (<c>SwitchbackSoakRunner</c>, <c>SkimRaceBenchmarkRunner</c>) for any card, and it launches the way
    /// the arcade launch modal does:
    /// <list type="number">
    /// <item>Boot: wait for the normal boot chain to put a local pilot in the menu (closing the first-run privacy
    /// overlay unanswered and skipping the username step, as the soak runners do).</item>
    /// <item>Launch: <c>SyncFromArcadeGame</c>, <c>ConfigurePlayerCounts</c>, the domain and AI-difficulty requests,
    /// then <c>GameDataSO.InvokeGameLaunch</c> - the same event the modal raises, which SceneLoader answers.</item>
    /// <item>Scene: wait for the mode's scene, its <see cref="MiniGameControllerBase"/> and a vessel in every seat.</item>
    /// <item>Start: press the public Ready (<see cref="MiniGameControllerBase.OnReadyClicked"/>) until a turn runs, hand
    /// the host's hull to the AI (<see cref="PilotControl.TrySetAIFlies"/>, the Third Eye's AI-flies switch) so EVERY
    /// seat is the AI, and raise <c>Time.timeScale</c>.</item>
    /// <item>Match: sample every seat's path length, press Ready again between turns, and stop when
    /// <c>GameDataSO.OnMiniGameEnd</c> fires or the budget runs out.</item>
    /// </list>
    /// It never writes a vessel, prism, score, course, timer or end condition. It runs from
    /// <see cref="EditorApplication.update"/>, so nothing of it is in the game's assemblies or in a build.
    /// </summary>
    public sealed class ModeSmokeDriver
    {
        public enum Phase { Boot, Settle, SceneWait, StartTurn, Match, Done }

        const double RetryReadySeconds = 15.0;
        const double SampleSeconds = 0.5;

        readonly ModeSmokeConfigSO _config;
        readonly SO_ArcadeGame _card;
        readonly ModeSmokeResult _result;
        readonly double _startedAt;

        GameDataSO _gameData;
        Phase _phase = Phase.Boot;
        double _phaseStarted;
        double _lastReady = double.NegativeInfinity;
        double _lastSample;
        double _lastBootPoke;
        double _matchRealStart;
        float _matchGameStart;
        float _matchRealCap;
        bool _skippedName;
        bool _wasTurnRunning;
        bool _subscribed;
        int _maxHumanSeats;
        bool _sampled;
        readonly Dictionary<IPlayer, Vector3> _lastPos = new();
        readonly Dictionary<IPlayer, float> _path = new();
        readonly HashSet<IPlayer> _flownByAI = new();
        readonly List<string> _notes = new();

        public Phase Current => _phase;
        public bool IsMatchPhase => _phase == Phase.Match || _phase == Phase.StartTurn;

        public ModeSmokeDriver(ModeSmokeConfigSO config, SO_ArcadeGame card, ModeSmokeResult result, double now)
        {
            _config = config;
            _card = card;
            _result = result;
            _startedAt = now;
            _phaseStarted = now;
            _result.phaseReached = Phase.Boot.ToString();
        }

        /// <summary>Advances the run. Returns true once the mode is finished (passed or not) and Play can be left.</summary>
        public bool Tick(double now)
        {
            if (_phase == Phase.Done) return true;
            if (_gameData == null) _gameData = FindGameData();
            if (_gameData == null) return Fail(now, "GameDataSO not found");

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

        /// <summary>Stops the run where it is (the launcher's own watchdog, or a harness fault).</summary>
        public void Abort(double now, string why)
        {
            if (_phase == Phase.Done) return;
            Fail(now, why);
        }

        void Enter(Phase phase, double now)
        {
            _phase = phase;
            _phaseStarted = now;
            _result.phaseReached = phase.ToString();
        }

        // ── Boot ─────────────────────────────────────────────
        void TickBoot(double now)
        {
            if (IsMenu() && _gameData.LocalPlayer?.Vessel != null)
            {
                _result.reachedMenu = true;
                Enter(Phase.Settle, now);
                return;
            }
            if (now - _lastBootPoke >= 0.25)
            {
                _lastBootPoke = now;
                PokeBootGates();
            }
            if (now - _phaseStarted > _config.BootTimeoutSeconds)
                Finish(now, $"menu never gave a local pilot within {_config.BootTimeoutSeconds:0} s");
        }

        /// <summary>
        /// Nobody is at the keyboard to answer the first-run age gate or claim a name, and the boot holds for both. The
        /// soak runners' answer, unchanged: close the overlay unanswered (nothing is recorded, collection stays off), and
        /// take the username step's own route to the menu without claiming a name.
        /// </summary>
        void PokeBootGates()
        {
            var privacy = PrivacyConsentOverlay.Current;
            if (privacy)
            {
                Note("closed the unanswered privacy overlay");
                UnityEngine.Object.Destroy(privacy.gameObject);
            }
            if (_skippedName) return;
            var auth = UnityEngine.Object.FindAnyObjectByType<AuthenticationSceneController>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            if (auth && auth.GetType().GetField("_awaitingPlayerInput", flags)?.GetValue(auth) is true)
            {
                _skippedName = true;
                Note("skipped the username step");
                auth.GetType().GetMethod("NavigateToMainMenu", flags)?.Invoke(auth, null);
            }
        }

        // ── Launch ───────────────────────────────────────────
        void TickSettle(double now)
        {
            if (now - _phaseStarted < 1.5) return;
            Configure();
            Subscribe();
            _gameData.InvokeGameLaunch();
            Enter(Phase.SceneWait, now);
        }

        /// <summary>The arcade launch modal's SyncAllGameDataForLaunch, for an all-AI match with one host seat.</summary>
        void Configure()
        {
            _gameData.SyncFromArcadeGame(_card);
            _gameData.IsTraining = false;
            _gameData.IsWeeklyChallenge = false;
            if (_gameData.selectedVesselClass != null && _card.Vessels is { Count: > 0 } && _card.Vessels[0] != null)
                _gameData.selectedVesselClass.Value = _card.Vessels[0].Class;
            if (_gameData.SelectedIntensity != null) _gameData.SelectedIntensity.Value = _result.intensity;
            _gameData.ConfigurePlayerCounts(_result.players, 1);
            _gameData.SetRequestedAIDomains(null);
            _gameData.SetRequestedAIVessels(null);
            _gameData.RequestedDomainCount = Mathf.Clamp(Mathf.Min(_result.players, 3),
                Mathf.Max(1, _card.MinDomainsAllowed), Mathf.Max(_card.MinDomainsAllowed, _card.MaxDomainsAllowed));
            _gameData.RequestedAIDifficulty = AIDifficultyRules.Resolve(_config.AIDifficulty);
            _gameData.SetRequestedAIDifficulties(null);
        }

        void TickSceneWait(double now)
        {
            if (SceneManager.GetActiveScene().name == _gameData.SceneName)
            {
                _result.sceneLoaded = true;
                var controller = UnityEngine.Object.FindAnyObjectByType<MiniGameControllerBase>();
                int ready = CountSeats();
                _result.seats = Mathf.Max(_result.seats, ready);
                if (controller != null && ready >= _result.players && now - _phaseStarted > 2.0)
                {
                    Enter(Phase.StartTurn, now);
                    return;
                }
            }
            if (now - _phaseStarted > _config.SceneTimeoutSeconds)
                Finish(now, _result.sceneLoaded
                    ? $"scene loaded but the controller and {_result.players} seats were not ready within {_config.SceneTimeoutSeconds:0} s"
                    : $"scene '{_gameData.SceneName}' not loaded within {_config.SceneTimeoutSeconds:0} s");
        }

        // ── Start ────────────────────────────────────────────
        void TickStartTurn(double now)
        {
            if (_gameData.IsTurnRunning)
            {
                _result.turnStarted = true;
                OnTurnBegan();
                _matchRealStart = now;
                _matchGameStart = Time.time;
                _matchRealCap = _result.matchBudgetSeconds / Mathf.Max(1f, _config.TimeScale) * 1.5f + 60f;
                _lastSample = now;
                Enter(Phase.Match, now);
                return;
            }
            PressReadyIfDue(now);
            if (now - _phaseStarted > _config.StartTimeoutSeconds)
                Finish(now, $"no turn started within {_config.StartTimeoutSeconds:0} s of pressing Ready");
        }

        void PressReadyIfDue(double now)
        {
            if (now - _lastReady < RetryReadySeconds) return;
            _lastReady = now;
            var controller = UnityEngine.Object.FindAnyObjectByType<MiniGameControllerBase>();
            if (controller != null) controller.OnReadyClicked();
        }

        /// <summary>Every seat to the AI and the clock up, at the start of each turn (a turn may respawn the hulls).</summary>
        void OnTurnBegan()
        {
            _wasTurnRunning = true;
            foreach (var p in _gameData.Players)
            {
                if (p == null || !Alive(p.Vessel) || p.IsInitializedAsAI) continue;
                if (p is not Player human || !human) continue;
                if (!PilotControl.TrySetAIFlies(human, true, _gameData, out string refusal))
                    Note($"host seat not handed to the AI: {refusal}");
            }
            Time.timeScale = _config.TimeScale;
            _lastPos.Clear();
        }

        // ── Match ────────────────────────────────────────────
        void TickMatch(double now)
        {
            bool running = _gameData.IsTurnRunning;
            if (running && !_wasTurnRunning) OnTurnBegan();
            if (!running && _wasTurnRunning) _wasTurnRunning = false;
            // A hit-stop or a celebration (Astro League, the Thresher's launch view) slows the clock and then puts it
            // back to exactly 1. Put the smoke's speed back on top of that; a pause (0) or a slow-motion in progress
            // is left alone.
            if (running && Mathf.Approximately(Time.timeScale, 1f) && _config.TimeScale > 1f)
                Time.timeScale = _config.TimeScale;

            if (now - _lastSample >= SampleSeconds)
            {
                _lastSample = now;
                if (running) Sample();
            }

            if (_result.gameEnded)
            {
                Finish(now, null);
                return;
            }
            if (!running) PressReadyIfDue(now);

            float gameSeconds = Time.time - _matchGameStart;
            if (gameSeconds > _result.matchBudgetSeconds)
                Finish(now, $"match ran {gameSeconds:0} game seconds without the game-over event");
            else if (now - _matchRealStart > _matchRealCap)
                Finish(now, $"match ran {now - _matchRealStart:0} real seconds without the game-over event (time scale now {Time.timeScale:0.##})");
        }

        /// <summary>
        /// One look at every seat while a turn runs: how far each hull has flown, and whether the AI flies it. A seat
        /// a human flies at ANY sample counts against "every seat is the AI" for the whole match.
        /// </summary>
        void Sample()
        {
            int seats = 0, human = 0;
            foreach (var p in _gameData.Players)
            {
                if (p == null || !Alive(p.Vessel)) continue;
                var hull = p.Vessel.Transform;
                if (hull == null) continue;
                seats++;
                if (IsAISeat(p)) _flownByAI.Add(p);
                else human++;
                Vector3 pos = hull.position;
                if (_lastPos.TryGetValue(p, out var last))
                    _path[p] = (_path.TryGetValue(p, out var d) ? d : 0f) + Vector3.Distance(last, pos);
                _lastPos[p] = pos;
            }
            if (seats == 0) return;
            _sampled = true;
            _result.seats = Mathf.Max(_result.seats, seats);
            _maxHumanSeats = Mathf.Max(_maxHumanSeats, human);
        }

        static bool Alive(IVessel vessel) => vessel is UnityEngine.Object o ? o : vessel != null;

        static bool IsAISeat(IPlayer p) => p.IsInitializedAsAI || (p is Player human && PilotControl.IsHeldByAI(human));

        // ── Events ───────────────────────────────────────────
        void Subscribe()
        {
            if (_subscribed) return;
            if (_gameData.OnMiniGameEnd != null) _gameData.OnMiniGameEnd.OnRaised += OnGameEnd;
            else Note("GameDataSO.OnMiniGameEnd is not wired");
            if (_gameData.OnMiniGameTurnEnd != null) _gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnd;
            if (_gameData.OnWinnerCalculated != null) _gameData.OnWinnerCalculated.OnRaised += OnWinner;
            _subscribed = true;
        }

        public void Unsubscribe()
        {
            if (!_subscribed || _gameData == null) return;
            if (_gameData.OnMiniGameEnd != null) _gameData.OnMiniGameEnd.OnRaised -= OnGameEnd;
            if (_gameData.OnMiniGameTurnEnd != null) _gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnd;
            if (_gameData.OnWinnerCalculated != null) _gameData.OnWinnerCalculated.OnRaised -= OnWinner;
            _subscribed = false;
        }

        void OnGameEnd() => _result.gameEnded = true;
        void OnTurnEnd() => _result.turnsEnded++;
        void OnWinner() => _result.winnerCalculated = true;

        // ── Finish ───────────────────────────────────────────
        bool Fail(double now, string fault)
        {
            _result.harnessFault = fault;
            Finish(now, null);
            return true;
        }

        void Finish(double now, string why)
        {
            if (!string.IsNullOrEmpty(why)) Note(why);
            if (_phase == Phase.Match && _gameData != null)
            {
                // A last look at the seats; a hull torn down mid-sample must not keep the run from finishing.
                try { Sample(); }
                catch (Exception e) { Note($"last sample skipped: {e.GetType().Name}"); }
            }
            int moving = 0;
            foreach (var kv in _path)
                if (kv.Value >= _config.MovingDistance && _flownByAI.Contains(kv.Key)) moving++;
            _result.movingSeats = moving;
            _result.aiSeats = _sampled ? Mathf.Max(0, _result.seats - _maxHumanSeats) : 0;
            if (_result.turnStarted) _result.gameSeconds = Time.time - _matchGameStart;
            _result.wallSeconds = (float)(now - _startedAt);
            _result.notes = string.Join("; ", _notes);
            Time.timeScale = 1f;
            Unsubscribe();
            _phase = Phase.Done;
        }

        void Note(string line)
        {
            if (!_notes.Contains(line)) _notes.Add(line);
        }

        // ── Helpers ──────────────────────────────────────────
        int CountSeats()
        {
            int n = 0;
            foreach (var p in _gameData.Players) if (p != null && Alive(p.Vessel)) n++;
            return n;
        }

        static bool IsMenu() =>
            SceneManager.GetActiveScene().name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0;

        static GameDataSO FindGameData()
        {
            var loaded = Resources.FindObjectsOfTypeAll<GameDataSO>();
            if (loaded is { Length: > 0 }) return loaded[0];
            var guids = AssetDatabase.FindAssets("t:GameDataSO");
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<GameDataSO>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }
    }
}
