// Extracted from Assets/_Scripts/System/ApplicationLifecycleManager.cs (the static
// surface only — the IsQuitting flag Singleton<T> needs, plus the OnAppPaused /
// OnAppQuitting static C# events ApplicationStateMachine subscribes; grown 2026-07-08
// for the bootstrap arc). The full MonoBehaviour lifecycle manager (SOAP bridge,
// focus/scene events) ports with the bootstrap arc; this static surface stays
// source-compatible.
namespace CosmicShore.Core
{
    public static class ApplicationLifecycleManager
    {
        public static bool IsQuitting { get; private set; }

        /// <summary>OS pause/resume (original: raised from OnApplicationPause).</summary>
        public static event System.Action<bool> OnAppPaused;

        /// <summary>OS quit (original: raised from OnApplicationQuit).</summary>
        public static event System.Action OnAppQuitting;

        /// <summary>Host calls this on OS pause/resume (test/harness entry point).</summary>
        public static void NotifyPaused(bool pauseStatus) => OnAppPaused?.Invoke(pauseStatus);

        /// <summary>Host calls this when the process begins shutting down.</summary>
        public static void NotifyQuitting()
        {
            IsQuitting = true;
            OnAppQuitting?.Invoke();
        }
    }
}
