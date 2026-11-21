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
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void MaybeStart()
        {
            if (Args == null || !Args.IsActive) return;
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
                    reason = _args.Role == MultiplayerAutoRole.Host ? await HostAsync(ct) : await JoinAsync(ct);
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
            return null;
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

            for (int attempt = 1; attempt <= MaxJoinAttempts; attempt++)
            {
                PartyPlayerData target = default;
                if (!await WaitUntil(ct, attempt == 1 ? "host in the online list, joinable" : null,
                        () => TryFindHost(kind, value, out target)))
                    return "the host never showed up as joinable in the online list";

                var controller = PartyInviteController.Instance;
                if (controller == null) return "PartyInviteController is not loaded";

                Step($"JOIN pressed for {target.DisplayName} (attempt {attempt})");
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
                    if (IsClientInParty()) { Step("in the party as a client"); return null; }
                    await UniTask.Delay(TimeSpan.FromSeconds(PollSeconds), DelayType.Realtime, cancellationToken: ct);
                }
                if (TimedOut()) break;
                Step($"join attempt {attempt} did not reach the party ({StateLine()})");
            }
            return $"not in the party after {MaxJoinAttempts} attempts";
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
            if (_args.QuitWhenDone) Application.Quit(ok ? 0 : 1);
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
