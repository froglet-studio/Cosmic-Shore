using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Unified facade for all player cloud data.
    /// Single Responsibility: orchestrates initialization and provides typed access
    ///                        to every data domain - does not own any domain logic.
    /// Dependency Inversion: depends on ICloudSaveProvider and ICloudDataRepository
    ///                       interfaces, not concrete UGS types.
    ///
    /// Registered in AppManager DI as a lazy MonoBehaviour singleton.
    /// Subscribes to SOAP OnSignedIn event for auth-driven initialization.
    /// </summary>
    public class UGSDataService : MonoBehaviour, IUGSDataService
    {
        /// <summary>
        /// Static accessor for non-DI contexts (editor tools, static utility classes).
        /// Runtime MonoBehaviours must use [Inject] instead.
        /// </summary>
        internal static UGSDataService Instance { get; private set; }

        [Header("Hangar Sync")]
        [Tooltip("Vessel list to sync unlock state with cloud on initialization.")]
        [SerializeField] SO_VesselList vesselList;

        [Inject] AuthenticationDataVariable _authData;

        // ── Repositories ──
        PlayerProfileRepository _profile;
        ModeStatsRepository _modeStats;
        GameProgressionRepository _progression;
        HangarRepository _hangar;
        EpisodeProgressRepository _episodes;
        PlayerSettingsRepository _settings;
        WeeklyChallengeRepository _weeklyChallenge;
        TrainingProgressRepository _training;
        SquadRepository _squad;
        LoadoutRepository _loadout;

        ICloudSaveProvider _provider;
        List<ICloudDataWriter> _allRepos;

        // ── IUGSDataService ──

        public bool IsInitialized { get; private set; }
        public event Action OnInitialized;

        // Read-only accessors (for UI / query-only consumers)
        public ICloudDataReader<PlayerProfileData> Profile => _profile;
        public ICloudDataReader<ModeStatsCloudData> ModeStats => _modeStats;
        public ICloudDataReader<GameModeProgressionData> Progression => _progression;
        public ICloudDataReader<HangarCloudData> Hangar => _hangar;
        public ICloudDataReader<EpisodeProgressCloudData> Episodes => _episodes;
        public ICloudDataReader<PlayerSettingsCloudData> Settings => _settings;
        public ICloudDataReader<WeeklyChallengeCloudData> WeeklyChallenge => _weeklyChallenge;
        public ICloudDataReader<TrainingProgressCloudData> TrainingProgress => _training;
        public ICloudDataReader<SquadCloudData> Squad => _squad;
        public ICloudDataReader<LoadoutCloudData> Loadout => _loadout;

        // Typed write access (for game systems that mutate + mark dirty)
        public PlayerProfileRepository ProfileRepo => _profile;
        public ModeStatsRepository ModeStatsRepo => _modeStats;
        public GameProgressionRepository ProgressionRepo => _progression;
        public HangarRepository HangarRepo => _hangar;
        public EpisodeProgressRepository EpisodesRepo => _episodes;
        public PlayerSettingsRepository SettingsRepo => _settings;
        public WeeklyChallengeRepository WeeklyChallengeRepo => _weeklyChallenge;
        public TrainingProgressRepository TrainingProgressRepo => _training;
        public SquadRepository SquadRepo => _squad;
        public LoadoutRepository LoadoutRepo => _loadout;

        void Awake()
        {
            Instance = this;

            // Resolve vesselList at runtime if not assigned via inspector
            if (vesselList == null)
                vesselList = Resources.FindObjectsOfTypeAll<SO_VesselList>().FirstOrDefault();

            _provider = new UGSCloudSaveProvider();
            CreateRepositories();
        }

        void Start()
        {
            _authData.Value.OnSignedIn.OnRaised += HandleSignedIn;

            if (_authData.Value.IsSignedIn)
                HandleSignedIn();
        }

        void OnDisable()
        {
            if (_authData != null)
                _authData.Value.OnSignedIn.OnRaised -= HandleSignedIn;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        async void HandleSignedIn()
        {
            try
            {
                if (!IsInitialized)
                    await InitializeAsync();
                else if (_offlineInitialized)
                    await ReloadFromCloudAfterLateSignInAsync();
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[UGSDataService] HandleSignedIn failed: {e.Message}");
            }
        }

        void CreateRepositories()
        {
            _profile = new PlayerProfileRepository(_provider);
            _modeStats = new ModeStatsRepository(_provider);
            _progression = new GameProgressionRepository(_provider);
            _hangar = new HangarRepository(_provider);
            _episodes = new EpisodeProgressRepository(_provider);
            _settings = new PlayerSettingsRepository(_provider);
            _weeklyChallenge = new WeeklyChallengeRepository(_provider);
            _training = new TrainingProgressRepository(_provider);
            _squad = new SquadRepository(_provider);
            _loadout = new LoadoutRepository(_provider);

            _allRepos = new List<ICloudDataWriter>
            {
                _profile, _modeStats, _progression,
                _hangar, _episodes, _settings,
                _weeklyChallenge, _training, _squad, _loadout
            };
        }

        /// <summary>
        /// True when the repositories were initialized WITHOUT a signed-in cloud provider -
        /// every key answered from the <see cref="LocalCloudDataCache"/> snapshot (or fresh
        /// defaults). Set by <see cref="InitializeOfflineAsync"/>; cleared once a late
        /// sign-in reconciles against the cloud.
        /// </summary>
        bool _offlineInitialized;

        /// <summary>
        /// Offline-session init (see <see cref="OfflineModeService"/>): runs the exact same
        /// load pipeline as the online path, but with the provider unavailable each
        /// repository falls back to its last-known-good local snapshot - so the player still
        /// gets their display name, unlocked vessels, unlocked episodes, game progression and
        /// settings with no network at all. <c>OnInitialized</c> fires as usual, which is what
        /// lets PlayerDataService merge the cached profile through its ordinary path.
        /// </summary>
        public async Task InitializeOfflineAsync(CancellationToken ct = default)
        {
            if (IsInitialized) return;

            _offlineInitialized = true;
            CSDebug.Log("[UGSDataService] Offline init - loading repositories from local snapshots...");
            await InitializeAsync(ct);
        }

        /// <summary>
        /// Reconciles an offline-initialized session after a LATE sign-in (network recovered
        /// and auth retried). Clean repositories re-load from the cloud (cloud wins);
        /// repositories carrying unsaved offline progress are left alone - their debounced
        /// save loop flushes them up now that the provider is available. Each reloaded
        /// repository raises its own OnDataChanged, so live consumers refresh.
        /// </summary>
        async Task ReloadFromCloudAfterLateSignInAsync(CancellationToken ct = default)
        {
            _offlineInitialized = false;
            CSDebug.Log("[UGSDataService] Late sign-in after offline init - reconciling clean repositories from cloud...");

            var loads = new List<Task>();
            foreach (var repo in _allRepos)
                if (!repo.IsDirty && repo is ICloudDataReloadable reloadable)
                    loads.Add(reloadable.LoadAsync(ct));

            await Task.WhenAll(loads);
            SyncHangarToVessels();
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (IsInitialized) return;

            CSDebug.Log("[UGSDataService] Loading all repositories from cloud...");

            await Task.WhenAll(
                _profile.LoadAsync(ct),
                _modeStats.LoadAsync(ct),
                _progression.LoadAsync(ct),
                _hangar.LoadAsync(ct),
                _episodes.LoadAsync(ct),
                _settings.LoadAsync(ct),
                _weeklyChallenge.LoadAsync(ct),
                _training.LoadAsync(ct),
                _squad.LoadAsync(ct),
                _loadout.LoadAsync(ct)
            );

            // Restore vessel unlock state from cloud → SO_Vessel assets
            SyncHangarToVessels();

            IsInitialized = true;
            OnInitialized?.Invoke();

            CSDebug.Log("[UGSDataService] All repositories loaded successfully.");
        }

        public async Task FlushAllAsync(CancellationToken ct = default)
        {
            // Only flush repositories with pending changes - clean repos would
            // otherwise re-upload an unchanged payload on every flush.
            var tasks = new List<Task>();
            foreach (var repo in _allRepos)
                if (repo.IsDirty)
                    tasks.Add(repo.SaveAsync(ct));

            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        }

        public async Task<bool> ResetAllDataAsync(CancellationToken ct = default)
        {
            try
            {
                CSDebug.Log("[UGSDataService] Resetting all player data...");

                await Task.WhenAll(
                    _profile.ResetAsync(ct),
                    _modeStats.ResetAsync(ct),
                    _progression.ResetAsync(ct),
                    _hangar.ResetAsync(ct),
                    _episodes.ResetAsync(ct),
                    _settings.ResetAsync(ct),
                    _weeklyChallenge.ResetAsync(ct),
                    _training.ResetAsync(ct),
                    _squad.ResetAsync(ct),
                    _loadout.ResetAsync(ct)
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
        /// Reconciles vessel ownership between the authored SO_Vessel assets and HANGAR_DATA,
        /// in both directions:
        ///
        /// <list type="bullet">
        ///   <item>Starters (<c>SO_Vessel.OwnedFromStart</c>) are seeded INTO the cloud record.
        ///   Without this the player's one free vessel never appears in HANGAR_DATA, because
        ///   <c>VesselUnlockSystem.UnlockVessel</c> early-returns on an already-unlocked vessel
        ///   and so never persists it - which is why the Squirrel was missing from every
        ///   player's hangar payload.</item>
        ///   <item>Purchases recorded in the cloud are applied back onto the assets.</item>
        ///   <item><c>SelectedVessel</c> falls back to the starter when the player has never
        ///   opened the vessel panel - the only writer is a deliberate pick, so before the first
        ///   one the field read as null.</item>
        /// </list>
        ///
        /// Called automatically after initialization and available publicly for re-sync.
        /// </summary>
        public void SyncHangarToVessels()
        {
            if (vesselList == null || _hangar?.Data == null) return;

            var hangar = _hangar.Data;
            long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string firstStarter = null;
            bool changed = false;

            foreach (var vessel in vesselList.VesselList)
            {
                if (vessel == null || string.IsNullOrWhiteSpace(vessel.Name)) continue;

                if (vessel.OwnedFromStart)
                {
                    firstStarter ??= vessel.Name;

                    if (!hangar.IsVesselUnlocked(vessel.Name))
                    {
                        hangar.UnlockVessel(vessel.Name, nowUtcMs);
                        changed = true;
                    }
                }

                if (hangar.IsVesselUnlocked(vessel.Name))
                    vessel.Unlock();
            }

            // Prefer a vessel the player actually owns; fall back to the starter.
            if (string.IsNullOrWhiteSpace(hangar.SelectedVessel) ||
                !hangar.IsVesselUnlocked(hangar.SelectedVessel))
            {
                string fallback = firstStarter ?? FirstUnlockedName(hangar);
                if (!string.IsNullOrWhiteSpace(fallback) && hangar.SelectedVessel != fallback)
                {
                    hangar.SelectedVessel = fallback;
                    changed = true;
                }
            }

            if (string.IsNullOrWhiteSpace(hangar.PreferredVessel))
            {
                // Nothing flown yet, so "most hours played" has no real answer - derive one from
                // the records rather than leaving it empty. Real flight time overwrites it later.
                hangar.RecomputePreferredVessel();
                changed |= !string.IsNullOrWhiteSpace(hangar.PreferredVessel);
            }

            if (changed)
                _hangar.MarkDirty();

            CSDebug.Log($"[UGSDataService] Synced hangar for {vesselList.VesselList.Count} vessels - " +
                        $"{hangar.UnlockedVesselCount()} unlocked, selected '{hangar.SelectedVessel}'.");
        }

        static string FirstUnlockedName(HangarCloudData hangar)
        {
            foreach (var name in hangar.UnlockedVesselNames())
                return name;
            return null;
        }
    }
}
