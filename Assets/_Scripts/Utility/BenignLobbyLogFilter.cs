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
    /// (<c>LobbyChannel.HandleLobbyChanges</c> -> <c>Logger.LogException</c>) before any of our
    /// awaits, so it cannot be try/caught the way HostConnectionService.IsBenignLobbyPatcherError
    /// handles the same error on its own refresh path. We instead decorate Unity's global
    /// <see cref="ILogHandler"/> and drop only this exact signature; every other log is forwarded
    /// to the original handler verbatim.
    ///
    /// Editor / Development only — the error has only been observed in the Editor and the release
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
            CSDebug.Log("[BenignLobbyLogFilter] Installed — suppressing the benign LobbyPatcher ArgumentOutOfRangeException.");
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
                => _inner.LogFormat(logType, context, format, args);

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
        }
    }
}
#endif
