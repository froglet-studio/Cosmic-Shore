using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

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

        /// <summary>
        /// Info-level log with the stack trace suppressed.
        ///
        /// <para>
        /// For recurring diagnostics whose call site is fixed and already named in
        /// the message. Unity attaches a full managed stack to every
        /// <see cref="Debug.Log"/>, and for a line that fires on a timer inside a
        /// deep async chain that stack is ~100 console lines of UGS/UniTask
        /// plumbing per occurrence - which buries the very signal the diagnostic
        /// exists to surface, and costs a real stack walk each time.
        /// </para>
        ///
        /// <para>
        /// Use for periodic/telemetry lines. Do NOT use for anything you would
        /// want to debug from - a genuine error still wants its stack.
        /// </para>
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void LogNoStack(string message)
        {
            if (!LogEnabled) return;
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", message);
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
