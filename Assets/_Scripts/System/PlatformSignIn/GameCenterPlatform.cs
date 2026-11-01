using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.Utility;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace CosmicShore.Core
{
    /// <summary>
    /// The player's Game Center identity, as UGS needs it to sign in (fair play, Threat Board T1). The native half is
    /// <c>Assets/Plugins/iOS/GameCenter/GameCenterSignIn.mm</c>. iOS players only; everywhere else every call answers
    /// "no identity" at once.
    /// </summary>
    public static class GameCenterPlatform
    {
        /// <summary>What Apple's identity-verification signature carries, ready for UGS.</summary>
        public readonly struct Identity
        {
            public readonly string TeamPlayerId, PublicKeyUrl, Signature, Salt;
            public readonly ulong Timestamp;

            public Identity(string teamPlayerId, string publicKeyUrl, string signature, string salt, ulong timestamp)
            {
                TeamPlayerId = teamPlayerId;
                PublicKeyUrl = publicKeyUrl;
                Signature = signature;
                Salt = salt;
                Timestamp = timestamp;
            }
        }

        public static bool IsSupported =>
#if UNITY_IOS && !UNITY_EDITOR
            true;
#else
            false;
#endif

        /// <summary>
        /// The local player's identity, or null when they are not signed in to Game Center, Apple refuses, or no
        /// answer comes within <paramref name="timeoutSeconds"/>. Never shows Game Center's sign-in screen.
        /// </summary>
        public static async Task<Identity?> GetIdentityAsync(float timeoutSeconds)
        {
#if UNITY_IOS && !UNITY_EDITOR
            int id;
            var pending = new TaskCompletionSource<Identity?>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (s_pending) { id = ++s_nextId; s_pending[id] = pending; }
            CS_GameCenterSignIn(id, OnAnswer);

            var finished = await Task.WhenAny(pending.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
            lock (s_pending) s_pending.Remove(id);
            if (finished != pending.Task)
            {
                CSDebug.LogWarning("[GameCenter] No Game Center identity within the timeout; signing in without it.");
                return null;
            }
            return pending.Task.Result;
#else
            await Task.CompletedTask;
            return null;
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        static readonly Dictionary<int, TaskCompletionSource<Identity?>> s_pending = new();
        static int s_nextId;

        delegate void Callback(int requestId, string teamPlayerId, string publicKeyUrl, string signature, string salt,
                               ulong timestamp, string error);

        [DllImport("__Internal")]
        static extern void CS_GameCenterSignIn(int requestId, Callback callback);

        // GameKit answers on its own queue; the awaiting code returns to the main thread itself (AsMainThread).
        [AOT.MonoPInvokeCallback(typeof(Callback))]
        static void OnAnswer(int requestId, string teamPlayerId, string publicKeyUrl, string signature, string salt,
                             ulong timestamp, string error)
        {
            TaskCompletionSource<Identity?> pending;
            lock (s_pending)
                if (!s_pending.TryGetValue(requestId, out pending)) return;

            if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(teamPlayerId))
            {
                CSDebug.LogVerbose(CSLogChannel.Boot, $"[GameCenter] No identity ({error}).");
                pending.TrySetResult(null);
                return;
            }
            pending.TrySetResult(new Identity(teamPlayerId, publicKeyUrl, signature, salt, timestamp));
        }
#endif
    }
}
