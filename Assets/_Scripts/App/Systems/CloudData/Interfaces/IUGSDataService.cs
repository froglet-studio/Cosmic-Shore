using System;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.App.Systems.CloudData.Models;
using CosmicShore.Game.Analytics;
using CosmicShore.Game.Progression;
using CosmicShore.App.Profile;

namespace CosmicShore.App.Systems.CloudData
{
    /// <summary>
    /// Facade interface for all player cloud data.
    /// Provides typed access to every data domain without exposing internals.
    /// </summary>
    public interface IUGSDataService
    {
        bool IsInitialized { get; }
        event Action OnInitialized;

        // ── Existing Data Domains ──
        ICloudDataReader<PlayerProfileData> Profile { get; }
        ICloudDataReader<PlayerStatsProfile> Stats { get; }
        ICloudDataReader<VesselStatsCloudData> VesselStats { get; }
        ICloudDataReader<GameModeProgressionData> Progression { get; }

        // ── New Data Domains ──
        ICloudDataReader<HangarCloudData> Hangar { get; }
        ICloudDataReader<EpisodeProgressCloudData> Episodes { get; }
        ICloudDataReader<PlayerSettingsCloudData> Settings { get; }

        /// <summary>Initializes all repositories after authentication.</summary>
        Task InitializeAsync(CancellationToken ct = default);

        /// <summary>Forces all dirty repositories to save immediately.</summary>
        Task FlushAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Resets all player data (all domains) and re-saves to cloud.
        /// Returns true if reset was successful.
        /// </summary>
        Task<bool> ResetAllDataAsync(CancellationToken ct = default);
    }
}
