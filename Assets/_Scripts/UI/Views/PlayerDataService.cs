using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

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

        [Inject] UGSDataService _ugsDataService;
        [Inject] AnalyticsServiceFacade _analytics;

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

            var ds = _ugsDataService;
            if (ds != null)
                ds.OnInitialized -= HandleDataServiceReady;

            OnProfileChanged -= SyncProfileToGameData;

            if (gameData != null)
                gameData.OnMiniGameEnd.OnRaised -= HandleMiniGameEnd;
        }

        void Start()
        {
            OnProfileChanged += SyncProfileToGameData;

            // Lifetime games/flight-time totals. Menu freestyle never raises this, so the
            // lava lamp stays out - matching the analytics game_completed boundary.
            if (gameData != null)
                gameData.OnMiniGameEnd.OnRaised += HandleMiniGameEnd;

            if (_ugsDataService == null)
            {
                // DI failed to supply the cloud data service (e.g. instantiated outside a
                // ContainerScope). Keep the local default profile so UI still renders rather
                // than throwing in Start and aborting the rest of init.
                CSDebug.LogWarning("[PlayerDataService] UGSDataService was not injected; running on local profile only.");
                return;
            }

            if (_ugsDataService.IsInitialized)
                HandleDataServiceReady();
            else
                _ugsDataService.OnInitialized += HandleDataServiceReady;
        }

        void HandleDataServiceReady()
        {
            _ugsDataService.OnInitialized -= HandleDataServiceReady;

            MergeCloudProfile();
            StampSessionLifecycle();
            ApplyPendingDebugCrystals();

            IsInitialized = true;
            OnProfileChanged?.Invoke(CurrentProfile);

            // Backfill this account into the public name registry once per session, so
            // players who set their name before the uniqueness feature shipped become
            // visible to other players' duplicate checks over time.
            if (DisplayNameValidator.Config.EnableUniquenessCheck && CurrentProfile != null)
                DisplayNameRegistry
                    .PublishOwnNameAsync(DisplayNameValidator.NormalizeForUniqueness(CurrentProfile.Identity.DisplayName))
                    .Forget();

            // Notify currency displays of the freshly-loaded cloud value. Those views subscribe
            // to the static balance event, which is otherwise only raised on mutation
            // (AddCrystals), so without this they'd show the local-default 0 until the next change.
            OnCrystalBalanceChanged?.Invoke(GetCrystalBalance());
        }

        /// <summary>
        /// Stamps the account timeline once per session: first-seen (once ever), last-seen,
        /// session count, and the client build/platform. These are the segmentation
        /// denominators every "is this regression real" question needs, and nothing recorded
        /// them before.
        /// </summary>
        void StampSessionLifecycle()
        {
            if (CurrentProfile?.Lifecycle == null) return;

            var lifecycle = CurrentProfile.Lifecycle;
            long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (lifecycle.FirstSeenUtcMs == 0)
                lifecycle.FirstSeenUtcMs = nowUtcMs;

            lifecycle.LastSeenUtcMs = nowUtcMs;
            lifecycle.SessionCount++;
            lifecycle.LastAppVersion = Application.version;
            lifecycle.LastPlatform = Application.platform.ToString();

            SyncCurrentProfileToRepo();
        }

        int _lastRecordedGameSequence;

        void HandleMiniGameEnd()
        {
            // Some modes raise OnMiniGameEnd more than once; latch the clock's game sequence
            // so lifetime totals count each game exactly once.
            if (FlightClock.CompletedGameSequence == _lastRecordedGameSequence)
                return;

            _lastRecordedGameSequence = FlightClock.CompletedGameSequence;
            RecordGameCompleted(FlightClock.LastGameSeconds);
        }

        /// <summary>
        /// Adds one game's flight time to the lifetime total and bumps the completed-game
        /// count. Called at game end; the lifetime total must reconcile with the sum of the
        /// per-game flight_time_seconds analytics events.
        /// </summary>
        public void RecordGameCompleted(float flightTimeSeconds)
        {
            if (CurrentProfile?.Lifecycle == null) return;

            CurrentProfile.Lifecycle.GamesCompleted++;
            if (flightTimeSeconds > 0f)
                CurrentProfile.Lifecycle.TotalFlightTimeSeconds += flightTimeSeconds;

            ScheduleSave();
        }

        /// <summary>
        /// Adds flight time to the lifetime total without counting a game. This is how menu
        /// freestyle lands here: it is time at the stick, but no game was completed, so
        /// <see cref="ProfileLifecycle.GamesCompleted"/> must not move.
        /// </summary>
        public void RecordFlightTime(float flightTimeSeconds)
        {
            if (CurrentProfile?.Lifecycle == null || flightTimeSeconds <= 0f) return;

            CurrentProfile.Lifecycle.TotalFlightTimeSeconds += flightTimeSeconds;
            ScheduleSave();
        }

        /// <summary>
        /// Merges cloud profile data from UGSDataService.ProfileRepo on top of local defaults.
        /// Performs union merge for unlocked rewards (local wins ties).
        /// </summary>
        void MergeCloudProfile()
        {
            var ds = _ugsDataService;
            if (ds?.ProfileRepo == null) return;

            var cloudData = ds.ProfileRepo.Data;
            if (cloudData?.Identity == null || string.IsNullOrEmpty(cloudData.Identity.UserId))
            {
                // No cloud profile → push local defaults to cloud
                SyncCurrentProfileToRepo();
                return;
            }

            // Merge unlocked rewards: union of local + cloud sets
            bool needsResync = false;
            var localRewards = CurrentProfile.Economy?.UnlockedRewardIds ?? new List<string>();
            var cloudRewards = cloudData.Economy.UnlockedRewardIds ?? new List<string>();
            foreach (var rewardId in localRewards)
            {
                if (!cloudRewards.Contains(rewardId))
                {
                    cloudRewards.Add(rewardId);
                    needsResync = true;
                }
            }
            cloudData.Economy.UnlockedRewardIds = cloudRewards;

            // Update local userId from auth
            try
            {
                if (UnityServices.State == ServicesInitializationState.Initialized &&
                    AuthenticationService.Instance != null &&
                    AuthenticationService.Instance.IsSignedIn)
                {
                    cloudData.Identity.UserId = AuthenticationService.Instance.PlayerId;
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
            CurrentProfile = new PlayerProfileData();
            CurrentProfile.Identity.UserId = string.IsNullOrEmpty(playerId)
                ? Guid.NewGuid().ToString("N")
                : playerId;
            CurrentProfile.Identity.DisplayName = GenerateDefaultDisplayName();
            CurrentProfile.Identity.AvatarId = GetDefaultAvatarId();
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
            var ds = _ugsDataService;
            if (ds?.ProfileRepo == null || CurrentProfile == null) return;

            // Assign whole groups rather than individual fields: adding a field to any group
            // then needs no change here, which is one of the reasons the model is grouped.
            var repoData = ds.ProfileRepo.Data;
            repoData.SchemaVersion = CurrentProfile.SchemaVersion;
            repoData.Identity = CurrentProfile.Identity;
            repoData.Economy = CurrentProfile.Economy;
            repoData.Lifecycle = CurrentProfile.Lifecycle;

            ds.ProfileRepo.MarkDirty();
        }

        void ScheduleSave()
        {
            SyncCurrentProfileToRepo();
        }

        bool _immediateSaveInFlight;
        bool _immediateSaveRequestedAgain;

        /// <summary>
        /// Pushes the profile to UGS Cloud Save immediately (in addition to the debounced save),
        /// so deliberate user actions like changing the avatar persist right away rather than
        /// after the ~1.5s debounce. Mirrors GameModeProgressionService.SaveImmediateAsync.
        ///
        /// Calls COALESCE rather than overlap. Every caller pairs this with ScheduleSave(), which
        /// has already copied the current profile into the repo, so a flush that is already in
        /// flight will carry any newer data anyway - and two concurrent SaveAsync calls against one
        /// repository is a race worth not having. A request arriving mid-flush therefore sets a
        /// flag and the loop below flushes exactly once more, instead of starting a second write.
        ///
        /// This was `async void`, which is why it mattered: two rapid deliberate actions (the
        /// username confirm button was clickable twice, see AuthenticationSceneController) issued
        /// overlapping saves, and an exception escaping an `async void` cannot be observed by any
        /// caller - it goes straight to the runtime as unhandled.
        /// </summary>
        void SaveProfileImmediateAsync()
        {
            if (_immediateSaveInFlight)
            {
                _immediateSaveRequestedAgain = true;
                return;
            }

            RunImmediateSaveAsync().Forget();
        }

        async UniTaskVoid RunImmediateSaveAsync()
        {
            _immediateSaveInFlight = true;
            try
            {
                do
                {
                    _immediateSaveRequestedAgain = false;

                    var repo = _ugsDataService?.ProfileRepo;
                    if (repo == null) return;

                    try
                    {
                        await repo.SaveAsync();
                    }
                    catch (Exception e)
                    {
                        CSDebug.LogWarning($"[PlayerDataService] Immediate profile save failed: {e.Message}. " +
                                           "Falling back to the debounced save.");
                        return;
                    }
                }
                while (_immediateSaveRequestedAgain);
            }
            finally
            {
                _immediateSaveInFlight = false;
            }
        }

        // ----------------- Public API -----------------

        /// <summary>
        /// Flushes the profile to Cloud Save immediately, on top of the debounced save.
        /// Use for writes where a dropped save is player-visible and unacceptable - notably
        /// real-money entitlements (<see cref="CosmicShore.Core.EpisodeTokenService"/>).
        /// </summary>
        public void PersistProfileNow()
        {
            ScheduleSave();
            SaveProfileImmediateAsync();
        }

        public void SetAvatarId(int avatarId)
        {
            if (CurrentProfile == null)
                return;

            CurrentProfile.Identity.AvatarId = avatarId;
            // OnProfileChanged drives the menu UI (ProfileScreen/widgets), gameData.LocalPlayerAvatarId,
            // and the local Player's NetAvatarId (Player.HandleProfileLoadedAfterSpawn → replicates
            // the new avatar to every peer in-game).
            OnProfileChanged?.Invoke(CurrentProfile);
            ScheduleSave();
            SaveProfileImmediateAsync(); // persist the avatar to UGS now, not just on debounce
        }

        /// <summary>
        /// The ONLY way a display name is changed. Runs the full local rule set
        /// (length, characters, format, reserved names, profanity — see
        /// <see cref="DisplayNameValidator"/>), then the global duplicate check
        /// (<see cref="DisplayNameRegistry"/>), and only then writes the profile,
        /// claims the name in the public registry, and syncs the UGS player name.
        /// The returned result carries the user-facing failure message on rejection
        /// and the sanitized name that was saved on success.
        /// </summary>
        public async UniTask<DisplayNameValidationResult> TrySetDisplayNameAsync(string requestedName)
        {
            var validation = DisplayNameValidator.Validate(requestedName);
            if (!validation.IsValid)
                return validation;

            if (CurrentProfile == null)
                return DisplayNameValidationResult.Fail(DisplayNameError.ServiceUnavailable,
                    "Profile isn't ready yet. Try again in a moment.");

            string sanitized = validation.SanitizedName;
            string normalized = DisplayNameValidator.NormalizeForUniqueness(sanitized);
            string currentNormalized = DisplayNameValidator.NormalizeForUniqueness(CurrentProfile.Identity.DisplayName);

            // Re-claiming your own name (e.g. changing only casing/spacing) never needs
            // an availability check — the registry entry is already yours.
            if (DisplayNameValidator.Config.EnableUniquenessCheck &&
                !string.Equals(normalized, currentNormalized, StringComparison.Ordinal))
            {
                var availability = await DisplayNameRegistry.CheckAvailabilityAsync(normalized);

                if (availability == DisplayNameAvailability.Taken)
                    return DisplayNameValidationResult.Fail(DisplayNameError.Taken,
                        "That name is already taken. Try another one.");

                if (availability == DisplayNameAvailability.Unknown)
                {
                    if (DisplayNameValidator.Config.BlockWhenUniquenessUnknown)
                        return DisplayNameValidationResult.Fail(DisplayNameError.ServiceUnavailable,
                            "Can't check name availability right now. Try again later.");

                    CSDebug.LogWarning($"[PlayerDataService] Availability unknown for '{sanitized}' - allowing the change (fail-open policy).");
                }
            }

            ApplyValidatedDisplayName(sanitized);
            DisplayNameRegistry.PublishOwnNameAsync(normalized).Forget();
            SyncUgsPlayerNameAsync(sanitized).Forget();

            return validation;
        }

        /// <summary>
        /// Raw profile write. Private on purpose: every caller must come through
        /// <see cref="TrySetDisplayNameAsync"/> so no UI can skip validation.
        /// </summary>
        void ApplyValidatedDisplayName(string displayName)
        {
            CurrentProfile.Identity.DisplayName = displayName;
            OnProfileChanged?.Invoke(CurrentProfile);
            ScheduleSave();
            SaveProfileImmediateAsync(); // a chosen name is a deliberate action - persist now
        }

        /// <summary>
        /// Keeps the UGS account player name in sync with the Cloud Save display name,
        /// otherwise friends see the auto-generated "Pilot9898" format in their friend
        /// list. UGS player names cannot contain spaces or punctuation, so the name is
        /// compacted ("Sky Walker" → "SkyWalker") instead of failing silently.
        /// </summary>
        async UniTask SyncUgsPlayerNameAsync(string displayName)
        {
            var sb = new System.Text.StringBuilder(displayName.Length);
            foreach (char c in displayName)
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);

            if (sb.Length == 0)
                return;

            try
            {
                if (UnityServices.State == ServicesInitializationState.Initialized &&
                    AuthenticationService.Instance != null &&
                    AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.UpdatePlayerNameAsync(sb.ToString()).AsMainThread();
                }
            }
            catch (Exception ex)
            {
                CSDebug.LogWarning($"[PlayerDataService] UpdatePlayerNameAsync failed (non-critical): {ex.Message}");
            }
        }

        void SyncProfileToGameData(PlayerProfileData data)
        {
            if (gameData != null)
            {
                gameData.LocalPlayerDisplayName = data.Identity.DisplayName;
                gameData.LocalPlayerAvatarId = data.Identity.AvatarId;
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

        public int GetCrystalBalance()
        {
            return CurrentProfile?.Economy?.CrystalBalance ?? 0;
        }

        public int AddCrystals(int amount, string source = null)
        {
            if (CurrentProfile == null || amount <= 0) return GetCrystalBalance();

            CurrentProfile.Economy.CrystalBalance += amount;
            CurrentProfile.Economy.LifetimeCrystalsEarned += amount;
            ScheduleSave();
            OnCrystalBalanceChanged?.Invoke(CurrentProfile.Economy.CrystalBalance);
            OnProfileChanged?.Invoke(CurrentProfile);
            _analytics?.RecordCrystalsEarned(amount, source, CurrentProfile.Economy.CrystalBalance);
            CSDebug.Log($"[PlayerDataService] Added {amount} crystals. Balance: {CurrentProfile.Economy.CrystalBalance}");
            return CurrentProfile.Economy.CrystalBalance;
        }

        public bool TrySpendCrystals(int amount, string source = null)
        {
            if (CurrentProfile == null || amount <= 0) return false;
            if (CurrentProfile.Economy.CrystalBalance < amount)
            {
                _analytics?.RecordCrystalSpendBlocked(amount, source, CurrentProfile.Economy.CrystalBalance);
                return false;
            }

            CurrentProfile.Economy.CrystalBalance -= amount;
            CurrentProfile.Economy.LifetimeCrystalsSpent += amount;
            ScheduleSave();
            OnCrystalBalanceChanged?.Invoke(CurrentProfile.Economy.CrystalBalance);
            OnProfileChanged?.Invoke(CurrentProfile);
            _analytics?.RecordCrystalsSpent(amount, source, CurrentProfile.Economy.CrystalBalance);
            CSDebug.Log($"[PlayerDataService] Spent {amount} crystals. Balance: {CurrentProfile.Economy.CrystalBalance}");
            return true;
        }

        /// <summary>
        /// Marks a reward as unlocked in the player's profile.
        /// </summary>
        public void UnlockReward(string rewardId)
        {
            if (CurrentProfile == null || string.IsNullOrEmpty(rewardId))
                return;

            CurrentProfile.Economy.UnlockedRewardIds ??= new List<string>();

            if (CurrentProfile.Economy.UnlockedRewardIds.Contains(rewardId))
                return;

            CurrentProfile.Economy.UnlockedRewardIds.Add(rewardId);
            OnProfileChanged?.Invoke(CurrentProfile);
            ScheduleSave();
            CSDebug.Log($"[PlayerDataService] Reward unlocked: {rewardId}");
        }

        /// <summary>
        /// Checks if a reward has been unlocked.
        /// </summary>
        public bool IsRewardUnlocked(string rewardId)
        {
            return CurrentProfile?.Economy?.UnlockedRewardIds != null &&
                   CurrentProfile.Economy.UnlockedRewardIds.Contains(rewardId);
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
                CurrentProfile.Economy.CrystalBalance += pending;
                ScheduleSave();
                OnCrystalBalanceChanged?.Invoke(CurrentProfile.Economy.CrystalBalance);
                CSDebug.Log($"[PlayerDataService] Applied {pending} pending debug crystals. Balance: {CurrentProfile.Economy.CrystalBalance}");
            }
#endif
        }
    }
}
