using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Obvious.Soap;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;
using Reflex.Attributes;


namespace CosmicShore.UI
{
    public class PlayerDataService : MonoBehaviour
    {
        [Header("Cloud Save")]
        [SerializeField] private string cloudSaveProfileKey = UGSKeys.PlayerProfile;
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Header("SOAP")]
        [SerializeField] private StringVariable playerDisplayName;

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

        private void OnEnable()
        {
            OnProfileChanged += SyncProfileToGameData;
        }

        async void Start()
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
                    await InitializeAfterAuth();
                }
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[PlayerDataService] Start failed: {e.Message}");
            }
        }

        void OnDestroy()
        {
            if (authenticationDataVariable != null)
                authenticationData.OnSignedIn.OnRaised -= HandleSignedIn;
            OnProfileChanged -= SyncProfileToGameData;
        }

        async void HandleSignedIn()
        {
            try
            {
                if (IsInitialized)
                    return;

                await InitializeAfterAuth();
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[PlayerDataService] HandleSignedIn failed: {e.Message}");
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
                CSDebug.LogWarning("[PlayerDataService] Not signed in. Keeping local profile.");
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
                        bool needsResync = false;

                        // Merge XP: keep the higher value (local may have earned XP before cloud loaded)
                        int localXP = CurrentProfile.xp;
                        if (localXP > cloudData.xp)
                        {
                            cloudData.xp = localXP;
                            needsResync = true;
                        }

                        // Merge unlocked rewards: union of local + cloud sets
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

                        CurrentProfile = cloudData;

                        if (needsResync)
                            await SaveProfileAsync(true);

                        return;
                    }

                    CSDebug.LogWarning("[PlayerDataService] Failed to parse profile JSON. Keeping local.");
                }
                else
                {
                    // No cloud profile yet → upload local
                    await SaveProfileAsync(canUseCloudSave);
                }
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[PlayerDataService] Cloud load failed: {e.Message}. Keeping local.");
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
                CSDebug.LogWarning($"[PlayerDataService] Save failed: {e.Message}");
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
                CSDebug.LogWarning($"[PlayerDataService] Debounced save failed: {e.Message}");
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

            if (playerDisplayName != null)
                playerDisplayName.Value = data.displayName;
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
            CSDebug.Log($"[PlayerDataService] XP added: +{amount}, Total: {CurrentProfile.xp}");
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
    }
}
