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
    /// try/caught the way HostConnectionService.IsBenignLobbyPatcherError handles the same error
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
            CSDebug.Log("[BenignLobbyLogFilter] Installed - suppressing the benign LobbyPatcher ArgumentOutOfRangeException.");
        }

        private sealed class FilteringLogHandler : ILogHandler
        {
            private readonly ILogHandler _inner;

            public FilteringLogHandler(ILogHandler inner) => _inner = inner;

            public void LogException(Exception exception, UnityEngine.Object context)
            {
                if (IsBenignLobbyPatcherError(exception)) return;
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

            // Mirrors HostConnectionService.IsBenignLobbyPatcherError: an ArgumentOutOfRangeException
            // whose stack passes through LobbyPatcher is unambiguously the SDK's stale-index patch.
            // Our own code never calls LobbyPatcher, so the false-positive risk is nil.
            private static bool IsBenignLobbyPatcherError(Exception e)
            {
                for (var current = e; current != null; current = current.InnerException)
                {
                    if (current is ArgumentOutOfRangeException
                        && (current.StackTrace?.Contains("LobbyPatcher") ?? false))
                        return true;
                }
                return false;
            }

            // The SDK conveys the exception either as an Exception argument or pre-rendered into
            // the message string (whose ToString() carries the LobbyPatcher throw stack). Cover both.
            private static bool IsBenignLobbyPatcherLogFormat(string format, object[] args)
            {
                if (args != null)
                {
                    foreach (var a in args)
                        if (a is Exception ex && IsBenignLobbyPatcherError(ex))
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
