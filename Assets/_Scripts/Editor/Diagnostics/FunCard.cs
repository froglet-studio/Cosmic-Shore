using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The one-minute fun card (Garrett, 2026-11-12; Docs/ElementalLoop/PLAYBOOK.md step 1 and 2, section 7 item 1).
    ///
    /// <para>When Play stops after an ARCADE match (a card on the ArcadeGames list; arena games such as Regatta are
    /// not asked), a small window names the vessel, the game and the rung the person just flew and asks three
    /// things: was it fun (yes / almost / no), when it was not, what held it back (one of the six fun dimensions of
    /// Docs/AI_SYSTEM/TIME_GAME_FUN_SCORES.md; Ladder is the rung answer), and was the rung right for its goal (I1
    /// intro, I2 fun, I3 challenging, I4 very hard: too easy / right / too hard). An optional line rides along.</para>
    ///
    /// <para>The answer is written as <c>FunCards/funcard_&lt;Mode&gt;_&lt;Vessel&gt;_&lt;time&gt;.json</c> beside the
    /// flight records and sent to the person's <c>bugledger/&lt;user&gt;</c> branch with the Bug Hunt send
    /// (<see cref="BugHuntReports"/> kind <c>fun</c>), which never touches the working tree or branch.
    /// <c>Tools/Build/fun_cards.py apply</c> turns the answers into the loop's sign-offs: a "fun: yes" signs off step 1
    /// for the vessel and the game's element, and four rungs each "fun: yes" and "right" sign off step 2. The newest
    /// answer per vessel, game and rung wins; a card never takes a sign-off back.</para>
    ///
    /// <para>Editor-only diagnostics UI (not player-facing text). Never opens in batch mode. A reader of the flight
    /// record and a writer of its own answer file only.</para>
    /// </summary>
    [InitializeOnLoad]
    public sealed class FunCard : EditorWindow
    {
        public const string Schema = "cosmic-shore.funcard/1";
        public const string FolderName = "FunCards";
        const string ArcadeListPath = "Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset";
        const string AskedKey = "CosmicShore.FunCard.Asked";
        const string OffKey = "CosmicShore.FunCard.Off";
        const int RecentMinutes = 30;

        static readonly string[] RungGoal = { "", "intro", "fun", "challenging", "very hard" };
        static readonly (string key, string label, string tip)[] Weakest =
        {
            ("Verb", "Verb", "The vessel's own move is not the way to win"),
            ("Flow", "Flow", "The next target was unclear, or there was dead air"),
            ("Feedback", "Feedback", "Nothing rewarded me moment to moment"),
            ("Rivals", "Rivals", "The AI rivals did not race or finish"),
            ("Fairness", "Fairness", "Soft-locked, unfair, or I could not finish"),
        };

        [SerializeField] string flightFile = "";
        [SerializeField] string mode = "";
        [SerializeField] string hull = "";
        [SerializeField] int intensity;
        [SerializeField] bool finished;
        [SerializeField] float raceSeconds;
        [SerializeField] string fun = "";
        [SerializeField] string rung = "";
        [SerializeField] string weakest = "";
        [SerializeField] string note = "";

        /// <summary>Folder the answers are written to, beside the flight records.</summary>
        public static string Folder => Path.Combine(DiagnosticsHUD.OutputDirectory, FolderName);

        static FunCard()
        {
            if (Application.isBatchMode) return;
            EditorApplication.playModeStateChanged += change =>
            {
                // After the Bug Hunt send's own after-play pass, so the confirmation and the card do not race.
                if (change == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += () => EditorApplication.delayCall += AskAfterPlay;
            };
        }

        [MenuItem("FrogletTools/Diagnostics/Fun Card (last match)", false, 130)]
        [FrogletTool(FrogletToolCategory.Diagnostics, Importance = 4,
            Description = "Answer the one-minute fun card for the last arcade match you flew: the elemental loop's step 1 and 2 sign-off.",
            DocPath = "Docs/ElementalLoop/PLAYBOOK.md")]
        public static void OpenForLastMatch()
        {
            var flight = NewestArcadeFlight(ignoreAsked: true, maxAgeMinutes: 24 * 60);
            if (flight == null)
            {
                EditorUtility.DisplayDialog("Fun Card", "No arcade match flown in the last day to answer for.", "OK");
                return;
            }
            Open(flight.Value);
        }

        [MenuItem("FrogletTools/Diagnostics/Fun Card: ask after every match", true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked("FrogletTools/Diagnostics/Fun Card: ask after every match", !EditorPrefs.GetBool(OffKey, false));
            return true;
        }

        [MenuItem("FrogletTools/Diagnostics/Fun Card: ask after every match", false, 131)]
        static void Toggle() => EditorPrefs.SetBool(OffKey, !EditorPrefs.GetBool(OffKey, false));

        // ── Finding the match ───────────────────────────────────────────────────────────────────

        struct Flight
        {
            public string File, Mode, Hull;
            public int Intensity;
            public bool Finished;
            public float Seconds;
        }

        static void AskAfterPlay()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.GetBool(OffKey, false)) return;
                var flight = NewestArcadeFlight(ignoreAsked: false, maxAgeMinutes: RecentMinutes);
                if (flight != null) Open(flight.Value);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FunCard] After-play card failed: {e.GetType().Name}: {e.Message}");
            }
        }

        static Flight? NewestArcadeFlight(bool ignoreAsked, int maxAgeMinutes)
        {
            string dir = HumanFlightRecorder.Folder;
            if (!Directory.Exists(dir)) return null;
            var cutoff = DateTime.UtcNow.AddMinutes(-maxAgeMinutes);
            var asked = new HashSet<string>(EditorPrefs.GetString(AskedKey, "").Split('\n'), StringComparer.Ordinal);
            var arcade = ArcadeModes();
            FileInfo newest = null;
            foreach (var path in Directory.GetFiles(dir, "flight_*.json"))
            {
                var info = new FileInfo(path);
                if (info.LastWriteTimeUtc < cutoff) continue;
                if (!ignoreAsked && asked.Contains(info.Name)) continue;
                if (newest == null || info.LastWriteTimeUtc > newest.LastWriteTimeUtc) newest = info;
            }
            if (newest == null) return null;
            if (!ignoreAsked) Remember(newest.Name);   // asked once per match, answered or not
            var f = ReadHeader(newest.FullName);
            if (f == null || string.IsNullOrEmpty(f.Value.Hull) || f.Value.Hull == "Unknown") return null;
            return arcade.Contains(f.Value.Mode) ? f : null;
        }

        static HashSet<string> ArcadeModes()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            var list = AssetDatabase.LoadAssetAtPath<SO_GameList>(ArcadeListPath);
            if (list && list.Games != null)
                foreach (var g in list.Games)
                    if (g) set.Add(g.Mode.ToString());
            return set;
        }

        /// <summary>The flight record's header (the first few KB: mode, hull, intensity, finished, raceSeconds).</summary>
        static Flight? ReadHeader(string path)
        {
            var buffer = new char[8192];
            int read;
            using (var reader = new StreamReader(path)) read = reader.Read(buffer, 0, buffer.Length);
            string head = new string(buffer, 0, read);
            string S(string key) { var m = Regex.Match(head, "\"" + key + "\":\"([^\"]*)\""); return m.Success ? m.Groups[1].Value : ""; }
            string N(string key) { var m = Regex.Match(head, "\"" + key + "\":([-0-9.]+|true|false)"); return m.Success ? m.Groups[1].Value : ""; }
            int.TryParse(N("intensity"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int intensity);
            float.TryParse(N("raceSeconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds);
            if (intensity < 1 || intensity > 4) return null;
            return new Flight
            {
                File = Path.GetFileName(path), Mode = S("mode"), Hull = S("hull"), Intensity = intensity,
                Finished = N("finished") == "true", Seconds = seconds,
            };
        }

        static void Remember(string file)
        {
            var list = new List<string>(EditorPrefs.GetString(AskedKey, "").Split('\n', StringSplitOptions.RemoveEmptyEntries));
            list.Add(file);
            if (list.Count > 200) list.RemoveRange(0, list.Count - 200);
            EditorPrefs.SetString(AskedKey, string.Join("\n", list));
        }

        static void Open(Flight f)
        {
            var open = Resources.FindObjectsOfTypeAll<FunCard>();
            var w = open.Length > 0 ? open[0] : CreateInstance<FunCard>();
            w.titleContent = new GUIContent("Fun Card");
            w.flightFile = f.File;
            w.mode = f.Mode;
            w.hull = f.Hull;
            w.intensity = f.Intensity;
            w.finished = f.Finished;
            w.raceSeconds = f.Seconds;
            w.fun = w.rung = w.weakest = w.note = "";
            var main = EditorGUIUtility.GetMainWindowPosition();
            const float width = 460f, height = 300f;
            w.minSize = new Vector2(width, height);
            w.position = new Rect(main.center.x - width / 2f, main.y + 120f, width, height);
            w.ShowUtility();
            w.Focus();
        }

        // ── Drawing ─────────────────────────────────────────────────────────────────────────────

        void OnGUI()
        {
            string goal = intensity is >= 1 and <= 4 ? RungGoal[intensity] : "";
            string result = finished ? $"finished in {raceSeconds:0}s" : "did not finish";
            FrogletEditorPalette.Banner($"{hull} in {mode}, I{intensity} ({goal})", $"One minute: {result}. Your answer signs off the elemental loop.",
                FrogletEditorPalette.Lime);
            GUILayout.Space(8);

            Row("Was it fun?", new[] { ("yes", "Yes", FrogletEditorPalette.Jade), ("almost", "Almost", FrogletEditorPalette.Gold), ("no", "No", FrogletEditorPalette.Ruby) },
                ref fun);

            if (fun is "almost" or "no")
            {
                GUILayout.Space(6);
                GUILayout.Label("What held it back most?", FrogletEditorPalette.SectionLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    foreach (var (key, label, tip) in Weakest)
                        if (FrogletEditorPalette.ColorButton(label, FrogletEditorPalette.Azure, 82f, 24f, tip, true, weakest != key))
                            weakest = key;
                }
            }

            GUILayout.Space(6);
            Row($"Was I{intensity} right for \"{goal}\"?",
                new[] { ("easy", "Too easy", FrogletEditorPalette.Azure), ("right", "Right", FrogletEditorPalette.Jade), ("hard", "Too hard", FrogletEditorPalette.Coral) },
                ref rung);

            GUILayout.Space(6);
            GUILayout.Label("Anything else (optional)", FrogletEditorPalette.SectionLabel);
            note = EditorGUILayout.TextField(note);

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (FrogletEditorPalette.ColorButton("Skip", FrogletEditorPalette.Slate, 90f, 28f, "Close without answering", true, true))
                    Close();
                bool ready = fun != "" && rung != "";
                if (FrogletEditorPalette.ColorButton("Send", FrogletEditorPalette.Lime, 110f, 28f,
                        ready ? "Send to the elemental loop" : "Answer fun and the rung first", ready))
                {
                    Send();
                    Close();
                }
            }
            GUILayout.Space(8);
        }

        static void Row(string question, (string key, string label, Color color)[] options, ref string value)
        {
            GUILayout.Label(question, FrogletEditorPalette.SectionLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var (key, label, color) in options)
                    if (FrogletEditorPalette.ColorButton(label, color, 110f, 26f, null, true, value != key))
                        value = key;
            }
        }

        // ── Sending ─────────────────────────────────────────────────────────────────────────────

        void Send()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var now = DateTime.Now;
                string file = Path.Combine(Folder, $"funcard_{mode}_{hull}_{now:yyyyMMdd_HHmmss}.json");
                File.WriteAllText(file, ToJson(now));
                BugLedgerSender.SendInBackground();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FunCard] Could not save the answer: {e.GetType().Name}: {e.Message}");
            }
        }

        string ToJson(DateTime now)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            Pair(sb, "schema", Schema).Append(',');
            Pair(sb, "mode", mode).Append(',');
            Pair(sb, "hull", hull).Append(',');
            sb.Append("\"intensity\":").Append(intensity.ToString(CultureInfo.InvariantCulture)).Append(',');
            Pair(sb, "fun", fun).Append(',');
            Pair(sb, "rung", rung).Append(',');
            if (fun != "yes" && weakest != "") Pair(sb, "weakest", weakest).Append(',');
            if (!string.IsNullOrWhiteSpace(note)) Pair(sb, "note", note.Trim()).Append(',');
            Pair(sb, "flight", flightFile).Append(',');
            sb.Append("\"finished\":").Append(finished ? "true" : "false").Append(',');
            sb.Append("\"raceSeconds\":").Append(raceSeconds.ToString("0.##", CultureInfo.InvariantCulture)).Append(',');
            Pair(sb, "by", Environment.UserName).Append(',');
            Pair(sb, "date", now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.Append('}');
            return sb.ToString();
        }

        static StringBuilder Pair(StringBuilder sb, string key, string value)
        {
            string v = (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
            return sb.Append('"').Append(key).Append("\":\"").Append(v).Append('"');
        }
    }
}
