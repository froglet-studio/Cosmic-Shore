#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using Obvious.Soap;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Object = UnityEngine.Object;

namespace CosmicShore.Tests
{
    /// <summary>
    /// ElementalComebackSystem wiring tests — validates that a system reaches its SUBSCRIBED
    /// state however it was created, and that its deficit is read from the mode's own score.
    ///
    /// WHY THIS MATTERS:
    /// Both failure modes here are silent. (1) AddComponent runs OnEnable synchronously, so the
    /// original EnsureExists assigned gameData one line too late: OnEnable bailed before
    /// subscribing, nothing re-subscribed, and the comeback system applied no buff at all in
    /// every mode that does not author it in-scene (all of them except SkimRace). (2) A mode whose
    /// Score is assigned only at game end reads a flat zero deficit for the whole match, so a
    /// correctly-wired system still does nothing. Neither surfaces as an error — only as a
    /// mechanic that quietly never fires.
    ///
    /// SCOPE (what edit mode can honestly prove): MonoBehaviour lifecycle messages don't run
    /// outside play mode, and Soap's ScriptableEvent.Raise() early-returns on
    /// !Application.isPlaying — so these tests assert the SUBSCRIPTION (via IsRunning and Soap's
    /// own listener registry), not event delivery. Buffs actually landing is the in-editor
    /// verification step.
    ///
    /// The binding tests drive AddComponent + Bind directly rather than going through
    /// EnsureExists: that reproduces the exact ordering under test, and edit-mode tests run
    /// against whatever scene the developer has open, so a FindFirstObjectByType path could pick
    /// up a scene-authored instance (MinigameSkimRace has one) instead of the fixture's.
    /// </summary>
    [TestFixture]
    public class ElementalComebackSystemTests
    {
        readonly List<Object> _created = new();

        T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }

