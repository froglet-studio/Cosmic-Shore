using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;
using HintBinding = CosmicShore.UI.InputDeviceIconSetSwitcher.HintBinding;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The microgame drill's one root asset (<c>Resources/DrillLibrary</c>): which Lesson each
    /// flight scheme teaches, which tips each card and hull gets, the pacing, and every piece of
    /// chrome text the coach shows. Zero-wire - the runner loads it by path.
    ///
    /// <para><b>Every word here is a field</b> (CLAUDE.md § Code Style: all player-facing text has
    /// a human-facing control). An empty field shows nothing; there is no hard-coded fallback.
    /// ASCII only - the UI font carries 97 glyphs.</para>
    ///
    /// <para>Docs/ModePreview/TRAINING_PLAN.md §4.2.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "DrillLibrary", menuName = "ScriptableObjects/Drill/Drill Library")]
    public sealed class DrillLibrarySO : ScriptableObject
    {
        public const string ResourcePath = "DrillLibrary";

        public static DrillLibrarySO Load() => Resources.Load<DrillLibrarySO>(ResourcePath);

        [Serializable]
        public class MetricTips
        {
            public ScoringMetric Metric;
            public TipListSO Tips;
        }

        [Serializable]
        public class ModeTips
        {
            public GameModes Mode;
            public TipListSO Tips;
            [Tooltip("On = this card plays ONLY these mode tips; off = they are added to its metric family's.")]
            public bool ReplacesMetricTips;
        }

        [Serializable]
        public class HullOverride
        {
            public VesselClassType Vessel;

            [Tooltip("Tips for this hull, played in the Mentor's ship-basics tier ahead of the derived ability tips.")]
            public TipListSO Tips;

            [Tooltip("On = ReplacementSteps IS this hull's Lesson, instead of its scheme's template.")]
            public bool ReplaceLesson;
            public List<LessonStep> ReplacementSteps = new();

            [Tooltip("Ids of template steps this hull skips.")]
            public List<string> SuppressedStepIds = new();

            [Tooltip("Elements whose automatic 'did you know' ability tip this hull does not get.")]
            public List<Element> SilencedAbilityTips = new();
        }

        /// <summary>The label for a pad control, for {glyph:...} in running text. The chip beside
        /// the prompt draws the real artwork from <c>ControlGlyphSetSO</c>; this is the word.</summary>
        [Serializable]
        public class PadControlLabel
        {
            public HintBinding Binding;
            public string Label;
        }

        /// <summary>The label of a flight control, per scheme and device.</summary>
        [Serializable]
        public class FlightControlLabel
        {
            public DrillControl Control;
            public FlightScheme Scheme;
            [Tooltip("On = the label a keyboard + mouse player reads; off = a gamepad player's.")]
            public bool Keyboard;
            public string Label;
        }

        /// <summary>Every piece of coach chrome text.</summary>
        [Serializable]
        public class DrillStrings
        {
            [Tooltip("Header while the Lesson runs.")]
            public string LessonTitle;
            [Tooltip("Header while the Mentor runs.")]
            public string MentorTitle;
            [Tooltip("The Lesson's skip button, once skipping is allowed.")]
            public string SkipLabel;
            [Tooltip("The Mentor's 'next tip' affordance.")]
            public string NextLabel;
            [Tooltip("Shown when the Mentor has said every tip. Tokens allowed. Empty = the Mentor just stops.")]
            [TextArea(2, 3)] public string MentorClosingLine;
            [Tooltip("Shown on the Play button's place while the first Lesson holds it shut. Empty = nothing.")]
            public string PlayLockedCaption;

            public List<PadControlLabel> PadControlLabels = new();
            public List<FlightControlLabel> FlightControlLabels = new();
        }

        [Header("Lesson")]
        [SerializeField] LessonTemplateSO twoThumbLesson;
        [SerializeField] LessonTemplateSO oneThumbLesson;

        [Tooltip("Seconds into a skippable Lesson before Skip appears (D2).")]
        [SerializeField, Min(0f)] float lessonSkipDelaySeconds = 3f;

        [Tooltip("A step's hint appears after this many seconds unless the step authors its own.")]
        [SerializeField, Min(0f)] float defaultHintAfterSeconds = 8f;

        [Header("Mentor")]
        [SerializeField] List<MetricTips> metricTips = new();
        [SerializeField] List<ModeTips> modeTips = new();

        [Tooltip("Played after ship and mode tips: element upgrades, the Game of the Week board.")]
        [SerializeField] TipListSO advancedTips;

        [Tooltip("The automatic 'did you know' tip for each of a hull's non-Time abilities. Tokens use the " +
                 "element E the tip is about: write it as {ability:E}, {abilityDescription:E}, {glyph:E}. " +
                 "Empty = no automatic ability tips.")]
        [SerializeField, TextArea(2, 3)] string abilityTipTemplate;

        [Tooltip("Seconds a tip stays up unless it authors its own (D10: ~6).")]
        [SerializeField, Min(0.5f)] float mentorDwellSeconds = 6f;
        [Tooltip("Quiet seconds between tips (D10: ~8).")]
        [SerializeField, Min(0f)] float mentorGapSeconds = 8f;
        [Tooltip("A tip waiting for its Moment is said anyway after this long (D10: 14).")]
        [SerializeField, Min(0f)] float momentTimeoutSeconds = 14f;

        [Header("Per hull")]
        [SerializeField] List<HullOverride> hullOverrides = new();

        [Header("Text")]
        [SerializeField] DrillStrings strings = new();

        /// <summary>The element placeholder an ability tip template is written with.</summary>
        public const string AbilityTipElementPlaceholder = "E";

        public LessonTemplateSO LessonFor(FlightScheme scheme) =>
            scheme == FlightScheme.OneThumb ? oneThumbLesson : twoThumbLesson;

        public float LessonSkipDelaySeconds => lessonSkipDelaySeconds;
        public float DefaultHintAfterSeconds => defaultHintAfterSeconds;
        public float MentorDwellSeconds => mentorDwellSeconds;
        public float MentorGapSeconds => mentorGapSeconds;
        public float MomentTimeoutSeconds => momentTimeoutSeconds;
        public string AbilityTipTemplate => abilityTipTemplate;
        public TipListSO AdvancedTips => advancedTips;
        public DrillStrings Strings => strings;
        public IReadOnlyList<MetricTips> AllMetricTips => metricTips;
        public IReadOnlyList<ModeTips> AllModeTips => modeTips;
        public IReadOnlyList<HullOverride> AllHullOverrides => hullOverrides;

        public HullOverride OverrideFor(VesselClassType vessel)
        {
            foreach (var o in hullOverrides)
                if (o != null && o.Vessel == vessel) return o;
            return null;
        }

        public TipListSO TipsForMetric(ScoringMetric metric)
        {
            foreach (var m in metricTips)
                if (m != null && m.Metric == metric) return m.Tips;
            return null;
        }

        public ModeTips TipsForMode(GameModes mode)
        {
            foreach (var m in modeTips)
                if (m != null && m.Mode == mode) return m;
            return null;
        }

        public string PadLabel(HintBinding binding)
        {
            foreach (var l in strings.PadControlLabels)
                if (l != null && l.Binding == binding) return l.Label;
            return null;
        }

        public string FlightLabel(DrillControl control, FlightScheme scheme, bool keyboard)
        {
            foreach (var l in strings.FlightControlLabels)
                if (l != null && l.Control == control && l.Scheme == scheme && l.Keyboard == keyboard)
                    return l.Label;
            return null;
        }

        /// <summary>The ability tip template written for one element: its placeholder E becomes the
        /// element's name, so the ordinary resolver fills it.</summary>
        public string AbilityTipFor(Element element) =>
            string.IsNullOrEmpty(abilityTipTemplate)
                ? string.Empty
                : abilityTipTemplate.Replace(":" + AbilityTipElementPlaceholder + "}", ":" + element + "}");
    }
}
