namespace CosmicShore.Engine.Services
{
    /// <summary>
    /// Mirror of <c>Unity.Services.Core.ServicesInitializationState</c> so verbatim
    /// guards like <c>UnityServices.State == ServicesInitializationState.Initialized</c>
    /// compile unchanged.
    /// </summary>
    public enum ServicesInitializationState
    {
        Uninitialized = 0,
        Initializing = 1,
        Initialized = 2,
    }

    /// <summary>
    /// Placeholder shim for the Unity Gaming Services core singleton
    /// (<c>Unity.Services.Core.UnityServices</c>) until the services phase ports the
    /// real initialization layer — same precedent as the E13
    /// <see cref="AuthenticationService"/> shim. Harness-configurable: tests / the CLI
    /// set <see cref="State"/> directly; the default is benign (uninitialized) so
    /// verbatim call sites like <c>PlayerDataService.MergeCloudProfile</c>'s auth-id
    /// guard keep working headless.
    /// </summary>
    public static class UnityServices
    {
        public static ServicesInitializationState State { get; set; }
            = ServicesInitializationState.Uninitialized;

        /// <summary>
        /// Local initialization (original contract: <c>UnityServices.InitializeAsync()</c>).
        /// The shim has no wire to bring up — it flips <see cref="State"/> to Initialized
        /// and completes, the observable contract verbatim callers
        /// (AuthenticationServiceFacade.InitializeCore) depend on. The real SDK binding
        /// replaces the body at the services phase.
        /// </summary>
        public static System.Threading.Tasks.Task InitializeAsync()
        {
            State = ServicesInitializationState.Initialized;
            return System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>Restore the benign uninitialized default (test isolation helper).</summary>
        public static void Reset() => State = ServicesInitializationState.Uninitialized;
    }

    /// <summary>
    /// Placeholder for <c>Unity.Services.Core.RequestFailedException</c> — the UGS request
    /// layer's typed failure, wrapping the HTTP status (or SDK error code) on
    /// <see cref="ErrorCode"/>. Grown so ported error classifiers
    /// (<c>NetworkDiagnostics.ClassifyException</c>,
    /// <c>HostConnectionService.IsDefiniteSessionGoneException</c>) run their typed arms
    /// verbatim; real construction sites arrive with the services phase.
    /// </summary>
    public class RequestFailedException : System.Exception
    {
        public int ErrorCode { get; }

        public RequestFailedException(int errorCode, string message)
            : base(message) => ErrorCode = errorCode;

        public RequestFailedException(int errorCode, string message, System.Exception innerException)
            : base(message, innerException) => ErrorCode = errorCode;
    }
}
