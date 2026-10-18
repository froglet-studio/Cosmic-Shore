using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
#endif

namespace CosmicShore.Utility
{
    /// <summary>
    /// The one switch for the "development build stuff" a player build carries: the
    /// <c>DiagnosticsHUD</c> overlay, the AI-training flight recorder, the load-insights host,
    /// Unity's on-screen developer console (the red error panel), and <see cref="CSDebug.Log"/>
    /// info logging. Turn it OFF and a development build plays like a release build - while it
    /// stays a development build, so Android's Patch / Patch And Run still work on it.
    ///
    /// <para><b>How to flip it on a device:</b> hold four fingers on the screen for two seconds
    /// (or Shift+F7 on a keyboard), or press <c>Off</c> on the diagnostics HUD. The choice is
    /// stored in PlayerPrefs, so it survives every Patch and reinstall-over-the-top until the
    /// app's data is cleared. A build compiled with the scripting define
    /// <c>CS_DEV_DIAGNOSTICS_OFF</c> (a Build Profile's Scripting Defines) starts OFF on a fresh
    /// install instead of ON.</para>
    ///
    /// <para><b>Scope:</b> the Editor starts every Play session ON and never persists the
    /// choice, so nobody's editor HUD vanishes because of a phone test. A release build answers
    /// <c>false</c> and compiles none of the rest. What this does NOT change: Unity's
    /// "Development Build" watermark (Unity has no API for it), profiler autoconnect and script
    /// debugging (Build Profile options - leave them unticked for a normal-feeling build), and
    /// asset-authored test shortcuts (the Episode Token config's unverified grants, the Weekly
    /// Challenge catalogue's test block), which are switched on their own assets.</para>
    /// </summary>
    public static class DevDiagnostics
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>PlayerPrefs key holding the device's choice (1 = on, 0 = off).</summary>
        public const string PrefKey = "CosmicShore.DevDiagnostics";

#if CS_DEV_DIAGNOSTICS_OFF
        const bool BuildDefault = false;
#else
        const bool BuildDefault = true;
#endif

        // -1 = not read yet this session. Read lazily so the RuntimeInitializeOnLoad order of
        // the systems that ask does not matter.
        static int s_state = -1;
        static bool s_logEnabledBeforeOff = true;

        /// <summary>Raised with the new value whenever <see cref="SetEnabled"/> changes it.</summary>
        public static event Action<bool> Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_state = -1;
            s_logEnabledBeforeOff = true;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            // Only an OFF start is applied: an ON start leaves CSDebug.LogEnabled and the developer
            // console as whatever already set them (the Editor's Logging toolbox, Unity's defaults).
            if (!Enabled) Apply(false);
            DevDiagnosticsToggle.EnsureSpawned();
        }

        /// <summary>True when the development-build diagnostics should run.</summary>
        public static bool Enabled
        {
            get
            {
                if (s_state < 0)
                {
#if UNITY_EDITOR
                    s_state = 1;
#else
                    s_state = PlayerPrefs.GetInt(PrefKey, BuildDefault ? 1 : 0) != 0 ? 1 : 0;
#endif
                }
                return s_state == 1;
            }
        }

        /// <summary>
        /// Turns the diagnostics on or off now. A player stores the choice for the next launch;
        /// the Editor keeps it for this Play session only.
        /// </summary>
        public static void SetEnabled(bool on)
        {
            if (Enabled == on) return;
            s_state = on ? 1 : 0;
#if !UNITY_EDITOR
            PlayerPrefs.SetInt(PrefKey, s_state);
            PlayerPrefs.Save();
#endif
            Apply(on);
            Changed?.Invoke(on);
        }

        public static void Toggle() => SetEnabled(!Enabled);

        static void Apply(bool on)
        {
            if (on)
            {
                CSDebug.LogEnabled = s_logEnabledBeforeOff;
            }
            else
            {
                s_logEnabledBeforeOff = CSDebug.LogEnabled;
                CSDebug.LogEnabled = false;
            }
#if !UNITY_EDITOR
            // Unity's own on-screen console that a development player pops open on an error.
            Debug.developerConsoleEnabled = on;
            if (!on) Debug.developerConsoleVisible = false;
#endif
        }
#else
        /// <summary>Release builds carry no development diagnostics.</summary>
        public static bool Enabled => false;
#endif
    }
}
