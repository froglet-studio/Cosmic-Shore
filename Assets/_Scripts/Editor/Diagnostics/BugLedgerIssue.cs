using System;
using System.Globalization;
using System.Text;

namespace CosmicShore.Editor
{
    /// <summary>The three lifecycle states an issue file can carry. Strings, not an enum, because
    /// the value is serialized into committed JSON and must survive code churn verbatim.</summary>
    public static class BugLedgerIssueState
    {
        public const string Open = "open";
        public const string Validating = "validating";   // marked fixed; awaiting clean sessions
        public const string Ignored = "ignored";         // parked; matching errors never reopen it
    }

    public static class BugLedgerIssueKind
    {
        public const string Auto = "auto";               // filed from a captured error signature
        public const string Custom = "custom";           // filed by a human (no signature unless added)
    }

    /// <summary>
    /// One bug in the shared ledger — the in-memory form of one
    /// <c>BugLedger/issues/&lt;id&gt;.bug.json</c> file at the project root.
    ///
    /// <para>Serialization is hand-rolled on purpose: the capture path runs on a background thread
    /// where <c>JsonUtility</c> is unsafe, and the committed files must stay merge-friendly and
    /// reviewable — stable field order, one field per line, invariant culture, <c>\n</c> endings,
    /// so a diff is exactly the fields that changed. Unknown keys are skipped on read (forward
    /// compatibility), missing keys keep their defaults.</para>
    /// </summary>
    public sealed class BugLedgerIssue
    {
        public const int SchemaVersion = 1;

        public string Id = "";
        public string Kind = BugLedgerIssueKind.Auto;
        public string State = BugLedgerIssueState.Open;
        public string Title = "";
        public string Notes = "";
        /// <summary>Normalized error fingerprint (empty = manual-only validation).</summary>
        public string Signature = "";
        /// <summary>First captured message, truncated — what the console actually said.</summary>
        public string Sample = "";
        /// <summary>Captured stack excerpt, truncated.</summary>
        public string Stack = "";
        /// <summary>"PlayMode" or "EditMode" — which clean-session clock validates it.</summary>
        public string Scope = "";
        public string LogType = "";
        public string Reporter = "";
        public string Machine = "";
        public string CreatedUtc = "";
        public string LastSeenUtc = "";
        public string FixedBy = "";
        public string FixedUtc = "";
        public int TimesSeen;
        public int Regressions;
        public int CleanSessions;
        public int CleanSessionsRequired = 2;
        public bool ValidationPaused;

        public bool HasSignature => !string.IsNullOrEmpty(Signature);
        public bool IsOpen => State == BugLedgerIssueState.Open;
        public bool IsValidating => State == BugLedgerIssueState.Validating;
        public bool IsIgnored => State == BugLedgerIssueState.Ignored;

        // ── Write ────────────────────────────────────────────────────────────────

