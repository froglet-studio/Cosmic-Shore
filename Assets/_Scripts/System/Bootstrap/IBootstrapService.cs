using System.Threading;
using Cysharp.Threading.Tasks;

namespace CosmicShore.Core
{
    /// <summary>
    /// Interface for services that participate in the bootstrap initialization sequence.
    /// MonoBehaviours implementing this interface can be added to AppManager's
    /// bootstrap service list for ordered async initialization.
    /// </summary>
    public interface IBootstrapService
    {
        /// <summary>
        /// Human-readable name for bootstrap logging.
        /// </summary>
        string ServiceName { get; }

        /// <summary>
        /// Initialize the service asynchronously. Called during bootstrap in list order.
        /// </summary>
        UniTask InitializeAsync(CancellationToken ct);

        /// <summary>
        /// Whether the service completed initialization successfully.
        /// Checked after InitializeAsync returns.
        /// </summary>
        bool IsInitialized { get; }
    }
}
