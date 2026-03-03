using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.App.Profile;
using CosmicShore.App.Systems.CloudData.Models;
using CosmicShore.Game.Analytics;
using CosmicShore.Game.Progression;
using CosmicShore.Services.Auth;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.App.Systems.CloudData
{
    /// <summary>
    /// Unified facade for all player cloud data.
    /// Single Responsibility: orchestrates initialization and provides typed access
    ///                        to every data domain — does not own any domain logic.
    /// Dependency Inversion: depends on ICloudSaveProvider and ICloudDataRepository
    ///                       interfaces, not concrete UGS types.
    ///
    /// Attach to a root-level GameObject that persists across scenes.
    /// </summary>
    public class UGSDataService : MonoBehaviour, IUGSDataService
    {
        public static UGSDataService Instance { get; private set; }

        [Header("Hangar Sync")]
        [Tooltip("Vessel list to sync unlock state with cloud on initialization.")]
        [SerializeField] SO_VesselList vesselList;

        // ── Repositories ──
        PlayerProfileRepository _profile;
        PlayerStatsRepository _stats;
        VesselStatsRepository _vesselStats;
        GameProgressionRepository _progression;
        HangarRepository _hangar;
        EpisodeProgressRepository _episodes;
        PlayerSettingsRepository _settings;

        ICloudSaveProvider _provider;
        List<ICloudDataWriter> _allRepos;

        // ── IUGSDataService ──

        public bool IsInitialized { get; private set; }
        public event Action OnInitialized;

        // Read-only accessors (for UI / query-only consumers)
        public ICloudDataReader<PlayerProfileData> Profile => _profile;
        public ICloudDataReader<PlayerStatsProfile> Stats => _stats;
        public ICloudDataReader<VesselStatsCloudData> VesselStats => _vesselStats;
        public ICloudDataReader<GameModeProgressionData> Progression => _progression;
        public ICloudDataReader<HangarCloudData> Hangar => _hangar;
        public ICloudDataReader<EpisodeProgressCloudData> Episodes => _episodes;
        public ICloudDataReader<PlayerSettingsCloudData> Settings => _settings;

        // Typed write access (for game systems that mutate + mark dirty)
        public PlayerProfileRepository ProfileRepo => _profile;
        public PlayerStatsRepository StatsRepo => _stats;
        public VesselStatsRepository VesselStatsRepo => _vesselStats;
        public GameProgressionRepository ProgressionRepo => _progression;
        public HangarRepository HangarRepo => _hangar;
        public EpisodeProgressRepository EpisodesRepo => _episodes;
        public PlayerSettingsRepository SettingsRepo => _settings;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            _provider = new UGSCloudSaveProvider();
            CreateRepositories();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            var auth = AuthenticationController.Instance;
            if (auth != null)
                auth.OnSignedIn -= HandleSignedIn;
        }

        async void Start()
        {
            try
            {
                var auth = AuthenticationController.Instance;
                if (auth != null)
                    auth.OnSignedIn += HandleSignedIn;

                if (auth != null && auth.IsSignedIn)
                    await InitializeAsync();
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[UGSDataService] Start failed: {e.Message}");
            }
        }

        async void HandleSignedIn(string playerId)
        {
            try
            {
                if (!IsInitialized)
                    await InitializeAsync();
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[UGSDataService] HandleSignedIn failed: {e.Message}");
            }
        }

        void CreateRepositories()
        {
            _profile = new PlayerProfileRepository(_provider);
            _stats = new PlayerStatsRepository(_provider);
            _vesselStats = new VesselStatsRepository(_provider);
            _progression = new GameProgressionRepository(_provider);
            _hangar = new HangarRepository(_provider);
            _episodes = new EpisodeProgressRepository(_provider);
            _settings = new PlayerSettingsRepository(_provider);

            _allRepos = new List<ICloudDataWriter>
            {
                _profile, _stats, _vesselStats, _progression,
                _hangar, _episodes, _settings
            };
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (IsInitialized) return;

            CSDebug.Log("[UGSDataService] Loading all repositories from cloud...");

            await Task.WhenAll(
                _profile.LoadAsync(ct),
                _stats.LoadAsync(ct),
                _vesselStats.LoadAsync(ct),
                _progression.LoadAsync(ct),
                _hangar.LoadAsync(ct),
                _episodes.LoadAsync(ct),
                _settings.LoadAsync(ct)
            );

            // Restore vessel unlock state from cloud → SO_Vessel assets
            SyncHangarToVessels();

            IsInitialized = true;
            OnInitialized?.Invoke();

            CSDebug.Log("[UGSDataService] All repositories loaded successfully.");
        }

        public async Task FlushAllAsync(CancellationToken ct = default)
        {
            var tasks = new List<Task>();
            foreach (var repo in _allRepos)
                tasks.Add(repo.SaveAsync(ct));

            await Task.WhenAll(tasks);
        }

        public async Task<bool> ResetAllDataAsync(CancellationToken ct = default)
        {
            try
            {
                CSDebug.Log("[UGSDataService] Resetting all player data...");

                await Task.WhenAll(
                    _profile.ResetAsync(ct),
                    _stats.ResetAsync(ct),
                    _vesselStats.ResetAsync(ct),
                    _progression.ResetAsync(ct),
                    _hangar.ResetAsync(ct),
                    _episodes.ResetAsync(ct),
                    _settings.ResetAsync(ct)
                );

                CSDebug.Log("[UGSDataService] All player data reset successfully.");
                return true;
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[UGSDataService] Reset failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Restores vessel SO_Vessel.isLocked states from cloud data.
        /// Called automatically after initialization and available publicly for re-sync.
        /// </summary>
        public void SyncHangarToVessels()
        {
            if (vesselList == null || _hangar?.Data == null) return;

            foreach (var vessel in vesselList.VesselList)
            {
                if (vessel == null) continue;

                if (_hangar.Data.IsVesselUnlocked(vessel.Name))
                    vessel.Unlock();
            }

            CSDebug.Log($"[UGSDataService] Synced hangar unlock state for {vesselList.VesselList.Count} vessels.");
        }
    }
}
