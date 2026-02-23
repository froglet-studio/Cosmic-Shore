using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.Services.Auth;
using CosmicShore.Soap;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;

namespace CosmicShore.App.Profile
{
    public class PlayerDataService : MonoBehaviour
    {
        public static PlayerDataService Instance { get; private set; }

        [Header("Cloud Save")]
        [SerializeField] private string cloudSaveProfileKey = "player_profile";
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Header("Game Data")]
        [SerializeField] private GameDataSO gameData;

        public PlayerProfileData CurrentProfile { get; private set; }
        public bool              IsInitialized  { get; private set; }

        public event Action<PlayerProfileData> OnProfileChanged;

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

            // DontDestroyOnLoad only works on root GameObjects.
            // Detach from any parent (Canvas, Panel, etc.) first so the
            // object survives scene transitions.
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            // Always have a profile ready so AddXP/GetXP never fail.
            // Cloud data will merge on top once auth completes.
            CreateLocalDefaultProfile(null);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            var auth = AuthenticationController.Instance;
            if (auth != null)
                auth.OnSignedIn -= HandleSignedInFromAuth;

            OnProfileChanged -= SyncProfileToGameData;
        }

        async void Start()
        {
            try
            {
                OnProfileChanged += SyncProfileToGameData;

                // Subscribe to auth events via singleton (survives scene transitions)
                var auth = AuthenticationController.Instance;
                if (auth != null)
                    auth.OnSignedIn += HandleSignedInFromAuth;

                // If already signed in, initialize immediately
                if (UnityServices.State == ServicesInitializationState.Initialized &&
                    auth != null && auth.IsSignedIn)
                {
                    await InitializeAfterAuth();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerDataService] Start failed: {e.Message}");
            }
        }

        async void HandleSignedInFromAuth(string playerId)
        {
            try
            {
                if (IsInitialized)
                    return;

                await InitializeAfterAuth();
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerDataService] HandleSignedInFromAuth failed: {e.Message}");
            }
        }

        private async Task InitializeAfterAuth()
        {
            if (IsInitialized)
                return;

            string playerId        = null;
            bool   canUseCloudSave = false;

            if (UnityServices.State == ServicesInitializationState.Initialized)
            {
                try
                {
                    canUseCloudSave = AuthenticationService.Instance != null &&
                                      AuthenticationService.Instance.IsSignedIn;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayerDataService] Auth state check failed: {ex.Message}");
                    canUseCloudSave = false;
                }
            }

            if (canUseCloudSave)
            {
                playerId = AuthenticationService.Instance.PlayerId;
            }

            await LoadOrCreateProfileAsync(playerId, canUseCloudSave);

            IsInitialized = true;
            OnProfileChanged?.Invoke(CurrentProfile);
        }

        async Task LoadOrCreateProfileAsync(string playerId, bool canUseCloudSave)
        {
            // Update userId now that we know the real player id
            if (!string.IsNullOrEmpty(playerId))
                CurrentProfile.userId = playerId;

            if (!canUseCloudSave)
            {
                Debug.LogWarning("[PlayerDataService] Not signed in. Keeping local profile.");
                return;
            }

            try
            {
                var keys   = new HashSet<string> { cloudSaveProfileKey };
                var result = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                if (result.TryGetValue(cloudSaveProfileKey, out var item))
                {
                    var json = item.Value.GetAs<string>();
                    var cloudData = JsonUtility.FromJson<PlayerProfileData>(json);

                    if (cloudData != null)
                    {
                        // Merge: keep the higher XP (local may have earned XP before cloud loaded)
                        int localXP = CurrentProfile.xp;
                        CurrentProfile = cloudData;
                        if (localXP > CurrentProfile.xp)
                        {
                            CurrentProfile.xp = localXP;
                            await SaveProfileAsync(true);
                        }
                        return;
                    }

                    Debug.LogWarning("[PlayerDataService] Failed to parse profile JSON. Keeping local.");
                }
                else
                {
                    // No cloud profile yet → upload local
                    await SaveProfileAsync(canUseCloudSave);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerDataService] Cloud load failed: {e.Message}. Keeping local.");
            }
        }

        void CreateLocalDefaultProfile(string playerId)
        {
            CurrentProfile = new PlayerProfileData
            {
                userId      = string.IsNullOrEmpty(playerId) ? Guid.NewGuid().ToString("N") : playerId,
                displayName = "Pilot",
                avatarId    = GetDefaultAvatarId()
            };
        }

        int GetDefaultAvatarId()
        {
            if (profileIcons != null && profileIcons.profileIcons.Count > 0)
                return profileIcons.profileIcons[0].Id;

            return 0;
        }

        async Task SaveProfileAsync(bool canUseCloudSave)
        {
            if (CurrentProfile == null || !canUseCloudSave)
                return;

            try
            {
                var json = JsonUtility.ToJson(CurrentProfile);
                var data = new Dictionary<string, object>
                {
                    { cloudSaveProfileKey, json }
                };

                await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerDataService] Save failed: {e.Message}");
            }
        }

        /// <summary>
        /// Marks profile as dirty and schedules a debounced cloud save.
        /// Multiple calls within SAVE_DEBOUNCE_SECONDS collapse into one save.
        /// </summary>
        void ScheduleDebouncedSave()
        {
            _saveDirty = true;
            if (!_saveInFlight)
                RunDebouncedSave();
        }

        async void RunDebouncedSave()
        {
            if (_saveInFlight) return;
            _saveInFlight = true;

            try
            {
                await Task.Delay((int)(SAVE_DEBOUNCE_SECONDS * 1000));

                while (_saveDirty)
                {
                    _saveDirty = false;

                    bool canSave = UnityServices.State == ServicesInitializationState.Initialized &&
                                   AuthenticationService.Instance != null &&
                                   AuthenticationService.Instance.IsSignedIn;

                    await SaveProfileAsync(canSave);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerDataService] Debounced save failed: {e.Message}");
            }
            finally
            {
                _saveInFlight = false;
                if (_saveDirty)
                    RunDebouncedSave();
            }
        }

        // ----------------- Public API -----------------

        public void SetAvatarId(int avatarId)
        {
            if (CurrentProfile == null)
                return;

            CurrentProfile.avatarId = avatarId;
            OnProfileChanged?.Invoke(CurrentProfile);
            ScheduleDebouncedSave();
        }

        public void SetDisplayName(string displayName)
        {
            if (CurrentProfile == null)
                return;

            CurrentProfile.displayName = displayName;
            OnProfileChanged?.Invoke(CurrentProfile);
            ScheduleDebouncedSave();
        }

        void SyncProfileToGameData(PlayerProfileData data)
        {
            if (gameData != null)
            {
                gameData.LocalPlayerDisplayName = data.displayName;
                gameData.LocalPlayerAvatarId = data.avatarId;
                gameData.LocalPlayerXP = data.xp;
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

        /// <summary>
        /// Returns the player's current XP, or 0 if no profile is loaded.
        /// </summary>
        public int GetXP()
        {
            return CurrentProfile?.xp ?? 0;
        }

        /// <summary>
        /// Adds XP to the player's profile and syncs to Cloud Save.
        /// Returns the new total XP.
        /// </summary>
        public void AddXP(int amount)
        {
            if (CurrentProfile == null || amount <= 0)
                return;

            CurrentProfile.xp += amount;
            OnProfileChanged?.Invoke(CurrentProfile);
            ScheduleDebouncedSave();
            Debug.Log($"[PlayerDataService] XP added: +{amount}, Total: {CurrentProfile.xp}");
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
            ScheduleDebouncedSave();
            Debug.Log($"[PlayerDataService] Reward unlocked: {rewardId}");
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
    }
}
