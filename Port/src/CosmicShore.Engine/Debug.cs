using System;

namespace CosmicShore.Engine
{
    public enum LogType
    {
        Log = 0,
        Warning = 1,
        Error = 2,
        Exception = 3,
        Assert = 4,
    }

    /// <summary>Pluggable destination for engine log output.</summary>
    public interface ILogSink
    {
        void Write(LogType type, string message, Object context, Exception exception = null);
    }

    /// <summary>Default sink: severity-tagged lines on the console. Thread-safe.</summary>
    public sealed class ConsoleLogSink : ILogSink
    {
        public void Write(LogType type, string message, Object context, Exception exception = null)
        {
            string tag = type switch
            {
                LogType.Warning => "WARN ",
                LogType.Error => "ERROR",
                LogType.Exception => "EXCPT",
                LogType.Assert => "ASSRT",
                _ => "INFO ",
            };
            string ctx = context is not null ? $" [{context.name}]" : string.Empty;
            string line = exception is null
                ? $"[{tag}]{ctx} {message}"
                : $"[{tag}]{ctx} {message}\n{exception}";
            Console.WriteLine(line);
        }
    }

    /// <summary>Test/CLI sink that records every entry for inspection.</summary>
    public sealed class CapturingLogSink : ILogSink
    {
        public readonly System.Collections.Generic.List<(LogType Type, string Message, Object Context, Exception Exception)> Entries = new();

        public void Write(LogType type, string message, Object context, Exception exception = null)
        {
            lock (Entries) Entries.Add((type, message, context, exception));
        }
    }

    /// <summary>
    /// Engine logging facade with the API surface ported code expects. Output routes
    /// through <see cref="Sink"/> — swap it for capture in tests or structured output later.
    /// </summary>
    public static class Debug
    {
        public static ILogSink Sink = new ConsoleLogSink();

        /// <summary>The engine's default logger (original contract: Debug.unityLogger).</summary>
        public static ILogger unityLogger { get; } = new Logger();

        [ThreadStatic] static bool t_raising;

        /// <summary>Writes to the sink, then raises Application.logMessageReceived (original ordering).</summary>
        internal static void Emit(LogType type, string message, Object context, Exception exception = null)
        {
            if (!unityLogger.logEnabled || !unityLogger.IsLogTypeAllowed(type)) return;
            Sink.Write(type, message, context, exception);
            if (t_raising) return; // a handler that logs must not recurse
            t_raising = true;
            try { Application.RaiseLog(message ?? string.Empty, exception?.StackTrace ?? string.Empty, type); }
            catch (Exception) { }
            finally { t_raising = false; }
        }

        public static bool isDebugBuild =>
#if DEBUG
            true;
#else
            false;
#endif

        public static void Log(object message) => Emit(LogType.Log, message?.ToString(), null);
        public static void Log(object message, Object context) => Emit(LogType.Log, message?.ToString(), context);
        public static void LogFormat(string format, params object[] args) => Emit(LogType.Log, string.Format(format, args), null);
        public static void LogFormat(Object context, string format, params object[] args) => Emit(LogType.Log, string.Format(format, args), context);

        public static void LogWarning(object message) => Emit(LogType.Warning, message?.ToString(), null);
        public static void LogWarning(object message, Object context) => Emit(LogType.Warning, message?.ToString(), context);
        public static void LogWarningFormat(string format, params object[] args) => Emit(LogType.Warning, string.Format(format, args), null);
        public static void LogWarningFormat(Object context, string format, params object[] args) => Emit(LogType.Warning, string.Format(format, args), context);

        public static void LogError(object message) => Emit(LogType.Error, message?.ToString(), null);
        public static void LogError(object message, Object context) => Emit(LogType.Error, message?.ToString(), context);
        public static void LogErrorFormat(string format, params object[] args) => Emit(LogType.Error, string.Format(format, args), null);
        public static void LogErrorFormat(Object context, string format, params object[] args) => Emit(LogType.Error, string.Format(format, args), context);

        public static void LogException(Exception exception) => Emit(LogType.Exception, exception?.Message, null, exception);
        public static void LogException(Exception exception, Object context) => Emit(LogType.Exception, exception?.Message, context, exception);

        // Editor-visualization no-ops (rays render once a debug-draw layer exists).
        public static void DrawLine(Vector3 start, Vector3 end, Color color = default, float duration = 0f) { }
        public static void DrawRay(Vector3 start, Vector3 dir, Color color = default, float duration = 0f) { }

        public static void Assert(bool condition, string message = "Assertion failed")
        {
            if (!condition) Emit(LogType.Assert, message, null);
        }
    }
}

