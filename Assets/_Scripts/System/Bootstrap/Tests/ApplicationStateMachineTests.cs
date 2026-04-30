using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Core
{
    [TestFixture]
    public class ApplicationStateMachineTests
    {
        ApplicationStateDataVariable _stateVariable;
        ApplicationStateMachine _sm;

        [SetUp]
        public void SetUp()
        {
            _stateVariable = ScriptableObject.CreateInstance<ApplicationStateDataVariable>();

            // ScriptableVariable<T>._value may be null for reference types when
            // created via CreateInstance (no serialization pass). Force-initialize it.
            if (_stateVariable.Value == null)
            {
                var field = typeof(Obvious.Soap.ScriptableVariable<ApplicationStateData>)
                    .GetField("_value", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(_stateVariable, new ApplicationStateData());
            }

            _sm = new ApplicationStateMachine(_stateVariable, null, null, allowLog: false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_stateVariable != null)
                Object.DestroyImmediate(_stateVariable);
        }

        #region Initial State

        [Test]
        public void InitialState_IsNone()
        {
            Assert.AreEqual(ApplicationState.None, _sm.Current);
        }

        #endregion

        #region Valid Transitions

        [Test]
        public void None_To_Bootstrapping_Succeeds()
        {
            bool result = _sm.TransitionTo(ApplicationState.Bootstrapping);

            Assert.IsTrue(result);
            Assert.AreEqual(ApplicationState.Bootstrapping, _sm.Current);
        }

        [Test]
        public void Bootstrapping_To_Authenticating_Succeeds()
        {
            _sm.TransitionTo(ApplicationState.Bootstrapping);

            bool result = _sm.TransitionTo(ApplicationState.Authenticating);

            Assert.IsTrue(result);
            Assert.AreEqual(ApplicationState.Authenticating, _sm.Current);
        }

        [Test]
        public void Authenticating_To_MainMenu_Succeeds()
        {
            _sm.TransitionTo(ApplicationState.Bootstrapping);
            _sm.TransitionTo(ApplicationState.Authenticating);

            bool result = _sm.TransitionTo(ApplicationState.MainMenu);

            Assert.IsTrue(result);
            Assert.AreEqual(ApplicationState.MainMenu, _sm.Current);
        }

        [Test]
        public void MainMenu_To_LoadingGame_Succeeds()
        {
            AdvanceTo(ApplicationState.MainMenu);

            bool result = _sm.TransitionTo(ApplicationState.LoadingGame);

            Assert.IsTrue(result);
            Assert.AreEqual(ApplicationState.LoadingGame, _sm.Current);
        }

        [Test]
        public void LoadingGame_To_InGame_Succeeds()
        {
            AdvanceTo(ApplicationState.LoadingGame);

            bool result = _sm.TransitionTo(ApplicationState.InGame);

            Assert.IsTrue(result);
            Assert.AreEqual(ApplicationState.InGame, _sm.Current);
        }

        [Test]
        public void InGame_To_GameOver_Succeeds()
        {
            AdvanceTo(ApplicationState.InGame);

            bool result = _sm.TransitionTo(ApplicationState.GameOver);

            Assert.IsTrue(result);
            Assert.AreEqual(ApplicationState.GameOver, _sm.Current);
        }

        [Test]
        public void GameOver_To_MainMenu_Succeeds()
        {
            AdvanceTo(ApplicationState.GameOver);

            bool result = _sm.TransitionTo(ApplicationState.MainMenu);

            Assert.IsTrue(result);
            Assert.AreEqual(ApplicationState.MainMenu, _sm.Current);
        }

        [Test]
        public void GameOver_To_LoadingGame_Succeeds_Replay()
        {
            AdvanceTo(ApplicationState.GameOver);

            bool result = _sm.TransitionTo(ApplicationState.LoadingGame);

            Assert.IsTrue(result);
            Assert.AreEqual(ApplicationState.LoadingGame, _sm.Current);
        }

        [Test]
        public void GameOver_To_InGame_Succeeds_Restart()
        {
            AdvanceTo(ApplicationState.GameOver);

            bool result = _sm.TransitionTo(ApplicationState.InGame);

            Assert.IsTrue(result);
            Assert.AreEqual(ApplicationState.InGame, _sm.Current);
        }

        [Test]
        public void InGame_To_MainMenu_Succeeds_EarlyExit()
        {
            AdvanceTo(ApplicationState.InGame);

            bool result = _sm.TransitionTo(ApplicationState.MainMenu);

            Assert.IsTrue(result);
            Assert.AreEqual(ApplicationState.MainMenu, _sm.Current);
        }

        [Test]
        public void LoadingGame_To_MainMenu_Succeeds_CancelledLoad()
        {
            AdvanceTo(ApplicationState.LoadingGame);

            bool result = _sm.TransitionTo(ApplicationState.MainMenu);

            Assert.IsTrue(result);
            Assert.AreEqual(ApplicationState.MainMenu, _sm.Current);
        }

        #endregion

        #region Invalid Transitions

        [Test]
        public void None_To_InGame_Fails()
        {
            bool result = _sm.TransitionTo(ApplicationState.InGame);

            Assert.IsFalse(result);
            Assert.AreEqual(ApplicationState.None, _sm.Current);
        }

        [Test]
        public void Bootstrapping_To_MainMenu_Fails()
        {
            _sm.TransitionTo(ApplicationState.Bootstrapping);

            bool result = _sm.TransitionTo(ApplicationState.MainMenu);

            Assert.IsFalse(result);
            Assert.AreEqual(ApplicationState.Bootstrapping, _sm.Current);
        }

        [Test]
        public void MainMenu_To_GameOver_Fails()
        {
            AdvanceTo(ApplicationState.MainMenu);

            bool result = _sm.TransitionTo(ApplicationState.GameOver);

            Assert.IsFalse(result);
            Assert.AreEqual(ApplicationState.MainMenu, _sm.Current);
        }

        #endregion

        #region Same-State Transition

        [Test]
        public void SameState_ReturnsTrue_NoOp()
        {
            _sm.TransitionTo(ApplicationState.Bootstrapping);

            bool result = _sm.TransitionTo(ApplicationState.Bootstrapping);

            Assert.IsTrue(result);
            Assert.AreEqual(ApplicationState.Bootstrapping, _sm.Current);
        }

        #endregion

        #region ShuttingDown (Terminal — Always Allowed)

        [Test]
        public void ShuttingDown_AllowedFromNone()
        {
            bool result = _sm.TransitionTo(ApplicationState.ShuttingDown);

            Assert.IsTrue(result);
            Assert.AreEqual(ApplicationState.ShuttingDown, _sm.Current);
        }

        [Test]
        public void ShuttingDown_AllowedFromInGame()
        {
            AdvanceTo(ApplicationState.InGame);

            bool result = _sm.TransitionTo(ApplicationState.ShuttingDown);

            Assert.IsTrue(result);
            Assert.AreEqual(ApplicationState.ShuttingDown, _sm.Current);
        }

        #endregion

        #region Paused

        [Test]
        public void Paused_AllowedFromInGame()
        {
            AdvanceTo(ApplicationState.InGame);

            bool result = _sm.TransitionTo(ApplicationState.Paused);

            Assert.IsTrue(result);
            Assert.AreEqual(ApplicationState.Paused, _sm.Current);
        }

        [Test]
        public void Paused_NotAllowedFromShuttingDown()
        {
            _sm.TransitionTo(ApplicationState.ShuttingDown);

            bool result = _sm.TransitionTo(ApplicationState.Paused);

            Assert.IsFalse(result);
            Assert.AreEqual(ApplicationState.ShuttingDown, _sm.Current);
        }

        [Test]
        public void HandleAppPaused_PausesAndRestores()
        {
            AdvanceTo(ApplicationState.InGame);

            _sm.HandleAppPaused(true);
            Assert.AreEqual(ApplicationState.Paused, _sm.Current);

            _sm.HandleAppPaused(false);
            Assert.AreEqual(ApplicationState.InGame, _sm.Current);
        }

        [Test]
        public void Paused_CanOnlyReturnToPreviousState()
        {
            AdvanceTo(ApplicationState.MainMenu);
            _sm.TransitionTo(ApplicationState.Paused);

            // Trying to go to InGame from Paused (when we paused from MainMenu) should fail.
            bool result = _sm.TransitionTo(ApplicationState.InGame);

            Assert.IsFalse(result);
            Assert.AreEqual(ApplicationState.Paused, _sm.Current);
        }

        #endregion

        #region Disconnected

        [Test]
        public void Disconnected_AllowedFromInGame()
        {
            AdvanceTo(ApplicationState.InGame);

            bool result = _sm.TransitionTo(ApplicationState.Disconnected);

            Assert.IsTrue(result);
            Assert.AreEqual(ApplicationState.Disconnected, _sm.Current);
        }

        [Test]
        public void Disconnected_NotAllowedFromNone()
        {
            bool result = _sm.TransitionTo(ApplicationState.Disconnected);

            Assert.IsFalse(result);
            Assert.AreEqual(ApplicationState.None, _sm.Current);
        }

        [Test]
        public void Disconnected_To_MainMenu_Succeeds()
        {
            AdvanceTo(ApplicationState.InGame);
            _sm.TransitionTo(ApplicationState.Disconnected);

            bool result = _sm.TransitionTo(ApplicationState.MainMenu);

            Assert.IsTrue(result);
            Assert.AreEqual(ApplicationState.MainMenu, _sm.Current);
        }

        #endregion

        #region PreviousState Tracking

        [Test]
        public void PreviousState_TrackedOnTransition()
        {
            _sm.TransitionTo(ApplicationState.Bootstrapping);
            _sm.TransitionTo(ApplicationState.Authenticating);

            Assert.AreEqual(ApplicationState.Bootstrapping, _stateVariable.Value.PreviousState);
        }

        #endregion

        #region Helpers

        void AdvanceTo(ApplicationState target)
        {
            // Walk the happy path to reach the target state.
            var path = target switch
            {
                ApplicationState.Bootstrapping => new[] { ApplicationState.Bootstrapping },
                ApplicationState.Authenticating => new[] { ApplicationState.Bootstrapping, ApplicationState.Authenticating },
                ApplicationState.MainMenu => new[] { ApplicationState.Bootstrapping, ApplicationState.Authenticating, ApplicationState.MainMenu },
                ApplicationState.LoadingGame => new[] { ApplicationState.Bootstrapping, ApplicationState.Authenticating, ApplicationState.MainMenu, ApplicationState.LoadingGame },
                ApplicationState.InGame => new[] { ApplicationState.Bootstrapping, ApplicationState.Authenticating, ApplicationState.MainMenu, ApplicationState.LoadingGame, ApplicationState.InGame },
                ApplicationState.GameOver => new[] { ApplicationState.Bootstrapping, ApplicationState.Authenticating, ApplicationState.MainMenu, ApplicationState.LoadingGame, ApplicationState.InGame, ApplicationState.GameOver },
                _ => new ApplicationState[0],
            };

            foreach (var state in path)
            {
                bool ok = _sm.TransitionTo(state);
                Assert.IsTrue(ok, $"Failed to advance to {state} (current: {_sm.Current})");
            }
        }

        #endregion
    }
}
