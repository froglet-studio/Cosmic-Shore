using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine.Services;
using CosmicShore.Engine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Domain service for player profile data (display name, avatar, crystals, rewards).
    /// Delegates all cloud persistence to UGSDataService.ProfileRepo.
    /// Keeps domain logic (merge, defaults, events, crystal math) here.
    /// </summary>
    public class PlayerDataService : MonoBehaviour
    {
        public static PlayerDataService Instance { get; private set; }

        [Header("Profile")]
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Header("Game Data")]
        [SerializeField] private GameDataSO gameData;

        // PORT Deviation #14 (C2, restore when UGSDataService ports — CloudSave repo, services phase):
        // [Inject] UGSDataService _ugsDataService;
        // PORT Deviation (drift-sync, restore when AnalyticsServiceFacade ports — UGS Analytics, services phase):
        // [Inject] AnalyticsServiceFacade _analytics;

        public PlayerProfileData CurrentProfile { get; private set; }
        public bool              IsInitialized  { get; private set; }

        public event Action<PlayerProfileData> OnProfileChanged;

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

            CreateLocalDefaultProfile(null);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            // PORT Deviation #14 (C2, restore when UGSDataService ports):
            // var ds = _ugsDataService;
            // if (ds != null)
            //     ds.OnInitialized -= HandleDataServiceReady;

            OnProfileChanged -= SyncProfileToGameData;
        }

        void Start()
        {
            OnProfileChanged += SyncProfileToGameData;

            // PORT Deviation #14 (C2, restore when UGSDataService ports — headless the cloud
            // data service is never injected, so the original null-fallback below is the main
            // path until the services phase):
            // if (_ugsDataService == null)
            {
                // DI failed to supply the cloud data service (e.g. instantiated outside a
                // ContainerScope). Keep the local default profile so UI still renders rather
                // than throwing in Start and aborting the rest of init.
                CSDebug.LogWarning("[PlayerDataService] UGSDataService was not injected; running on local profile only.");
                return;
            }

            // PORT Deviation #14 (C2, restore when UGSDataService ports):
            // if (_ugsDataService.IsInitialized)
            //     HandleDataServiceReady();
            // else
            //     _ugsDataService.OnInitialized += HandleDataServiceReady;
        }

        void HandleDataServiceReady()
        {
            // PORT Deviation #14 (C2, restore when UGSDataService ports):
            // _ugsDataService.OnInitialized -= HandleDataServiceReady;

            MergeCloudProfile();

            // Stamp account-creation time once for cohorting (cross-session, cloud-persisted).
            if (CurrentProfile != null && CurrentProfile.firstSeenUtc == 0)
            {
                CurrentProfile.firstSeenUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                SyncCurrentProfileToRepo();
            }

            ApplyPendingDebugCrystals();

            IsInitialized = true;
            OnProfileChanged?.Invoke(CurrentProfile);

            // Notify currency/XP displays of the freshly-loaded cloud values. These views
            // subscribe to the static balance/xp events but those are otherwise only raised on
            // mutation (AddCrystals/AddXP), so without this they'd show the local-default 0
            // until the next change.
            OnCrystalBalanceChanged?.Invoke(CurrentProfile?.crystalBalance ?? 0);
            OnXPChanged?.Invoke(CurrentProfile?.xp ?? 0);
        }

        /// <summary>
        /// Merges cloud profile data from UGSDataService.ProfileRepo on top of local defaults.
        /// Performs union merge for unlocked rewards (local wins ties).
        /// </summary>
        void MergeCloudProfile()
        {
            // PORT Deviation #14 (C2, restore when UGSDataService ports — ProfileRepo is the
            // CloudSave repository. Headless there is no repo, so cloudData stays null and the
            // no-cloud-profile branch pushes local defaults via the (also staged)
            // SyncCurrentProfileToRepo. The merge + auth-id logic past the branch compiles live
            // against the E13 auth shim; restore by deleting the null seed below.):
            // var ds = _ugsDataService;
            // if (ds?.ProfileRepo == null) return;
            // var cloudData = ds.ProfileRepo.Data;
            PlayerProfileData cloudData = null;
            if (cloudData == null || string.IsNullOrEmpty(cloudData.userId))
            {
                // No cloud profile → push local defaults to cloud
                SyncCurrentProfileToRepo();
                return;
            }

            // Merge unlocked rewards: union of local + cloud sets
            bool needsResync = false;
            var localRewards = CurrentProfile.unlockedRewardIds ?? new List<string>();
            var cloudRewards = cloudData.unlockedRewardIds ?? new List<string>();
            foreach (var rewardId in localRewards)
            {
                if (!cloudRewards.Contains(rewardId))
                {
                    cloudRewards.Add(rewardId);
                    needsResync = true;
                }
            }
            cloudData.unlockedRewardIds = cloudRewards;

            // Update local userId from auth
            try
            {
                if (UnityServices.State == ServicesInitializationState.Initialized &&
                    AuthenticationService.Instance != null &&
                    AuthenticationService.Instance.IsSignedIn)
                {
                    cloudData.userId = AuthenticationService.Instance.PlayerId;
                }
            }
            catch { /* auth not ready, keep existing userId */ }

            CurrentProfile = cloudData;

            if (needsResync)
            {
                SyncCurrentProfileToRepo();
            }
        }

        void CreateLocalDefaultProfile(string playerId)
        {
            CurrentProfile = new PlayerProfileData
            {
                userId      = string.IsNullOrEmpty(playerId) ? Guid.NewGuid().ToString("N") : playerId,
                displayName = GenerateDefaultDisplayName(),
                avatarId    = GetDefaultAvatarId()
            };
        }

        static string GenerateDefaultDisplayName()
        {
            int suffix = CosmicShore.Engine.Random.Range(1000, 10000);
            return $"Pilot{suffix}";
        }

        int GetDefaultAvatarId()
        {
            if (profileIcons != null && profileIcons.profileIcons.Count > 0)
                return profileIcons.profileIcons[0].Id;

            return 0;
        }

        /// <summary>
        /// Copies CurrentProfile fields into the repository's data object and marks dirty.
        /// The repository handles debounced cloud persistence.
        /// </summary>
        void SyncCurrentProfileToRepo()
        {
            // PORT Deviation #14 (C2, whole body — UGS CloudSave repo, services phase):
            // var ds = _ugsDataService;
            // if (ds?.ProfileRepo == null || CurrentProfile == null) return;
            //
            // var repoData = ds.ProfileRepo.Data;
            // repoData.userId = CurrentProfile.userId;
            // repoData.displayName = CurrentProfile.displayName;
            // repoData.avatarId = CurrentProfile.avatarId;
            // repoData.crystalBalance = CurrentProfile.crystalBalance;
            // repoData.xp = CurrentProfile.xp;
            // repoData.unlockedRewardIds = CurrentProfile.unlockedRewardIds;
            // repoData.firstSeenUtc = CurrentProfile.firstSeenUtc;
            //
            // ds.ProfileRepo.MarkDirty();
        }

        void ScheduleSave()
        {
            SyncCurrentProfileToRepo();
        }

        /// <summary>
        /// Pushes the profile to UGS Cloud Save immediately (in addition to the debounced save),
        /// so deliberate user actions like changing the avatar persist right away rather than
        /// after the ~1.5s debounce. Mirrors GameModeProgressionService.SaveImmediateAsync.
        /// </summary>
        void SaveProfileImmediateAsync()
        {
            // PORT Deviation #14 (C2, whole body — UGS CloudSave repo, services phase; the
            // original is `async void` awaiting repo.SaveAsync()):
            // var repo = _ugsDataService?.ProfileRepo;
            // if (repo == null) return;
            //
            // try
            // {
            //     await repo.SaveAsync();
            // }
            // catch (Exception e)
            // {
            //     CSDebug.LogWarning($"[PlayerDataService] Immediate profile save failed: {e.Message}. " +
            //                        "Falling back to the debounced save.");
            // }
        }

        // ----------------- Public API -----------------

        public void SetAvatarId(int avatarId)
        {
            if (CurrentProfile == null)
                return;

            CurrentProfile.avatarId = avatarId;
            // OnProfileChanged drives the menu UI (ProfileScreen/widgets), gameData.LocalPlayerAvatarId,
            // and the local Player's NetAvatarId (Player.HandleProfileLoadedAfterSpawn → replicates
            // the new avatar to every peer in-game).
            OnProfileChanged?.Invoke(CurrentProfile);
            ScheduleSave();
            SaveProfileImmediateAsync(); // persist the avatar to UGS now, not just on debounce
        }

        public void SetDisplayName(string displayName)
        {
            if (CurrentProfile == null)
                return;

            CurrentProfile.displayName = displayName;
            OnProfileChanged?.Invoke(CurrentProfile);
            ScheduleSave();
        }

        void SyncProfileToGameData(PlayerProfileData data)
        {
            if (gameData != null)
            {
                gameData.LocalPlayerDisplayName = data.displayName;
                gameData.LocalPlayerAvatarId = data.avatarId;
            }
        }

        public Sprite GetAvatarSprite(int avatarId)
        {
            if (profileIcons == null || profileIcons.profileIcons == null || profileIcons.profileIcons.Count == 0)
                return null;

            for (int i = 0; i < profileIcons.profileIcons.Count; i++)
            {
                if (profileIcons.profileIcons[i].Id == avatarId)
                    return profileIcons.profileIcons[i].IconSprite;
            }

            return profileIcons.profileIcons[0].IconSprite;
        }

        // ----------------- Crystal Currency -----------------

        public static event Action<int> OnCrystalBalanceChanged;

        // Raised whenever the player's XP total changes (and once on cloud load).
        public static event Action<int> OnXPChanged;

        public int GetCrystalBalance()
        {
            return CurrentProfile?.crystalBalance ?? 0;
        }

        public int GetXP()
        {
            return CurrentProfile?.xp ?? 0;
        }

        /// <summary>
        /// Adds XP to the player's profile, persists it, and notifies listeners.
        /// Mirrors <see cref="AddCrystals"/> so the XP progress bar has a single,
        /// authoritative earning + persistence path.
        /// </summary>
        public int AddXP(int amount)
        {
            if (CurrentProfile == null || amount <= 0) return GetXP();

            CurrentProfile.xp += amount;
            ScheduleSave();
            OnXPChanged?.Invoke(CurrentProfile.xp);
            OnProfileChanged?.Invoke(CurrentProfile);
            CSDebug.Log($"[PlayerDataService] Added {amount} XP. Total: {CurrentProfile.xp}");
            return CurrentProfile.xp;
        }

        public int AddCrystals(int amount, string source = null)
        {
            if (CurrentProfile == null || amount <= 0) return GetCrystalBalance();

            CurrentProfile.crystalBalance += amount;
            ScheduleSave();
            OnCrystalBalanceChanged?.Invoke(CurrentProfile.crystalBalance);
            OnProfileChanged?.Invoke(CurrentProfile);
            // PORT Deviation (drift-sync, restore with AnalyticsServiceFacade): _analytics?.RecordCrystalsEarned(amount, source, CurrentProfile.crystalBalance);
            CSDebug.Log($"[PlayerDataService] Added {amount} crystals. Balance: {CurrentProfile.crystalBalance}");
            return CurrentProfile.crystalBalance;
        }

        public bool TrySpendCrystals(int amount, string source = null)
        {
            if (CurrentProfile == null || amount <= 0) return false;
            if (CurrentProfile.crystalBalance < amount)
            {
                // PORT Deviation (drift-sync, restore with AnalyticsServiceFacade): _analytics?.RecordCrystalSpendBlocked(amount, source, CurrentProfile.crystalBalance);
                return false;
            }

            CurrentProfile.crystalBalance -= amount;
            ScheduleSave();
            OnCrystalBalanceChanged?.Invoke(CurrentProfile.crystalBalance);
            OnProfileChanged?.Invoke(CurrentProfile);
            // PORT Deviation (drift-sync, restore with AnalyticsServiceFacade): _analytics?.RecordCrystalsSpent(amount, source, CurrentProfile.crystalBalance);
            CSDebug.Log($"[PlayerDataService] Spent {amount} crystals. Balance: {CurrentProfile.crystalBalance}");
            return true;
        }

        /// <summary>
        /// Marks a reward as unlocked in the player's profile.
        /// </summary>
        public void UnlockReward(string rewardId)
        {
            if (CurrentProfile == null || string.IsNullOrEmpty(rewardId))
                return;

            if (CurrentProfile.unlockedRewardIds == null)
                CurrentProfile.unlockedRewardIds = new List<string>();

            if (CurrentProfile.unlockedRewardIds.Contains(rewardId))
                return;

            CurrentProfile.unlockedRewardIds.Add(rewardId);
            OnProfileChanged?.Invoke(CurrentProfile);
            ScheduleSave();
            CSDebug.Log($"[PlayerDataService] Reward unlocked: {rewardId}");
        }

        /// <summary>
        /// Checks if a reward has been unlocked.
        /// </summary>
        public bool IsRewardUnlocked(string rewardId)
        {
            return CurrentProfile?.unlockedRewardIds != null &&
                   CurrentProfile.unlockedRewardIds.Contains(rewardId);
        }

        /// <summary>
        /// Forcing a UI refresh without a save (e.g. when an external system
        /// adjusts CurrentProfile directly or after re-binding).
        /// </summary>
        public void RefreshProfileVisuals()
        {
            if (CurrentProfile == null) return;
            OnProfileChanged?.Invoke(CurrentProfile);
        }

        // ----------------- Debug Crystal Support -----------------

        /// <summary>
        /// Applies any pending debug crystals that were queued from the Froglet Toolbox
        /// while in edit mode. Called once during initialization.
        /// </summary>
        void ApplyPendingDebugCrystals()
        {
#if UNITY_EDITOR
            int pending = LogControlWindow.ConsumePendingDebugCrystals();
            if (pending > 0 && CurrentProfile != null)
            {
                CurrentProfile.crystalBalance += pending;
                ScheduleSave();
                OnCrystalBalanceChanged?.Invoke(CurrentProfile.crystalBalance);
                CSDebug.Log($"[PlayerDataService] Applied {pending} pending debug crystals. Balance: {CurrentProfile.crystalBalance}");
            }
#endif
        }
    }
}
