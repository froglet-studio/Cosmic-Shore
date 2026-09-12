using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>One goal to draw. Entry 0 of a stack is the primary.</summary>
    public readonly struct GoalEntry
    {
        public readonly Sprite Glyph;
        public readonly string Label;
        public readonly int Current;
        public readonly int Target;
        /// <summary>Set when the goal is a count; clear for a clock or any other raw readout.</summary>
        public readonly bool IsCount;
        public readonly string RawValue;

        GoalEntry(Sprite glyph, string label, int current, int target, bool isCount, string raw)
        {
            Glyph = glyph; Label = label; Current = current;
            Target = target; IsCount = isCount; RawValue = raw;
        }

        public static GoalEntry Count(Sprite glyph, string label, int current, int target) =>
            new GoalEntry(glyph, label, current, target, true, null);

        public static GoalEntry Text(Sprite glyph, string label, string value) =>
            new GoalEntry(glyph, label, 0, 0, false, value);
    }

    /// <summary>
    /// The top-left goal stack: the mode's objective on top, any further goals under it, in one
    /// vertical layout group anchored to the corner so the stack grows DOWNWARD and nothing below
    /// it moves.
    ///
    /// It adds no plumbing. The number is the one every turn monitor already publishes through
    /// <c>onUpdateTurnMonitorDisplay</c> - the metric REMAINING - and the label, glyph and target
    /// come from the mode's own <c>ScoringRuleSO</c> by way of <see cref="ObjectiveIconSetSO"/>.
    /// So a new mode picking an existing metric gets a correct goal line for free, and the row
    /// can never disagree with the condition that actually ends the turn.
    ///
    /// The two inputs arrive on different schedules - the metric when the game config syncs, the
    /// count on every monitor tick - so both are stored and the row is rebuilt from whichever
    /// lands. Neither is meaningful without the other, which is why the stack hides until it has
    /// both rather than showing half an objective.
    /// </summary>
    public class GoalStack : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The rows, top to bottom. Authored in the prefab rather than pooled: the count " +
                 "is small and fixed, and a serialized row can be styled in the inspector.")]
        [SerializeField] GoalRow[] rows = new GoalRow[0];

        [Tooltip("Leave empty to load Resources/ObjectiveIconSet.")]
        [SerializeField] ObjectiveIconSetSO iconSet;

        [Header("Content")]
        [Tooltip("What to call the objective when the mode publishes a value that is not a count " +
                 "- the six time-limited scenes. Their monitor raises a clock string, so the row " +
                 "shows it verbatim with no target and no progress bar.")]
        [SerializeField] string clockLabel = "Time remaining";

        readonly List<GoalEntry> _entries = new();

        ScoringMetric? _metric;
        int _target;
        bool _secondsMode;
        string _payload = string.Empty;

        void Awake()
        {
            if (iconSet == null) iconSet = ObjectiveIconSetSO.Load();
            Rebuild();
        }

        /// <summary>
        /// Point the stack at a mode's scoring metric and target. Idempotent, and safe to call
        /// before the game config has synced - a null metric means "not known yet", which hides
        /// the stack rather than naming another mode's objective.
        /// </summary>
        public void SetObjective(ScoringMetric? metric, int target, bool secondsRemaining = false)
        {
            _metric = metric;
            _target = target;
            _secondsMode = secondsRemaining;
            Rebuild();
        }

        /// <summary>
        /// The turn monitor's own readout, verbatim. What it MEANS is not knowable from the string
        /// - every monitor publishes a bare integer, a time monitor included - so the reading is
        /// decided by <see cref="SetObjective"/>'s secondsRemaining, off the monitor's own
        /// declaration.
        /// </summary>
        public void SetMonitorPayload(string payload)
        {
            _payload = payload ?? string.Empty;
            Rebuild();
        }

        /// <summary>
        /// Draw an explicit list of goals, entry 0 first. Nothing authors a multi-goal list yet -
        /// a ScoringRuleSO names exactly one objective, the one that ends the turn - so this is
        /// the seam a mode-authored list plugs into, and the layout below already handles it.
        /// </summary>
        public void SetGoals(IReadOnlyList<GoalEntry> goals)
        {
            _entries.Clear();
            if (goals != null)
                for (int i = 0; i < goals.Count; i++) _entries.Add(goals[i]);
            Draw();
        }

        void Rebuild()
        {
            _entries.Clear();

            if (!string.IsNullOrEmpty(_payload))
            {
                if (_secondsMode)
                {
                    // Seconds, so it gets its own label and no glyph - naming it after the metric
                    // would read as "Collect crystals 1:12". Formatted here because the monitor
                    // publishes a bare integer.
                    _entries.Add(GoalEntry.Text(null, clockLabel, FormatClock(_payload)));
                }
                else if (_metric.HasValue && _target > 0 && int.TryParse(_payload, out int remaining))
                {
                    Sprite glyph = iconSet != null ? iconSet.For(_metric.Value) : null;
                    string label = iconSet != null ? iconSet.LabelFor(_metric.Value) : string.Empty;
                    // The monitor publishes what is LEFT; the row shows what is DONE.
                    int current = Mathf.Clamp(_target - remaining, 0, _target);
                    _entries.Add(GoalEntry.Count(glyph, label, current, _target));
                }
                // else: a count we cannot NAME (the config has not synced, or the mode's rule
                // publishes no target). Draw nothing rather than a bare number under a borrowed
                // label - an unlabelled count is the thing the ring was retired for.
            }

            Draw();
        }

        /// <summary>Seconds as m:ss. A bare "72" is what the ring showed; this is why it went.</summary>
        static string FormatClock(string payload)
        {
            if (!int.TryParse(payload, out int seconds) || seconds < 0) return payload;
            return $"{seconds / 60}:{seconds % 60:00}";
        }

        void Draw()
        {
            if (rows == null) return;

            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null) continue;

                if (i >= _entries.Count) { row.Hide(); continue; }

                var e = _entries[i];
                var rank = i == 0 ? GoalRank.Primary : GoalRank.Secondary;
                if (e.IsCount) row.ShowCount(e.Glyph, e.Label, e.Current, e.Target, rank);
                else           row.ShowText(e.Glyph, e.Label, e.RawValue, rank);
            }
        }
    }
}
