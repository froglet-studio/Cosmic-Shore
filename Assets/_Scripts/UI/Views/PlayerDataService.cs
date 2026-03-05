using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using Reflex.Attributes;


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

        [Header("Cloud Save")]
        [SerializeField] private string cloudSaveProfileKey = UGSKeys.PlayerProfile;
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Header("Game Data")]
        [Inject] private GameDataSO gameData;

        public PlayerProfileData CurrentProfile { get; private set; }
        public bool              IsInitialized  { get; private set; }

        public event Action<PlayerProfileData> OnProfileChanged;

        [Inject] AuthenticationDataVariable authenticationDataVariable;
        AuthenticationData authenticationData => authenticationDataVariable.Value;

        // Save debouncing: coalesces rapid saves into a single cloud call
        private const float SAVE_DEBOUNCE_SECONDS = 1.5f;
        private bool _saveDirty;
        private bool _saveInFlight;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            OnProfileChanged += SyncProfileToGameData;
        }

        void Start()
        {
            try
            {
                // Subscribe to auth events here (not OnEnable) because Reflex
                // DI injection happens after Awake/OnEnable but before Start.
                if (authenticationDataVariable != null)
                    authenticationData.OnSignedIn.OnRaised += HandleSignedIn;

                CreateLocalDefaultProfile(null);

                // If already signed in, initialize immediately
                if (authenticationDataVariable != null && authenticationData.IsSignedIn)
                {
                    _ = InitializeAfterAuth();
                }
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[PlayerDataService] Start failed: {e.Message}");
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (authenticationDataVariable != null)
                authenticationData.OnSignedIn.OnRaised -= HandleSignedIn;
            OnProfileChanged -= SyncProfileToGameData;
        }

        async void HandleSignedIn()
        {
            try
            {
                await InitializeAfterAuth();
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[PlayerDataService] HandleSignedIn failed: {e.Message}");
            }
        }

        async Task InitializeAfterAuth()
        {
            var ds = UGSDataService.Instance;
            if (ds != null)
            {
                if (ds.IsInitialized)
                    HandleDataServiceReady();
                else
                    ds.OnInitialized += HandleDataServiceReady;
            }
        }

        void HandleDataServiceReady()
        {
            var ds = UGSDataService.Instance;
            if (ds != null)
                ds.OnInitialized -= HandleDataServiceReady;

            MergeCloudProfile();

            ApplyPendingDebugCrystals();

            IsInitialized = true;
            OnProfileChanged?.Invoke(CurrentProfile);
        }

        /// <summary>
        /// Merges cloud profile data from UGSDataService.ProfileRepo on top of local defaults.
        /// Performs union merge for unlocked rewards (local wins ties).
        /// </summary>
        void MergeCloudProfile()
        {
            var ds = UGSDataService.Instance;
            if (ds?.ProfileRepo == null) return;

            var cloudData = ds.ProfileRepo.Data;
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
            int suffix = UnityEngine.Random.Range(1000, 10000);
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
            var ds = UGSDataService.Instance;
            if (ds?.ProfileRepo == null || CurrentProfile == null) return;

            var repoData = ds.ProfileRepo.Data;
            repoData.userId = CurrentProfile.userId;
            repoData.displayName = CurrentProfile.displayName;
            repoData.avatarId = CurrentProfile.avatarId;
            repoData.crystalBalance = CurrentProfile.crystalBalance;
            repoData.unlockedRewardIds = CurrentProfile.unlockedRewardIds;

            ds.ProfileRepo.MarkDirty();
        }

        void ScheduleSave()
        {
            SyncCurrentProfileToRepo();
        }

        // ----------------- Public API -----------------

        public void SetAvatarId(int avatarId)
        {
            if (CurrentProfile == null)
                return;

            CurrentProfile.avatarId = avatarId;
            OnProfileChanged?.Invoke(CurrentProfile);
            ScheduleSave();
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

            if (authenticationDataVariable != null)
                authenticationData.UserName.Value = data.displayName;
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

        // ----------------- XP -----------------

        public int GetXP()
        {
            return CurrentProfile?.xp ?? 0;
        }

        public void AddXP(int amount)
        {
            if (CurrentProfile == null || amount <= 0) return;

            CurrentProfile.xp += amount;
            ScheduleSave();
            OnProfileChanged?.Invoke(CurrentProfile);
            CSDebug.Log($"[PlayerDataService] Added {amount} XP. Total: {CurrentProfile.xp}");
        }

        // ----------------- Crystal Currency -----------------

        public static event Action<int> OnCrystalBalanceChanged;

        public int GetCrystalBalance()
        {
            return CurrentProfile?.crystalBalance ?? 0;
        }

        public int AddCrystals(int amount)
        {
            if (CurrentProfile == null || amount <= 0) return GetCrystalBalance();

            CurrentProfile.crystalBalance += amount;
            ScheduleSave();
            OnCrystalBalanceChanged?.Invoke(CurrentProfile.crystalBalance);
            OnProfileChanged?.Invoke(CurrentProfile);
            CSDebug.Log($"[PlayerDataService] Added {amount} crystals. Balance: {CurrentProfile.crystalBalance}");
            return CurrentProfile.crystalBalance;
        }

        public bool TrySpendCrystals(int amount)
        {
            if (CurrentProfile == null || amount <= 0) return false;
            if (CurrentProfile.crystalBalance < amount) return false;

            CurrentProfile.crystalBalance -= amount;
            ScheduleSave();
            OnCrystalBalanceChanged?.Invoke(CurrentProfile.crystalBalance);
            OnProfileChanged?.Invoke(CurrentProfile);
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