        /// <summary>
        /// A GameDataSO with the three turn/game SOAP channels wired — the minimum the comeback
        /// system subscribes to. The events are public fields, so no asset loading is needed.
        /// </summary>
        GameDataSO MakeGameData(GameModes mode)
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            gameData.GameMode = mode;
            gameData.OnMiniGameTurnStarted = Track(ScriptableObject.CreateInstance<ScriptableEventNoParam>());
            gameData.OnMiniGameTurnEnd = Track(ScriptableObject.CreateInstance<ScriptableEventNoParam>());
            gameData.OnMiniGameEnd = Track(ScriptableObject.CreateInstance<ScriptableEventNoParam>());
            return gameData;
        }

        /// <summary>
        /// Reproduces the auto-created path: AddComponent — which in play mode runs OnEnable with
        /// a null gameData — followed by the handoff EnsureExists performs.
        /// </summary>
        ElementalComebackSystem MakeAutoCreated(GameDataSO gameData)
        {
            var host = Track(new GameObject("ComebackHost"));
            var system = host.AddComponent<ElementalComebackSystem>();
            system.Bind(gameData);
            return system;
        }

        // Soap maintains the subscriber's owning Object per event (the OnRaised add/remove
        // accessors), so subscription is observable without raising anything.
        static bool IsSubscribedTo(ScriptableEventNoParam channel, Object subscriber) =>
            channel.GetAllObjects().Contains(subscriber);

        static void AssertSubscribedToAll(GameDataSO gameData, ElementalComebackSystem system, bool expected)
        {
            Assert.AreEqual(expected, IsSubscribedTo(gameData.OnMiniGameTurnStarted, system), "turn started");
            Assert.AreEqual(expected, IsSubscribedTo(gameData.OnMiniGameTurnEnd, system), "turn end");
            Assert.AreEqual(expected, IsSubscribedTo(gameData.OnMiniGameEnd, system), "game end");
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i]) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        #region Binding (the regression)

        [Test]
        public void Bind_SubscribesToTheTurnAndGameEvents()
        {
            // The bug: the caller assigned gameData after AddComponent had already run OnEnable,
            // so the system stayed permanently unsubscribed with only a one-off error to show it.
            var gameData = MakeGameData(GameModes.Joust);
            var system = MakeAutoCreated(gameData);

            Assert.IsTrue(system.IsRunning,
                "Binding after AddComponent must subscribe — an unsubscribed system never " +
                "activates and applies no comeback buff at all.");
            AssertSubscribedToAll(gameData, system, true);
        }

        [Test]
        public void Bind_WithNullLeavesTheSystemUnsubscribed()
        {
            var host = Track(new GameObject("ComebackHost"));
            var system = host.AddComponent<ElementalComebackSystem>();

            system.Bind(null);

            Assert.IsFalse(system.IsRunning,
                "A null handoff must not report a running system — Start is what fails loud on it.");
        }

        [Test]
        public void Bind_IsIdempotent()
        {
            // A double subscription would run the handlers twice per raise. The _subscribed guard
            // is what makes the three entry points (OnEnable/Bind/Start) safe to overlap.
            var gameData = MakeGameData(GameModes.Joust);
            var system = MakeAutoCreated(gameData);

            system.Bind(gameData);
            system.Bind(gameData);

            Assert.IsTrue(system.IsRunning);
            Assert.AreEqual(1, gameData.OnMiniGameTurnStarted.GetAllObjects().Count,
                "The system must be registered exactly once.");
        }

        [Test]
        public void Bind_DoesNotRepointAnAlreadyBoundSystem()
        {
            // A scene-authored instance that Reflex already injected must keep its own reference
            // when EnsureExists offers it another one.
            var authored = MakeGameData(GameModes.SkimRace);
            var system = MakeAutoCreated(authored);

            var other = MakeGameData(GameModes.Joust);
            system.Bind(other);

            AssertSubscribedToAll(other, system, false);
            AssertSubscribedToAll(authored, system, true);
        }

        #endregion

        #region The deficit IS the score

        /// <summary>
        /// A rule whose domain values are set directly, so the tests can prove the comeback reads
        /// <see cref="ScoringRuleSO.DomainValue"/> without building IRoundStats fixtures.
        /// </summary>
        class FixedDomainValueRule : ScoringRuleSO
        {
            public readonly Dictionary<Domains, int> Values = new();
            public override int DomainValue(GameDataSO gameData, Domains domain) =>
                Values.TryGetValue(domain, out var v) ? v : 0;
            public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
            {
                winner = Domains.Blue;
                return false;
            }
            public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime) { }
            public override List<ScoreResult> BuildResults(GameDataSO gameData) => new();
            public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) => default;
        }

        [Test]
        public void DomainScore_IsTheRulesDomainValue()
        {
            // The regression this replaces: the comeback read an AUTHORED stat selector that a
            // cloned scene carried over from its donor, so eight modes caught up on a stat they
            // did not score. It now reads the one function the score itself is read through.
            var gameData = MakeGameData(GameModes.Bends);
            var rule = Track(ScriptableObject.CreateInstance<FixedDomainValueRule>());
            rule.Values[Domains.Jade] = 9;
            rule.Values[Domains.Ruby] = 3;
            gameData.ScoringRule = rule;

            Assert.AreEqual(9f, ElementalComebackSystem.DomainScore(gameData, Domains.Jade));
            Assert.AreEqual(3f, ElementalComebackSystem.DomainScore(gameData, Domains.Ruby));
        }

        [Test]
        public void IsHigherBetter_IgnoresTheRulesGolfFlag()
        {
            // A rule's GolfRules is about the FINAL Score (finish time / sentinel). Its
            // DomainValue is what ResolveWinner MAXIMIZES, so it is higher-is-better during play
            // even in golf-scored races (SkimRace, Joust, every gate race).
            var gameData = MakeGameData(GameModes.SkimRace);
            gameData.ScoringRule = Track(ScriptableObject.CreateInstance<FixedDomainValueRule>());

            Assert.IsTrue(ElementalComebackSystem.IsHigherBetter(gameData, legacyGolfRules: true));
        }

        [Test]
        public void IsHigherBetter_LegacyFallbackHonoursGolf()
        {
            var gameData = MakeGameData(GameModes.OnlineDuelForTheCell);
            Assert.IsFalse(ElementalComebackSystem.IsHigherBetter(gameData, legacyGolfRules: true));
            Assert.IsTrue(ElementalComebackSystem.IsHigherBetter(gameData, legacyGolfRules: false));
        }

        [Test]
        public void DomainScore_NullGameDataIsZero()
        {
            Assert.AreEqual(0f, ElementalComebackSystem.DomainScore(null, Domains.Jade));
        }

        /// <summary>
        /// The structural half of the fix: the component must carry NO serialized setting that
        /// could choose a stat. Every scene serializes this component, and a scene cloned from a
        /// donor keeps the donor's values - so any authorable selector here is the same bug
        /// waiting for its next clone. If a genuinely per-mode comeback knob is ever needed, it
        /// belongs on the mode's ScoringRuleSO (or its SO_ArcadeGame card), where the score lives.
        /// </summary>
        [Test]
        public void Component_SerializesNoStatSelector()
        {
            var allowed = new HashSet<string>
            {
                "comebackProfile", "updateInterval", "comebackAudioCooldown", "debugLogging",
            };

            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

            foreach (var field in typeof(ElementalComebackSystem).GetFields(flags))
            {
                bool serialized = field.IsPublic
                    ? !field.IsNotSerialized
                    : field.IsDefined(typeof(SerializeField), false);
                if (!serialized) continue;

                Assert.IsTrue(allowed.Contains(field.Name),
                    $"ElementalComebackSystem serializes '{field.Name}'. A scene-authored comeback " +
                    "setting is how eight modes shipped catching up on another mode's stat - the " +
                    "deficit must come from the mode's ScoringRuleSO.DomainValue, not from the scene.");
            }
        }

        #endregion
    }
}
#endif
