using System;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.App.Systems.CloudData
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
        void MarkDirty();
        Task SaveAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Full repository contract for a single cloud-saved data domain.
    /// Single Responsibility: one repository per data domain (profile, stats, hangar, etc.).
    /// Open/Closed: new data domains add new ICloudDataRepository implementations
    ///              without modifying existing ones.
    /// </summary>
    public interface ICloudDataRepository<out T> : ICloudDataReader<T>, ICloudDataWriter where T : class
    {
        string CloudKey { get; }
        event Action OnDataChanged;
        Task LoadAsync(CancellationToken ct = default);
    }
}
