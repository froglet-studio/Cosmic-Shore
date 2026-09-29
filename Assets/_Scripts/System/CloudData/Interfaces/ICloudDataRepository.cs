using System;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Core
{
    /// <summary>
    /// Interface Segregation: read-only access to cloud-persisted data.
    /// </summary>
    public interface ICloudDataReader<out T>
    {
        T Data { get; }
        bool IsLoaded { get; }
    }

    /// <summary>
    /// Interface Segregation: write/sync operations for cloud-persisted data.
    /// </summary>
    public interface ICloudDataWriter
    {
        /// <summary>Whether there are unsaved changes pending (failed or not-yet-flushed).</summary>
        bool IsDirty { get; }
        void MarkDirty();
        Task<bool> SaveAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Interface Segregation: the load half of the lifecycle, without the generic parameter.
    /// Lets <see cref="UGSDataService"/> re-load a heterogeneous repository list (e.g. the
    /// clean-repo cloud reconcile after an offline session's late sign-in) without knowing
    /// each repository's data type.
    /// </summary>
    public interface ICloudDataReloadable
    {
        Task LoadAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Full repository contract for a single cloud-saved data domain.
    /// Single Responsibility: one repository per data domain (profile, stats, hangar, etc.).
    /// Open/Closed: new data domains add new ICloudDataRepository implementations
    ///              without modifying existing ones.
    /// </summary>
    public interface ICloudDataRepository<out T> : ICloudDataReader<T>, ICloudDataWriter, ICloudDataReloadable where T : class
    {
        string CloudKey { get; }
        event Action OnDataChanged;
    }
}
