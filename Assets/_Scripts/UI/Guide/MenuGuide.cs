using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The app shell's GUIDED PATH state (Docs/HomeHub/ARCHITECTURE.md §8).
    ///
    /// <para>While a guide is on, the player is shown the ONE thing they may press next, under a
    /// spotlight, with everything else dimmed and dead - except Settings, which is never taken
    /// away. They press it themselves. Nothing here navigates, selects or opens anything on the
    /// player's behalf: a player who walked the path once by hand can find it again, and a player
    /// who was carried there cannot.</para>
    ///
    /// <para>This class is the shared STATE the rest of the menu reads (so the pad's own shortcuts
    /// and a modal's B-to-close stand down, and the one card the guide points at can be pressed even
    /// if progression has it locked). The drawing is <see cref="MenuSpotlight"/>; the steps are
    /// whoever drives the guide (today the Quest Graph's <c>QuestGuideToMicrogameNode</c>).</para>
    /// </summary>
    public static class MenuGuide
    {
        /// <summary>True while a guided path is running.</summary>
        public static bool IsActive { get; private set; }

        /// <summary>The arcade mode the path leads to, if it leads to one.</summary>
        public static GameModes? TargetMode { get; private set; }

        /// <summary>Raised when a guide begins or ends.</summary>
        public static event Action Changed;

        /// <summary>Start a guide. <paramref name="targetMode"/> is the card the path ends on, if any.</summary>
        public static void Begin(GameModes? targetMode)
        {
            IsActive = true;
            TargetMode = targetMode;
            Changed?.Invoke();
        }

        /// <summary>End the guide. Idempotent.</summary>
        public static void End()
        {
            if (!IsActive) return;
            IsActive = false;
            TargetMode = null;
            Changed?.Invoke();
        }

        /// <summary>
        /// True when the guide must leave <paramref name="mode"/>'s card pressable even though
        /// progression (or an arcade funnel) has it locked: a path may always open the one door it
        /// points at.
        /// </summary>
        public static bool ExemptsMode(GameModes mode) => IsActive && TargetMode == mode;

        /// <summary>
        /// The windows a guide never takes away. Settings (and the credits it opens) must stay
        /// reachable on every screen, at every step - a player who cannot turn the volume down
        /// while being guided has been trapped, not guided.
        /// </summary>
        public static bool IsAlwaysAvailable(ScreenSwitcher.ModalWindows modal) =>
            modal is ScreenSwitcher.ModalWindows.SETTINGS or ScreenSwitcher.ModalWindows.CREDITS;

        /// <summary>
        /// The on-screen controls that open an always-available window, DERIVED rather than listed:
        /// every Button whose inspector-wired onClick calls <c>ModalWindowIn</c> on an
        /// always-available <see cref="ModalWindowManager"/>, every <see cref="MenuHubButton"/>
        /// targeting one, and anything carrying <see cref="MenuGuideAlwaysAvailable"/>. Derived so a
        /// Settings button moved, re-skinned or duplicated next week is still let through.
        /// </summary>
        public static void CollectAlwaysAvailable(List<RectTransform> into)
        {
            into.Clear();

            foreach (var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!button) continue;
                var evt = button.onClick;
                for (int i = 0; i < evt.GetPersistentEventCount(); i++)
                {
                    if (evt.GetPersistentTarget(i) is ModalWindowManager modal
                        && IsAlwaysAvailable(modal.ModalType)
                        && evt.GetPersistentMethodName(i) == nameof(ModalWindowManager.ModalWindowIn))
                    {
                        into.Add((RectTransform)button.transform);
                        break;
                    }
                }
            }

            foreach (var hub in UnityEngine.Object.FindObjectsByType<MenuHubButton>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (hub && IsAlwaysAvailable(hub.Target))
                    into.Add((RectTransform)hub.transform);

            foreach (var marker in UnityEngine.Object.FindObjectsByType<MenuGuideAlwaysAvailable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (marker)
                    into.Add((RectTransform)marker.transform);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsActive = false;
            TargetMode = null;
            Changed = null;
        }
    }
}
