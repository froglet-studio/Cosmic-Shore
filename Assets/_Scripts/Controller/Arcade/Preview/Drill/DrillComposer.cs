using System.Collections.Generic;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>The facts the composer needs about one hull, beyond its prompt tokens.</summary>
    public interface IDrillHullFacts : IDrillTokenSource
    {
        VesselClassType Vessel { get; }
        FlightScheme Scheme { get; }

        /// <summary>True when the hull has a bound drift action.</summary>
        bool CanDrift { get; }

        /// <summary>True when the hull's ability for <paramref name="element"/> exists and is bound
        /// to a control - false for an open design slot and for a passive ability.</summary>
        bool AbilityHasInput(Element element);
    }

    /// <summary>A Lesson step ready to run: its words resolved for this hull.</summary>
    public readonly struct ComposedLessonStep
    {
        public readonly LessonStep Source;
        public readonly string Prompt;
        public readonly string Hint;
        public readonly float HintAfterSeconds;

        public ComposedLessonStep(LessonStep source, string prompt, string hint, float hintAfter)
        {
            Source = source;
            Prompt = prompt;
            Hint = hint;
            HintAfterSeconds = hintAfter;
        }
    }

    /// <summary>Where a Mentor tip came from - the authoring tool colours by it.</summary>
    public enum DrillTipOrigin
    {
        Hull = 0,
        DerivedAbility = 1,
        Mode = 2,
        Advanced = 3,
    }

    /// <summary>A Mentor tip ready to say.</summary>
    public readonly struct ComposedTip
    {
        public readonly string Id;
        public readonly string Prompt;
        public readonly MentorTier Tier;
        public readonly int Priority;
        public readonly float DwellSeconds;
        public readonly DrillCondition Moment;
        public readonly Element Element;
        public readonly DrillTipOrigin Origin;
        public readonly bool Seen;

        public ComposedTip(string id, string prompt, MentorTier tier, int priority, float dwell,
                           DrillCondition moment, Element element, DrillTipOrigin origin, bool seen)
        {
            Id = id;
            Prompt = prompt;
            Tier = tier;
            Priority = priority;
            DwellSeconds = dwell;
            Moment = moment;
            Element = element;
            Origin = origin;
            Seen = seen;
        }
    }

    /// <summary>
    /// Pure function: (library, mode, hull, what the player has seen) -> the Lesson's steps and
    /// the Mentor's playlist. The runner and the authoring tool both call it, so what the tool
    /// shows is what runs (Docs/ModePreview/TRAINING_PLAN.md §4.5).
    /// </summary>
    public static class DrillComposer
    {
        /// <summary>The non-Time elements, in the HUD's display order. Time is taught by the Lesson;
        /// these three become the Mentor's derived "did you know" tips.</summary>
        static readonly Element[] DerivedTipElements = { Element.Charge, Element.Mass, Element.Space };

        /// <summary>Derived tips sit after every curated ship-basics tip.</summary>
        public const int DerivedTipPriorityBase = 1000;

        /// <summary>
        /// The Lesson for this hull. <paramref name="dropped"/>, when given, receives one line per
        /// authored step that did not make it and why (for the authoring tool).
        /// </summary>
        public static List<ComposedLessonStep> ComposeLesson(DrillLibrarySO library, IDrillHullFacts hull,
                                                             List<string> dropped = null)
        {
            var result = new List<ComposedLessonStep>();
            if (library == null || hull == null) return result;

            var hullOverride = library.OverrideFor(hull.Vessel);
            IReadOnlyList<LessonStep> steps;
            if (hullOverride != null && hullOverride.ReplaceLesson)
                steps = hullOverride.ReplacementSteps;
            else
            {
                var template = library.LessonFor(hull.Scheme);
                if (template == null)
                {
                    dropped?.Add($"No Lesson template for the {hull.Scheme} scheme.");
                    return result;
                }
                steps = template.Steps;
            }

            foreach (var step in steps)
            {
                if (step == null) continue;
                string label = string.IsNullOrEmpty(step.Id) ? "(no id)" : step.Id;

                if (hullOverride != null && !hullOverride.ReplaceLesson &&
                    !string.IsNullOrEmpty(step.Id) && hullOverride.SuppressedStepIds.Contains(step.Id))
                {
                    dropped?.Add($"{label}: suppressed for {hull.Vessel}.");
                    continue;
                }

                if (!Applies(step, hull))
                {
                    dropped?.Add($"{label}: {step.Applicability} does not hold for {hull.Vessel}.");
                    continue;
                }

                string prompt = DrillTokens.Resolve(step.Prompt, hull, out var outcome);
                if (outcome == DrillTokenOutcome.MissingFact)
                {
                    dropped?.Add($"{label}: names a fact {hull.Vessel} does not have.");
                    continue;
                }
                if (string.IsNullOrEmpty(prompt))
                {
                    dropped?.Add($"{label}: empty prompt.");
                    continue;
                }

                // A hint that cannot be said honestly is simply not given; the step still runs.
                string hint = DrillTokens.Resolve(step.HintPrompt, hull, out var hintOutcome);
                if (hintOutcome == DrillTokenOutcome.MissingFact) hint = string.Empty;

                float hintAfter = step.HintAfterSeconds > 0f ? step.HintAfterSeconds : library.DefaultHintAfterSeconds;
                result.Add(new ComposedLessonStep(step, prompt, hint, hintAfter));
            }
            return result;
        }

        static bool Applies(LessonStep step, IDrillHullFacts hull) => step.Applicability switch
        {
            DrillApplicability.HullDrifts => hull.CanDrift,
            DrillApplicability.AbilityHasInput => step.Element != Element.None && hull.AbilityHasInput(step.Element),
            _ => true,
        };

        /// <summary>
        /// The Mentor's playlist for this card and hull: hull tips, derived ability tips, the
        /// card's mode tips, then advanced tips - ordered by tier, then priority, then authored
        /// order - with tips in <paramref name="seenTipIds"/> moved to the END rather than dropped,
        /// so a returning player hears something new first and the Mentor never goes silent.
        /// </summary>
        public static List<ComposedTip> ComposeMentor(DrillLibrarySO library, IDrillHullFacts hull,
                                                      GameModes mode, ScoringMetric metric,
                                                      ICollection<string> seenTipIds = null)
        {
            var tips = new List<(ComposedTip tip, int order)>();
            if (library == null || hull == null) return new List<ComposedTip>();

            var ids = new HashSet<string>();
            int order = 0;

            var hullOverride = library.OverrideFor(hull.Vessel);
            AddList(hullOverride?.Tips, DrillTipOrigin.Hull);

            // Derived ability tips: one per non-Time ability the hull really has.
            foreach (var element in DerivedTipElements)
            {
                if (hullOverride != null && hullOverride.SilencedAbilityTips.Contains(element)) continue;
                string template = library.AbilityTipFor(element);
                if (string.IsNullOrEmpty(template)) continue;

                string prompt = DrillTokens.Resolve(template, hull, out var outcome);
                if (outcome == DrillTokenOutcome.MissingFact || string.IsNullOrEmpty(prompt)) continue;

                string id = $"ability:{hull.Vessel}:{element}";
                if (!ids.Add(id)) continue;
                tips.Add((new ComposedTip(id, prompt, MentorTier.ShipBasics,
                    DerivedTipPriorityBase + (int)element, library.MentorDwellSeconds, null, element,
                    DrillTipOrigin.DerivedAbility, IsSeen(id)), order++));
            }

            var modeEntry = library.TipsForMode(mode);
            if (modeEntry == null || !modeEntry.ReplacesMetricTips)
                AddList(library.TipsForMetric(metric), DrillTipOrigin.Mode);
            AddList(modeEntry?.Tips, DrillTipOrigin.Mode);

            AddList(library.AdvancedTips, DrillTipOrigin.Advanced);

            tips.Sort((a, b) =>
            {
                if (a.tip.Seen != b.tip.Seen) return a.tip.Seen ? 1 : -1;
                int c = a.tip.Tier.CompareTo(b.tip.Tier);
                if (c != 0) return c;
                c = a.tip.Priority.CompareTo(b.tip.Priority);
                return c != 0 ? c : a.order.CompareTo(b.order);
            });

            var result = new List<ComposedTip>(tips.Count);
            foreach (var t in tips) result.Add(t.tip);
            return result;

            bool IsSeen(string id) => seenTipIds != null && seenTipIds.Contains(id);

            void AddList(TipListSO list, DrillTipOrigin origin)
            {
                if (list == null) return;
                foreach (var tip in list.Tips)
                {
                    if (tip == null || string.IsNullOrEmpty(tip.Id) || !ids.Add(tip.Id)) continue;

                    string prompt = DrillTokens.Resolve(tip.Prompt, hull, out var outcome);
                    if (outcome == DrillTokenOutcome.MissingFact || string.IsNullOrEmpty(prompt))
                    {
                        ids.Remove(tip.Id);
                        continue;
                    }

                    float dwell = tip.DwellSeconds > 0f ? tip.DwellSeconds : library.MentorDwellSeconds;
                    tips.Add((new ComposedTip(tip.Id, prompt, tip.Tier, tip.Priority, dwell, tip.Moment,
                        tip.Element, origin, IsSeen(tip.Id)), order++));
                }
            }
        }
    }
}
