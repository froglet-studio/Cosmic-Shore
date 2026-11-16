using System;
using System.Reflection;
using CosmicShore.Engine;

namespace CosmicShore.Player
{
    /// <summary>
    /// The two first-run prompts a headless run has nobody to answer, answered the way the game's own soak
    /// runners do (<c>SwitchbackSoakRunner</c>, <c>ArkEscortSim.DismissPrivacyOverlay</c>):
    /// <list type="bullet">
    /// <item>the privacy overlay (age gate + consent) is closed UNANSWERED through its own
    /// <c>CloseUnanswered</c>, so nothing is granted and analytics stays off, and the Authentication scene
    /// that waits on it with no timeout hears it end;</item>
    /// <item>a fresh anonymous account that parks on the username step is sent straight to the menu (what the
    /// scene's safety timeout does), so no name is claimed for a throwaway account.</item>
    /// </list>
    /// Without this every unattended run (<c>--train</c>, <c>--race-judge</c>) sat in the Authentication scene
    /// forever: the overlay's wait was added to the boot after the trainer was written (2026-11).
    /// It is used only by runs that have no person; a windowed or <c>--arcade</c> run still answers the prompts.
    /// </summary>
    internal static class UnattendedBoot
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static bool _skippedName;

        /// <summary>Call once per frame while the run has not reached the menu.</summary>
        public static void Tick()
        {
            var privacy = CosmicShore.UI.PrivacyConsentOverlay.Current;
            if (privacy)
            {
                Console.WriteLine("[unattended] closing the unanswered privacy overlay (nothing granted; analytics stays off)");
                privacy.CloseUnanswered();
            }

            if (_skippedName) return;
            var auth = CosmicShore.Engine.Object.FindAnyObjectByType<CosmicShore.Core.AuthenticationSceneController>();
            if (!auth) return;
            if (auth.GetType().GetField("_awaitingPlayerInput", Any)?.GetValue(auth) is not true) return;
            _skippedName = true;
            Console.WriteLine("[unattended] skipping the username step (no name claimed)");
            auth.GetType().GetMethod("NavigateToMainMenu", Any, null, Type.EmptyTypes, null)?.Invoke(auth, null);
        }
    }
}
