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

        public static bool isDebugBuild =>
#if DEBUG
            true;
#else
            false;
#endif

        public static void Log(object message) => Sink.Write(LogType.Log, message?.ToString(), null);
        public static void Log(object message, Object context) => Sink.Write(LogType.Log, message?.ToString(), context);
        public static void LogFormat(string format, params object[] args) => Sink.Write(LogType.Log, string.Format(format, args), null);
        public static void LogFormat(Object context, string format, params object[] args) => Sink.Write(LogType.Log, string.Format(format, args), context);

        public static void LogWarning(object message) => Sink.Write(LogType.Warning, message?.ToString(), null);
        public static void LogWarning(object message, Object context) => Sink.Write(LogType.Warning, message?.ToString(), context);
        public static void LogWarningFormat(string format, params object[] args) => Sink.Write(LogType.Warning, string.Format(format, args), null);
        public static void LogWarningFormat(Object context, string format, params object[] args) => Sink.Write(LogType.Warning, string.Format(format, args), context);

        public static void LogError(object message) => Sink.Write(LogType.Error, message?.ToString(), null);
        public static void LogError(object message, Object context) => Sink.Write(LogType.Error, message?.ToString(), context);
        public static void LogErrorFormat(string format, params object[] args) => Sink.Write(LogType.Error, string.Format(format, args), null);
        public static void LogErrorFormat(Object context, string format, params object[] args) => Sink.Write(LogType.Error, string.Format(format, args), context);

        public static void LogException(Exception exception) => Sink.Write(LogType.Exception, exception?.Message, null, exception);
        public static void LogException(Exception exception, Object context) => Sink.Write(LogType.Exception, exception?.Message, context, exception);

        public static void Assert(bool condition, string message = "Assertion failed")
        {
            if (!condition) Sink.Write(LogType.Assert, message, null);
        }
    }
}
