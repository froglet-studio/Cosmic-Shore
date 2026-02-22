using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.Services.Auth;
using CosmicShore.Soap;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.Profile
{
    public class PlayerDataService : MonoBehaviour
    {
        public static PlayerDataService Instance { get; private set; }

        [Header("Cloud Save")]
        [SerializeField] private string cloudSaveProfileKey = "player_profile";
        [SerializeField] private SO_ProfileIconList profileIcons;
        
        [Header("UI")]
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private Image  avatarImage;
        
        [Header("Auth Hook")]
        [SerializeField] private AuthenticationController authController;

        [Header("Game Data")]
        [SerializeField] private GameDataSO gameData;

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
            DontDestroyOnLoad(gameObject);
        }

        async void Start()
        {
            try
            {
                Debug.Log("[PlayerDataService] Start() called.");
                if (authController != null)
                {
                    authController.OnSignedIn += HandleSignedInFromAuth;
                }
                OnProfileChanged += HandleProfileChanged;
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    Debug.Log("[PlayerDataService] UnityServices not initialized yet, skipping auth check.");
                    return;
                }
                bool canCheckAuth = true;
                try
                {
                    _ = AuthenticationService.Instance.IsSignedIn;
                }
                catch
                {
                    canCheckAuth = false;
                }

                if (canCheckAuth && AuthenticationService.Instance.IsSignedIn)
                {
                    Debug.Log("[PlayerDataService] Already signed in, initializing profile...");
                    await InitializeAfterAuth();
                }
                else
                {
                    Debug.Log("[PlayerDataService] Not signed in yet, waiting for auth callback.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerDataService] Start() exception: {e.Message}");
            }
        }

        async void HandleSignedInFromAuth(string playerId)
        {
            try
            {
                Debug.Log($"[PlayerDataService] HandleSignedInFromAuth called. playerId={playerId}, IsInitialized={IsInitialized}");
                if (IsInitialized)
                    return;

                await InitializeAfterAuth();
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerDataService] HandleSignedInFromAuth exception: {e.Message}");
            }
        }

        private async Task InitializeAfterAuth()
        {
            if (IsInitialized)
            {
                Debug.Log("[PlayerDataService] InitializeAfterAuth skipped — already initialized.");
                return;
            }

            Debug.Log("[PlayerDataService] InitializeAfterAuth starting...");
            string playerId        = null;
            bool   canUseCloudSave = false;

            if (UnityServices.State == ServicesInitializationState.Initialized)
            {
                try
                {
                    canUseCloudSave = AuthenticationService.Instance != null &&
                                      AuthenticationService.Instance.IsSignedIn;
                }
                catch
                {
                    canUseCloudSave = false;
                }
            }

            if (canUseCloudSave)
            {
                playerId = AuthenticationService.Instance.PlayerId;
            }

            await LoadOrCreateProfileAsync(playerId, canUseCloudSave);

            IsInitialized = true;
            Debug.Log($"[PlayerDataService] Profile initialized. displayName='{CurrentProfile?.displayName}', avatarId={CurrentProfile?.avatarId}, userId='{CurrentProfile?.userId}'");
            OnProfileChanged?.Invoke(CurrentProfile);
        }

        async Task LoadOrCreateProfileAsync(string playerId, bool canUseCloudSave)
        {
            if (!canUseCloudSave)
            {
                Debug.LogWarning("[UgsPlayerProfileService] Not signed in. Using local-only profile.");
                CreateLocalDefaultProfile(playerId);
                return;
            }

            try
            {
                var keys   = new HashSet<string> { cloudSaveProfileKey };
                var result = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                if (result.TryGetValue(cloudSaveProfileKey, out var item))
                {
                    var json = item.Value.GetAs<string>();
                    Debug.Log($"[PlayerDataService] Cloud Save JSON loaded: {json}");
                    var data = JsonUtility.FromJson<PlayerProfileData>(json);

                    if (data != null)
                    {
                        CurrentProfile = data;
                        Debug.Log($"[PlayerDataService] Cloud profile parsed OK. displayName='{data.displayName}', avatarId={data.avatarId}");
                        return;
                    }

                    Debug.LogWarning("[PlayerDataService] Failed to parse profile JSON. Creating default.");
                    CreateLocalDefaultProfile(playerId);
                }
                else
                {
                    // No profile yet → create & save
                    CreateLocalDefaultProfile(playerId);
                    await SaveProfileAsync(canUseCloudSave);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UgsPlayerProfileService] Load failed: {e.Message}");
                CreateLocalDefaultProfile(playerId);
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
                Debug.LogWarning($"[UgsPlayerProfileService] Save failed: {e.Message}");
            }
        }

        // ----------------- Public API -----------------

        public async void SetAvatarId(int avatarId)
        {
            try
            {
                if (CurrentProfile == null)
                {
                    Debug.LogWarning("[PlayerDataService] SetAvatarId called but CurrentProfile is null!");
                    return;
                }

                Debug.Log($"[PlayerDataService] SetAvatarId: old={CurrentProfile.avatarId}, new={avatarId}");
                CurrentProfile.avatarId = avatarId;
                OnProfileChanged?.Invoke(CurrentProfile);

                bool canUseCloudSave = UnityServices.State == ServicesInitializationState.Initialized &&
                                       AuthenticationService.Instance != null &&
                                       AuthenticationService.Instance.IsSignedIn;

                await SaveProfileAsync(canUseCloudSave);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UgsPlayerProfileService] SetAvatarId failed: {e.Message}");
            }
        }

        public async Task SetDisplayNameAsync(string displayName)
        {
            try
            {
                if (CurrentProfile == null)
                {
                    Debug.LogWarning("[PlayerDataService] SetDisplayNameAsync called but CurrentProfile is null!");
                    return;
                }

                Debug.Log($"[PlayerDataService] SetDisplayNameAsync: old='{CurrentProfile.displayName}', new='{displayName}'");
                CurrentProfile.displayName = displayName;
                OnProfileChanged?.Invoke(CurrentProfile);

                bool canUseCloudSave = UnityServices.State == ServicesInitializationState.Initialized &&
                                       AuthenticationService.Instance != null &&
                                       AuthenticationService.Instance.IsSignedIn;

                await SaveProfileAsync(canUseCloudSave);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UgsPlayerProfileService] SetDisplayNameAsync failed: {e.Message}");
            }
        }
        
        void HandleProfileChanged(PlayerProfileData data)
        {
            Debug.Log($"[PlayerDataService] HandleProfileChanged: displayName='{data.displayName}', avatarId={data.avatarId}");

            if (displayNameText != null)
                displayNameText.text = data.displayName;

            if (avatarImage != null)
            {
                var sprite = ResolveAvatarSprite(data.avatarId);
                avatarImage.sprite = sprite;
                avatarImage.enabled = sprite != null;
            }

            if (gameData != null)
            {
                gameData.LocalPlayerDisplayName = data.displayName;
                gameData.LocalPlayerAvatarId = data.avatarId;
                Debug.Log($"[PlayerDataService] -> GameDataSO updated: LocalPlayerDisplayName='{gameData.LocalPlayerDisplayName}', LocalPlayerAvatarId={gameData.LocalPlayerAvatarId}");
            }
            else
            {
                Debug.LogWarning("[PlayerDataService] HandleProfileChanged: gameData is null, cannot update GameDataSO!");
            }
        }

        Sprite ResolveAvatarSprite(int avatarId)
        {
            if (profileIcons == null || profileIcons.profileIcons == null || profileIcons.profileIcons.Count == 0)
                return null;

            for (int i = 0; i < profileIcons.profileIcons.Count; i++)
            {
                if (profileIcons.profileIcons[i].Id == avatarId)
                    return profileIcons.profileIcons[i].IconSprite;
            }

            // Fallback to first icon
            return profileIcons.profileIcons[0].IconSprite;
        }

        public Sprite GetAvatarSprite(int avatarId) => ResolveAvatarSprite(avatarId);

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
