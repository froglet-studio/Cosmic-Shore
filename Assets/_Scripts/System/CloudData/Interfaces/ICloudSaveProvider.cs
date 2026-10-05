using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Core
{
    /// <summary>How a cloud load ended. The three outcomes need three different reactions.</summary>
    public enum CloudLoadStatus
    {
        /// <summary>The cloud answered and the key was there.</summary>
        Loaded,
        /// <summary>The cloud answered and the key does not exist: a genuinely new player.</summary>
        Missing,
        /// <summary>
        /// The cloud did NOT give a usable answer (offline, not signed in, network or auth error,
        /// or a stored value that could not be read). The player's record may well exist, so the
        /// caller must not treat this as "new player" or write defaults over it.
        /// </summary>
        Failed
    }

    /// <summary>Result of <see cref="ICloudSaveProvider.TryLoadAsync{T}"/>.</summary>
    public readonly struct CloudLoadResult<T> where T : class
    {
        public readonly CloudLoadStatus Status;
        /// <summary>Non-null only when <see cref="Status"/> is <see cref="CloudLoadStatus.Loaded"/>.</summary>
        public readonly T Data;

        public CloudLoadResult(CloudLoadStatus status, T data = null)
        {
            Status = status;
            Data = data;
        }
    }

    /// <summary>
    /// Abstraction over the cloud save backend (UGS, or any future provider).
    /// Dependency Inversion: services depend on this interface, not on concrete UGS calls.
    /// </summary>
    public interface ICloudSaveProvider
    {
        /// <summary>Whether the provider is initialized and the player is authenticated.</summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Loads a single key and reports HOW it ended, so a failed load can be told apart from a
        /// missing key. Repositories use this; a plain <see cref="LoadAsync{T}"/> collapses both to
        /// null, which made a flaky connection look like a brand-new player.
        /// </summary>
        Task<CloudLoadResult<T>> TryLoadAsync<T>(string key, CancellationToken ct = default) where T : class, new();

        /// <summary>
        /// Loads a single key from cloud save, deserializing to T.
        /// Returns null if the key doesn't exist OR the load failed - use
        /// <see cref="TryLoadAsync{T}"/> when the difference matters.
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