        public string ToJson()
        {
            var sb = new StringBuilder(1024);
            sb.Append("{\n");
            sb.Append("  \"schema\": ").Append(SchemaVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            AppendString(sb, "id", Id);
            AppendString(sb, "kind", Kind);
            AppendString(sb, "state", State);
            AppendString(sb, "title", Title);
            AppendString(sb, "notes", Notes);
            AppendString(sb, "signature", Signature);
            AppendString(sb, "sample", Sample);
            AppendString(sb, "stack", Stack);
            AppendString(sb, "scope", Scope);
            AppendString(sb, "logType", LogType);
            AppendString(sb, "reporter", Reporter);
            AppendString(sb, "machine", Machine);
            AppendString(sb, "createdUtc", CreatedUtc);
            AppendString(sb, "lastSeenUtc", LastSeenUtc);
            AppendString(sb, "fixedBy", FixedBy);
            AppendString(sb, "fixedUtc", FixedUtc);
            AppendInt(sb, "timesSeen", TimesSeen);
            AppendInt(sb, "regressions", Regressions);
            AppendInt(sb, "cleanSessions", CleanSessions);
            AppendInt(sb, "cleanSessionsRequired", CleanSessionsRequired);
            sb.Append("  \"validationPaused\": ").Append(ValidationPaused ? "true" : "false").Append('\n');
            sb.Append("}\n");
            return sb.ToString();
        }

        static void AppendString(StringBuilder sb, string key, string value)
        {
            sb.Append("  \"").Append(key).Append("\": \"");
            Escape(sb, value);
            sb.Append("\",\n");
        }

        static void AppendInt(StringBuilder sb, string key, int value)
            => sb.Append("  \"").Append(key).Append("\": ")
                 .Append(value.ToString(CultureInfo.InvariantCulture)).Append(",\n");

        static void Escape(StringBuilder sb, string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
        }

        // ── Read ─────────────────────────────────────────────────────────────────

        /// <summary>Parses one issue file. Returns null for anything unreadable — a torn or
        /// hand-mangled file must never take the ledger down.</summary>
        public static BugLedgerIssue FromJson(string text)
        {
            try
            {
                var issue = new BugLedgerIssue { CleanSessionsRequired = 2 };
                int i = text.IndexOf('{');
                if (i < 0) return null;
                i++;

                while (i < text.Length)
                {
                    SkipWhitespace(text, ref i);
                    if (i >= text.Length) return null;
                    char c = text[i];
                    if (c == '}') break;
                    if (c == ',') { i++; continue; }
                    if (c != '"') return null;

                    string key = ParseString(text, ref i);
                    SkipWhitespace(text, ref i);
                    if (i >= text.Length || text[i] != ':') return null;
                    i++;
                    SkipWhitespace(text, ref i);
                    if (i >= text.Length) return null;

                    string value;
                    if (text[i] == '"') value = ParseString(text, ref i);
                    else
                    {
                        int start = i;
                        while (i < text.Length && text[i] != ',' && text[i] != '}' && !char.IsWhiteSpace(text[i])) i++;
                        value = text[start..i];
                    }
                    Assign(issue, key, value);
                }

                return string.IsNullOrEmpty(issue.Id) ? null : issue;
            }
            catch { return null; }
        }

        static void Assign(BugLedgerIssue issue, string key, string value)
        {
            switch (key)
            {
                case "id": issue.Id = value; break;
                case "kind": issue.Kind = value; break;
                case "state": issue.State = value; break;
                case "title": issue.Title = value; break;
                case "notes": issue.Notes = value; break;
                case "signature": issue.Signature = value; break;
                case "sample": issue.Sample = value; break;
                case "stack": issue.Stack = value; break;
                case "scope": issue.Scope = value; break;
                case "logType": issue.LogType = value; break;
                case "reporter": issue.Reporter = value; break;
                case "machine": issue.Machine = value; break;
                case "createdUtc": issue.CreatedUtc = value; break;
                case "lastSeenUtc": issue.LastSeenUtc = value; break;
                case "fixedBy": issue.FixedBy = value; break;
                case "fixedUtc": issue.FixedUtc = value; break;
                case "timesSeen": TryInt(value, ref issue.TimesSeen); break;
                case "regressions": TryInt(value, ref issue.Regressions); break;
                case "cleanSessions": TryInt(value, ref issue.CleanSessions); break;
                case "cleanSessionsRequired": TryInt(value, ref issue.CleanSessionsRequired); break;
                case "validationPaused": issue.ValidationPaused = value == "true"; break;
                // "schema" and unknown keys: skipped on purpose.
            }
        }

        static void TryInt(string value, ref int field)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                field = parsed;
        }

        static void SkipWhitespace(string text, ref int i)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        }

        /// <summary>Parses a quoted JSON string starting at <paramref name="i"/> (which must point
        /// at the opening quote); leaves <paramref name="i"/> just past the closing quote.</summary>
        static string ParseString(string text, ref int i)
        {
            i++; // opening quote
            var sb = new StringBuilder(32);
            while (i < text.Length)
            {
                char c = text[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= text.Length) break;
                char e = text[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 <= text.Length &&
                            int.TryParse(text.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                        {
                            sb.Append((char)code);
                            i += 4;
                        }
                        break;
                }
            }
            throw new FormatException("Unterminated string");
        }
    }
}
