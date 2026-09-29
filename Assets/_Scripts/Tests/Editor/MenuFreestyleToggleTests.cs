#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Tests for the Menu ↔ Freestyle toggle flow in Menu_Main.
    ///
    /// Validates:
    ///   - <see cref="Gameplay.MenuCrystalClickHandler"/> has the expected ownership guard
    ///   - <see cref="Gameplay.MenuCrystalClickHandler.IsMultiplayerSession"/> exists as a static helper
    ///   - <see cref="Core.MainMenuController"/> subscribes to both freestyle SOAP events
    ///   - State and camera coupling: freestyle events trigger both state transition AND camera switch
    /// </summary>
    [TestFixture]
    public class MenuFreestyleToggleTests
    {
        #region MenuCrystalClickHandler Guards

        [Test]
        public void MenuCrystalClickHandler_HasIsMultiplayerSessionMethod()
        {
            var method = typeof(Gameplay.MenuCrystalClickHandler)
                .GetMethod("IsMultiplayerSession", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method,
                "MenuCrystalClickHandler should have a static IsMultiplayerSession() method " +
                "to guard PauseSystem usage in multiplayer context.");
        }

        [Test]
        public void MenuCrystalClickHandler_IsMultiplayerSession_ReturnsBool()
        {
            var method = typeof(Gameplay.MenuCrystalClickHandler)
                .GetMethod("IsMultiplayerSession", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method);
            Assert.AreEqual(typeof(bool), method.ReturnType,
                "IsMultiplayerSession should return bool.");
        }

        [Test]
        public void MenuCrystalClickHandler_IsMultiplayerSession_ReturnsFalse_WhenNoNetworkManager()
        {
            // In edit mode, NetworkManager.Singleton is null - should return false (not throw).
            var method = typeof(Gameplay.MenuCrystalClickHandler)
                .GetMethod("IsMultiplayerSession", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method);
            bool result = (bool)method.Invoke(null, null);
            Assert.IsFalse(result,
                "IsMultiplayerSession should return false when NetworkManager.Singleton is null.");
        }

        [Test]
        public void MenuCrystalClickHandler_HasFreestyleEventsField()
        {
            var field = typeof(Gameplay.MenuCrystalClickHandler)
                .GetField("freestyleEvents", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field,
                "MenuCrystalClickHandler should have a freestyleEvents SerializeField " +
                "for raising SOAP events on state toggle.");
        }

        [Test]
        public void MenuCrystalClickHandler_HasIsInFreestyleProperty()
        {
            var prop = typeof(Gameplay.MenuCrystalClickHandler)
                .GetProperty("IsInFreestyle", BindingFlags.Instance | BindingFlags.Public);

            Assert.IsNotNull(prop);
            Assert.AreEqual(typeof(bool), prop.PropertyType);
            Assert.IsTrue(prop.CanRead);
        }

        /// <summary>
        /// The exit transition must clear <c>_isInFreestyle</c> BEFORE it raises
        /// <c>OnMenuStateTransitionStart</c>, because subscribers of that event read the flag back
        /// as "are we still flying?" while handling it.
        ///
        /// <para>It used to be cleared after the camera blend's await, so every such subscriber was
        /// told YES on the way out. <c>ScreenSwitcher.HandleExitFreestyle</c> is the one that paid:
        /// it recomputes the home hub's visibility from that answer, so exiting freestyle left the
        /// hub row (Mission / Toy Box / Arena / Arcade) faded out and non-interactable, with nothing
        /// scheduled to recompute it.</para>
        ///
        /// <para>Source-order rather than behaviour, because the transition is an async
        /// <c>UniTaskVoid</c> driven by a live vessel, a camera controller and an injected SOAP
        /// container - none of which exist in edit mode.</para>
        /// </summary>
        [Test]
        public void TransitionToMenu_ClearsFreestyleFlag_BeforeRaisingTransitionStart()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Scripts/Controller/Multiplayer/MenuCrystalClickHandler.cs");
            Assert.IsTrue(File.Exists(path), $"Expected MenuCrystalClickHandler at {path}");

            string source = File.ReadAllText(path);

            int method = source.IndexOf("UniTaskVoid TransitionToMenu()", System.StringComparison.Ordinal);
            Assert.Greater(method, -1, "TransitionToMenu() not found.");

            int raise = source.IndexOf("OnMenuStateTransitionStart.Raise()", method, System.StringComparison.Ordinal);
            Assert.Greater(raise, -1, "TransitionToMenu() should raise OnMenuStateTransitionStart.");

            int clear = source.IndexOf("_isInFreestyle = false", method, System.StringComparison.Ordinal);
            Assert.Greater(clear, -1, "TransitionToMenu() should clear _isInFreestyle.");

            Assert.Less(clear, raise,
                "_isInFreestyle must be cleared BEFORE OnMenuStateTransitionStart is raised - " +
                "subscribers read IsInFreestyle while handling that event, and a stale 'true' " +
                "leaves the home hub buttons disabled after returning from freestyle.");
        }

        #endregion

        #region MainMenuController Freestyle Event Handling

        [Test]
        public void MainMenuController_HasHandleEnterFreestyleMethod()
        {
            var method = typeof(Core.MainMenuController)
                .GetMethod("HandleEnterFreestyle", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method,
                "MainMenuController should have HandleEnterFreestyle to combine " +
                "state transition (→ Freestyle) with camera switching.");
        }

        [Test]
        public void MainMenuController_HasHandleExitFreestyleMethod()
        {
            var method = typeof(Core.MainMenuController)
                .GetMethod("HandleExitFreestyle", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method,
                "MainMenuController should have HandleExitFreestyle to combine " +
                "state transition (→ Ready) with camera switching.");
        }

        [Test]
        public void MainMenuController_HasCurrentStateProperty()
        {
            var prop = typeof(Core.MainMenuController)
                .GetProperty("CurrentState", BindingFlags.Instance | BindingFlags.Public);

            Assert.IsNotNull(prop);
            Assert.AreEqual(typeof(Data.MainMenuState), prop.PropertyType);
        }

        [Test]
        public void MainMenuController_HasOnStateChangedEvent()
        {
            var evt = typeof(Core.MainMenuController)
                .GetEvent("OnStateChanged", BindingFlags.Instance | BindingFlags.Public);

            Assert.IsNotNull(evt,
                "MainMenuController should expose OnStateChanged event for UI systems " +
                "to react to menu state changes including freestyle transitions.");
        }

        #endregion

        #region MenuFreestyleEventsContainerSO Structure

        [Test]
        public void MenuFreestyleEventsContainerSO_HasOnGameStateTransitionStart()
        {
            var field = typeof(ScriptableObjects.MenuFreestyleEventsContainerSO)
                .GetField("OnGameStateTransitionStart", BindingFlags.Instance | BindingFlags.Public);

            Assert.IsNotNull(field,
                "MenuFreestyleEventsContainerSO must have OnGameStateTransitionStart SOAP event.");
        }

        [Test]
        public void MenuFreestyleEventsContainerSO_HasOnGameStateTransitionEnd()
        {
            var field = typeof(ScriptableObjects.MenuFreestyleEventsContainerSO)
                .GetField("OnGameStateTransitionEnd", BindingFlags.Instance | BindingFlags.Public);

            Assert.IsNotNull(field,
                "MenuFreestyleEventsContainerSO must have OnGameStateTransitionEnd SOAP event.");
        }

        [Test]
        public void MenuFreestyleEventsContainerSO_HasOnMenuStateTransitionStart()
        {
            var field = typeof(ScriptableObjects.MenuFreestyleEventsContainerSO)
                .GetField("OnMenuStateTransitionStart", BindingFlags.Instance | BindingFlags.Public);

            Assert.IsNotNull(field,
                "MenuFreestyleEventsContainerSO must have OnMenuStateTransitionStart SOAP event.");
        }

        [Test]
        public void MenuFreestyleEventsContainerSO_HasOnMenuStateTransitionEnd()
        {
            var field = typeof(ScriptableObjects.MenuFreestyleEventsContainerSO)
                .GetField("OnMenuStateTransitionEnd", BindingFlags.Instance | BindingFlags.Public);

            Assert.IsNotNull(field,
                "MenuFreestyleEventsContainerSO must have OnMenuStateTransitionEnd SOAP event.");
        }

        #endregion
    }
}
#endif
