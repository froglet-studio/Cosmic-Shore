// Extracted from Assets/_Scripts/System/ApplicationLifecycleManager.cs (the static
// IsQuitting flag only — what Singleton<T> needs). The full lifecycle manager ports
// with the bootstrap arc; this static surface stays source-compatible.
namespace CosmicShore.Core
{
    public static class ApplicationLifecycleManager
    {
        public static bool IsQuitting { get; private set; }

        /// <summary>Host calls this when the process begins shutting down.</summary>
        public static void NotifyQuitting() => IsQuitting = true;
    }
}
