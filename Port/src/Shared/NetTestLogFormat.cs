#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Amoebius
{
    /// <summary>
    /// The multiplayer test log's file format (<c>cosmicshore.nettestlog.v1</c>), shared by the game that writes it
    /// (<c>CosmicShore.Player/NetTestLog.cs</c>) and the launcher that sends it to Claude (<c>LauncherJobs.NetLogs.cs</c>).
    /// The Unity editor writes the same format at Play stop (<c>Assets/_Scripts/Editor/Diagnostics/NetTestLogs.cs</c>);
    /// <c>Tools/Build/net_logs.py</c> reads both. Pure: no game, no engine, no I/O but <see cref="NewFiles"/>.
    /// </summary>
    public static class NetTestLogFormat
    {
        public const string Schema = "cosmicshore.nettestlog.v1";
        public const int MaxFileBytes = 450 * 1024;   // under the Bug Ledger's 512 KB per-file cap

        /// <summary>One console line: seconds since the run started, "log" / "warning" / "error" / "exception" / "assert", the message, its first stack frame.</summary>
        public readonly record struct Line(float T, string Type, string Msg, string? At);

        public sealed class Info
        {
            public string Peer = "main", Why = "", Exit = "", User = "";
            public string? Crash, Branch, Commit;
            public DateTime Started, Ended;
        }

        /// <summary>
        /// Why a session is a multiplayer test, or null. Only evidence of ANOTHER player counts: the host approving
        /// its own client 0 and party transitions happen in solo online play too. The Unity editor's
        /// <c>NetTestLogs.MultiplayerEvidence</c> applies the same rule to the same record.
        /// </summary>
        public static string? MultiplayerEvidence(string? role, IEnumerable<(string? e, string? detail)>? lifecycle)
        {
            if (role == "client" || role == "spectator") return "role " + role;
            if (lifecycle == null) return null;
            foreach (var (e, detail) in lifecycle)
                switch (e)
                {
                    case "clientApproved":
                        var id = (detail ?? "").Split(' ')[0];
                        if (id.Length > 0 && id != "0") return "client " + id + " approved";
                        break;
                    case "clientLeft":
                    case "leaverToAI":
                    case "refused":
                    case "bounce":
                        return e;
                }
            return null;
        }

        /// <summary>"main" unless named; lower-case letters, digits and '-' only (it is part of a file name and a git path).</summary>
        public static string PeerName(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "main";
            return Slug(raw.Trim());
        }

        /// <summary>The Bug Ledger's slug (Unity's <c>BugHuntReports.Slug</c>): lower case, anything but a letter, digit or '-' becomes '-'.</summary>
        public static string Slug(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "unknown";
            return new string(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-').ToArray());
        }

        /// <summary><c>net_20261015_101500_p2.json</c>: the name the Unity editor gives its files, which the reader groups by.</summary>
        public static string FileName(DateTime startedUtc, string? peer) =>
            $"net_{startedUtc.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}_{PeerName(peer)}.json";

        /// <summary>"p2" from "net_20261015_101500_p2.json".</summary>
        public static string PeerOf(string fileName)
        {
            var parts = Path.GetFileNameWithoutExtension(fileName).Split('_');
            return parts.Length >= 4 ? string.Join("_", parts.Skip(3)) : Path.GetFileName(fileName);
        }

        /// <summary>Where a net log lives on the Bug Ledger branch: the path Unity's sender gives the <c>net</c> kind.</summary>
        public static string ReportPath(string user, string fileName) => $"BugLedger/shared/reports/{Slug(user)}/net/{Path.GetFileName(fileName)}";

        /// <summary>The Bug Ledger branch a user's reports go to (Unity's <c>BugLedgerSender.BranchFor</c>).</summary>
        public static string Branch(string user) => "bugledger/" + Slug(user);

        /// <summary>The players these logs came from, main first: "main", "p2", "p3".</summary>
        public static List<string> PeersOf(IEnumerable<string> fileNames) =>
            fileNames.Select(PeerOf).Distinct().OrderBy(p => p == "main" ? 0 : 1).ThenBy(p => p, StringComparer.Ordinal).ToList();

        /// <summary>"main, p2 and p3".</summary>
        public static string Players(IReadOnlyList<string> peers) => peers.Count switch
        {
            0 => "this test",
            1 => peers[0],
            _ => string.Join(", ", peers.Take(peers.Count - 1)) + " and " + peers[^1],
        };

        /// <summary>The net logs in <paramref name="dir"/> not in <paramref name="known"/> (by file name), oldest first.</summary>
        public static List<FileInfo> NewFiles(string dir, ISet<string> known)
        {
            if (!Directory.Exists(dir)) return new();
            return new DirectoryInfo(dir).GetFiles("net_*.json")
                .Where(f => !known.Contains(f.Name) && f.Length <= 512 * 1024)
                .OrderBy(f => f.Name, StringComparer.Ordinal).ToList();
        }

        /// <summary>The file's text. Newest lines win: the end of a session is where the failure is, so the oldest go until it fits.</summary>
        public static string Build(string recordJson, Info info, IReadOnlyList<Line> lines, int dropped, int errors, int warnings,
                                   int maxBytes = MaxFileBytes)
        {
            int first = 0;
            while (true)
            {
                string text = Compose(recordJson, info, lines, first, dropped + first, errors, warnings);
                if (Encoding.UTF8.GetByteCount(text) <= maxBytes || first >= lines.Count) return text;
                first = Math.Min(lines.Count, first + Math.Max(50, (lines.Count - first) / 5));
            }
        }

        static string Compose(string recordJson, Info info, IReadOnlyList<Line> lines, int first, int dropped, int errors, int warnings)
        {
            using var stream = new MemoryStream();
            using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                w.WriteStartObject();
                w.WriteString("schema", Schema);
                w.WriteString("engine", "amoebius");
                w.WriteString("peer", PeerName(info.Peer));
                w.WriteBoolean("isVirtualPlayer", false);
                w.WriteString("why", info.Why);
                w.WriteString("user", info.User);
                w.WriteString("branch", info.Branch);
                w.WriteString("commit", info.Commit);
                w.WriteString("playStartedUtc", info.Started.ToString("o", CultureInfo.InvariantCulture));
                w.WriteString("playEndedUtc", info.Ended.ToString("o", CultureInfo.InvariantCulture));
                w.WriteString("exit", info.Exit);
                if (info.Crash != null) w.WriteString("crash", Clip(info.Crash, 4000));
                w.WritePropertyName("record");
                using (var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(recordJson) ? "{}" : recordJson)) doc.WriteTo(w);
                w.WriteStartObject("console");
                w.WriteNumber("errors", errors);
                w.WriteNumber("warnings", warnings);
                w.WriteNumber("dropped", dropped);
                w.WriteStartArray("lines");
                for (int i = first; i < lines.Count; i++)
                {
                    var l = lines[i];
                    w.WriteStartObject();
                    w.WriteNumber("t", Math.Round(l.T, 3));
                    w.WriteString("type", l.Type);
                    w.WriteString("msg", l.Msg);
                    if (l.At != null) w.WriteString("at", l.At);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
        }

        public static string? Clip(string? s, int max) => s == null ? null : s.Length <= max ? s : s[..max] + "...";
    }
}
