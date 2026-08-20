using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

// NOTE: deliberately NO `using System;` here. Every Log overload below takes a
// UnityEngine.Object context by its short name, and importing System makes bare `Object`
// ambiguous with System.Object (CS0104) on all seven of them. [System.Flags] is spelled out
// for the same reason.

namespace CosmicShore.Utility
{
    /// <summary>
    /// Controls which log levels are active at runtime.
    /// </summary>
    public enum CSLogLevel
    {
        /// <summary>All logs enabled (Log, Warning, Error).</summary>
        All = 0,
        /// <summary>Only warnings and errors are logged. Debug.Log calls are suppressed.</summary>
        WarningsAndErrors = 1,
        /// <summary>All logging is disabled.</summary>
        Off = 2
    }

    /// <summary>
    /// Opt-in diagnostic channels for BRING-UP TELEMETRY — the dense per-step traces a system
    /// needs while it is being built, which are noise for everyone else once it works.
    ///
    /// <para>The rule: a trace that answers "why did this system not do the thing" belongs on a
    /// channel (<see cref="CSDebug.LogVerbose"/>), not on <see cref="CSDebug.Log"/>. Channels
    /// default to <see cref="None"/>, so a finished system is silent until someone turns its
    /// channel back on in <c>FrogletTools &gt; Toolbox &gt; Logging</c>. That is what keeps a
    /// past development cycle's instrumentation from being either console spam or deleted
    /// knowledge.</para>
    ///
    /// <para>Only add a member when you are converting real call sites onto it — an unused
    /// channel is a promise the toolbox cannot keep.</para>
    /// </summary>
    [System.Flags]
    public enum CSLogChannel
    {
        None = 0,
        /// <summary>
        /// <c>[FLOW-n]</c> — the numbered player/vessel spawn + session bring-up trace across
        /// MultiplayerSetup, the vessel initializers, SceneLoader and the minigame controllers.
        /// </summary>
        NetworkFlow = 1 << 0,
        /// <summary>
        /// <c>[GyroidColony]</c> — octagon-colony lattice telemetry (founder claims, the 5s
        /// population heartbeat). Coherence DEFECTS stay on the warning channel and are
        /// unaffected by this flag.
        /// </summary>
        GyroidColony = 1 << 1,
        /// <summary>
        /// <c>[QuasicrystalColony]</c> — star-colony lattice telemetry (founder claims, plant
        /// completions, births). Defect warnings (blocked reseed mints) stay on the warning
        /// channel and are unaffected by this flag.
        /// </summary>
        QuasicrystalColony = 1 << 2,
        /// <c>[ScarabNucleusField]</c> — the Scarab nucleus-seeding ability: seeds planted, balls
        /// knocked in or out, and the overload detonation. Off by default like every channel; a
        /// real fault here is still a warning and is unaffected by this flag.
        /// </summary>
        ScarabNucleus = 1 << 3,
        /// <summary>
        /// <c>[ScarabSwitch]</c> / <c>[PlaceSwitch]</c> — the Scarab's switch: placements,
        /// refusals, and the wing dais a strike pays out. Off by default like every channel;
        /// a real fault here is still a warning and is unaffected by this flag.
        /// </summary>
        ScarabSwitch = 1 << 4,
        /// <summary>
        /// <c>[ScarabJuke]</c> / <c>[ScarabCavitation]</c> — the right-stick dash and the swept
        /// cavitation plate that rides it (fire direction, plate radius, cooldown). Both used to
        /// log unconditionally on every dash, which is per-input console spam for a finished
        /// system. Off by default like every channel; a real fault here is still an error and is
        /// unaffected by this flag.
        /// </summary>
        ScarabDash = 1 << 5,
        All = ~0
    }

    /// <summary>
    /// Centralized debug logger for Cosmic Shore.
    ///
    /// Features:
    /// - Runtime log level control via <see cref="LogLevel"/> property.
    /// - In release builds (non-Editor, non-Development), all <c>Log</c> and <c>LogFormat</c>
    ///   calls are stripped entirely by the compiler via [Conditional] attributes,
    ///   eliminating both the method call and argument evaluation at the call site.
    /// - Warnings and errors are always compiled in but respect the runtime <see cref="LogLevel"/>.
    ///
    /// Usage:
    ///   CSDebug.Log("hello");                       // same as Debug.Log
    ///   CSDebug.LogWarning("careful", this);         // same as Debug.LogWarning with context
    ///   CSDebug.LogLevel = CSLogLevel.WarningsAndErrors;  // suppress info logs
    ///   CSDebug.LogLevel = CSLogLevel.Off;                // silence everything
    /// </summary>
    public static class CSDebug
    {
        /// <summary>
        /// Per-type flags for granular control. Toggle individual log types on/off.
        /// </summary>
        public static bool LogEnabled = true;
        public static bool WarningsEnabled = true;
        public static bool ErrorsEnabled = true;

