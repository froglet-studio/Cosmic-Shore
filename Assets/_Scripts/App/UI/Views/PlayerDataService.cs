using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;
using YourGame.Services.Auth;   
using UnityEngine.UI;

namespace CosmicShore.App.Profile
{
    public class PlayerDataService : MonoBehaviour
    {
        [Header("Cloud Save")]
        [SerializeField] private string           cloudSaveProfileKey = "player_profile";
        [SerializeField] private SO_ProfileIconList profileIcons;
        
        [Header("UI")]
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private Image  avatarImage;
        
        [Header("Auth Hook ")]
        [SerializeField] private AuthenticationController authController;

        public PlayerProfileData CurrentProfile { get; private set; }
        public bool              IsInitialized  { get; private set; }

        public event Action<PlayerProfileData> OnProfileChanged;
        
        async void Start()
        {
            try
            {
                if (authController != null)
                {
                    authController.OnSignedIn += HandleSignedInFromAuth;
                }
                OnProfileChanged += HandleProfileChanged;
                if (UnityServices.State != ServicesInitializationState.Initialized) return;
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
                    await InitializeAfterAuth();
                }
            }
            catch (Exception e)
            {
              // TODO handle exception
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
                 // TODO handle exception
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
                    var data = JsonUtility.FromJson<PlayerProfileData>(json);

                    if (data != null)
                    {
                        CurrentProfile = data;
                        return;
                    }

                    Debug.LogWarning("[UgsPlayerProfileService] Failed to parse profile JSON. Creating default.");
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
                    return;

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
                    return;

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
            if (displayNameText != null)
                displayNameText.text = data.displayName;

            var sprite = ResolveAvatarSprite(data.avatarId);
            avatarImage.sprite  = sprite;
        }

        Sprite ResolveAvatarSprite(int avatarId)
        {
            for (int i = 0; i < profileIcons.profileIcons.Count; i++)
            {
                if (profileIcons.profileIcons[i].Id == avatarId)
                    return profileIcons.profileIcons[i].IconSprite;
            }

            return null;
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
