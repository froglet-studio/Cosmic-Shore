using System;
using System.Collections.Generic;
using CosmicShore.Editor.Froglet;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The after-play confirmation, and the Project Record it opens into (Yash, 2026-10-17: "bring the pop-up back as
    /// a confirmation only, with no question ... minimized, a small confirmation, but you can expand it ... performance,
    /// network, flights, errors ... a project diagnosis history ... Ys-bleeding-edge and bleeding-edge as a performance
    /// info"). Docs/DIAGNOSTICS.md § "The after-play confirmation and the Project Record".
    ///
    /// <para><b>Small:</b> after <see cref="BugHuntAutoSend"/> sends a play test's new errors, captures, AI flight
    /// records and multiplayer logs, a small window says what went and whether the send reached
    /// <c>bugledger/&lt;user&gt;</c> (<see cref="BugLedgerSender.LastOutcome"/>). Nothing to answer; it closes by itself
    /// unless it is expanded or the pointer is over it.</para>
    ///
    /// <para><b>Expanded:</b> the Project Record (<see cref="ProjectRecord"/>): an overview, then one tab each for
    /// performance, network, AI flights and errors. Each tab says what that data is for, what this machine has sent
    /// (from its bugledger branch) and, for performance and errors, what each branch on the performance path publishes
    /// in its <c>Docs/perf/status.json</c> (Ys-bleeding-edge first, then bleeding-edge). Also opened from
    /// <b>FrogletTools &gt; Diagnostics &gt; Project Record</b>. A reader: it changes nothing.</para>
    /// </summary>
    public sealed class BugHuntSendConfirmation : EditorWindow
    {
        const float CollapsedWidth = 440f;
        const float CollapsedHeight = 132f;
        const float ExpandedWidth = 720f;
        const float ExpandedHeight = 600f;
        const double AutoCloseSeconds = 20.0;

        static readonly string[] Tabs = { "Overview", "Performance", "Network", "AI flights", "Errors" };

        [SerializeField] string sentSummary = "";
        [SerializeField] bool expanded;
        [SerializeField] bool fromSend;
        [SerializeField] int tab;
        Vector2 _scroll;
        double _closeAt = double.MaxValue;
        bool _settled;
        double _nextRepaint;

        // ── Opening ─────────────────────────────────────────────────────────────────────────────

        /// <summary>After an automatic send: the small confirmation. <paramref name="summary"/> is what went.</summary>
        public static void ShowAfterSend(string summary)
        {
            var w = GetOrCreate();
            w.sentSummary = summary ?? "";
            w.fromSend = true;
            w.expanded = false;
            w._settled = false;
            w._closeAt = double.MaxValue;
            w.Resize(false);
            w.ShowUtility();
            ProjectRecord.LoadInBackground();
        }

        [MenuItem("FrogletTools/Diagnostics/Project Record", false, 129)]
        [FrogletTool(FrogletToolCategory.Diagnostics, Importance = 4,
            Description = "What this machine sent to Claude, and the performance status of Ys-bleeding-edge and bleeding-edge.",
            DocPath = "Docs/DIAGNOSTICS.md#the-after-play-confirmation-and-the-project-record")]
        public static void OpenRecord()
        {
            var w = GetOrCreate();
            w.fromSend = false;
            w.expanded = true;
            w._closeAt = double.MaxValue;
            w.Resize(true);
            w.ShowUtility();
            ProjectRecord.LoadInBackground();
        }

        static BugHuntSendConfirmation GetOrCreate()
        {
            var open = Resources.FindObjectsOfTypeAll<BugHuntSendConfirmation>();
            var w = open.Length > 0 ? open[0] : CreateInstance<BugHuntSendConfirmation>();
            w.titleContent = new GUIContent("Sent to Claude");
            return w;
        }

        void Resize(bool big)
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            float wdt = big ? ExpandedWidth : CollapsedWidth, hgt = big ? ExpandedHeight : CollapsedHeight;
            minSize = new Vector2(big ? 520f : CollapsedWidth, big ? 360f : CollapsedHeight);
            position = new Rect(main.xMax - wdt - 24f, main.y + 72f, wdt, hgt);
        }

        // ── Lifetime ────────────────────────────────────────────────────────────────────────────

        void OnEnable() => EditorApplication.update += Tick;
        void OnDisable() => EditorApplication.update -= Tick;

        void Tick()
        {
            if (fromSend && !expanded && !_settled && BugLedgerSender.LastOutcome != BugLedgerSender.SendOutcome.Sending)
            {
                _settled = true;
                _closeAt = EditorApplication.timeSinceStartup + AutoCloseSeconds;
            }
            if (!expanded && EditorApplication.timeSinceStartup >= _closeAt)
            {
                Close();
                return;
            }
            // A countdown and a background load are all that change between events: four repaints a second is plenty.
            if (EditorApplication.timeSinceStartup < _nextRepaint) return;
            _nextRepaint = EditorApplication.timeSinceStartup + 0.25;
            Repaint();
        }

        // ── Drawing ─────────────────────────────────────────────────────────────────────────────

        void OnGUI()
        {
            // The pointer over the window holds it open: someone is reading it.
            if (!expanded && _settled && Event.current.type == EventType.MouseMove)
                _closeAt = EditorApplication.timeSinceStartup + AutoCloseSeconds;
            wantsMouseMove = !expanded;

            if (fromSend) DrawConfirmation();
            if (expanded) DrawRecord();
        }

        void DrawConfirmation()
        {
            var (label, color, line) = OutcomeLine();
            using (new EditorGUILayout.HorizontalScope())
            {
                var pill = GUILayoutUtility.GetRect(96f, 20f, GUILayout.Width(96f), GUILayout.Height(20f));
                FrogletEditorPalette.StatusPill(pill, label, color);
                GUILayout.Label("Play test sent to Claude", FrogletEditorPalette.CardTitle);
            }
            GUILayout.Label(string.IsNullOrEmpty(sentSummary) ? "Your play test's new data." : sentSummary,
                FrogletEditorPalette.CardBodyWrapped);
            GUILayout.Label(line, FrogletEditorPalette.CardBodyWrapped);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton(expanded ? "Hide project record" : "Project record  ▸",
                        FrogletEditorPalette.Info, 170f, 22f, "Performance, network, AI flights and errors: what you sent and what each branch reports"))
                {
                    expanded = !expanded;
                    Resize(expanded);
                    if (expanded && ProjectRecord.Latest == null) ProjectRecord.LoadInBackground();
                }
                GUILayout.FlexibleSpace();
                if (!expanded && _closeAt < double.MaxValue)
                    GUILayout.Label($"closes in {Math.Max(0, (int)(_closeAt - EditorApplication.timeSinceStartup))} s",
                        EditorStyles.miniLabel);
                if (FrogletEditorPalette.ColorButton("Close", FrogletEditorPalette.Slate, 64f, 22f, outline: true))
                    Close();
            }
        }

        (string label, Color color, string line) OutcomeLine()
        {
            string where = ProjectRecord.Latest?.SendBranch is { Length: > 0 } b ? b : BugLedgerSender.BranchFor(Environment.UserName);
            return BugLedgerSender.LastOutcome switch
            {
                BugLedgerSender.SendOutcome.Sending => ("SENDING", FrogletEditorPalette.Info, $"Pushing to {where}…"),
                BugLedgerSender.SendOutcome.Sent => ("SENT", FrogletEditorPalette.Ok,
                    $"On {where}. Nothing in your project or branch changed; Claude reads it from there."),
                BugLedgerSender.SendOutcome.NothingNew => ("UP TO DATE", FrogletEditorPalette.Ok,
                    "Claude already had all of it; nothing needed sending."),
                BugLedgerSender.SendOutcome.Failed => ("NOT SENT", FrogletEditorPalette.Error,
                    $"Could not reach GitHub ({Trim(BugLedgerSender.LastDetail, 120)}). It stays on this machine and goes with the next send."),
                _ => ("READY", FrogletEditorPalette.Muted, "Waiting for the send to start."),
            };
        }

        void DrawRecord()
        {
            var record = ProjectRecord.Latest;
            if (!fromSend)
                FrogletEditorPalette.Banner("Project Record",
                    "What this machine sent to Claude, and what each performance branch reports", FrogletEditorPalette.Info);
            else
                GUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                tab = GUILayout.Toolbar(tab, Tabs, GUILayout.Height(22f));
                GUILayout.Space(8f);
                bool loading = ProjectRecord.IsLoading;
                if (FrogletEditorPalette.ColorButton(loading ? "Loading…" : "Refresh from GitHub", FrogletEditorPalette.Info,
                        140f, 22f, "Fetch the branches again (read-only)", enabled: !loading, outline: true))
                    ProjectRecord.LoadInBackground();
            }

            if (record == null)
            {
                GUILayout.Label(ProjectRecord.IsLoading ? "Reading GitHub…" : "Nothing loaded yet.", FrogletEditorPalette.CardBody);
                return;
            }
            if (!string.IsNullOrEmpty(record.Problem))
                Note($"Could not read everything: {record.Problem}", FrogletEditorPalette.Warn);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (tab)
            {
                case 0: DrawOverview(record); break;
                case 1: DrawPerformance(record); break;
                case 2: DrawKind(record, "net", "Network",
                    "One log per player of a multiplayer play test (the main editor and each virtual player): its session record " +
                    "and console. Claude reads them with Tools/Build/net_logs.py to find desyncs, lobby and relay failures. " +
                    "Written only by a multiplayer test; the main editor sends every player's file."); break;
                case 3: DrawKind(record, "flights", "AI flights",
                    "A record of how you flew a mode (HumanFlightRecorder, automatic in every mode). The AIs learn from these " +
                    "(Tools/Build/learn_from_flights.py): the more people race, the better the AI pilots get."); break;
                default: DrawErrors(record); break;
            }
            EditorGUILayout.EndScrollView();
            GUILayout.Label($"Read {record.LoadedLocal:HH:mm:ss} from origin. Read-only: nothing here changes your project.",
                EditorStyles.miniLabel);
        }

        void DrawOverview(ProjectRecord r)
        {
            Section("Performance path");
            foreach (var b in r.Branches)
            {
                Header(b.Branch, b.Role);
                if (!b.Found) { Note(b.Error, FrogletEditorPalette.Muted); continue; }
                Line($"Status written {b.GeneratedUtc}" + (b.LastChange.Length > 0 ? $"  ·  last change {b.LastChange}" : ""));
                Line((b.GatesOk ? "Gates: all OK" : "Gates RED: " + string.Join(", ", b.RedGates)) +
                     $"  ·  regressions: {b.Regressions.Count}  ·  open console bugs: {b.BugsOpen} (+{b.BugsValidating} validating)",
                     b.GatesOk && b.Regressions.Count == 0 ? FrogletEditorPalette.Ok : FrogletEditorPalette.Warn);
                foreach (var a in b.Amoebius) Line("Amoebius " + a);
                if (b.OwedScenarios.Count > 0) Line("Captures still owed: " + string.Join(", ", b.OwedScenarios));
                for (int i = 0; i < b.Next.Count && i < 3; i++) Line($"Next {i + 1}. {b.Next[i]}");
            }

            Section($"What you sent ({r.SendBranch})");
            if (r.Sends.Count == 0) Note("Nothing on your branch yet: it is created by your first send.", FrogletEditorPalette.Muted);
            for (int i = 0; i < r.Sends.Count && i < 5; i++)
                Line($"{r.Sends[i].When}  {r.Sends[i].Author}: {r.Sends[i].Subject}");
            Line(KindCount(r, "perf", "performance file") + "  ·  " + KindCount(r, "net", "network log") + "  ·  " +
                 KindCount(r, "flights", "AI flight record") + "  ·  " + KindCount(r, "redlog", "error log"));
        }

        void DrawPerformance(ProjectRecord r)
        {
            Note("Your editor captures a scene by itself after it has run long enough in Release (S2, Skim Race, S5 and the " +
                 "rest), and the capture goes to Claude after Play. The daily sweep (Tools/Build/perf_hunt.py) compares every " +
                 "capture with its baseline and writes each branch's Docs/perf/status.json, shown here.", FrogletEditorPalette.Muted);
            foreach (var b in r.Branches)
            {
                Section(b.Branch);
                Line(b.Role, FrogletEditorPalette.Muted);
                if (!b.Found) { Note(b.Error, FrogletEditorPalette.Muted); continue; }
                Line($"Status written {b.GeneratedUtc}" + (b.LastChange.Length > 0 ? $"  ·  last change {b.LastChange}" : ""));
                List("Regressions", b.Regressions, FrogletEditorPalette.Warn, 8);
                List("Amoebius (allocations, no Unity)", b.Amoebius, null, 4);
                if (b.Captures.Count > 0)
                {
                    GUILayout.Label("Latest captures", FrogletEditorPalette.SectionLabel);
                    foreach (var c in b.Captures)
                        Line($"{c.When}  {c.User}  {c.Key}  ·  CPU {c.CpuMs:0.0} ms  ·  GC {c.GcKB:0} KB  ·  {Trim(c.Verdict, 60)}",
                            c.Valid ? (Color?)null : FrogletEditorPalette.Muted);
                }
                if (b.OwedScenarios.Count > 0) Line("Captures still owed: " + string.Join(", ", b.OwedScenarios), FrogletEditorPalette.Info);
                List("Next steps", b.Next, null, 6);
                List("Open performance tasks", b.Tasks, null, 8);
            }
            Section("Your performance files");
            Files(r, "perf");
        }

        void DrawKind(ProjectRecord r, string kind, string title, string what)
        {
            Note(what, FrogletEditorPalette.Muted);
            Section($"Your {title.ToLowerInvariant()} on {r.SendBranch}");
            Files(r, kind);
        }

        void DrawErrors(ProjectRecord r)
        {
            Note("Every red Console error of a play test is filed on this machine and sent once. Claude fixes it on your " +
                 "bugledger branch; after you pull the fix, your next clean plays close it (VALIDATING, then fixed). " +
                 "An error that comes back reopens.", FrogletEditorPalette.Muted);
            Section("On this machine");
            BugLedger.CountsByState(out int open, out int validating, out int ignored);
            Line($"{open} open  ·  {validating} validating  ·  {ignored} ignored",
                open > 0 ? FrogletEditorPalette.Warn : FrogletEditorPalette.Ok);
            int shown = 0;
            foreach (var issue in BugLedger.Snapshot())
            {
                if (!issue.IsOpen || shown++ >= 10) continue;
                Line($"{issue.Id}  ×{issue.TimesSeen}  {Trim(issue.Title, 110)}");
            }
            foreach (var b in r.Branches)
            {
                if (!b.Found) continue;
                Section($"Team, as {b.Branch} reports it");
                Line($"{b.BugsOpen} open  ·  {b.BugsValidating} validating");
                foreach (var t in b.BugTitles) Line(Trim(t, 130));
            }
            Section("Your error logs");
            Files(r, "redlog");
            Files(r, "crash");
        }

        // ── Small drawers ───────────────────────────────────────────────────────────────────────

        static string KindCount(ProjectRecord r, string kind, string noun)
        {
            int n = r.Files.TryGetValue(kind, out var f) ? f.Count : 0;
            return $"{n} {noun}{(n == 1 ? "" : "s")}";
        }

        static void Files(ProjectRecord r, string kind)
        {
            if (!r.Files.TryGetValue(kind, out var f) || f.Count == 0)
            {
                Line($"No {kind} files sent yet.", FrogletEditorPalette.Muted);
                return;
            }
            Line($"{f.Count} sent; newest:");
            foreach (var name in f.Newest) Line("   " + name);
        }

        static void Section(string title)
        {
            GUILayout.Space(8f);
            GUILayout.Label(title, FrogletEditorPalette.SectionHeader);
        }

        static void Header(string title, string sub)
        {
            GUILayout.Space(4f);
            GUILayout.Label(title, FrogletEditorPalette.CardTitle);
            if (!string.IsNullOrEmpty(sub)) Line(sub, FrogletEditorPalette.Muted);
        }

        static void List(string title, List<string> items, Color? color, int max)
        {
            if (items.Count == 0) return;
            GUILayout.Label(title, FrogletEditorPalette.SectionLabel);
            for (int i = 0; i < items.Count && i < max; i++) Line("• " + items[i], color);
            if (items.Count > max) Line($"  … and {items.Count - max} more", FrogletEditorPalette.Muted);
        }

        static void Line(string text, Color? color = null)
        {
            var style = FrogletEditorPalette.CardBodyWrapped;
            if (color.HasValue)
                style = new GUIStyle(style) { normal = { textColor = FrogletEditorPalette.Adapt(color.Value) } };
            GUILayout.Label(text, style);
        }

        static void Note(string text, Color color)
        {
            var style = new GUIStyle(FrogletEditorPalette.CardBodyWrapped)
                { normal = { textColor = FrogletEditorPalette.Adapt(color) }, fontStyle = FontStyle.Italic };
            GUILayout.Label(text, style);
        }

        static string Trim(string s, int max) => string.IsNullOrEmpty(s) || s.Length <= max ? s ?? "" : s.Substring(0, max - 1) + "…";
    }
}
