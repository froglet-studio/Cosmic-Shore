using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CosmicShore.Game.Party;
using CosmicShore.Services.Auth;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.Profile
{
    /// <summary>
    /// Loads and saves the local player's profile (displayName + avatarId).
    /// Single source of truth for profile data — PartyManager reads from here.
    ///
    /// CHANGES from original:
    ///   - Added static Instance for easy access by PartyManager.
    ///   - After loading/creating a profile, display name is pushed to
    ///     AuthenticationService.UpdatePlayerNameAsync() so UGS player name
    ///     (used by MultiplayerSetup.GetPlayerNameAsync) stays in sync.
    /// </summary>
    public class PlayerDataService : MonoBehaviour
    {
        // -----------------------------------------------------------------------------------------
        // Singleton

        public static PlayerDataService Instance { get; private set; }

        // -----------------------------------------------------------------------------------------
        // Inspector

        [Header("Cloud Save")]
        [SerializeField] private string cloudSaveProfileKey = "player_profile";
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Header("UI")]
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private Image    avatarImage;

        [Header("Auth Hook")]
        [SerializeField] private AuthenticationController authController;

        // -----------------------------------------------------------------------------------------
        // Public state

        public PlayerProfileData CurrentProfile { get; private set; }
        public bool              IsInitialized  { get; private set; }

        public event Action<PlayerProfileData> OnProfileChanged;

        // -----------------------------------------------------------------------------------------
        // Unity Lifecycle

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        async void Start()
        {
            try
            {
                if (authController != null)
                    authController.OnSignedIn += HandleSignedInFromAuth;

                OnProfileChanged += HandleProfileChanged;

                if (UnityServices.State != ServicesInitializationState.Initialized) return;

                bool canCheckAuth = true;
                try   { _ = AuthenticationService.Instance.IsSignedIn; }
                catch { canCheckAuth = false; }

                if (canCheckAuth && AuthenticationService.Instance.IsSignedIn)
                    await InitializeAfterAuth();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerDataService] Start error: {e.Message}");
            }
        }

        // -----------------------------------------------------------------------------------------
        // Auth callbacks

        async void HandleSignedInFromAuth(string playerId)
        {
            try
            {
                if (IsInitialized) return;
                await InitializeAfterAuth();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerDataService] HandleSignedIn error: {e.Message}");
            }
        }

        // -----------------------------------------------------------------------------------------
        // Initialization

        private async Task InitializeAfterAuth()
        {
            if (IsInitialized) return;

            string playerId        = null;
            bool   canUseCloudSave = false;

            if (UnityServices.State == ServicesInitializationState.Initialized)
            {
                try
                {
                    canUseCloudSave = AuthenticationService.Instance != null &&
                                      AuthenticationService.Instance.IsSignedIn;
                }
                catch { canUseCloudSave = false; }
            }

            if (canUseCloudSave)
                playerId = AuthenticationService.Instance.PlayerId;

            await LoadOrCreateProfileAsync(playerId, canUseCloudSave);

            IsInitialized = true;
            OnProfileChanged?.Invoke(CurrentProfile);

            // KEY CHANGE: sync display name to UGS so MultiplayerSetup.GetPlayerNameAsync() returns it
            await SyncDisplayNameToUGSAsync();

            PartyManager.Instance?.SyncProfileFromPlayerDataService();
        }

        // -----------------------------------------------------------------------------------------
        // Load / Create

        async Task LoadOrCreateProfileAsync(string playerId, bool canUseCloudSave)
        {
            if (!canUseCloudSave)
            {
                Debug.LogWarning("[PlayerDataService] Not signed in. Using local-only profile.");
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

                    if (data != null) { CurrentProfile = data; return; }

                    Debug.LogWarning("[PlayerDataService] Failed to parse profile JSON. Creating default.");
                    CreateLocalDefaultProfile(playerId);
                }
                else
                {
                    CreateLocalDefaultProfile(playerId);
                    await SaveProfileAsync(canUseCloudSave);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerDataService] Load failed: {e.Message}");
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

        // -----------------------------------------------------------------------------------------
        // UGS Name Sync

        /// <summary>
        /// Pushes the local profile's displayName to the UGS Authentication service
        /// so MultiplayerSetup.GetPlayerNameAsync() always returns the correct name.
        /// </summary>
        private async Task SyncDisplayNameToUGSAsync()
        {
            if (CurrentProfile == null) return;

            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized) return;
                if (AuthenticationService.Instance == null || !AuthenticationService.Instance.IsSignedIn) return;

                await AuthenticationService.Instance.UpdatePlayerNameAsync(CurrentProfile.displayName);
                Debug.Log($"[PlayerDataService] UGS player name synced to '{CurrentProfile.displayName}'");
            }
            catch (Exception e)
            {
                // Non-fatal: name sync failure shouldn't block the game
                Debug.LogWarning($"[PlayerDataService] UGS name sync failed: {e.Message}");
            }
        }

        // -----------------------------------------------------------------------------------------
        // Save

        async Task SaveProfileAsync(bool canUseCloudSave)
        {
            if (CurrentProfile == null || !canUseCloudSave) return;

            try
            {
                var json = JsonUtility.ToJson(CurrentProfile);
                var data = new Dictionary<string, object> { { cloudSaveProfileKey, json } };
                await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerDataService] Save failed: {e.Message}");
            }
        }

        // -----------------------------------------------------------------------------------------
        // Public API

        public async void SetAvatarId(int avatarId)
        {
            try
            {
                if (CurrentProfile == null) return;

                CurrentProfile.avatarId = avatarId;
                OnProfileChanged?.Invoke(CurrentProfile);

                bool canUseCloudSave = CanUseCloudSave();
                await SaveProfileAsync(canUseCloudSave);

                // Update PartyManager's cached local profile
               PartyManager.Instance?.SyncProfileFromPlayerDataService();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerDataService] SetAvatarId failed: {e.Message}");
            }
        }

        public async Task SetDisplayNameAsync(string displayName)
        {
            try
            {
                if (CurrentProfile == null) return;

                CurrentProfile.displayName = displayName;
                OnProfileChanged?.Invoke(CurrentProfile);

                bool canUseCloudSave = CanUseCloudSave();
                await SaveProfileAsync(canUseCloudSave);

                // Sync the new name to UGS immediately
                await SyncDisplayNameToUGSAsync();

                // Update PartyManager's cached local profile
                CosmicShore.Game.Party.PartyManager.Instance?.SyncProfileFromPlayerDataService();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerDataService] SetDisplayNameAsync failed: {e.Message}");
            }
        }

        // -----------------------------------------------------------------------------------------
        // UI

        void HandleProfileChanged(PlayerProfileData data)
        {
            if (displayNameText != null)
                displayNameText.text = data.displayName;

            var sprite = ResolveAvatarSprite(data.avatarId);
            if (avatarImage != null)
                avatarImage.sprite = sprite;
        }

        Sprite ResolveAvatarSprite(int avatarId)
        {
            return !profileIcons ? null : (from icon in profileIcons.profileIcons where icon.Id == avatarId select icon.IconSprite).FirstOrDefault();
        }

        public void RefreshProfileVisuals()
        {
            if (CurrentProfile == null) return;
            OnProfileChanged?.Invoke(CurrentProfile);
        }

        // -----------------------------------------------------------------------------------------
        // Helpers

        private bool CanUseCloudSave()
        {
            return UnityServices.State == ServicesInitializationState.Initialized &&
                   AuthenticationService.Instance != null &&
                   AuthenticationService.Instance.IsSignedIn;
        }
    }
}