        /// <summary>
        /// Convenience property for preset log levels.
        /// Getter derives the closest preset from the individual flags.
        /// Setter applies the preset by setting all flags at once.
        /// </summary>
        public static CSLogLevel LogLevel
        {
            get
            {
                if (LogEnabled && WarningsEnabled && ErrorsEnabled) return CSLogLevel.All;
                if (!LogEnabled && WarningsEnabled && ErrorsEnabled) return CSLogLevel.WarningsAndErrors;
                if (!LogEnabled && !WarningsEnabled && !ErrorsEnabled) return CSLogLevel.Off;
                // Custom combination that doesn't map to a preset; treat as All.
                return CSLogLevel.All;
            }
            set
            {
                switch (value)
                {
                    case CSLogLevel.All:
                        LogEnabled = true;
                        WarningsEnabled = true;
                        ErrorsEnabled = true;
                        break;
                    case CSLogLevel.WarningsAndErrors:
                        LogEnabled = false;
                        WarningsEnabled = true;
                        ErrorsEnabled = true;
                        break;
                    case CSLogLevel.Off:
                        LogEnabled = false;
                        WarningsEnabled = false;
                        ErrorsEnabled = false;
                        break;
                }
            }
        }

        // ──────────────────────────────────────────────
        //  Log  (info / debug level)
        //  Stripped entirely in release builds.
        // ──────────────────────────────────────────────

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Log(object message)
        {
            if (!LogEnabled) return;
            Debug.Log(message);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Log(object message, Object context)
        {
            if (!LogEnabled) return;
            Debug.Log(message, context);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void LogFormat(string format, params object[] args)
        {
            if (!LogEnabled) return;
            Debug.LogFormat(format, args);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void LogFormat(Object context, string format, params object[] args)
        {
            if (!LogEnabled) return;
            Debug.LogFormat(context, format, args);
        }

        // ──────────────────────────────────────────────
        //  LogVerbose  (opt-in diagnostic channels)
        //  Stripped entirely in release builds; silent
        //  in the Editor until the channel is enabled.
        // ──────────────────────────────────────────────

        /// <summary>
        /// Which bring-up channels are currently emitting. Defaults to
        /// <see cref="CSLogChannel.None"/> — a finished system stays silent without deleting the
        /// trace that made it work. Toggled from FrogletTools &gt; Toolbox &gt; Logging.
        /// </summary>
        public static CSLogChannel VerboseChannels = CSLogChannel.None;

        /// <summary>
        /// True when <paramref name="channel"/> is emitting. Guard with this — rather than
        /// calling <see cref="LogVerbose"/> directly — anywhere the MESSAGE ITSELF is expensive
        /// to build (string interpolation in a per-frame or per-contact path): the arguments of a
        /// [Conditional] call are still evaluated in the Editor, so an unguarded interpolated
        /// string costs its allocation on every call even while the channel is off.
        /// </summary>
        public static bool IsVerbose(CSLogChannel channel)
            => LogEnabled && (VerboseChannels & channel) != 0;

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogVerbose(CSLogChannel channel, object message)
        {
            if (!IsVerbose(channel)) return;
            Debug.Log(message);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogVerbose(CSLogChannel channel, object message, Object context)
        {
            if (!IsVerbose(channel)) return;
            Debug.Log(message, context);
        }

        // ──────────────────────────────────────────────
        //  Warning
        //  Always compiled; respects runtime LogLevel.
        // ──────────────────────────────────────────────

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogWarning(object message)
        {
            if (!WarningsEnabled) return;
            Debug.LogWarning(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogWarning(object message, Object context)
        {
            if (!WarningsEnabled) return;
            Debug.LogWarning(message, context);
        }

        public static void LogWarningFormat(string format, params object[] args)
        {
            if (!WarningsEnabled) return;
            Debug.LogWarningFormat(format, args);
        }

        public static void LogWarningFormat(Object context, string format, params object[] args)
        {
            if (!WarningsEnabled) return;
            Debug.LogWarningFormat(context, format, args);
        }

        // ──────────────────────────────────────────────
        //  Error
        //  Always compiled; respects runtime LogLevel.
        // ──────────────────────────────────────────────

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogError(object message)
        {
            if (!ErrorsEnabled) return;
            Debug.LogError(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogError(object message, Object context)
        {
            if (!ErrorsEnabled) return;
            Debug.LogError(message, context);
        }

        public static void LogErrorFormat(string format, params object[] args)
        {
            if (!ErrorsEnabled) return;
            Debug.LogErrorFormat(format, args);
        }

        public static void LogErrorFormat(Object context, string format, params object[] args)
        {
            if (!ErrorsEnabled) return;
            Debug.LogErrorFormat(context, format, args);
        }
    }
}
