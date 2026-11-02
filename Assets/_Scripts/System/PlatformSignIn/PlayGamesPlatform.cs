using System;
using System.Threading.Tasks;
using CosmicShore.Utility;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;
using UnityEngine.Scripting;
#endif

namespace CosmicShore.Core
{
    /// <summary>
    /// The player's Play Games server auth code, as UGS needs it to sign in (fair play, Threat Board T1). The Java half
    /// is <c>Assets/Plugins/Android/PlayGamesSignIn.androidlib</c>. Android players only; everywhere else every call
    /// answers "no code" at once.
    /// </summary>
    public static class PlayGamesPlatform
    {
        const string BridgeClass = "com.froglet.cosmicshore.playgames.PlayGamesSignInBridge";

        public static bool IsSupported =>
#if UNITY_ANDROID && !UNITY_EDITOR
            true;
#else
            false;
#endif

        /// <summary>
        /// A one-time auth code for <paramref name="serverClientId"/>, or null when the player is not signed in to Play
        /// Games, Google refuses, or no answer comes within <paramref name="timeoutSeconds"/>. Never shows a screen.
        /// </summary>
        public static async Task<string> GetServerAuthCodeAsync(string serverClientId, float timeoutSeconds)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var bridge = new AndroidJavaClass(BridgeClass);
                bridge.CallStatic("requestServerAuthCode", activity, serverClientId, new Callback(pending));
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[PlayGames] Could not start Play Games ({e.Message}); signing in without it.");
                return null;
            }

            var finished = await Task.WhenAny(pending.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
            if (finished != pending.Task)
            {
                CSDebug.LogWarning("[PlayGames] No Play Games answer within the timeout; signing in without it.");
                return null;
            }
            return pending.Task.Result;
#else
            await Task.CompletedTask;
            return null;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>The Java bridge's callback; Java calls <see cref="onResult"/> by name, once.</summary>
        [Preserve]
        sealed class Callback : AndroidJavaProxy
        {
            readonly TaskCompletionSource<string> _pending;

            public Callback(TaskCompletionSource<string> pending) : base(BridgeClass + "$Callback") => _pending = pending;

            [Preserve]
            public void onResult(string authCode, string error)
            {
                if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(authCode))
                {
                    CSDebug.LogVerbose(CSLogChannel.Boot, $"[PlayGames] No auth code ({error}).");
                    _pending.TrySetResult(null);
                    return;
                }
                _pending.TrySetResult(authCode);
            }
        }
#endif
    }
}
