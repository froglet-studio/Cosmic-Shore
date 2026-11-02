using UnityEngine;
#if UNITY_EDITOR
using CosmicShore.Utility;
using UnityEditor;
#endif

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Lets the Editor fly by touch so Unity Remote 5 can test phone controls without a build.
    /// The Editor reports <c>DeviceType.Desktop</c> even with Remote attached, so every
    /// "is this a phone?" check (InputController, InputDeviceActuation, KitInputDevice) also asks
    /// <see cref="IsForced"/>. True only in the Editor, when Unity Remote is connected or the
    /// menu toggle <b>Cosmic Shore/Touch/Force Touch In Editor</b> is on. Always false in players,
    /// so builds behave exactly as before.
    /// </summary>
    public static class EditorTouchOverride
    {
#if UNITY_EDITOR
        const string PrefKey = "CosmicShore.Touch.ForceTouchInEditor";
        const string MenuPath = "Cosmic Shore/Touch/Force Touch In Editor";
        static bool lastState;

        public static bool IsForced
        {
            get
            {
                bool forced = EditorApplication.isRemoteConnected || EditorPrefs.GetBool(PrefKey, false);
                if (forced != lastState)
                {
                    lastState = forced;
                    CSDebug.Log($"[EditorTouchOverride] Touch input in Editor {(forced ? "ON" : "OFF")} " +
                                $"(remote={EditorApplication.isRemoteConnected}, menu={EditorPrefs.GetBool(PrefKey, false)})");
                }
                return forced;
            }
        }

        [MenuItem(MenuPath)]
        static void Toggle() => EditorPrefs.SetBool(PrefKey, !EditorPrefs.GetBool(PrefKey, false));

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PrefKey, false));
            return true;
        }
#else
        public static bool IsForced => false;
#endif
    }
}
