// ─────────────────────────────────────────────────────────────────────────────
// MultiplayerAutoArgs.cs
// The command line of an automatic multiplayer run, and the rendezvous file a host leaves for its joiners.
//
// WHY IT EXISTS:
//   Two MPPM Editor clones need ~24 GB on the team's machines and ran at 1-10 fps under that memory pressure
//   (multiplayer test of 2026-11-02), so four players cannot be tested that way. Standalone DEVELOPMENT players
//   cost a fraction of that. MultiplayerAutoPilot drives them; this file is its pure half: no Unity objects, so
//   MultiplayerAutoArgsTests covers every rule without a running game.
//
// THE ARGUMENTS (development builds and the editor only; a release player ignores them):
//   -csMpProfile <name>        the UGS auth profile, so two players on one PC are two UGS players
//   -csMpAutoHost <file>       host: write the rendezvous file, then wait until -csMpExpect humans are connected
//   -csMpAutoJoin <target>     join: <target> is a rendezvous file, "id:<UGS player id>" or "name:<display name>"
//   -csMpExpect <n>            players the host waits for, itself included (2-4; default 2)
//   -csMpTimeout <seconds>     give up after this long (default 240)
//   -csMpResult <file>         where the result JSON goes (default persistentDataPath/MpAuto/<role>-<profile>.json)
//   -csMpQuit                  quit when done: exit code 0 on success, 1 on failure
// Tools/Build/mp_auto.py launches N built players with these arguments and reads their results.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public enum MultiplayerAutoRole
    {
        None = 0,
        Host = 1,
        Join = 2,
    }

    /// <summary>Who the joiner looks for: the host's UGS player id, its display name, or a rendezvous file.</summary>
    public enum MultiplayerAutoTargetKind
    {
        None = 0,
        RendezvousFile = 1,
        PlayerId = 2,
        DisplayName = 3,
    }

    /// <summary>The parsed command line of an automatic multiplayer run (see the file header).</summary>
    public sealed class MultiplayerAutoArgs
    {
        public const string ProfileArg = "-csMpProfile";
        public const string HostArg = "-csMpAutoHost";
        public const string JoinArg = "-csMpAutoJoin";
        public const string ExpectArg = "-csMpExpect";
        public const string TimeoutArg = "-csMpTimeout";
        public const string ResultArg = "-csMpResult";
        public const string QuitArg = "-csMpQuit";

        public const int DefaultExpect = 2;
        public const int MaxExpect = 4;          // A party is 4. Never 6 (Docs/MULTIPLAYER_START_HERE.md §2).
        public const float DefaultTimeoutSeconds = 240f;

        // UGS auth profile names: letters, digits, '-' and '_', at most 30 characters.
        public const int MaxProfileLength = 30;

        public MultiplayerAutoRole Role { get; private set; }
        public string Profile { get; private set; } = string.Empty;
        public string RendezvousPath { get; private set; } = string.Empty;
        public MultiplayerAutoTargetKind TargetKind { get; private set; }
        public string TargetValue { get; private set; } = string.Empty;
        public int Expect { get; private set; } = DefaultExpect;
        public float TimeoutSeconds { get; private set; } = DefaultTimeoutSeconds;
        public string ResultPath { get; private set; } = string.Empty;
        public bool QuitWhenDone { get; private set; }

        /// <summary>Problems found while parsing. A run with any is refused (the result file says why).</summary>
        public readonly List<string> Errors = new();

        public bool IsActive => Role != MultiplayerAutoRole.None;

        /// <summary>Parses <paramref name="args"/> (the process command line, first element the executable).</summary>
        public static MultiplayerAutoArgs Parse(IReadOnlyList<string> args)
        {
            var a = new MultiplayerAutoArgs();
            if (args == null) return a;

            for (int i = 0; i < args.Count; i++)
            {
                string arg = args[i];
                if (string.IsNullOrEmpty(arg)) continue;

                if (Is(arg, QuitArg)) { a.QuitWhenDone = true; continue; }

                bool takesValue = Is(arg, ProfileArg) || Is(arg, HostArg) || Is(arg, JoinArg)
                                  || Is(arg, ExpectArg) || Is(arg, TimeoutArg) || Is(arg, ResultArg);
                if (!takesValue) continue;

                string value = i + 1 < args.Count ? args[i + 1] : null;
                if (string.IsNullOrEmpty(value) || value.StartsWith("-cs", StringComparison.Ordinal))
                {
                    a.Errors.Add($"{arg} needs a value");
                    continue;
                }
                i++;

                if (Is(arg, ProfileArg)) a.SetProfile(value);
                else if (Is(arg, HostArg)) a.SetRole(MultiplayerAutoRole.Host, arg, () => a.RendezvousPath = value);
                else if (Is(arg, JoinArg)) a.SetRole(MultiplayerAutoRole.Join, arg, () => a.SetTarget(value));
                else if (Is(arg, ExpectArg)) a.SetExpect(value);
                else if (Is(arg, TimeoutArg)) a.SetTimeout(value);
                else if (Is(arg, ResultArg)) a.ResultPath = value;
            }

            if (a.IsActive && string.IsNullOrEmpty(a.Profile))
                a.Errors.Add($"{ProfileArg} is required for an automatic run (two players on one PC would share one UGS player)");
            return a;
        }

        /// <summary>Is <paramref name="profile"/> a name UGS accepts as an auth profile?</summary>
        public static bool IsValidProfile(string profile)
        {
            if (string.IsNullOrEmpty(profile) || profile.Length > MaxProfileLength) return false;
            foreach (char c in profile)
                if (!(char.IsLetterOrDigit(c) && c < 128) && c != '-' && c != '_') return false;
            return true;
        }

        /// <summary>The result file's default name for this run.</summary>
        public string DefaultResultFileName() =>
            $"{(Role == MultiplayerAutoRole.Host ? "host" : "join")}-{(string.IsNullOrEmpty(Profile) ? "default" : Profile)}.json";

        void SetRole(MultiplayerAutoRole role, string arg, Action apply)
        {
            if (Role != MultiplayerAutoRole.None && Role != role)
            {
                Errors.Add($"{HostArg} and {JoinArg} cannot both be given");
                return;
            }
            Role = role;
            apply();
        }

        void SetProfile(string value)
        {
            if (!IsValidProfile(value))
            {
                Errors.Add($"{ProfileArg} '{value}' is not a valid UGS profile (letters, digits, '-', '_'; at most {MaxProfileLength})");
                return;
            }
            Profile = value;
        }

        void SetTarget(string value)
        {
            if (value.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
            {
                TargetKind = MultiplayerAutoTargetKind.PlayerId;
                TargetValue = value.Substring(3);
            }
            else if (value.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
            {
                TargetKind = MultiplayerAutoTargetKind.DisplayName;
                TargetValue = value.Substring(5);
            }
            else
            {
                TargetKind = MultiplayerAutoTargetKind.RendezvousFile;
                TargetValue = value;
                RendezvousPath = value;
            }
            if (string.IsNullOrEmpty(TargetValue))
            {
                Errors.Add($"{JoinArg} '{value}' names no host");
                TargetKind = MultiplayerAutoTargetKind.None;
            }
        }

        void SetExpect(string value)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n < 2 || n > MaxExpect)
            {
                Errors.Add($"{ExpectArg} '{value}' must be 2-{MaxExpect}");
                return;
            }
            Expect = n;
        }

        void SetTimeout(string value)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float s) || s <= 0f)
            {
                Errors.Add($"{TimeoutArg} '{value}' must be a positive number of seconds");
                return;
            }
            TimeoutSeconds = s;
        }

        static bool Is(string arg, string name) => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>What a host writes for its joiners: who to look for in the online list, and when it was written.</summary>
    [Serializable]
    public sealed class MultiplayerAutoRendezvous
    {
        public string playerId;
        public string displayName;
        public string sessionId;
        public long writtenUnixSeconds;

        /// <summary>A rendezvous older than this is a previous run's, not this host's.</summary>
        public const long MaxAgeSeconds = 15 * 60;

        public string ToJson() => JsonUtility.ToJson(this);

        /// <summary>Reads <paramref name="json"/>; false for anything that does not name a player.</summary>
        public static bool TryParse(string json, out MultiplayerAutoRendezvous rendezvous)
        {
            rendezvous = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try { rendezvous = JsonUtility.FromJson<MultiplayerAutoRendezvous>(json); }
            catch (ArgumentException) { return false; }
            return rendezvous != null && !string.IsNullOrEmpty(rendezvous.playerId);
        }

        public bool IsFresh(long nowUnixSeconds) =>
            writtenUnixSeconds > 0 && nowUnixSeconds - writtenUnixSeconds <= MaxAgeSeconds && writtenUnixSeconds <= nowUnixSeconds + 60;
    }

    /// <summary>The joiner's lookup: which online row is the host it was told to join.</summary>
    public static class MultiplayerAutoTarget
    {
        /// <summary>
        /// True when <paramref name="playerId"/> / <paramref name="displayName"/> is the host named by
        /// <paramref name="kind"/> and <paramref name="value"/>. A rendezvous file is resolved to a player id first.
        /// Display names match case-insensitively, as the <c>party</c> console command does.
        /// </summary>
        public static bool Matches(MultiplayerAutoTargetKind kind, string value, string playerId, string displayName) =>
            kind switch
            {
                MultiplayerAutoTargetKind.PlayerId => !string.IsNullOrEmpty(value) && string.Equals(playerId, value, StringComparison.Ordinal),
                MultiplayerAutoTargetKind.DisplayName => !string.IsNullOrEmpty(value) && string.Equals(displayName, value, StringComparison.OrdinalIgnoreCase),
                _ => false,
            };
    }
}
