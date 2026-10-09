#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Suppresses the single benign <see cref="ArgumentOutOfRangeException"/> the UGS Lobby SDK
    /// logs from <c>LobbyPatcher.ApplyPatchesToLobby</c> when a WebSocket "lobby changed" delta
    /// references a stale player/data index.
    ///
    /// The SDK throws, catches, and logs this on its own event task
    /// (<c>LobbyChannel.HandleLobbyChanges</c>) before any of our awaits, so it cannot be
    /// try/caught the way HostConnectionService's refresh catches handle the same error
    /// on its own refresh path. It reaches Unity through the <c>LogFormat</c> route
    /// (Debug.LogError / unityLogger.Log(LogType.Exception, e)) - NOT <c>LogException</c> - so we
    /// decorate Unity's global <see cref="ILogHandler"/> and drop the signature on both overrides;
    /// every other log is forwarded to the original handler verbatim.
    ///
    /// Editor / Development only - the error has only been observed in the Editor and the release
    /// behaviour of the handler swap is untested.
    /// </summary>
    public static class BenignLobbyLogFilter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // Idempotent: never wrap our own wrapper (guards Editor re-inits / repeated calls).
            if (Debug.unityLogger.logHandler is FilteringLogHandler) return;

            Debug.unityLogger.logHandler = new FilteringLogHandler(Debug.unityLogger.logHandler);
            CSDebug.LogVerbose(CSLogChannel.Party, "[BenignLobbyLogFilter] Installed - suppressing the benign LobbyPatcher ArgumentOutOfRangeException.");
        }

        private sealed class FilteringLogHandler : ILogHandler
        {
            private readonly ILogHandler _inner;

            public FilteringLogHandler(ILogHandler inner) => _inner = inner;

            public void LogException(Exception exception, UnityEngine.Object context)
            {
                if (UgsRequestPolicy.IsLobbyPatcherStaleIndex(exception)) return;
                _inner.LogException(exception, context);
            }

            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                // The Lobby SDK surfaces the benign exception through this route - via
                // Debug.LogError / unityLogger.Log(LogType.Exception, e) - not LogException.
                if ((logType == LogType.Exception || logType == LogType.Error)
                    && IsBenignLobbyPatcherLogFormat(format, args))
                    return;

                _inner.LogFormat(logType, context, format, args);
            }

            // The LobbyPatcher rule is UgsRequestPolicy.IsLobbyPatcherStaleIndex - the same test the
            // refresh catches read through UgsRequestPolicy.Classify, so the console and the retry
            // layer can never disagree about which exception is the SDK's stale-index patch. This
            // filter deliberately keeps that NARROW rule rather than the whole Benign class: it
            // decides what the console shows, and only the one shape has ever spammed it.

            // The SDK conveys the exception either as an Exception argument or pre-rendered into
            // the message string (whose ToString() carries the LobbyPatcher throw stack). Cover both.
            private static bool IsBenignLobbyPatcherLogFormat(string format, object[] args)
            {
                if (args != null)
                {
                    foreach (var a in args)
                        if (a is Exception ex && UgsRequestPolicy.IsLobbyPatcherStaleIndex(ex))
                            return true;
                }

                string rendered;
                try { rendered = (args is { Length: > 0 }) ? string.Format(format, args) : format; }
                catch { return false; } // never suppress if the message can't be rendered safely

                return rendered != null
                    && rendered.Contains("LobbyPatcher")
                    && rendered.Contains("ArgumentOutOfRangeException");
            }
        }
    }
}
#endif
