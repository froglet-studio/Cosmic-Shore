#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The drill's data layer (Docs/ModePreview/TRAINING_PLAN.md §4): the first-time rule, the
    /// token resolver's two failure modes, and the composer's Lesson and Mentor. The fuller sweep,
    /// with negative controls, runs outside the editor in Tools/Build/drill_harness.
    /// </summary>
    public class DrillComposerTests
    {
        sealed class Hull : IDrillHullFacts
        {
            public VesselClassType Vessel => VesselClassType.Squirrel;
            public FlightScheme Scheme { get; set; } = FlightScheme.TwoThumb;
            public bool CanDrift { get; set; } = true;
            public string VesselName => "Squirrel";
            public string ModeName => "Switchback";
            public bool TimeBound = true;

            public bool AbilityHasInput(Element e) => e == Element.Time && TimeBound;

            public bool TryAbility(Element e, out string label, out string description)
            {
                label = e == Element.Time ? "Skim" : null;
                description = e == Element.Time ? "Boost near prisms." : null;
                return label != null;
            }

            public bool TryAbilityControlLabel(Element e, out string label)
            {
                label = AbilityHasInput(e) ? "RT" : null;
                return label != null;
            }

            public bool TryControlLabel(DrillControl c, out string label)
            {
                label = c == DrillControl.Steer ? "LS" : null;
                return label != null;
            }
        }

        static void Set(object o, string field, object value) =>
            o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, value);

        readonly List<Object> _made = new();

        T Make<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _made.Add(so);
            return so;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) Object.DestroyImmediate(o);
            _made.Clear();
        }

        DrillLibrarySO Library()
        {
            var template = Make<LessonTemplateSO>();
            Set(template, "steps", new List<LessonStep>
            {
                new() { Id = "steer", Prompt = "Use {glyph:Steer} to steer.", Condition = new SteerHeldCondition() },
                new() { Id = "time", Prompt = "Hold {glyph:Time} - {ability:Time}.", Element = Element.Time,
                        Applicability = DrillApplicability.AbilityHasInput, Condition = new AbilityActivatedCondition() },
                new() { Id = "drift", Prompt = "Drift.", Applicability = DrillApplicability.HullDrifts,
                        Condition = new DriftHeldCondition() },
            });
            var library = Make<DrillLibrarySO>();
            Set(library, "twoThumbLesson", template);
            return library;
        }

        [Test]
        public void FirstLessonsAreForcedThenSkippable()
        {
            Assert.IsFalse(DrillProgressStore.IsLessonSkippable(FlightScheme.OneThumb, false, false));
            Assert.IsFalse(DrillProgressStore.IsLessonSkippable(FlightScheme.TwoThumb, true, false),
                "The first two-thumb Lesson is forced even after a one-thumb one (D7).");
            Assert.IsTrue(DrillProgressStore.IsLessonSkippable(FlightScheme.OneThumb, true, false));
            Assert.IsTrue(DrillProgressStore.IsLessonSkippable(FlightScheme.TwoThumb, true, true));
        }

        [Test]
        public void ATypoIsShownAndAMissingFactIsDropped()
        {
            var hull = new Hull();
            Assert.AreEqual("Use {abilty:Time}", DrillTokens.Resolve("Use {abilty:Time}", hull, out var typo));
            Assert.AreEqual(DrillTokenOutcome.UnknownToken, typo);

            DrillTokens.Resolve("Use {ability:Charge}", hull, out var missing);
            Assert.AreEqual(DrillTokenOutcome.MissingFact, missing);
        }

        [Test]
        public void TheLessonFollowsWhatTheHullCanDo()
        {
            var library = Library();
            var hull = new Hull();

            var steps = DrillComposer.ComposeLesson(library, hull);
            Assert.AreEqual(3, steps.Count);
            Assert.AreEqual("Hold RT - Skim.", steps[1].Prompt);

            hull.CanDrift = false;
            hull.TimeBound = false;
            steps = DrillComposer.ComposeLesson(library, hull);
            Assert.AreEqual(1, steps.Count, "No drift and a passive Time ability leave only the steering step.");
        }

        [Test]
        public void SeenTipsMoveToTheEnd()
        {
            var library = Library();
            var tips = Make<TipListSO>();
            Set(tips, "tips", new List<MentorTip>
            {
                new() { Id = "a", Prompt = "A", Tier = MentorTier.ShipBasics },
                new() { Id = "b", Prompt = "B", Tier = MentorTier.WinTheMode },
                new() { Id = "c", Prompt = "C", Tier = MentorTier.WinFaster },
            });
            Set(library, "advancedTips", tips);

            var list = DrillComposer.ComposeMentor(library, new Hull(), GameModes.Switchback,
                ScoringMetric.SwitchesThreaded, new HashSet<string> { "a" });
            CollectionAssert.AreEqual(new[] { "b", "c", "a" }, list.ConvertAll(t => t.Id));
        }
    }
}
#endif