namespace CosmicShore.Engine
{
    /// <summary>Original contract: UnityEngine.ILogHandler.</summary>
    public interface ILogHandler
    {
        void LogFormat(LogType logType, Object context, string format, params object[] args);
        void LogException(Exception exception, Object context);
    }

    /// <summary>Original contract: UnityEngine.ILogger.</summary>
    public interface ILogger : ILogHandler
    {
        ILogHandler logHandler { get; set; }
        bool logEnabled { get; set; }
        LogType filterLogType { get; set; }
        bool IsLogTypeAllowed(LogType logType);
        void Log(LogType logType, object message);
        void Log(LogType logType, object message, Object context);
        void Log(LogType logType, string tag, object message);
        void Log(LogType logType, string tag, object message, Object context);
        void Log(object message);
        void Log(string tag, object message);
        void Log(string tag, object message, Object context);
        void LogWarning(string tag, object message);
        void LogWarning(string tag, object message, Object context);
        void LogError(string tag, object message);
        void LogError(string tag, object message, Object context);
        void LogFormat(LogType logType, string format, params object[] args);
        void LogException(Exception exception);
    }

    /// <summary>
    /// Original contract: UnityEngine.Logger. <see cref="filterLogType"/> keeps a message when
    /// its severity is at or above the filter in the engine's order (Exception is always kept
    /// unless the filter is Exception-only), and the default handler is the engine log.
    /// </summary>
    public class Logger : ILogger
    {
        sealed class EngineHandler : ILogHandler
        {
            public void LogFormat(LogType logType, Object context, string format, params object[] args)
                => Debug.Sink.Write(logType, args == null || args.Length == 0 ? format : string.Format(format, args), context);
            public void LogException(Exception exception, Object context)
                => Debug.Sink.Write(LogType.Exception, exception?.Message, context, exception);
        }

        public Logger() { logHandler = new EngineHandler(); }
        public Logger(ILogHandler logHandler) { this.logHandler = logHandler; }

        public ILogHandler logHandler { get; set; }
        public bool logEnabled { get; set; } = true;
        public LogType filterLogType { get; set; } = LogType.Log;

        public bool IsLogTypeAllowed(LogType logType)
        {
            if (!logEnabled) return false;
            if (logType == LogType.Exception && filterLogType != LogType.Exception) return true;
            if (filterLogType == LogType.Exception) return logType == LogType.Exception;
            return Rank(logType) >= Rank(filterLogType);
        }

        // Severity order: Log < Warning < Assert < Error < Exception.
        static int Rank(LogType t) => t switch { LogType.Log => 0, LogType.Warning => 1, LogType.Assert => 2, LogType.Error => 3, _ => 4 };

        static string Text(object message) => message is null ? "Null" : message.ToString();

        public void Log(LogType logType, object message) => Log(logType, message, null);
        public void Log(LogType logType, object message, Object context)
        {
            if (!IsLogTypeAllowed(logType)) return;
            if (ReferenceEquals(this, Debug.unityLogger)) Debug.Emit(logType, Text(message), context);
            else logHandler.LogFormat(logType, context, "{0}", Text(message));
        }
        public void Log(LogType logType, string tag, object message) => Log(logType, tag, message, null);
        public void Log(LogType logType, string tag, object message, Object context) => Log(logType, (object)$"{tag}: {Text(message)}", context);
        public void Log(object message) => Log(LogType.Log, message);
        public void Log(string tag, object message) => Log(LogType.Log, tag, message);
        public void Log(string tag, object message, Object context) => Log(LogType.Log, tag, message, context);
        public void LogWarning(string tag, object message) => Log(LogType.Warning, tag, message);
        public void LogWarning(string tag, object message, Object context) => Log(LogType.Warning, tag, message, context);
        public void LogError(string tag, object message) => Log(LogType.Error, tag, message);
        public void LogError(string tag, object message, Object context) => Log(LogType.Error, tag, message, context);
        public void LogFormat(LogType logType, string format, params object[] args) => LogFormat(logType, null, format, args);
        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            if (!IsLogTypeAllowed(logType)) return;
            Log(logType, (object)(args == null || args.Length == 0 ? format : string.Format(format, args)), context);
        }
        public void LogException(Exception exception) => LogException(exception, null);
        public void LogException(Exception exception, Object context)
        {
            if (!logEnabled) return;
            if (ReferenceEquals(this, Debug.unityLogger)) Debug.Emit(LogType.Exception, exception?.Message, context, exception);
            else logHandler.LogException(exception, context);
        }
    }
}
