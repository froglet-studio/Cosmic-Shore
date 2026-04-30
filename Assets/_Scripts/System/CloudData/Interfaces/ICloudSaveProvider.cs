using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Core
{
    /// <summary>
    /// Abstraction over the cloud save backend (UGS, or any future provider).
    /// Dependency Inversion: services depend on this interface, not on concrete UGS calls.
    /// </summary>
    public interface ICloudSaveProvider
    {
        /// <summary>Whether the provider is initialized and the player is authenticated.</summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Loads a single key from cloud save, deserializing to T.
        /// Returns default(T) if key doesn't exist.
        /// </summary>
        Task<T> LoadAsync<T>(string key, CancellationToken ct = default) where T : class, new();

        /// <summary>
        /// Saves a single key/value pair to cloud save.
        /// </summary>
        Task SaveAsync<T>(string key, T data, CancellationToken ct = default) where T : class;
    }
}
