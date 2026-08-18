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
    /// state however it was created, and that every mode gets a score source that is actually
    /// live during play.
    ///
    /// WHY THIS MATTERS:
    /// Both failure modes here are silent. (1) AddComponent runs OnEnable synchronously, so the
    /// original EnsureExists assigned gameData one line too late: OnEnable bailed before
    /// subscribing, nothing re-subscribed, and the comeback system applied no buff at all in
    /// every mode that does not author it in-scene (all of them except HexRace). (2) A mode whose
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
    /// up a scene-authored instance (MinigameHexRace has one) instead of the fixture's.
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
            var gameData = MakeGameData(GameModes.MultiplayerJoust);
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
            var gameData = MakeGameData(GameModes.MultiplayerJoust);
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
            var authored = MakeGameData(GameModes.HexRace);
            var system = MakeAutoCreated(authored);

            var other = MakeGameData(GameModes.MultiplayerJoust);
            system.Bind(other);

            AssertSubscribedToAll(other, system, false);
            AssertSubscribedToAll(authored, system, true);
        }

        #endregion

        #region Per-mode score sources

        // Every mode whose Score is written only at game end needs the stat it accumulates
        // DURING play, or the deficit reads a flat zero for the whole match.
        static readonly object[] LiveSourceCases =
        {
            new object[] { GameModes.MultiplayerJoust, ElementalComebackSystem.ScoreDifferenceSource.Jousts },
            new object[] { GameModes.NucleusRush, ElementalComebackSystem.ScoreDifferenceSource.Goals },
            new object[] { GameModes.HexRace, ElementalComebackSystem.ScoreDifferenceSource.CrystalsCollected },
            new object[] { GameModes.MultiplayerCrystalCapture, ElementalComebackSystem.ScoreDifferenceSource.CrystalsCollected },
            new object[] { GameModes.AstroLeague, ElementalComebackSystem.ScoreDifferenceSource.Goals },
            new object[] { GameModes.Rampage, ElementalComebackSystem.ScoreDifferenceSource.PrismsDestroyed },
            new object[] { GameModes.Ribcage, ElementalComebackSystem.ScoreDifferenceSource.PrismsDestroyed },
            new object[] { GameModes.WildlifeLiberation, ElementalComebackSystem.ScoreDifferenceSource.LifeformsKilled },
            new object[] { GameModes.DogFight, ElementalComebackSystem.ScoreDifferenceSource.CombatPoints },
        };

        [TestCaseSource(nameof(LiveSourceCases))]
        public void DefaultSourceFor_UsesTheModesLiveStat(
            GameModes mode, ElementalComebackSystem.ScoreDifferenceSource expected)
        {
            Assert.AreEqual(expected, ElementalComebackSystem.DefaultSourceFor(MakeGameData(mode)),
                $"{mode} assigns Score only at game end, so the comeback deficit must read its live stat.");
        }

        [Test]
        public void DefaultSourceFor_LegacyTimeScoredModesKeepScore()
        {
            // Cellular Duel / Wildlife Blitz co-op / Freestyle accumulate Score live through
            // TimePlayedScoring, so Score is the honest source there.
            Assert.AreEqual(ElementalComebackSystem.ScoreDifferenceSource.Score,
                ElementalComebackSystem.DefaultSourceFor(MakeGameData(GameModes.MultiplayerCellularDuel)));
        }

        [Test]
        public void DefaultSourceFor_NullGameDataFallsBackToScore()
        {
            Assert.AreEqual(ElementalComebackSystem.ScoreDifferenceSource.Score,
                ElementalComebackSystem.DefaultSourceFor(null));
        }

        #endregion
    }
}
#endif
