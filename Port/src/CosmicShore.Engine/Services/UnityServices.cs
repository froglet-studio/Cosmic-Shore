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

        /// <summary>
        /// Original contract: <c>UnityServices.InitializeAsync(InitializationOptions)</c>. The
        /// game passes the build stamp's UGS environment (AuthenticationServiceFacade.InitializeCore);
        /// the shim records it on <see cref="EnvironmentName"/> and initializes as above.
        /// </summary>
        public static System.Threading.Tasks.Task InitializeAsync(InitializationOptions options)
        {
            EnvironmentName = options?.EnvironmentName;
            return InitializeAsync();
        }

        /// <summary>The environment the last <see cref="InitializeAsync(InitializationOptions)"/> named, or null.</summary>
        public static string EnvironmentName { get; private set; }

        /// <summary>Restore the benign uninitialized default (test isolation helper).</summary>
        public static void Reset() => State = ServicesInitializationState.Uninitialized;
    }

    /// <summary>
    /// Mirror of <c>Unity.Services.Core.InitializationOptions</c>: the options the game passes to
    /// <see cref="UnityServices.InitializeAsync(InitializationOptions)"/>. Only the environment
    /// name is carried; it is set by the <c>SetEnvironmentName</c> extension, as in the SDK.
    /// </summary>
    public class InitializationOptions
    {
        public string EnvironmentName { get; internal set; }
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

namespace CosmicShore.Engine.Services
{
    /// <summary>Why a Cloud Save request failed (original contract: Unity.Services.CloudSave.CloudSaveExceptionReason).</summary>
    public enum CloudSaveExceptionReason
    {
        Unknown = 0, NoInternetConnection = 1, ProjectIdMissing = 2, PlayerIdMissing = 3, AccessTokenMissing = 4,
        InvalidArgument = 5, Unauthorized = 6, KeyLimitExceeded = 7, NotFound = 8, TooManyRequests = 9,
        ServiceUnavailable = 10, Conflict = 11, Forbidden = 12,
    }

    /// <summary>A typed Cloud Save failure (original contract: Unity.Services.CloudSave.CloudSaveException).</summary>
    public class CloudSaveException : RequestFailedException
    {
        public CloudSaveExceptionReason Reason { get; }

        public CloudSaveException(CloudSaveExceptionReason reason, int errorCode, string message, System.Exception innerException = null)
            : base(errorCode, message, innerException) => Reason = reason;
    }

    /// <summary>Raised when the request failed validation (original contract).</summary>
    public class CloudSaveValidationException : CloudSaveException
    {
        public CloudSaveValidationException(CloudSaveExceptionReason reason, int errorCode, string message, System.Exception innerException = null)
            : base(reason, errorCode, message, innerException) { }
    }

    /// <summary>Raised when the service rate-limited the caller (original contract).</summary>
    public class CloudSaveRateLimitedException : CloudSaveException
    {
        public float RetryAfter { get; }
        public CloudSaveRateLimitedException(CloudSaveExceptionReason reason, int errorCode, string message, float retryAfter, System.Exception innerException = null)
            : base(reason, errorCode, message, innerException) => RetryAfter = retryAfter;
    }
}

namespace CosmicShore.Engine.Services.Environments
{
    /// <summary>Mirror of <c>Unity.Services.Core.Environments.EnvironmentsOptionsExtensions</c>.</summary>
    public static class EnvironmentsOptionsExtensions
    {
        /// <summary>Names the UGS environment to initialize against (original contract: <c>SetEnvironmentName</c>).</summary>
        public static InitializationOptions SetEnvironmentName(this InitializationOptions self, string environmentName)
        {
            self.EnvironmentName = environmentName;
            return self;
        }
    }
}
