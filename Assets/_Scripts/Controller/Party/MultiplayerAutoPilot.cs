// ─────────────────────────────────────────────────────────────────────────────
// MultiplayerAutoPilot.cs
// Hosts or joins a party on its own when a DEVELOPMENT player is launched with -csMpAutoHost / -csMpAutoJoin
// (arguments: MultiplayerAutoArgs.cs). Built for standalone players, not MPPM clones: two Editors used ~24 GB on
// the test machine and the clone ran at 1-10 fps (multiplayer test of 2026-11-02); built players need a
// fraction of that, so four can run on one PC.
//
// WHAT IT CALLS:
//   Nothing of its own. The host is the solo party session every player already hosts at boot (the eager per-user
//   Relay); the joiner calls PartyInviteController.JoinPartyAsync, the online row's JOIN button, pre-flight and all
//   - the same call the `party join` console command makes. A run that passes here passed through the real path.
//
// WHAT IT LEAVES:
//   A result JSON per player (ok, reason, the steps with their times, the `party` state line at the end) and a
//   net-log mark at each step (`mpAuto`), so Tools/Build/mp_auto.py and net_logs.py read the same run.
//
// Compiled into development builds and the editor only (Docs/CONDITIONAL_COMPILATION.md Pattern 1): the type
// exists everywhere, the behaviour does not, and even there it does nothing without the arguments.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public sealed class MultiplayerAutoPilot : MonoBehaviour
    {
        /// <summary>The run's outcome, as written to the result file.</summary>
        [Serializable]
        public sealed class Result
        {
            public string role;
            public string profile;
            public bool ok;
            public string reason;
            public float seconds;
            public int expect;
            public string partyLine;
            public List<string> steps = new();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>The run's arguments, parsed once at boot. Read by AuthenticationServiceFacade for the profile.</summary>
        public static MultiplayerAutoArgs Args { get; private set; }

        const float PollSeconds = 0.5f;
        const float JoinSettleSeconds = 90f;   // after a JOIN press, how long to wait for InParty before pressing again
        const int MaxJoinAttempts = 3;

        MultiplayerAutoArgs _args;
        readonly Result _result = new();
        float _startTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ParseArgs()
        {
            Args = MultiplayerAutoArgs.Parse(Environment.GetCommandLineArgs());
            if (Args.PartyLog) CSDebug.VerboseChannels |= CSLogChannel.Party;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void MaybeStart()
        {
            if (Args == null || !Args.IsActive) return;
            // After FrameStallWatch.Reset (SubsystemRegistration), which clears it. A built player writes no net record.
            FrameStallWatch.LogEveryStall = true;
            StallFrameMarkers.IncludeScriptMarkers = true;
            var go = new GameObject("[MultiplayerAutoPilot]");
            DontDestroyOnLoad(go);
            go.AddComponent<MultiplayerAutoPilot>()._args = Args;
        }

        void Start()
        {
            _startTime = Time.realtimeSinceStartup;
            _result.role = _args.Role.ToString();
            _result.profile = _args.Profile;
            _result.expect = _args.Expect;
            RunAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid RunAsync(CancellationToken ct)
        {
            bool ok = false;
            string reason;
            try
            {
                if (_args.Errors.Count > 0)
                    reason = "bad arguments: " + string.Join("; ", _args.Errors);
                else
                {
                    bool isHost = _args.Role == MultiplayerAutoRole.Host;
                    reason = isHost ? await HostAsync(ct) : await JoinAsync(ct);
                    if (reason == null && !string.IsNullOrEmpty(_args.LaunchMode)) reason = await MatchAsync(ct, isHost);
                }
                ok = reason == null;
            }
            catch (OperationCanceledException)
            {
                reason = "cancelled (the player is closing)";
            }
            catch (Exception e)
            {
                reason = $"{e.GetType().Name}: {e.Message}";
            }
            Finish(ok, ok ? "ok" : reason);
        }

        // ── Host ─────────────────────────────────────────────────────────────────

        async UniTask<string> HostAsync(CancellationToken ct)
        {
            TryDelete(_args.RendezvousPath);   // a previous run's file must never send a joiner to a dead session
            if (!string.IsNullOrEmpty(_args.RendezvousPath))
            {
                ClearJoinedMarkers();
                TryDelete(SeenPath());
            }
            if (!await WaitUntil(ct, "solo party session up", () => OwnSessionId() != null)) return "the solo party session never came up";

            string written = null;
            bool done = false;
            while (!done)
            {
                if (TimedOut()) return $"only {HumanCount()} of {_args.Expect} players arrived";

                // Re-written whenever the session changes (a re-host after a bounce), so a joiner never chases a dead one.
                string session = OwnSessionId();
                if (session != null && session != written)
                {
                    WriteRendezvous(session);
                    written = session;
                }

                done = IsHostWithEveryone();
                if (!done) await UniTask.Delay(TimeSpan.FromSeconds(PollSeconds), DelayType.Realtime, cancellationToken: ct);
            }
            Step($"{_args.Expect} players in the party");
            // Tells the joiners the host counted them. A joiner that quit the moment it was in could be gone before
            // this poll saw it (2026-11-22: joiner PASS at 48.7 s, host "only 1 of 2 players arrived").
            if (!string.IsNullOrEmpty(_args.RendezvousPath)) WriteAllTextAtomic(SeenPath(), _args.Profile);

            // Stay up until every joiner says it is in. The host counts a joiner the moment it connects, seconds
            // before that joiner's own vessel is ready; quitting then pulled the session from under it (the first
            // built-player run, 2026-11-22: host PASS at 97 s, joiner never ready).
            if (!string.IsNullOrEmpty(_args.RendezvousPath))
            {
                if (!await WaitUntil(ct, "every joiner confirmed", () => JoinedMarkerCount() >= _args.Expect - 1))
                    return $"only {JoinedMarkerCount()} of {_args.Expect - 1} joiners confirmed they were in the party";
            }
            return null;
        }

        const string JoinedSuffix = ".joined";
        const float HostSeenWaitSeconds = 30f;

        string SeenPath() => _args.RendezvousPath + ".seen";

        string JoinedMarkerPath(string profile) => $"{_args.RendezvousPath}.{profile}{JoinedSuffix}";

        int JoinedMarkerCount()
        {
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(_args.RendezvousPath));
                string pattern = Path.GetFileName(_args.RendezvousPath) + ".*" + JoinedSuffix;
                return Directory.Exists(dir) ? Directory.GetFiles(dir, pattern).Length : 0;
            }
            catch (IOException) { return 0; }
        }

        void ClearJoinedMarkers()
        {
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(_args.RendezvousPath));
                if (!Directory.Exists(dir)) return;
                foreach (string suffix in new[] { JoinedSuffix, InMatchSuffix, BackSuffix })
                foreach (string f in Directory.GetFiles(dir, Path.GetFileName(_args.RendezvousPath) + ".*" + suffix))
                    TryDelete(f);
            }
            catch (IOException) { }
        }

        bool IsHostWithEveryone()
        {
            var nm = NetworkManager.Singleton;
            var data = ConnectionData();
            return nm != null && nm.IsHost && data != null
                   && SpectatorSession.CountHumanClients(nm) >= _args.Expect
                   && data.DistinctPartyMemberCount >= _args.Expect;
        }

        void WriteRendezvous(string session)
        {
            var data = ConnectionData();
            var r = new MultiplayerAutoRendezvous
            {
                playerId = data.LocalPlayerId,
                displayName = data.LocalDisplayName,
                sessionId = session,
                writtenUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            if (!string.IsNullOrEmpty(_args.RendezvousPath))
                WriteAllTextAtomic(_args.RendezvousPath, r.ToJson());
            Step($"rendezvous written (session {Short(session)})");
        }

        // ── Join ─────────────────────────────────────────────────────────────────

        async UniTask<string> JoinAsync(CancellationToken ct)
        {
            if (!await WaitUntil(ct, "signed in and online", () => OwnSessionId() != null)) return "never came online";

            var kind = _args.TargetKind;
            string value = _args.TargetValue;
            if (kind == MultiplayerAutoTargetKind.RendezvousFile)
            {
                MultiplayerAutoRendezvous r = null;
                if (!await WaitUntil(ct, "host's rendezvous file", () => TryReadRendezvous(out r)))
                    return $"no fresh rendezvous at {_args.RendezvousPath}";
                kind = MultiplayerAutoTargetKind.PlayerId;
                value = r.playerId;
            }

            int pressed = 0;
            for (int attempt = 1; attempt <= MaxJoinAttempts; attempt++)
            {
                PartyPlayerData target = default;
                if (!await WaitUntil(ct, attempt == 1 ? "host in the online list, joinable" : null,
                        () => TryFindHost(kind, value, out target)))
                    return "the host never showed up as joinable in the online list";

                var controller = PartyInviteController.Instance;
                if (controller == null) return "PartyInviteController is not loaded";

                Step($"JOIN pressed for {target.DisplayName} (attempt {attempt})");
                pressed = attempt;
                try
                {
                    await controller.JoinPartyAsync(target);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Step($"join attempt {attempt} threw {e.GetType().Name}: {e.Message}");
                }

                float settleUntil = Time.realtimeSinceStartup + JoinSettleSeconds;
                while (Time.realtimeSinceStartup < settleUntil && !TimedOut())
                {
                    if (IsClientInParty())
                    {
                        Step("in the party as a client");
                        if (_args.TargetKind == MultiplayerAutoTargetKind.RendezvousFile)
                        {
                            WriteAllTextAtomic(JoinedMarkerPath(_args.Profile), _args.Profile);   // tells the host it may close
                            // Stay until the host has counted everyone, or this player's quit races its poll.
                            float seenBy = Time.realtimeSinceStartup + HostSeenWaitSeconds;
                            while (!File.Exists(SeenPath()) && Time.realtimeSinceStartup < seenBy && !TimedOut())
                                await UniTask.Delay(TimeSpan.FromSeconds(PollSeconds), DelayType.Realtime, cancellationToken: ct);
                            Step(File.Exists(SeenPath()) ? "the host counted everyone" : "the host never said it counted everyone");
                        }
                        return null;
                    }
                    await UniTask.Delay(TimeSpan.FromSeconds(PollSeconds), DelayType.Realtime, cancellationToken: ct);
                }
                if (TimedOut()) break;
                Step($"join attempt {attempt} did not reach the party ({StateLine()})");
            }
            return TimedOut()
                ? $"not in the party when the {_args.TimeoutSeconds:0} s ran out (JOIN pressed {pressed} time(s))"
                : $"not in the party after {pressed} JOIN presses";
        }

        bool IsClientInParty()
        {
            var nm = NetworkManager.Singleton;
            var data = ConnectionData();
            var host = HostConnectionService.Instance;
            return nm != null && nm.IsConnectedClient && !nm.IsServer && data != null && host != null
                   && ((IPartyStateQuery)host).CurrentState == PartyState.InParty
                   && data.DistinctPartyMemberCount >= 2;
        }

        bool TryFindHost(MultiplayerAutoTargetKind kind, string value, out PartyPlayerData target)
        {
            target = default;
            var online = ConnectionData()?.OnlinePlayers;
            if (online == null) return false;
            foreach (var p in online)
            {
                if (!p.HasJoinableSession) continue;
                if (!MultiplayerAutoTarget.Matches(kind, value, p.PlayerId, p.DisplayName)) continue;
                target = p;
                return true;
            }
            return false;
        }

        bool TryReadRendezvous(out MultiplayerAutoRendezvous r)
        {
            r = null;
            try
            {
                if (!File.Exists(_args.RendezvousPath)) return false;
                return MultiplayerAutoRendezvous.TryParse(File.ReadAllText(_args.RendezvousPath), out r)
                       && r.IsFresh(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
            catch (IOException) { return false; }   // the host is mid-write; read it next poll
        }

        // ── A launched match (-csMpLaunch) ───────────────────────────────────────

        const float MatchRunSeconds = 10f;       // how long the turn runs before the host takes everyone back
        // Longer than the countdown (four beats, about 4 s). A press during the countdown counts toward a new ready
        // gate, and when every player has pressed again the countdown restarts (CountdownTimer.BeginCountdown kills
        // the running one): at 3 s the turn never started (2026-11-22, both players pressing for 300 s).
        const float ReadyPressEverySeconds = 20f;
        const string MenuSceneName = "Menu_Main";
        const string InMatchSuffix = ".inmatch";
        const string BackSuffix = ".back";

        static GameDataSO FindGameData()
        {
            GameDataSO best = null;
            foreach (var g in Resources.FindObjectsOfTypeAll<GameDataSO>())
                if (g != null && (best == null || g.Players.Count > best.Players.Count)) best = g;
            return best;
        }

        int MarkerCount(string suffix)
        {
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(_args.RendezvousPath));
                return Directory.Exists(dir)
                    ? Directory.GetFiles(dir, Path.GetFileName(_args.RendezvousPath) + ".*" + suffix).Length : 0;
            }
            catch (IOException) { return 0; }
        }

        void WriteMarker(string suffix)
        {
            if (!string.IsNullOrEmpty(_args.RendezvousPath))
                WriteAllTextAtomic($"{_args.RendezvousPath}.{_args.Profile}{suffix}", _args.Profile);
        }

        static string ActiveScene() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        /// <summary>The match half of a run, the same for the host and a joiner except who launches and who returns.</summary>
        async UniTask<string> MatchAsync(CancellationToken ct, bool isHost)
        {
            var gameData = FindGameData();
            if (gameData == null) return "no GameDataSO is loaded";

            if (isHost)
            {
                if (!Enum.TryParse(_args.LaunchMode, true, out CosmicShore.Data.GameModes mode))
                    return $"{MultiplayerAutoArgs.LaunchArg} '{_args.LaunchMode}' is not a GameModes name";
                SO_ArcadeGame card = null;
                foreach (var g in Resources.FindObjectsOfTypeAll<SO_ArcadeGame>())
                    if (g != null && g.Mode == mode) { card = g; break; }
                if (card == null) return $"no loaded arcade card for {mode}";

                gameData.SyncFromArcadeGame(card);
                gameData.IsTraining = false;
                gameData.SelectedIntensity.Value = 1;
                gameData.ConfigurePlayerCounts(Mathf.Max(_args.Expect, 4), _args.Expect);   // the other seats are AIs
                gameData.InvokeGameLaunch();
                Step($"launched {card.name} ({gameData.SceneName})");
            }

            if (!await WaitUntil(ct, null, () => ActiveScene() != MenuSceneName && gameData.LocalPlayer != null))
                return $"never reached the match with a local player (scene {ActiveScene()})";
            Step($"in {ActiveScene()} with a local player");

            float nextPress = 0f;
            while (!gameData.IsTurnRunning)
            {
                if (TimedOut()) return $"the turn never started ({StateLine()})";
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    var controller = FindAnyObjectByType<MiniGameControllerBase>();
                    if (controller != null) controller.OnReadyClicked();
                    nextPress = Time.realtimeSinceStartup + ReadyPressEverySeconds;
                }
                await UniTask.Delay(TimeSpan.FromSeconds(PollSeconds), DelayType.Realtime, cancellationToken: ct);
            }
            Step("turn running");
            WriteMarker(InMatchSuffix);

            if (isHost)
            {
                if (!await WaitUntil(ct, "every player is in the turn", () => MarkerCount(InMatchSuffix) >= _args.Expect))
                    return $"only {MarkerCount(InMatchSuffix)} of {_args.Expect} players reached a running turn";
                await UniTask.Delay(TimeSpan.FromSeconds(MatchRunSeconds), DelayType.Realtime, cancellationToken: ct);
                var loader = FindAnyObjectByType<SceneLoader>();
                if (loader == null) return "no SceneLoader to return to the menu with";
                Step("returning everyone to the menu");
                loader.ReturnToMainMenu();
            }

            if (!await WaitUntil(ct, null, () => ActiveScene() == MenuSceneName))
                return $"never came back to the menu (scene {ActiveScene()}, {StateLine()})";
            Step("back on the menu");
            if (!await WaitUntil(ct, "party still together on the menu", () => isHost ? IsHostWithEveryone() : IsClientInParty()))
                return $"the party did not survive the return ({StateLine()})";
            WriteMarker(BackSuffix);

            if (isHost && !await WaitUntil(ct, "every player is back", () => MarkerCount(BackSuffix) >= _args.Expect))
                return $"only {MarkerCount(BackSuffix)} of {_args.Expect} players confirmed the return";
            return null;
        }

        // ── Shared ───────────────────────────────────────────────────────────────

        async UniTask<bool> WaitUntil(CancellationToken ct, string what, Func<bool> condition)
        {
            while (!condition())
            {
                if (TimedOut()) return false;
                await UniTask.Delay(TimeSpan.FromSeconds(PollSeconds), DelayType.Realtime, cancellationToken: ct);
            }
            if (what != null) Step(what);
            return true;
        }

        bool TimedOut() => Time.realtimeSinceStartup - _startTime > _args.TimeoutSeconds;

        static HostConnectionDataSO ConnectionData() => HostConnectionService.Instance ? HostConnectionService.Instance.ConnectionData : null;

        /// <summary>This player's own live party session id, or null before sign-in and the solo session.</summary>
        static string OwnSessionId()
        {
            var host = HostConnectionService.Instance;
            var data = ConnectionData();
            if (host == null || data == null || string.IsNullOrEmpty(data.LocalPlayerId)) return null;
            string id = ((IPartyStateQuery)host).ActivePartySessionId;
            return string.IsNullOrEmpty(id) ? null : id;
        }

        static int HumanCount()
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsServer ? SpectatorSession.CountHumanClients(nm) : 0;
        }

        static string StateLine()
        {
            var host = HostConnectionService.Instance;
            var data = ConnectionData();
            return host != null && data != null ? PartyConsoleCommand.Describe(host, data) : "party: not loaded";
        }

        void Step(string what)
        {
            float t = Time.realtimeSinceStartup - _startTime;
            _result.steps.Add($"{t:0.0}s {what}");
            NetSessionRecorder.Mark("mpAuto", what);
            CSDebug.LogVerbose(CSLogChannel.Party, $"[MultiplayerAutoPilot] {t:0.0}s {what}");
        }

        void Finish(bool ok, string reason)
        {
            _result.ok = ok;
            _result.reason = reason;
            _result.seconds = Time.realtimeSinceStartup - _startTime;
            _result.partyLine = StateLine();

            string path = !string.IsNullOrEmpty(_args.ResultPath)
                ? _args.ResultPath
                : Path.Combine(Application.persistentDataPath, "MpAuto", _args.DefaultResultFileName());
            try
            {
                WriteAllTextAtomic(path, JsonUtility.ToJson(_result, true));
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[MultiplayerAutoPilot] could not write the result to {path}: {e.Message}");
            }

            NetSessionRecorder.Mark("mpAuto", ok ? "PASS" : "FAIL " + reason);
            if (ok) CSDebug.Log($"[MultiplayerAutoPilot] {_args.Role} PASS in {_result.seconds:0.0}s -> {path}");
            else CSDebug.LogWarning($"[MultiplayerAutoPilot] {_args.Role} FAIL after {_result.seconds:0.0}s: {reason} -> {path}");

#if !UNITY_EDITOR
            if (_args.QuitWhenDone)
            {
                // The app defers a quit to leave its UGS session and then quits again itself, which lost the code.
                ApplicationLifecycleManager.QuitExitCode = ok ? 0 : 1;
                Application.Quit(ok ? 0 : 1);
            }
#endif
        }

        static void WriteAllTextAtomic(string path, string text)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        static void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        static string Short(string id) => string.IsNullOrEmpty(id) ? "-" : id.Length > 8 ? id.Substring(0, 8) : id;
#endif
    }
}
