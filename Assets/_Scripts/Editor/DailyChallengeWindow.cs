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
    /// <b>FrogletTools &gt; Game Modes &gt; Daily Challenge</b> - the one place the daily
    /// challenge is authored: which modes are in the pool, what each one asks for, how much
    /// SMALLER the daily run is than a real match of that mode, and the test shortcuts that make
    /// a 24h cycle testable in minutes.
    ///
    /// <para>It edits the single <see cref="DailyChallengeCatalogSO"/> at
    /// <c>Assets/Resources/DailyChallengeCatalog.asset</c> (created on first open). The runtime
    /// draws today's challenge from it with a hash of the UTC date, so the pool's ORDER is part
    /// of the draw - the preview below shows exactly which date lands on which mode.</para>
    ///
    /// <para>What the window exists for beyond the inspector is the VALIDATION: three of the four
    /// ways to author an unplayable challenge are invisible in a plain field list, and every one
    /// of them has already been hit once (see Docs/DAILY_CHALLENGE.md §4).</para>
    /// </summary>
    public class DailyChallengeWindow : EditorWindow
    {
        const string AssetPath = "Assets/Resources/" + DailyChallengeCatalogSO.ResourcePath + ".asset";
        const string ToolName = "Daily Challenge";

        /// <summary>
        /// Modes whose scoring metric is credited to a DOMAIN'S REPRESENTATIVE rather than to the
        /// player who earned it, so a personal objective on them measures the wrong thing. A
        /// curated fact, not a guess: Nucleus Rush's controller writes GoalsScored onto whichever
        /// player it picks per domain. Anything added here must be verified the same way.
        /// </summary>
        static readonly GameModes[] NotCreditedPerPlayer = { GameModes.NucleusRush };

        DailyChallengeCatalogSO _catalog;
        Vector2 _scroll;
        FrogletToolShipContext _ship;
        List<SO_ArcadeGame> _cards;
        EndConditionOverridesSO _endConditions;

        [MenuItem("FrogletTools/Game Modes/Daily Challenge")]
        [FrogletTool(FrogletToolCategory.GameModes, Importance = 4,
            Description = "Author the daily challenge pool, targets, and the test shortcuts.")]
        static void Open()
        {
            var w = GetWindow<DailyChallengeWindow>("Daily Challenge");
            w.minSize = new Vector2(520, 520);
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
                CommitScope = "daily-challenge",
                CommitSubject = n => $"chore(daily-challenge): catalog — {n} file(s)",
                Validate = Validate,
            };
        }

        static DailyChallengeCatalogSO LoadOrCreate()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<DailyChallengeCatalogSO>(AssetPath);
            if (cfg != null) return cfg;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            cfg = CreateInstance<DailyChallengeCatalogSO>();
            AssetDatabase.CreateAsset(cfg, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DailyChallenge] Created catalog at {AssetPath}");
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

        // ── GUI ────────────────────────────────────────────────────────────────

        void OnGUI()
        {
            if (_catalog == null)
            {
                EditorGUILayout.HelpBox("Catalog asset not found.", MessageType.Warning);
                if (GUILayout.Button("Create DailyChallengeCatalog asset")) _catalog = LoadOrCreate();
                return;
            }

            FrogletEditorPalette.Banner(
                "Daily Challenge",
                "One curated objective per UTC day - the same one for every player.",
                FrogletEditorPalette.ColorFor(FrogletToolCategory.GameModes));

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawCycleSection();
            FrogletEditorPalette.HorizontalRule();
            DrawPoolSection();
            FrogletEditorPalette.HorizontalRule();
            DrawPreviewSection();
            FrogletEditorPalette.HorizontalRule();
            DrawTestSection();

            EditorGUILayout.Space();
            if (GUILayout.Button("Ping catalog asset")) EditorGUIUtility.PingObject(_catalog);

            FrogletToolShipPanel.Draw(_ship, this);

            EditorGUILayout.EndScrollView();
        }

        void DrawCycleSection()
        {
            EditorGUILayout.LabelField("Cycle", FrogletEditorPalette.SectionHeader);
            EditorGUILayout.HelpBox(
                "Today's challenge is drawn from the pool by a hash of the UTC date - no server " +
                "call, and identical on every platform. Cloud Save stores only the player's " +
                "progress against it.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            int attempts = Mathf.Max(0, EditorGUILayout.IntField(
                new GUIContent("Attempts per day",
                    "1 = the design: the challenge is played ONCE. The attempt is spent at " +
                    "LAUNCH, so quitting mid-run does not buy a retry. 0 = unlimited."),
                _catalog.attemptsPerDay));

            bool respect = EditorGUILayout.ToggleLeft(
                new GUIContent("Respect mode progression locks",
                    "OFF by design: the daily challenge is an invitation into a mode the player " +
                    "may not have unlocked. Turning this on means two players on the same date " +
                    "can face DIFFERENT challenges, which is the one promise the design makes."),
                _catalog.respectModeProgression);

            if (EditorGUI.EndChangeCheck())
                Persist("Edit daily challenge cycle", () =>
                {
                    _catalog.attemptsPerDay = attempts;
                    _catalog.respectModeProgression = respect;
                });

            if (attempts == 0)
                EditorGUILayout.HelpBox(
                    "0 attempts per day = unlimited replays. Fine for testing; it is not the " +
                    "shipped design.", MessageType.Warning);
        }

        void DrawPoolSection()
        {
            EditorGUILayout.LabelField($"Pool ({_catalog.Pool?.Count ?? 0} entries)",
                FrogletEditorPalette.SectionHeader);
            EditorGUILayout.HelpBox(
                "The pool's ORDER is part of the draw - reordering or parking an entry re-rolls " +
                "which date gets which mode. Append rather than insert when that matters.",
                MessageType.None);

            _catalog.Pool ??= new List<DailyChallengeCatalogSO.Entry>();

            int removeAt = -1;
            for (int i = 0; i < _catalog.Pool.Count; i++)
            {
                if (DrawEntry(i, _catalog.Pool[i])) removeAt = i;
            }

            if (removeAt >= 0)
                Persist("Remove daily challenge entry", () => _catalog.Pool.RemoveAt(removeAt));

            EditorGUILayout.Space();
            if (FrogletEditorPalette.ColorButton("+  Add mode to pool",
                    FrogletEditorPalette.Ok, 200f))
                Persist("Add daily challenge entry",
                    () => _catalog.Pool.Add(new DailyChallengeCatalogSO.Entry()));
        }

        /// <summary>Draws one pool entry. Returns true when its Remove button was pressed.</summary>
        bool DrawEntry(int index, DailyChallengeCatalogSO.Entry entry)
        {
            if (entry == null) return false;

            bool remove = false;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle(entry.Enabled, GUILayout.Width(18));
            var mode = (GameModes)EditorGUILayout.EnumPopup(entry.Mode);
            if (EditorGUI.EndChangeCheck())
                Persist("Edit daily challenge entry", () => { entry.Enabled = enabled; entry.Mode = mode; });

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Remove", GUILayout.Width(70))) remove = true;
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            var metric = (ScoringMetric)EditorGUILayout.EnumPopup(
                new GUIContent("Metric", "The per-player stat counted. Normally the mode's own."), entry.Metric);
            int target = Mathf.Max(1, EditorGUILayout.IntField(
                new GUIContent("Objective target", "What the LOCAL player must reach."), entry.Target));
            int endOverride = Mathf.Max(0, EditorGUILayout.IntField(
                new GUIContent("Race target (0 = objective)",
                    "The mode's own end condition for a DAILY run - this is what makes it " +
                    "smaller than a real match. 0 uses the objective target, so the objective " +
                    "and the run end together."), entry.EndConditionOverride));
            float timeLimit = Mathf.Max(0f, EditorGUILayout.FloatField(
                new GUIContent("Time limit (s)", "0 = no limit."), entry.TimeLimitSeconds));
            int intensity = EditorGUILayout.IntSlider("Intensity", entry.Intensity, 1, 4);

            EditorGUILayout.BeginHorizontal();
            string verb = EditorGUILayout.TextField("Verb", entry.Verb);
            string noun = EditorGUILayout.TextField("Noun", entry.Noun);
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
                Persist("Edit daily challenge entry", () =>
                {
                    entry.Metric = metric;
                    entry.Target = target;
                    entry.EndConditionOverride = endOverride;
                    entry.TimeLimitSeconds = timeLimit;
                    entry.Intensity = intensity;
                    entry.Verb = verb;
                    entry.Noun = noun;
                });

            EditorGUILayout.LabelField("Reads as", $"“{DailyChallengeCatalogSO.BuildObjectiveText(entry)}”",
                FrogletEditorPalette.CardBody);

            foreach (var problem in ProblemsFor(entry))
                EditorGUILayout.HelpBox(problem.Message,
                    problem.IsError ? MessageType.Error : MessageType.Warning);

            EditorGUILayout.EndVertical();
            return remove;
        }

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
        /// before it was a check - see Docs/DAILY_CHALLENGE.md §4.
        /// </summary>
        IEnumerable<Problem> ProblemsFor(DailyChallengeCatalogSO.Entry entry)
        {
            var card = CardFor(entry.Mode);

            if (card == null)
            {
                yield return Problem.Error(
                    $"{entry.Mode} has no SO_ArcadeGame. The card would resolve a challenge that " +
                    "nothing can launch - the tile does nothing on whichever date draws it.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(card.SceneName))
                yield return Problem.Error(
                    $"{card.DisplayName} names no scene. Launching it would fail.");

            if (entry.Intensity < card.MinIntensity || entry.Intensity > card.MaxIntensity)
                yield return Problem.Error(
                    $"Intensity {entry.Intensity} is outside {card.DisplayName}'s range " +
                    $"({card.MinIntensity}-{card.MaxIntensity}). It is silently clamped at launch, " +
                    "so the challenge quietly becomes a different one.");

            // THE trap: the run ends when the RACE target is met, which ends the challenge with
            // it. An objective above that can never complete.
            if (entry.Target > entry.ResolvedEndCondition)
                yield return Problem.Error(
                    $"Objective ({entry.Target}) is above this run's race target " +
                    $"({entry.ResolvedEndCondition}). The turn ends first, so the objective can " +
                    "never be met. Raise the race target or lower the objective.");

            if (!EndConditionOverridesSO.CanOverrideTurnTarget(entry.Mode))
                yield return Problem.Warn(
                    $"{card.DisplayName}'s end condition does not go through " +
                    "EndConditionOverridesSO, so the race target above CANNOT shorten it - the " +
                    "daily run is the full-length match with a clock on it. Only the time limit " +
                    "makes it smaller.");
            else if (_endConditions != null &&
                     _endConditions.TryGetAuthoredTurnTarget(entry.Mode, out int normal) &&
                     entry.ResolvedEndCondition >= normal)
                yield return Problem.Warn(
                    $"A daily run races to {entry.ResolvedEndCondition}; a normal match of " +
                    $"{card.DisplayName} races to {normal}. The daily challenge is meant to be " +
                    "SMALLER than the real mode - lower the race target.");

            if (Array.IndexOf(NotCreditedPerPlayer, entry.Mode) >= 0)
                yield return Problem.Error(
                    $"{card.DisplayName} credits its metric to a DOMAIN'S REPRESENTATIVE, not to " +
                    "the player who earned it, so a personal objective here measures the wrong " +
                    "thing. Remove it from the pool.");

            if (entry.TimeLimitSeconds > 0f && entry.TimeLimitSeconds < 15f)
                yield return Problem.Warn(
                    "Under 15 seconds leaves no room for the countdown and spawn-in - the run is " +
                    "over before the player has control.");

            int duplicates = _catalog.Pool.Count(e => e != null && e.Enabled && e.Mode == entry.Mode);
            if (entry.Enabled && duplicates > 1)
                yield return Problem.Warn(
                    $"{card.DisplayName} appears {duplicates} times in the pool, so it comes up " +
                    "that much more often than the others.");
        }

        void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("What the next 7 days draw", FrogletEditorPalette.SectionHeader);

            if (_catalog.TestActive && _catalog.test.forcedPoolIndex >= 0)
            {
                EditorGUILayout.HelpBox(
                    "Test mode is forcing one pool entry, so every period below draws the same " +
                    "one. Clear the forced index to preview the real rotation.",
                    MessageType.Warning);
            }

            var now = DateTime.UtcNow;
            for (int i = 0; i < 7; i++)
            {
                var day = now.AddDays(i);
                var challenge = _catalog.ForDate(day);
                string label = i == 0 ? "today" : day.ToString("ddd dd MMM");

                EditorGUILayout.LabelField(
                    label,
                    challenge.IsValid
                        ? $"{ModeName(challenge.GameMode)} - {challenge.ObjectiveText} " +
                          $"(race to {challenge.EndConditionValue}, intensity {challenge.Intensity})"
                        : "no challenge (empty pool)",
                    FrogletEditorPalette.CardBody);
            }
        }

        string ModeName(GameModes mode)
        {
            var card = CardFor(mode);
            return card != null && !string.IsNullOrWhiteSpace(card.DisplayName)
                ? card.DisplayName
                : mode.ToString();
        }

        void DrawTestSection()
        {
            var test = _catalog.test ??= new DailyChallengeCatalogSO.TestSettings();

            EditorGUILayout.LabelField("Testing", FrogletEditorPalette.SectionHeader);

            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.ToggleLeft(
                new GUIContent("Test mode",
                    "Everything below is ignored while this is off, and in any release build " +
                    "regardless. A non-development BUILD fails outright while it is on."),
                test.enabled);
            if (EditorGUI.EndChangeCheck())
                Persist("Toggle daily challenge test mode", () => test.enabled = enabled);

            if (enabled)
                EditorGUILayout.HelpBox(
                    "TEST MODE IS ON. It cannot change a release player's behaviour (the runtime " +
                    "gate ignores it), and a non-development build will FAIL while it is on - " +
                    "which is the point: a flag left set must be loud, not silent.",
                    MessageType.Warning);

            using (new EditorGUI.DisabledScope(!enabled))
            {
                EditorGUI.BeginChangeCheck();

                int forced = EditorGUILayout.IntField(
                    new GUIContent("Force pool index (-1 = off)",
                        "Pin the draw to one entry instead of hashing the date. Indexes the list " +
                        "above as you see it."),
                    test.forcedPoolIndex);

                float dayMinutes = Mathf.Max(0f, EditorGUILayout.FloatField(
                    new GUIContent("Day length (minutes, 0 = real 24h)",
                        "Shrinks the cycle so rollover is testable. The period key changes shape, " +
                        "so a test period is never confused with a real day - switching back wipes " +
                        "the stored progress."),
                    test.dayLengthMinutes));

                bool ignoreLimit = EditorGUILayout.ToggleLeft(
                    new GUIContent("Ignore the once-per-day limit",
                        "Replay the challenge while tuning it."),
                    test.ignoreAttemptLimit);

                float scale = Mathf.Max(0.01f, EditorGUILayout.FloatField(
                    new GUIContent("Time limit scale",
                        "Multiplies every entry's clock. 0.25 turns 60s into 15s."),
                    test.timeLimitScale));

                if (EditorGUI.EndChangeCheck())
                    Persist("Edit daily challenge test settings", () =>
                    {
                        test.forcedPoolIndex = forced;
                        test.dayLengthMinutes = dayMinutes;
                        test.ignoreAttemptLimit = ignoreLimit;
                        test.timeLimitScale = scale;
                    });

                if (forced >= 0 && _catalog.Pool != null && forced >= _catalog.Pool.Count)
                    EditorGUILayout.HelpBox(
                        $"Forced index {forced} is past the end of the pool - the draw falls back " +
                        "to hashing the date.", MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Reset", FrogletEditorPalette.SectionLabel);
            EditorGUILayout.HelpBox(
                "Clears this machine's cached progress so the challenge can be played again. In " +
                "PLAY MODE it also rewrites the live cloud record; outside play mode only the " +
                "local snapshot is cleared, and the cloud copy reloads on the next sign-in.",
                MessageType.None);

            if (FrogletEditorPalette.ColorButton("Reset today's progress",
                    FrogletEditorPalette.Warn, 220f))
                ResetProgress();

            if (Application.isPlaying &&
                FrogletEditorPalette.ColorButton("Re-draw from catalog (play mode)",
                    FrogletEditorPalette.Info, 220f))
                DailyChallengeService.Instance?.RefreshFromCatalog();
        }

        void ResetProgress()
        {
            if (Application.isPlaying && DailyChallengeService.Instance != null)
            {
                DailyChallengeService.Instance.ResetTodayForTesting();
                Debug.Log("[DailyChallenge] Live progress reset.");
                return;
            }

            LocalCloudDataCache.Clear(UGSKeys.DailyChallenge);
            Debug.Log("[DailyChallenge] Local snapshot cleared. Enter play mode to re-read " +
                      "(the cloud copy still holds the old progress until it is overwritten).");
        }

        // ── Ship ───────────────────────────────────────────────────────────────

        FrogletToolValidation Validate()
        {
            var problems = new List<string>();

            if (_catalog.Pool == null || _catalog.Pool.Count(e => e != null && e.Enabled) == 0)
                problems.Add("The pool has no enabled entries - the card would show UNAVAILABLE.");

            if (_catalog.test != null && _catalog.test.enabled)
                problems.Add("Test mode is still on. It cannot change a release player's " +
                             "behaviour, but a non-development build will fail while it is set.");

            if (_catalog.Pool != null)
            {
                foreach (var entry in _catalog.Pool)
                {
                    if (entry == null || !entry.Enabled) continue;
                    foreach (var p in ProblemsFor(entry))
                        if (p.IsError) problems.Add($"{entry.Mode}: {p.Message}");
                }
            }

            return problems.Count == 0
                ? FrogletToolValidation.Pass(
                    $"{_catalog.Pool.Count(e => e != null && e.Enabled)} playable entries.")
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
    }
}
