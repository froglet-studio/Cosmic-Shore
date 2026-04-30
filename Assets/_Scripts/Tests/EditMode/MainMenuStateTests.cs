using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Tests for <see cref="MainMenuState"/> enum integrity and the
    /// <see cref="Core.MainMenuController"/> transition table.
    ///
    /// Validates:
    ///   - Enum values are stable (serialization safety)
    ///   - All states have defined outbound transitions
    ///   - Freestyle state transitions are bidirectional with Ready
    ///   - No duplicate integer values
    /// </summary>
    [TestFixture]
    public class MainMenuStateTests
    {
        #region Enum Integrity

        [Test]
        public void MainMenuState_HasExpectedMemberCount()
        {
            var values = Enum.GetValues(typeof(MainMenuState));
            Assert.AreEqual(5, values.Length,
                "MainMenuState member count changed. Update tests if a state was added/removed.");
        }

        [Test]
        [TestCase(MainMenuState.None, 0)]
        [TestCase(MainMenuState.Initializing, 1)]
        [TestCase(MainMenuState.Ready, 2)]
        [TestCase(MainMenuState.LaunchingGame, 3)]
        [TestCase(MainMenuState.Freestyle, 4)]
        public void MainMenuState_HasCorrectIntegerValue(MainMenuState state, int expectedValue)
        {
            Assert.AreEqual(expectedValue, (int)state,
                $"MainMenuState.{state} integer value changed from {expectedValue} to {(int)state}. " +
                "This will break all serialized references to this state.");
        }

        [Test]
        public void MainMenuState_AllValuesAreUnique()
        {
            var values = Enum.GetValues(typeof(MainMenuState)).Cast<int>().ToList();
            var distinct = values.Distinct().ToList();
            Assert.AreEqual(values.Count, distinct.Count,
                "MainMenuState has duplicate integer values. This causes ambiguous deserialization.");
        }

        #endregion

        #region Transition Table

        /// <summary>
        /// Extracts the ValidTransitions table from MainMenuController via reflection.
        /// </summary>
        static Dictionary<MainMenuState, HashSet<MainMenuState>> GetTransitionTable()
        {
            var controllerType = typeof(Core.MainMenuController);
            var field = controllerType.GetField("ValidTransitions",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(field, "Could not find ValidTransitions field on MainMenuController. " +
                                    "Was it renamed or removed?");

            var table = field.GetValue(null) as Dictionary<MainMenuState, HashSet<MainMenuState>>;
            Assert.IsNotNull(table, "ValidTransitions is null.");
            return table;
        }

        [Test]
        public void TransitionTable_AllStatesHaveEntries()
        {
            var table = GetTransitionTable();
            var allStates = Enum.GetValues(typeof(MainMenuState)).Cast<MainMenuState>();

            foreach (var state in allStates)
            {
                // Terminal states may not have outbound transitions
                if (state == MainMenuState.None) continue;

                Assert.IsTrue(table.ContainsKey(state),
                    $"MainMenuState.{state} has no entry in the transition table.");
            }
        }

        [Test]
        public void TransitionTable_None_CanTransitionToInitializing()
        {
            var table = GetTransitionTable();
            Assert.IsTrue(table[MainMenuState.None].Contains(MainMenuState.Initializing));
        }

        [Test]
        public void TransitionTable_Initializing_CanTransitionToReady()
        {
            var table = GetTransitionTable();
            Assert.IsTrue(table[MainMenuState.Initializing].Contains(MainMenuState.Ready));
        }

        [Test]
        public void TransitionTable_Ready_CanTransitionToFreestyle()
        {
            var table = GetTransitionTable();
            Assert.IsTrue(table[MainMenuState.Ready].Contains(MainMenuState.Freestyle),
                "Ready → Freestyle transition must be allowed for menu/freestyle toggle.");
        }

        [Test]
        public void TransitionTable_Freestyle_CanTransitionToReady()
        {
            var table = GetTransitionTable();
            Assert.IsTrue(table[MainMenuState.Freestyle].Contains(MainMenuState.Ready),
                "Freestyle → Ready transition must be allowed to return to menu.");
        }

        [Test]
        public void TransitionTable_Ready_CanTransitionToLaunchingGame()
        {
            var table = GetTransitionTable();
            Assert.IsTrue(table[MainMenuState.Ready].Contains(MainMenuState.LaunchingGame));
        }

        [Test]
        public void TransitionTable_Freestyle_CanTransitionToLaunchingGame()
        {
            var table = GetTransitionTable();
            Assert.IsTrue(table[MainMenuState.Freestyle].Contains(MainMenuState.LaunchingGame),
                "Freestyle → LaunchingGame transition must be allowed to launch games from freestyle.");
        }

        [Test]
        public void TransitionTable_LaunchingGame_CanTransitionToReady()
        {
            var table = GetTransitionTable();
            Assert.IsTrue(table[MainMenuState.LaunchingGame].Contains(MainMenuState.Ready),
                "LaunchingGame → Ready transition must be allowed for cancelled launches.");
        }

        [Test]
        public void TransitionTable_Freestyle_CannotTransitionToInitializing()
        {
            var table = GetTransitionTable();
            Assert.IsFalse(table[MainMenuState.Freestyle].Contains(MainMenuState.Initializing),
                "Freestyle → Initializing transition should not be allowed.");
        }

        [Test]
        public void TransitionTable_None_CannotTransitionToFreestyle()
        {
            var table = GetTransitionTable();
            Assert.IsFalse(table[MainMenuState.None].Contains(MainMenuState.Freestyle),
                "None → Freestyle transition should not be allowed (menu not ready).");
        }

        [Test]
        public void TransitionTable_Initializing_CannotTransitionToFreestyle()
        {
            var table = GetTransitionTable();
            Assert.IsFalse(table[MainMenuState.Initializing].Contains(MainMenuState.Freestyle),
                "Initializing → Freestyle transition should not be allowed (vessel not spawned yet).");
        }

        #endregion
    }
}
