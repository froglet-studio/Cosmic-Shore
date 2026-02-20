using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CosmicShore.Game.Party;
using CosmicShore.Utilities;
using TMPro;
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
    /// Authentication is now driven by AuthenticationDataVariable (no UGS AuthenticationService references).
    /// </summary>
    public class PlayerDataService : MonoBehaviour
    {
        // -----------------------------------------------------------------------------------------
        // Singleton

        public static PlayerDataService Instance { get; private set; }

        // -----------------------------------------------------------------------------------------
        // Inspector

        [Header("Auth (Source of Truth)")]
        [SerializeField] AuthenticationDataVariable authenticationDataVariable;
        AuthenticationData authenticationData => authenticationDataVariable.Value;

        [Header("Cloud Save")]
        [SerializeField] private string cloudSaveProfileKey = "player_profile";
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Header("UI")]
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private Image avatarImage;

        // -----------------------------------------------------------------------------------------
        // Public state

        public PlayerProfileData CurrentProfile { get; private set; }
        public bool IsInitialized { get; private set; }

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
                OnProfileChanged += HandleProfileChanged;

                // If services aren't initialized, we can't use Cloud Save anyway.
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    return;

                // If already signed in (eg: auth completed before this service started), initialize now.
                if (IsAuthSignedIn())
                    await InitializeAfterAuth();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerDataService] Start error: {e.Message}");
            }
        }

        // -----------------------------------------------------------------------------------------
        // Hooks for Soap events (wire these via ScriptableEvent listeners in the scene)

        /// <summary>
        /// Hook this method to AuthenticationData.OnSignedIn (ScriptableEventNoParam) via a listener component.
        /// </summary>
        public async void HandleSignedInEvent()
        {
            try
            {
                if (IsInitialized) return;
                await InitializeAfterAuth();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerDataService] HandleSignedInEvent error: {e.Message}");
            }
        }

        /// <summary>
        /// Hook this method to AuthenticationData.OnSignedOut (ScriptableEventNoParam) via a listener component.
        /// Optional: clears state so it can re-init after next sign-in.
        /// </summary>
        public void HandleSignedOutEvent()
        {
            // Depending on your design, you may or may not want to reset.
            // Resetting allows re-initialization after a new sign-in.
            IsInitialized = false;
            CurrentProfile = null;
        }

        // -----------------------------------------------------------------------------------------
        // Initialization

        private async Task InitializeAfterAuth()
        {
            if (IsInitialized) return;

            bool canUseCloudSave = CanUseCloudSave();
            string playerId = canUseCloudSave ? authenticationData.PlayerId : null;

            await LoadOrCreateProfileAsync(playerId, canUseCloudSave);

            IsInitialized = true;
            OnProfileChanged?.Invoke(CurrentProfile);

            // PartyManager reads from here
            PartyManager.Instance?.SyncProfileFromPlayerDataService();
        }

        // -----------------------------------------------------------------------------------------
        // Load / Create

        async Task LoadOrCreateProfileAsync(string playerId, bool canUseCloudSave)
        {
            if (!canUseCloudSave)
            {
                Debug.LogWarning("[PlayerDataService] Not signed in (via AuthenticationData). Using local-only profile.");
                CreateLocalDefaultProfile(playerId);
                return;
            }

            try
            {
                var keys = new HashSet<string> { cloudSaveProfileKey };
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
                userId = string.IsNullOrEmpty(playerId) ? Guid.NewGuid().ToString("N") : playerId,
                displayName = "Pilot",
                avatarId = GetDefaultAvatarId()
            };
        }

        int GetDefaultAvatarId()
        {
            if (profileIcons != null && profileIcons.profileIcons.Count > 0)
                return profileIcons.profileIcons[0].Id;
            return 0;
        }

        // -----------------------------------------------------------------------------------------
        // Save

        async Task SaveProfileAsync(bool canUseCloudSave)
        {
            if (CurrentProfile == null || !canUseCloudSave)
                return;

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

                // NOTE:
                // Previously this synced to UGS AuthenticationService.UpdatePlayerNameAsync().
                // That responsibility now belongs to your auth layer (if you still need it).

                PartyManager.Instance?.SyncProfileFromPlayerDataService();
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
            return !profileIcons
                ? null
                : (from icon in profileIcons.profileIcons
                   where icon.Id == avatarId
                   select icon.IconSprite).FirstOrDefault();
        }

        public void RefreshProfileVisuals()
        {
            if (CurrentProfile == null) return;
            OnProfileChanged?.Invoke(CurrentProfile);
        }

        // -----------------------------------------------------------------------------------------
        // Helpers

        bool IsAuthSignedIn()
        {
            // Treat "SignedIn" as the authoritative state.
            // (Some flows might set IsSignedIn true slightly earlier/later — we accept either.)
            return authenticationData != null &&
                   (authenticationData.State == AuthenticationData.AuthState.SignedIn || authenticationData.IsSignedIn);
        }

        private bool CanUseCloudSave()
        {
            return UnityServices.State == ServicesInitializationState.Initialized &&
                   IsAuthSignedIn() &&
                   !string.IsNullOrEmpty(authenticationData.PlayerId);
        }
    }
}