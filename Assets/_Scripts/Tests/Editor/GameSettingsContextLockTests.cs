#if UNITY_EDITOR
using CosmicShore.Data;
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="GameSettingsPanelController.IsMenuState"/> - when the Settings window locks its Performance
    /// options and exit actions. Only a match locks them. The menu's app state is not always exactly MainMenu
    /// (unfocused: Paused; a network blip: Disconnected; play started without the boot chain: None), and
    /// locking on "not MainMenu" put the in-match banner and dead dropdowns in the main menu (2026-10-17).
    /// </summary>
    public class GameSettingsContextLockTests
    {
        static readonly ApplicationState[] Match =
            { ApplicationState.LoadingGame, ApplicationState.InGame, ApplicationState.GameOver };

        static readonly ApplicationState[] Menu =
            { ApplicationState.None, ApplicationState.Bootstrapping, ApplicationState.Authenticating, ApplicationState.MainMenu };

        [Test]
        public void TheMenuStates_AreUnlocked()
        {
            foreach (var s in Menu)
                Assert.IsTrue(GameSettingsPanelController.IsMenuState(s, ApplicationState.None), s.ToString());
        }

        [Test]
        public void AMatch_IsLocked()
        {
            foreach (var s in Match)
                Assert.IsFalse(GameSettingsPanelController.IsMenuState(s, ApplicationState.MainMenu), s.ToString());
        }

        [Test]
        public void AnInterruption_KeepsTheStateItIsAround()
        {
            foreach (var interruption in new[] { ApplicationState.Paused, ApplicationState.Disconnected })
            {
                Assert.IsTrue(GameSettingsPanelController.IsMenuState(interruption, ApplicationState.MainMenu),
                    $"{interruption} out of the menu must stay unlocked");
                Assert.IsFalse(GameSettingsPanelController.IsMenuState(interruption, ApplicationState.InGame),
                    $"{interruption} out of a match must stay locked");
            }
        }
    }
}
#endif
