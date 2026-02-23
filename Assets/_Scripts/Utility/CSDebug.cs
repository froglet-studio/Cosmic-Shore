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
        /// Current log level. Defaults to <see cref="CSLogLevel.All"/>.
        /// Set to <see cref="CSLogLevel.WarningsAndErrors"/> to suppress informational logs,
        /// or <see cref="CSLogLevel.Off"/> to suppress all output.
        /// </summary>
        public static CSLogLevel LogLevel = CSLogLevel.All;

        // ──────────────────────────────────────────────
        //  Log  (info / debug level)
        //  Stripped entirely in release builds.
        // ──────────────────────────────────────────────

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Log(object message)
        {
            if (LogLevel != CSLogLevel.All) return;
            Debug.Log(message);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Log(object message, Object context)
        {
            if (LogLevel != CSLogLevel.All) return;
            Debug.Log(message, context);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void LogFormat(string format, params object[] args)
        {
            if (LogLevel != CSLogLevel.All) return;
            Debug.LogFormat(format, args);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void LogFormat(Object context, string format, params object[] args)
        {
            if (LogLevel != CSLogLevel.All) return;
            Debug.LogFormat(context, format, args);
        }

        // ──────────────────────────────────────────────
        //  Warning
        //  Always compiled; respects runtime LogLevel.
        // ──────────────────────────────────────────────

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogWarning(object message)
        {
            if (LogLevel == CSLogLevel.Off) return;
            Debug.LogWarning(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogWarning(object message, Object context)
        {
            if (LogLevel == CSLogLevel.Off) return;
            Debug.LogWarning(message, context);
        }

        public static void LogWarningFormat(string format, params object[] args)
        {
            if (LogLevel == CSLogLevel.Off) return;
            Debug.LogWarningFormat(format, args);
        }

        public static void LogWarningFormat(Object context, string format, params object[] args)
        {
            if (LogLevel == CSLogLevel.Off) return;
            Debug.LogWarningFormat(context, format, args);
        }

        // ──────────────────────────────────────────────
        //  Error
        //  Always compiled; respects runtime LogLevel.
        // ──────────────────────────────────────────────

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogError(object message)
        {
            if (LogLevel == CSLogLevel.Off) return;
            Debug.LogError(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogError(object message, Object context)
        {
            if (LogLevel == CSLogLevel.Off) return;
            Debug.LogError(message, context);
        }

        public static void LogErrorFormat(string format, params object[] args)
        {
            if (LogLevel == CSLogLevel.Off) return;
            Debug.LogErrorFormat(format, args);
        }

        public static void LogErrorFormat(Object context, string format, params object[] args)
        {
            if (LogLevel == CSLogLevel.Off) return;
            Debug.LogErrorFormat(context, format, args);
        }
    }
}
