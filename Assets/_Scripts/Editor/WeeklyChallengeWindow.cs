using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// <b>FrogletTools &gt; Game Modes &gt; Weekly Challenge</b> - the one place the weekly
    /// challenge is authored: which modes are in the pool, what each one asks for, how much
    /// SMALLER the weekly run is than a real match of that mode, and the test shortcuts that make
    /// a weekly cycle testable in minutes.
    ///
    /// <para>It edits the single <see cref="WeeklyChallengeCatalogSO"/> at
    /// <c>Assets/Resources/WeeklyChallengeCatalog.asset</c> (created on first open).</para>
    ///
    /// <para><b>Layout is master/detail, not a field list.</b> Eleven entries x nine fields is a
    /// page and a half of scrolling to compare two numbers that are 400px apart; the list on the
    /// left is one row per entry carrying the three things you actually scan for - is it on, what
    /// does it ask, is it broken - and only the SELECTED entry spends vertical space on fields.
    /// Preview and Testing are tabs rather than more sections for the same reason: they are
    /// different questions, and stacking them into one scroll makes every question harder.</para>
    ///
    /// <para>What the window exists for beyond the inspector is the VALIDATION: three of the four
    /// ways to author an unplayable challenge are invisible in a plain field list, and every one
    /// of them has already been hit once (see Docs/WEEKLY_CHALLENGE.md §4).</para>
    /// </summary>
    public class WeeklyChallengeWindow : EditorWindow
    {
        const string AssetPath = "Assets/Resources/" + WeeklyChallengeCatalogSO.ResourcePath + ".asset";
        const string ToolName = "Weekly Challenge";

        const float ListWidth = 330f;
        const float RowHeight = 42f;
        const float Gap = 8f;

        enum Tab { Pool = 0, Preview = 1, Testing = 2 }

        /// <summary>
        /// Modes whose scoring metric is credited to a DOMAIN'S REPRESENTATIVE rather than to the
        /// player who earned it, so a personal objective on them measures the wrong thing. A
        /// curated fact, not a guess: Nucleus Rush's controller writes GoalsScored onto whichever
        /// player it picks per domain. Anything added here must be verified the same way.
        /// </summary>
        static readonly GameModes[] NotCreditedPerPlayer = { GameModes.BroodRush };

        WeeklyChallengeCatalogSO _catalog;
        EndConditionOverridesSO _endConditions;
        FrogletToolShipContext _ship;
        List<SO_ArcadeGame> _cards;

        Tab _tab = Tab.Pool;
        int _selected;

        /// <summary>
        /// A structural edit (add / remove / reorder / duplicate) queued to run AFTER this OnGUI
        /// finishes. Mutating the pool mid-pass changes the control count between the Layout and
        /// Repaint events, which IMGUI reports as "changed between layout and repaint" and draws
        /// through as a flickering, half-built pane. Queue it, run it once, repaint.
        /// </summary>
        Action _deferred;
        Vector2 _listScroll, _detailScroll, _previewScroll, _testingScroll;

        [MenuItem("FrogletTools/Game Modes/Weekly Challenge")]
        [FrogletTool(FrogletToolCategory.GameModes, Importance = 4,
            Description = "Author the weekly challenge pool, targets, and the test shortcuts.")]
        static void Open()
        {
            var w = GetWindow<WeeklyChallengeWindow>("Weekly Challenge");
            w.minSize = new Vector2(860f, 520f);
            w.Show();
        }

        void OnEnable()
        {
            _catalog = LoadOrCreate();
            _endConditions = Resources.Load<EndConditionOverridesSO>(EndConditionOverridesSO.ResourcePath);
            RefreshCards();

            _ship = new FrogletToolShipContext(ToolName)
            {
                CommitType = "chore",
                CommitScope = "weekly-challenge",
                CommitSubject = n => $"chore(weekly-challenge): catalog — {n} file(s)",
                Validate = Validate,
            };
        }

        static WeeklyChallengeCatalogSO LoadOrCreate()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<WeeklyChallengeCatalogSO>(AssetPath);
            if (cfg != null) return cfg;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            cfg = CreateInstance<WeeklyChallengeCatalogSO>();
            AssetDatabase.CreateAsset(cfg, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[WeeklyChallenge] Created catalog at {AssetPath}");
            return cfg;
        }

        void RefreshCards()
        {
            _cards = AssetDatabase.FindAssets("t:SO_ArcadeGame")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>)
                .Where(c => c != null)
                .ToList();
        }

        SO_ArcadeGame CardFor(GameModes mode) => _cards?.FirstOrDefault(c => c.Mode == mode);

        string ModeName(GameModes mode)
        {
            var card = CardFor(mode);
            return card != null && !string.IsNullOrWhiteSpace(card.DisplayName)
                ? card.DisplayName
                : mode.ToString();
        }

        List<WeeklyChallengeCatalogSO.Entry> Pool =>
            _catalog.Pool ??= new List<WeeklyChallengeCatalogSO.Entry>();

        // ── Window ─────────────────────────────────────────────────────────────

        void OnGUI()
        {
            if (_catalog == null)
            {
                EditorGUILayout.HelpBox("Catalog asset not found.", MessageType.Warning);
                if (GUILayout.Button("Create WeeklyChallengeCatalog asset")) _catalog = LoadOrCreate();
                return;
            }

            FrogletEditorPalette.Banner(
                "Weekly Challenge",
                "One curated objective per UTC week — the same one for every player, and a shorter " +
                "run than the real mode.",
                FrogletEditorPalette.ColorFor(FrogletToolCategory.GameModes));

            DrawToolbar();

            // The body is built out of LAYOUT GROUPS, never out of a rect carved with
            // GUILayoutUtility.GetRect(..., ExpandHeight) and handed to GUILayout.BeginArea.
            // That combination looks right and renders nothing: an expanding GetRect returns its
            // MINIMUM (0x0) during the Layout event and the resolved rect only on Repaint, so
            // every layout control inside the area lays itself out against a zero-width viewport
            // and draws nothing - while non-layout GUI.* calls in the same area, which only need
            // the rect on Repaint, keep working. That asymmetry is what makes it look like "the
            // right panel is broken" rather than "the container is wrong".
            switch (_tab)
            {
                case Tab.Pool: DrawPoolTab(); break;
                case Tab.Preview: DrawScrollTab(ref _previewScroll, DrawPreviewBody); break;
                case Tab.Testing: DrawScrollTab(ref _testingScroll, DrawTestingBody); break;
            }

            DrawFooter();
            FrogletToolShipPanel.Draw(_ship, this);

            if (_deferred != null)
            {
                var run = _deferred;
                _deferred = null;
                run();
                Repaint();
            }
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                DrawTabButton(Tab.Pool, $"Pool ({Pool.Count})");
                DrawTabButton(Tab.Preview, "Preview");
                DrawTabButton(Tab.Testing, "Testing");

                if (_catalog.TestActive)
                {
                    GUILayout.Space(8);
                    var pill = GUILayoutUtility.GetRect(84, 15, GUILayout.Width(84));
                    pill.y += 2;
                    FrogletEditorPalette.StatusPill(pill, "TEST MODE ON", FrogletEditorPalette.Warn);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Ping asset", EditorStyles.toolbarButton, GUILayout.Width(74)))
                    EditorGUIUtility.PingObject(_catalog);

                if (GUILayout.Button("Rescan cards", EditorStyles.toolbarButton, GUILayout.Width(90)))
                {
                    RefreshCards();
                    Repaint();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawTabButton(Tab tab, string label)
        {
            bool on = _tab == tab;
            if (GUILayout.Toggle(on, label, EditorStyles.toolbarButton, GUILayout.Width(110)) != on)
                _tab = tab;
        }

        void DrawFooter()
        {
            int enabled = Pool.Count(e => e != null && e.Enabled);
            int broken = Pool.Count(e => e != null && e.Enabled && ProblemsFor(e).Any(p => p.IsError));

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                GUILayout.Label(
                    broken == 0
                        ? $"{enabled} playable / {Pool.Count} entries"
                        : $"{enabled} enabled / {Pool.Count} entries — {broken} with errors",
                    EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    _catalog.attemptsPerPeriod == 1
                        ? "one attempt a day, spent at launch"
                        : $"{_catalog.attemptsPerPeriod} attempts a day",
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// A tab whose whole content is one scrolling column. A layout scroll view expands to the
        /// space left between the toolbar above and the footer below on its own, in BOTH passes -
        /// which is the whole reason there is no explicit rect here. Each tab keeps its OWN scroll
        /// position; sharing one makes switching tabs land you somewhere arbitrary.
        /// </summary>
        static void DrawScrollTab(ref Vector2 scroll, Action draw)
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(6);
            draw();
            GUILayout.Space(8);
            EditorGUILayout.EndScrollView();
        }

        // ── Pool tab: list + detail ────────────────────────────────────────────

        void DrawPoolTab()
        {
            EditorGUILayout.BeginHorizontal();
            {
                DrawList();
                GUILayout.Space(Gap);
                DrawDetail();
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawList()
        {
            // A fixed-width vertical group: the scroll view inside takes the remaining height by
            // itself, and the Add bar under it stays pinned to the bottom of the pane.
            EditorGUILayout.BeginVertical(GUILayout.Width(ListWidth));
            {
                // helpBox as the PANE background rather than as a scroll-view style: the
                // (Vector2, GUIStyle) scroll-view overload is GUILayout's, not
                // EditorGUILayout's, and picking the wrong one here is a compile error that
                // reads like a missing using.
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
                {
                    for (int i = 0; i < Pool.Count; i++)
                    {
                        // A FIXED-height GetRect resolves identically in the Layout and Repaint
                        // passes, so the non-layout drawing inside DrawListRow is safe. This is the
                        // same shape FrogletMasterToolWindow uses for its cards.
                        var row = GUILayoutUtility.GetRect(0, RowHeight, GUILayout.ExpandWidth(true));
                        DrawListRow(row, i);
                    }

                    if (Pool.Count == 0)
                        EditorGUILayout.LabelField("No entries yet.", FrogletEditorPalette.CardBody);

                    GUILayout.FlexibleSpace();
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();

                if (FrogletEditorPalette.ColorButton(
                        "+  Add mode", FrogletEditorPalette.Ok, ListWidth - 8f, 22f,
                        "Append a new entry. Appending is safe; INSERTING or reordering re-rolls " +
                        "which date draws which mode."))
                    _deferred = () =>
                    {
                        Persist("Add weekly challenge entry",
                                () => Pool.Add(new WeeklyChallengeCatalogSO.Entry()));
                        _selected = Pool.Count - 1;
                    };
            }
            EditorGUILayout.EndVertical();
        }

        void DrawListRow(Rect row, int index)
        {
            var entry = Pool[index];
            if (entry == null) return;

            bool selected = index == _selected;
            var problems = ProblemsFor(entry).ToList();
            bool hasError = problems.Any(p => p.IsError);
            var accent = !entry.Enabled ? FrogletEditorPalette.Muted
                       : hasError ? FrogletEditorPalette.Error
                       : problems.Count > 0 ? FrogletEditorPalette.Warn
                       : FrogletEditorPalette.Ok;

            if (selected)
                FrogletEditorPalette.DrawCard(row, accent.WithAlpha(0.16f), accent.WithAlpha(0.65f));
            else if (index % 2 == 1)
                FrogletEditorPalette.DrawRect(row, FrogletEditorPalette.SurfaceRaised.WithAlpha(0.35f));

            FrogletEditorPalette.DrawAccentStripe(row, accent);

            float x = row.x + 8f;

            // The toggle goes FIRST. IMGUI hands an event to controls in declaration order, so a
            // full-row select button declared before it would swallow every click on the checkbox.
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUI.Toggle(new Rect(x, row.y + 12f, 16f, 16f), entry.Enabled);
            if (EditorGUI.EndChangeCheck())
                Persist("Toggle weekly challenge entry", () => entry.Enabled = enabled);

            x += 22f;

            // ...and the select button covers only what is left, so the pill's area stays inert
            // rather than being a second, invisible way to change the selection.
            var selectRect = new Rect(x, row.y, Mathf.Max(0f, row.width - (x - row.x) - 46f), row.height);
            if (GUI.Button(selectRect, GUIContent.none, GUIStyle.none))
            {
                _selected = index;
                GUI.FocusControl(null);
            }

            var titleRect = new Rect(x, row.y + 3f, selectRect.width, 16f);
            var bodyRect = new Rect(x, row.y + 20f, selectRect.width, 14f);

            using (new EditorGUI.DisabledScope(!entry.Enabled))
            {
                GUI.Label(titleRect, ModeName(entry.Mode), FrogletEditorPalette.CardTitle);
                GUI.Label(bodyRect, WeeklyChallengeCatalogSO.BuildObjectiveText(entry),
                          FrogletEditorPalette.CardBody);
            }

            var pillRect = new Rect(row.xMax - 42f, row.y + 13f, 34f, 15f);
            if (hasError)
                FrogletEditorPalette.StatusPill(pillRect, problems.Count(p => p.IsError) + " x",
                                                FrogletEditorPalette.Error);
            else if (problems.Count > 0)
                FrogletEditorPalette.StatusPill(pillRect, problems.Count + " !", FrogletEditorPalette.Warn);
            else if (entry.Enabled)
                FrogletEditorPalette.StatusPill(pillRect, "OK", FrogletEditorPalette.Ok);
            else
                FrogletEditorPalette.StatusPill(pillRect, "OFF", FrogletEditorPalette.Muted);
        }

        void DrawDetail()
        {
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
                {
                    GUILayout.Space(4);

                    if (Pool.Count == 0)
                    {
                        EditorGUILayout.HelpBox(
                            "The pool is empty, so the card shows UNAVAILABLE. Add a mode on the left.",
                            MessageType.Warning);
                    }
                    else
                    {
                        _selected = Mathf.Clamp(_selected, 0, Pool.Count - 1);
                        DrawEntryDetail(Pool[_selected], _selected);
                    }

                    GUILayout.Space(8);
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical();
        }

        void DrawEntryDetail(WeeklyChallengeCatalogSO.Entry entry, int index)
        {
            if (entry == null) return;

            float prevLabel = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 132f;

            // ── Header: name + the row-ordering and lifecycle controls ──
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(ModeName(entry.Mode), FrogletEditorPalette.Title, GUILayout.Height(22));
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(index <= 0))
                if (GUILayout.Button("Up", EditorStyles.miniButtonLeft, GUILayout.Width(30)))
                    _deferred = () => Move(index, index - 1);

            using (new EditorGUI.DisabledScope(index >= Pool.Count - 1))
                if (GUILayout.Button("Down", EditorStyles.miniButtonMid, GUILayout.Width(42)))
                    _deferred = () => Move(index, index + 1);

            if (GUILayout.Button("Duplicate", EditorStyles.miniButtonMid, GUILayout.Width(70)))
                _deferred = () => Duplicate(index);

            if (GUILayout.Button("Remove", EditorStyles.miniButtonRight, GUILayout.Width(60)))
                _deferred = () => Remove(index);

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(2);
            GUILayout.Label($"“{WeeklyChallengeCatalogSO.BuildObjectiveText(entry)}”",
                            FrogletEditorPalette.Subtitle);
            FrogletEditorPalette.HorizontalRule(4f);

            EditorGUI.BeginChangeCheck();

            // ── Mode ──
            GUILayout.Label("Mode", FrogletEditorPalette.SectionLabel);
            var mode = (GameModes)EditorGUILayout.EnumPopup("Game mode", entry.Mode);
            var metric = (ScoringMetric)EditorGUILayout.EnumPopup(
                new GUIContent("Scoring metric",
                    "The per-player stat counted. Normally the mode's own - a challenge counting " +
                    "something the mode does not surface leaves the player with no readout."),
                entry.Metric);
            int intensity = EditorGUILayout.IntSlider("Intensity", entry.Intensity, 1, 4);
            var domain = (Domains)EditorGUILayout.EnumPopup(
                new GUIContent("Domain",
                    "The colour the player flies. Pinned like the intensity - the run seats the " +
                    "card's minimum, so this is not a team decision anyone else is party to. Jade " +
                    "is the default and the one value that needs no server request, because the " +
                    "menu already resets every player to it."),
                entry.Domain);

            GUILayout.Space(6);

            // ── Objective (the two numbers that must be read together) ──
            GUILayout.Label("Objective", FrogletEditorPalette.SectionLabel);
            int target = Mathf.Max(1, EditorGUILayout.IntField(
                new GUIContent("Player must reach", "The LOCAL player's own count, never a domain sum."),
                entry.Target));
            float timeLimit = Mathf.Max(0f, EditorGUILayout.FloatField(
                new GUIContent("Time limit (s)", "0 = no limit."), entry.TimeLimitSeconds));

            GUILayout.Space(6);

            // ── Copy ──
            GUILayout.Label("Player-facing copy", FrogletEditorPalette.SectionLabel);
            EditorGUILayout.BeginHorizontal();
            string verb = EditorGUILayout.TextField("Verb", entry.Verb);
            EditorGUIUtility.labelWidth = 44f;
            string noun = EditorGUILayout.TextField("Noun", entry.Noun);
            EditorGUIUtility.labelWidth = 132f;
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
                Persist("Edit weekly challenge entry", () =>
                {
                    entry.Mode = mode;
                    entry.Metric = metric;
                    entry.Intensity = intensity;
                    entry.Domain = domain;
                    entry.Target = target;
                    entry.TimeLimitSeconds = timeLimit;
                    entry.Verb = verb;
                    entry.Noun = noun;
                });

            GUILayout.Space(8);
            EditorGUILayout.LabelField(
                "Pinned",
                $"intensity {entry.Intensity} · {WeeklyChallengeCatalogSO.ResolvePlayableDomain(entry.Domain)} · " +
                "seats the card's minimum · no AI",
                FrogletEditorPalette.CardBody);
            DrawSizeComparison(entry);

            var problems = ProblemsFor(entry).ToList();
            if (problems.Count > 0)
            {
                GUILayout.Space(6);
                GUILayout.Label("Problems", FrogletEditorPalette.SectionLabel);
                foreach (var p in problems)
                    EditorGUILayout.HelpBox(p.Message, p.IsError ? MessageType.Error : MessageType.Warning);
            }

            EditorGUIUtility.labelWidth = prevLabel;
        }

        /// <summary>
        /// "A normal match of this mode races to 20." The objective has to be reachable INSIDE an
        /// ordinary match, so the number the author is really checking against is the mode's own —
        /// which is exactly the number nobody remembers, so it gets its own line.
        /// </summary>
        void DrawSizeComparison(WeeklyChallengeCatalogSO.Entry entry)
        {
            if (_endConditions != null &&
                _endConditions.TryGetAuthoredTurnTarget(entry.Mode, out int normal))
            {
                EditorGUILayout.LabelField(
                    "Mode races to", $"{normal} (the objective must be reachable before that)",
                    FrogletEditorPalette.CardBody);
            }
            else
            {
                EditorGUILayout.LabelField(
                    "Mode races to", "auto-calculated from the mode's own track",
                    FrogletEditorPalette.CardBody);
            }
        }

        void Remove(int index)
        {
            if (index < 0 || index >= Pool.Count) return;
            Persist("Remove weekly challenge entry", () => Pool.RemoveAt(index));
            _selected = Mathf.Clamp(index - 1, 0, Mathf.Max(0, Pool.Count - 1));
        }

        void Move(int from, int to)
        {
            Persist("Reorder weekly challenge pool", () =>
            {
                var e = Pool[from];
                Pool.RemoveAt(from);
                Pool.Insert(to, e);
            });
            _selected = to;
        }

        void Duplicate(int index)
        {
            var src = Pool[index];
            var copy = new WeeklyChallengeCatalogSO.Entry
            {
                Enabled = src.Enabled,
                Mode = src.Mode,
                Metric = src.Metric,
                Target = src.Target,
                TimeLimitSeconds = src.TimeLimitSeconds,
                Intensity = src.Intensity,
                Verb = src.Verb,
                Noun = src.Noun,
            };
            Persist("Duplicate weekly challenge entry", () => Pool.Insert(index + 1, copy));
            _selected = index + 1;
        }

        // ── Validation ─────────────────────────────────────────────────────────

        readonly struct Problem
        {
            public readonly string Message;
            public readonly bool IsError;
            public Problem(string message, bool isError) { Message = message; IsError = isError; }
            public static Problem Error(string m) => new(m, true);
            public static Problem Warn(string m) => new(m, false);
        }

        /// <summary>
        /// Everything that makes an entry unplayable or misleading. Each of these was a real trap
        /// before it was a check - see Docs/WEEKLY_CHALLENGE.md §4.
        /// </summary>
        IEnumerable<Problem> ProblemsFor(WeeklyChallengeCatalogSO.Entry entry)
        {
            if (entry == null) yield break;

            var card = CardFor(entry.Mode);

            if (card == null)
            {
                yield return Problem.Error(
                    $"{entry.Mode} has no SO_ArcadeGame. The card would resolve a challenge that " +
                    "nothing can launch — the tile does nothing on whichever date draws it.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(card.SceneName))
                yield return Problem.Error(
                    $"{card.DisplayName} names no scene. Launching it would fail.");

            if (entry.Intensity < card.MinIntensity || entry.Intensity > card.MaxIntensity)
                yield return Problem.Error(
                    $"Intensity {entry.Intensity} is outside {card.DisplayName}'s range " +
                    $"({card.MinIntensity}–{card.MaxIntensity}). It is silently clamped at launch, " +
                    "so the challenge quietly becomes a different one.");

            // THE trap, and it survives the move to the mode's own end conditions: the turn ends
            // when the mode's RACE target is met, and that ends the challenge with it. An objective
            // above what a match of this mode can produce is unreachable by construction.
            if (_endConditions != null &&
                _endConditions.TryGetAuthoredTurnTarget(entry.Mode, out int normal))
            {
                if (entry.Target > normal)
                    yield return Problem.Error(
                        $"Objective ({entry.Target}) is above what {card.DisplayName} races to " +
                        $"({normal}). The turn ends first, so the objective can never be met.");
                else if (entry.Target == normal)
                    yield return Problem.Warn(
                        $"The objective equals {card.DisplayName}'s race target ({normal}). That " +
                        "target is a DOMAIN SUM while the objective is PERSONAL, so a teammate " +
                        "can end the run on somebody else's score before the player finishes.");
            }

            if (Array.IndexOf(NotCreditedPerPlayer, entry.Mode) >= 0)
                yield return Problem.Error(
                    $"{card.DisplayName} credits its metric to a DOMAIN'S REPRESENTATIVE, not to " +
                    "the player who earned it, so a personal objective here measures the wrong " +
                    "thing. Remove it from the pool.");

            if (WeeklyChallengeCatalogSO.ResolvePlayableDomain(entry.Domain) != entry.Domain)
                yield return Problem.Error(
                    $"{entry.Domain} is not a colour a player flies (Blue is the \"no team\" " +
                    "sentinel). The run falls back to Jade.");

            if (entry.TimeLimitSeconds > 0f && entry.TimeLimitSeconds < 15f)
                yield return Problem.Warn(
                    "Under 15 seconds leaves no room for the countdown and spawn-in — the run is " +
                    "over before the player has control.");

            int duplicates = Pool.Count(e => e != null && e.Enabled && e.Mode == entry.Mode);
            if (entry.Enabled && duplicates > 1)
                yield return Problem.Warn(
                    $"{card.DisplayName} appears {duplicates} times in the pool, so it comes up " +
                    "that much more often than the others.");
        }

        // ── Preview tab ────────────────────────────────────────────────────────

        void DrawPreviewBody()
        {
            GUILayout.Label("The next 14 days", FrogletEditorPalette.SectionHeader);
            EditorGUILayout.HelpBox(
                "ThisWeek's challenge is drawn from the pool by a hash of the UTC date — no server " +
                "call, and identical on every platform. The pool's ORDER is part of that draw, so " +
                "this is what a reorder actually changes.",
                MessageType.Info);

            if (_catalog.TestActive && _catalog.test.forcedPoolIndex >= 0)
                EditorGUILayout.HelpBox(
                    "Test mode is forcing one pool entry, so every period below draws the same " +
                    "one. Clear the forced index to preview the real rotation.",
                    MessageType.Warning);

            GUILayout.Space(4);

            var now = DateTime.UtcNow;
            for (int i = 0; i < 14; i++)
            {
                var day = now.AddDays(i);
                var challenge = _catalog.ForDate(day);

                var row = GUILayoutUtility.GetRect(0, 30f, GUILayout.ExpandWidth(true));
                if (i % 2 == 1)
                    FrogletEditorPalette.DrawRect(row, FrogletEditorPalette.SurfaceRaised.WithAlpha(0.3f));

                var accent = i == 0 ? FrogletEditorPalette.Ok : FrogletEditorPalette.Muted;
                FrogletEditorPalette.DrawAccentStripe(row, accent);

                GUI.Label(new Rect(row.x + 10f, row.y + 1f, 130f, 15f),
                          i == 0 ? "THIS WEEK" : day.ToString("dd MMM"),
                          FrogletEditorPalette.CardTitle);

                GUI.Label(new Rect(row.x + 10f, row.y + 15f, row.width - 20f, 14f),
                          challenge.IsValid
                              ? $"{ModeName(challenge.GameMode)} — {challenge.ObjectiveText} " +
                                $"· intensity {challenge.Intensity}"
                              : "no challenge (the pool has no enabled entries)",
                          FrogletEditorPalette.CardBody);
            }
        }

        // ── Testing tab ────────────────────────────────────────────────────────

        void DrawTestingBody()
        {
            float prevLabel = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 220f;

            GUILayout.Label("Cycle", FrogletEditorPalette.SectionHeader);

            EditorGUI.BeginChangeCheck();
            int attempts = Mathf.Max(0, EditorGUILayout.IntField(
                new GUIContent("Attempts per week",
                    "1 = the design: the challenge is played ONCE. The attempt is spent at " +
                    "LAUNCH, so quitting mid-run does not buy a retry. 0 = unlimited."),
                _catalog.attemptsPerPeriod));

            bool respect = EditorGUILayout.ToggleLeft(
                new GUIContent("Respect mode progression locks",
                    "OFF by design: the weekly challenge is an invitation into a mode the player " +
                    "may not have unlocked. Turning this on means two players on the same date " +
                    "can face DIFFERENT challenges, which is the one promise the design makes."),
                _catalog.respectModeProgression);

            if (EditorGUI.EndChangeCheck())
                Persist("Edit weekly challenge cycle", () =>
                {
                    _catalog.attemptsPerPeriod = attempts;
                    _catalog.respectModeProgression = respect;
                });

            if (attempts == 0)
                EditorGUILayout.HelpBox(
                    "0 attempts per week = unlimited replays. Fine for testing; it is not the " +
                    "shipped design.", MessageType.Warning);

            FrogletEditorPalette.HorizontalRule();

            GUILayout.Label("Leaderboard", FrogletEditorPalette.SectionHeader);

            EditorGUI.BeginChangeCheck();
            string boardId = EditorGUILayout.TextField(
                new GUIContent("UGS leaderboard ID",
                    "The ONE board the weekly challenge ranks on, created by hand in the UGS " +
                    "dashboard. Empty = ranking off; the panel shows nothing and no time is " +
                    "submitted. The id is IMMUTABLE in UGS once the board exists."),
                _catalog.leaderboardId ?? string.Empty).Trim();

            if (EditorGUI.EndChangeCheck())
                Persist("Edit weekly challenge leaderboard", () => _catalog.leaderboardId = boardId);

            EditorGUILayout.HelpBox(
                string.IsNullOrEmpty(boardId)
                    ? "No leaderboard ID: ranking is OFF. Completions are still recorded in Cloud " +
                      "Save; nothing is submitted and the panel stays empty."
                    : $"Create '{boardId}' in the UGS dashboard with Sort order ASCENDING (the " +
                      "score is a time), Update strategy KEEP BEST, and a WEEKLY reset on the UTC " +
                      "Monday boundary with ARCHIVING ON. None of those three can be enforced " +
                      "from code; the sort order is checked at runtime because it fails silently.",
                MessageType.Info);

            EditorGUILayout.Space(6);
            DrawRegionalBoards();

            FrogletEditorPalette.HorizontalRule();

            var test = _catalog.test ??= new WeeklyChallengeCatalogSO.TestSettings();

            GUILayout.Label("Test shortcuts", FrogletEditorPalette.SectionHeader);

            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.ToggleLeft(
                new GUIContent("Test mode",
                    "Everything below is ignored while this is off, and in any release build " +
                    "regardless. A non-development BUILD fails outright while it is on."),
                test.enabled);
            if (EditorGUI.EndChangeCheck())
                Persist("Toggle weekly challenge test mode", () => test.enabled = enabled);

            if (enabled)
                EditorGUILayout.HelpBox(
                    "TEST MODE IS ON. It cannot change a release player's behaviour (the runtime " +
                    "gate ignores it), and a non-development build will FAIL while it is on — " +
                    "which is the point: a flag left set must be loud, not silent.",
                    MessageType.Warning);

            using (new EditorGUI.DisabledScope(!enabled))
            {
                EditorGUI.BeginChangeCheck();

                int forced = EditorGUILayout.IntField(
                    new GUIContent("Force pool index (-1 = off)",
                        "Pin the draw to one entry instead of hashing the date. Indexes the pool " +
                        "list as you see it."),
                    test.forcedPoolIndex);

                float dayMinutes = Mathf.Max(0f, EditorGUILayout.FloatField(
                    new GUIContent("Period length in minutes (0 = a real week)",
                        "Shrinks the cycle so rollover is testable. The period key changes shape, " +
                        "so a test period is never confused with a real day — switching back wipes " +
                        "the stored progress."),
                    test.periodLengthMinutes));

                bool ignoreLimit = EditorGUILayout.ToggleLeft(
                    new GUIContent("Ignore the once-per-day limit",
                        "Replay the challenge while tuning it."),
                    test.ignoreAttemptLimit);

                float scale = Mathf.Max(0.01f, EditorGUILayout.FloatField(
                    new GUIContent("Time limit scale",
                        "Multiplies every entry's clock. 0.25 turns 60s into 15s."),
                    test.timeLimitScale));

                if (EditorGUI.EndChangeCheck())
                    Persist("Edit weekly challenge test settings", () =>
                    {
                        test.forcedPoolIndex = forced;
                        test.periodLengthMinutes = dayMinutes;
                        test.ignoreAttemptLimit = ignoreLimit;
                        test.timeLimitScale = scale;
                    });

                if (forced >= 0 && forced < Pool.Count)
                    EditorGUILayout.LabelField(" ", $"forced: {ModeName(Pool[forced].Mode)}",
                                               FrogletEditorPalette.CardBody);
                else if (forced >= 0)
                    EditorGUILayout.HelpBox(
                        $"Forced index {forced} is past the end of the pool — the draw falls back " +
                        "to hashing the date.", MessageType.Warning);
            }

            FrogletEditorPalette.HorizontalRule();

            GUILayout.Label("Reset", FrogletEditorPalette.SectionHeader);
            EditorGUILayout.HelpBox(
                "Clears this machine's cached progress so the challenge can be played again. In " +
                "PLAY MODE it also rewrites the live cloud record; outside play mode only the " +
                "local snapshot is cleared, and the cloud copy reloads on the next sign-in.",
                MessageType.None);

            EditorGUILayout.BeginHorizontal();
            if (FrogletEditorPalette.ColorButton("Reset this week's progress",
                    FrogletEditorPalette.Warn, 200f))
                ResetProgress();

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
                if (FrogletEditorPalette.ColorButton("Re-draw from catalog",
                        FrogletEditorPalette.Info, 180f,
                        tooltip: "Play mode only: re-resolves this week's challenge in the running " +
                                 "game so an edit here shows up without a domain reload."))
                    WeeklyChallengeService.Instance?.RefreshFromCatalog();
            EditorGUILayout.EndHorizontal();

            EditorGUIUtility.labelWidth = prevLabel;
        }

        void ResetProgress()
        {
            if (Application.isPlaying && WeeklyChallengeService.Instance != null)
            {
                WeeklyChallengeService.Instance.ResetPeriodForTesting();
                Debug.Log("[WeeklyChallenge] Live progress reset.");
                return;
            }

            LocalCloudDataCache.Clear(UGSKeys.WeeklyChallenge);
            Debug.Log("[WeeklyChallenge] Local snapshot cleared. Enter play mode to re-read " +
                      "(the cloud copy still holds the old progress until it is overwritten).");
        }

        // ── Ship ───────────────────────────────────────────────────────────────

        FrogletToolValidation Validate()
        {
            var problems = new List<string>();

            int enabled = Pool.Count(e => e != null && e.Enabled);
            if (enabled == 0)
                problems.Add("The pool has no enabled entries — the card would show UNAVAILABLE.");

            if (_catalog.test != null && _catalog.test.enabled)
                problems.Add("Test mode is still on. It cannot change a release player's " +
                             "behaviour, but a non-development build will fail while it is set.");

            foreach (var entry in Pool)
            {
                if (entry == null || !entry.Enabled) continue;
                foreach (var p in ProblemsFor(entry))
                    if (p.IsError) problems.Add($"{ModeName(entry.Mode)}: {p.Message}");
            }

            return problems.Count == 0
                ? FrogletToolValidation.Pass($"{enabled} playable entries.")
                : FrogletToolValidation.Fail("The catalog has problems.", problems);
        }

        void Persist(string undoLabel, Action mutate)
        {
            Undo.RecordObject(_catalog, undoLabel);
            mutate();
            EditorUtility.SetDirty(_catalog);
            AssetDatabase.SaveAssets();
            FrogletToolChangeLedger.Record(ToolName, AssetPath);
        }

        /// <summary>
        /// The per-region boards. <b>A region is its own board</b> — UGS has no region concept, so
        /// "regional" cannot be a filter over the world board; see
        /// <c>CosmicShore.Core.WeeklyChallengeRegion</c> for why filtering client-side produces an
        /// empty list for most regions.
        ///
        /// <para>The key is matched against the DEVICE'S two-letter ISO country (us, gb, sg), so a
        /// board covering several countries wants one row per country pointing at the same id.
        /// That is deliberate rather than a coarse continent enum: which countries share a board is
        /// a business decision, and burying it in code would mean a new region needs a build.</para>
        /// </summary>
        void DrawRegionalBoards()
        {
            EditorGUILayout.LabelField("Regional boards", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "OPTIONAL. Empty is the shipped default: the Regional tab reports that no board is " +
                "configured rather than showing the world board under a regional heading.\n\n" +
                "Each id is a SEPARATE leaderboard you create in the dashboard with the SAME " +
                "settings as the world board (ASCENDING, KEEP BEST, weekly reset + archiving). A " +
                "completion is submitted to the world board AND to the player's regional board.",
                MessageType.None);

            var boards = _catalog.regionalLeaderboards;
            int removeAt = -1;

            for (int i = 0; i < boards.Count; i++)
            {
                var board = boards[i];
                if (board == null) continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    string key = EditorGUILayout.TextField(board.regionKey ?? string.Empty,
                        GUILayout.Width(70f));
                    string id = EditorGUILayout.TextField(board.leaderboardId ?? string.Empty);

                    if (EditorGUI.EndChangeCheck())
                        Persist("Edit regional leaderboard", () =>
                        {
                            board.regionKey = key.Trim();
                            board.leaderboardId = id.Trim();
                        });

                    if (GUILayout.Button("−", GUILayout.Width(22f))) removeAt = i;
                }
            }

            if (removeAt >= 0)
                Persist("Remove regional leaderboard", () => boards.RemoveAt(removeAt));

            if (GUILayout.Button("Add region", GUILayout.Width(110f)))
                Persist("Add regional leaderboard",
                    () => boards.Add(new WeeklyChallengeCatalogSO.RegionalBoard()));

            // Two rows claiming one key is not an error - the first wins - but it IS the shape of a
            // typo, and a silently-ignored row reads as a board that does not work.
            for (int i = 0; i < boards.Count; i++)
            {
                if (boards[i] == null || string.IsNullOrWhiteSpace(boards[i].regionKey)) continue;
                for (int j = i + 1; j < boards.Count; j++)
                {
                    if (boards[j] == null) continue;
                    if (!string.Equals(boards[i].regionKey, boards[j].regionKey,
                            System.StringComparison.OrdinalIgnoreCase)) continue;

                    EditorGUILayout.HelpBox(
                        $"Region '{boards[i].regionKey}' is listed twice. The FIRST row wins and " +
                        "the other is ignored.", MessageType.Warning);
                    return;
                }
            }
        }

    }
}
