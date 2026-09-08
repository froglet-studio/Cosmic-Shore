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
        /// Saves a single key/value pair to cloud save, retrying with backoff on
        /// transient failure. Returns true on success, false if unavailable
        /// (offline / not signed in) or all attempts failed.
        /// </summary>
        Task<bool> SaveAsync<T>(string key, T data, CancellationToken ct = default) where T : class;

        /// <summary>
        /// Deletes one key outright. Returns true when the key is gone AFTERWARDS - which includes
        /// the case where it was never there, because "delete this" and "this does not exist" are
        /// the same outcome to a caller and treating the second as a failure makes a wipe report
        /// errors for every key a player happened not to have.
        /// </summary>
        Task<bool> DeleteAsync(string key, CancellationToken ct = default);
    }
}
