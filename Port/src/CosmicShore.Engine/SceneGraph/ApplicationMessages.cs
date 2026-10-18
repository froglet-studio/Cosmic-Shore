using System;

namespace CosmicShore.Engine
{
    /// <summary>
    /// The application messages Unity sends every behaviour on an active GameObject:
    /// <c>OnApplicationQuit</c>, <c>OnApplicationFocus(bool)</c> and <c>OnApplicationPause(bool)</c>.
    /// The player host drives them: the quit sequence at the end of a frame in which the game
    /// called <see cref="Application.Quit()"/> (or when the window is closed), focus when the
    /// window gains or loses it.
    /// </summary>
    public static partial class Application
    {
        /// <summary>The game called <see cref="Quit()"/> and the host has not run the quit sequence yet.</summary>
        public static bool QuitRequested { get; private set; }

        static bool s_quitSent;

        /// <summary>
        /// Unity's quit, in its order: every <see cref="wantsToQuit"/> handler may cancel (only when
        /// <paramref name="cancellable"/>); then <c>OnApplicationQuit</c> on every behaviour, then
        /// <see cref="quitting"/>. Runs once per process. True when the player should now close.
        /// </summary>
        public static bool RunQuitSequence(bool cancellable = true)
        {
            QuitRequested = false;
            if (s_quitSent) return true;
            if (cancellable && !CanQuit()) return false;
            s_quitSent = true;
            Broadcast(h => h.ApplicationQuit, (call, mb) => call(mb));
            quitting?.Invoke();
            return true;
        }

        /// <summary>The window gained or lost focus: <see cref="isFocused"/>, <see cref="focusChanged"/>, then <c>OnApplicationFocus</c>.</summary>
        public static void SetFocus(bool focused)
        {
            if (isFocused == focused) return;
            isFocused = focused;
            focusChanged?.Invoke(focused);
            Broadcast(h => h.ApplicationFocus, (call, mb) => call(mb, focused));
        }

        /// <summary>The application was paused or resumed (a phone sent to the background): <c>OnApplicationPause</c>.</summary>
        public static void SetPaused(bool paused) => Broadcast(h => h.ApplicationPause, (call, mb) => call(mb, paused));

        /// <summary>For a new world (tests): the quit sequence may run again.</summary>
        internal static void ResetQuit() { QuitRequested = false; s_quitSent = false; }

        static void Broadcast<TCall>(Func<LifecycleHooks, TCall> pick, Action<TCall, MonoBehaviour> invoke) where TCall : Delegate
        {
            if (GameLoop.Current == null) return;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb == null || pick(mb.hooks) is not { } call) continue;
                // One behaviour's failure must not keep the rest from saving their state, as in Unity.
                try { invoke(call, mb); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
