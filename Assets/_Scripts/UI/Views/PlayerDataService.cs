using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.Data;
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
            {
                ds.OnInitialized -= HandleDataServiceReady;
                if (ds.ProfileRepo != null)
                    ds.ProfileRepo.OnDataChanged -= HandleProfileRepoDataChanged;
            }

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
            if (ServerEconomyGate.Enabled) ConfirmWithServer(ServerEconomy.StateAsync());
            if (ServerEntitlementGate.Enabled) EpisodeTokenService.RefreshFromServer().Forget();

            if (_ugsDataService.ProfileRepo != null)
            {
                _ugsDataService.ProfileRepo.OnDataChanged -= HandleProfileRepoDataChanged;
                _ugsDataService.ProfileRepo.OnDataChanged += HandleProfileRepoDataChanged;
            }

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
        /// The profile repository can REPLACE its data object after this service merged it: a
        /// load that failed at sign-in and later recovers adopts the real cloud record
        /// (CloudDataRepository.TryResolveFailedLoadAsync), and a late sign-in after an offline
        /// boot reloads it (UGSDataService.ReloadFromCloudAfterLateSignInAsync). CurrentProfile
        /// still pointed at the stand-in, so the next SyncCurrentProfileToRepo (any AddCrystals,
        /// any game end) copied the stand-in's identity, economy and lifecycle over the adopted
        /// record and uploaded them - erasing the real crystals, name, owned episodes and
        /// redeemed orders, the very loss BH-2.1 exists to prevent. Re-merge when the repo now
        /// holds a REAL record that is not ours; a fresh/wiped one (no UserId) is left alone.
        /// </summary>
        void HandleProfileRepoDataChanged()
        {
            var repoData = _ugsDataService?.ProfileRepo?.Data;
            if (repoData == null || ReferenceEquals(repoData, CurrentProfile)) return;
            if (repoData.Identity == null || string.IsNullOrEmpty(repoData.Identity.UserId)) return;

            MergeCloudProfile();
            OnProfileChanged?.Invoke(CurrentProfile);
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
            if (profileIcons != null && profileIcons.ProfileIcons.Count > 0)
                return profileIcons.ProfileIcons[0].Id;

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

        /// <summary>
        /// Applies an avatar to the in-memory profile (menu UI, LocalPlayerAvatarId, the local
        /// Player's NetAvatarId) WITHOUT touching the cloud repo. For a pick made before the cloud
        /// profile has loaded (offline boot, or the window before HandleDataServiceReady): syncing
        /// then would copy a default profile over the record that has not been read yet, which is
        /// why <see cref="SetAvatarId"/> is gated on <c>IsInitialized</c> by its caller.
        /// </summary>
        public void ApplyAvatarIdLocally(int avatarId)
        {
            if (CurrentProfile == null)
                return;

            CurrentProfile.Identity.AvatarId = avatarId;
            OnProfileChanged?.Invoke(CurrentProfile);
        }

        /// <summary>Sets the favourite pilot (an SO_Captain asset name; empty clears it) and saves.</summary>
        public void SetFavouritePilot(string pilotKey)
        {
            if (CurrentProfile == null)
                return;

            CurrentProfile.Identity.FavouritePilot = pilotKey ?? string.Empty;
            OnProfileChanged?.Invoke(CurrentProfile);
            ScheduleSave();
        }

        /// <summary>A bought Hangar level (<c>HangarUpgradeStore</c>), 0 when none. Vessel and element are the enums' values.</summary>
        public int GetHangarUpgradeLevel(int vessel, int element)
        {
            var upgrades = CurrentProfile?.Economy?.HangarUpgrades;
            if (upgrades == null) return 0;
            foreach (var u in upgrades)
                if (u != null && u.Vessel == vessel && u.Element == element) return u.Level;
            return 0;
        }

        /// <summary>
        /// Saves a Hangar level on the profile. Called right after <see cref="TrySpendCrystals"/> pays for it, so
        /// the debit and the level ride the same debounced save.
        /// </summary>
        public void SetHangarUpgradeLevel(int vessel, int element, int level)
        {
            if (CurrentProfile?.Economy == null)
                return;

            var upgrades = CurrentProfile.Economy.HangarUpgrades ??= new List<HangarUpgradeLevel>();
            var entry = upgrades.Find(u => u != null && u.Vessel == vessel && u.Element == element);
            if (entry == null)
                upgrades.Add(entry = new HangarUpgradeLevel { Vessel = vessel, Element = element });
            entry.Level = level;
            OnProfileChanged?.Invoke(CurrentProfile);
            ScheduleSave();
        }

        // ---------------------------------------------------------------- elemental crystals (section 97)

        /// <summary>The player's crystals of one element, or null when they have never had any.</summary>
        public ElementalCrystalBalance GetElementalCrystals(Element element)
        {
            var list = CurrentProfile?.Economy?.ElementalCrystals;
            if (list == null) return null;
            foreach (var b in list)
                if (b != null && b.Element == (int)element) return b;
            return null;
        }

        /// <summary>How many crystals of <paramref name="element"/> the player holds now.</summary>
        public int GetElementalCrystalsHeld(Element element) => GetElementalCrystals(element)?.Held ?? 0;

        /// <summary>How many crystals of <paramref name="element"/> the player has spent, ever.</summary>
        public long GetElementalCrystalsSpent(Element element) => GetElementalCrystals(element)?.Spent ?? 0;

        /// <summary>
        /// Credits elemental crystals. Client-side for now, like the OmniCrystal balance before its server gate:
        /// nothing calls it yet, because how a match pays them out is the owner's call (section 97 lists it).
        /// </summary>
        public int AddElementalCrystals(Element element, int amount, string source = null)
        {
            if (CurrentProfile?.Economy == null || amount <= 0 || !IsElement(element)) return GetElementalCrystalsHeld(element);
            var b = ElementalBalanceFor(element);
            b.Held += amount;
            b.Earned += amount;
            OnProfileChanged?.Invoke(CurrentProfile);
            ScheduleSave();
            CSDebug.LogVerbose(CSLogChannel.CloudData, $"[PlayerDataService] Added {amount} {element} crystals ({source}). Held: {b.Held}");
            return b.Held;
        }

        /// <summary>Spends elemental crystals if the player holds enough; false (and nothing changes) otherwise.</summary>
        public bool TrySpendElementalCrystals(Element element, int amount, string source = null)
        {
            if (CurrentProfile?.Economy == null || amount <= 0 || !IsElement(element)) return false;
            var b = GetElementalCrystals(element);
            if (b == null || b.Held < amount) return false;
            b.Held -= amount;
            b.Spent += amount;
            OnProfileChanged?.Invoke(CurrentProfile);
            ScheduleSave();
            CSDebug.LogVerbose(CSLogChannel.CloudData, $"[PlayerDataService] Spent {amount} {element} crystals ({source}). Held: {b.Held}");
            return true;
        }

        ElementalCrystalBalance ElementalBalanceFor(Element element)
        {
            var list = CurrentProfile.Economy.ElementalCrystals ??= new List<ElementalCrystalBalance>();
            var b = GetElementalCrystals(element);
            if (b == null) list.Add(b = new ElementalCrystalBalance { Element = (int)element });
            return b;
        }

        static bool IsElement(Element e) => e is Element.Charge or Element.Mass or Element.Space or Element.Time;

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

        /// <summary>
        /// Any avatar from the shipped set, chosen at random.
        ///
        /// <para>For the AI seats an arcade card previews: a bot has no profile to read an avatar
        /// from, and giving them all icon 0 makes four seats read as one player repeated.</para>
        /// </summary>
        public Sprite GetRandomAvatarSprite()
        {
            if (profileIcons == null || profileIcons.ProfileIcons == null ||
                profileIcons.ProfileIcons.Count == 0)
                return null;

            return profileIcons.ProfileIcons[UnityEngine.Random.Range(0, profileIcons.ProfileIcons.Count)].IconSprite;
        }

        public Sprite GetAvatarSprite(int avatarId)
        {
            if (profileIcons == null || profileIcons.ProfileIcons == null || profileIcons.ProfileIcons.Count == 0)
                return null;

            for (int i = 0; i < profileIcons.ProfileIcons.Count; i++)
            {
                if (profileIcons.ProfileIcons[i].Id == avatarId)
                    return profileIcons.ProfileIcons[i].IconSprite;
            }

            return profileIcons.ProfileIcons[0].IconSprite;
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
            CSDebug.LogVerbose(CSLogChannel.CloudData, $"[PlayerDataService] Added {amount} crystals. Balance: {CurrentProfile.Economy.CrystalBalance}");
            if (ServerEconomyGate.Enabled) ConfirmWithServer(ServerEconomy.EarnAsync(source ?? DebugSource, amount));
            return CurrentProfile.Economy.CrystalBalance;
        }

        /// <summary>
        /// Buys a vessel: the crystals leave the local wallet at once and, with the server wallet on, the server sells
        /// it (<c>economy.js unlockVessel</c>), charging its own price and recording the vessel in the same write, so an
        /// edited save can neither unlock it for free nor keep it. The server's answer replaces the local wallet and
        /// vessels (<see cref="ApplyServerWallet"/>).
        /// </summary>
        public bool TryBuyVessel(string vesselName, int cost)
        {
            if (!ServerEconomyGate.Enabled) return cost <= 0 || TrySpendCrystals(cost, "vessel_unlock");
            if (cost > 0 && !TrySpendCrystals(cost, "vessel_unlock", reportToServer: false)) return false;
            ConfirmWithServer(ServerEconomy.UnlockVesselAsync(vesselName));
            return true;
        }

        /// <summary>Buys the next Hangar level, the server way when its wallet is on (<see cref="TryBuyVessel"/>).</summary>
        public bool TryBuyHangarLevel(int vessel, int element, int cost)
        {
            if (!ServerEconomyGate.Enabled) return TrySpendCrystals(cost, "hangar_upgrade");
            if (!TrySpendCrystals(cost, "hangar_upgrade", reportToServer: false)) return false;
            ConfirmWithServer(ServerEconomy.UpgradeHangarAsync(vessel, element));
            return true;
        }

        public bool TrySpendCrystals(int amount, string source = null) => TrySpendCrystals(amount, source, reportToServer: true);

        /// <param name="reportToServer">False when the caller sends the server its own request for this purchase (a
        /// vessel or a Hangar level), so the server is asked once.</param>
        public bool TrySpendCrystals(int amount, string source, bool reportToServer)
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
            CSDebug.LogVerbose(CSLogChannel.CloudData, $"[PlayerDataService] Spent {amount} crystals. Balance: {CurrentProfile.Economy.CrystalBalance}");
            if (ServerEconomyGate.Enabled && reportToServer) ConfirmWithServer(ServerEconomy.SpendAsync(source ?? DebugSource, amount, source));
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
            CSDebug.LogVerbose(CSLogChannel.CloudData, $"[PlayerDataService] Reward unlocked: {rewardId}");
        }

        /// <summary>
        /// Claims a once-only reward and its crystals together (today's toy activity). With the server-owned
        /// wallet on, the server checks the id (right day, not claimed before) and pays in one request; a
        /// separate UnlockReward + AddCrystals would reach it as an unknown earn source and be refused.
        /// </summary>
        public void ClaimReward(string rewardId, int crystals, string source)
        {
            if (CurrentProfile == null || string.IsNullOrEmpty(rewardId) || IsRewardUnlocked(rewardId))
                return;

            if (!ServerEconomyGate.Enabled)
            {
                UnlockReward(rewardId);
                if (crystals > 0) AddCrystals(crystals, source);
                return;
            }

            CurrentProfile.Economy.UnlockedRewardIds ??= new List<string>();
            CurrentProfile.Economy.UnlockedRewardIds.Add(rewardId);
            int amount = Mathf.Max(0, crystals);
            CurrentProfile.Economy.CrystalBalance += amount;
            CurrentProfile.Economy.LifetimeCrystalsEarned += amount;
            ScheduleSave();
            OnCrystalBalanceChanged?.Invoke(CurrentProfile.Economy.CrystalBalance);
            OnProfileChanged?.Invoke(CurrentProfile);
            if (amount > 0) _analytics?.RecordCrystalsEarned(amount, source, CurrentProfile.Economy.CrystalBalance);
            ConfirmWithServer(ServerEconomy.ClaimAsync(rewardId, amount));
        }

        // ----------------- Server-owned wallet (fair play, Threat Board T3) -----------------

        /// <summary>The earn/spend source the server allows only outside production (the Toolbox crystal field).</summary>
        const string DebugSource = "debug";

        int _pendingServerCalls;
        ServerEconomyResult _latestServerWallet;

        /// <summary>
        /// Every wallet change is applied locally at once (the UI never waits) and then confirmed by the server.
        /// When the last outstanding answer arrives, the server's wallet replaces the local one: a refused request
        /// (a forged amount, a reward claimed twice, a spend the server balance cannot cover) is undone there.
        /// Earlier answers are held, so two quick changes never flicker the balance back. A call that could not
        /// reach the server leaves the local value until the next answer.
        /// </summary>
        void ConfirmWithServer(UniTask<ServerEconomyResult> request)
        {
            _pendingServerCalls++;
            AwaitServerAsync(request).Forget();
        }

        async UniTaskVoid AwaitServerAsync(UniTask<ServerEconomyResult> request)
        {
            ServerEconomyResult result = null;
            try { result = await request; }
            finally
            {
                _pendingServerCalls--;
                if (result != null) _latestServerWallet = result;
                if (_pendingServerCalls == 0 && _latestServerWallet != null && this)
                {
                    ApplyServerWallet(_latestServerWallet);
                    _latestServerWallet = null;
                }
            }
        }

        void ApplyServerWallet(ServerEconomyResult wallet)
        {
            if (CurrentProfile == null) return;
            var economy = CurrentProfile.Economy;
            bool changed = economy.CrystalBalance != wallet.crystalBalance;

            economy.CrystalBalance = wallet.crystalBalance;
            economy.LifetimeCrystalsEarned = wallet.lifetimeCrystalsEarned;
            economy.LifetimeCrystalsSpent = wallet.lifetimeCrystalsSpent;
            economy.UnlockedRewardIds = wallet.unlockedRewardIds ?? new List<string>();
            ApplyServerUnlocks(wallet);

            ScheduleSave(); // PLAYER_PROFILE keeps a display copy; the server ignores it
            if (changed)
            {
                CSDebug.LogVerbose(CSLogChannel.CloudData, $"[PlayerDataService] Server wallet applied. Balance: {economy.CrystalBalance}");
                OnCrystalBalanceChanged?.Invoke(economy.CrystalBalance);
            }
            OnProfileChanged?.Invoke(CurrentProfile);
        }

        bool _unlockMigrationSent;

        /// <summary>
        /// The server's unlock records replace the client's (fair play T3). Until this account's old unlocks have been
        /// brought across once (<c>migrateUnlocks</c>), the local ones are sent instead and nothing is overwritten, so
        /// the first server answer can never take away something the player had.
        /// </summary>
        void ApplyServerUnlocks(ServerEconomyResult wallet)
        {
            if (!wallet.unlocksMigrated)
            {
                if (_unlockMigrationSent) return;
                _unlockMigrationSent = true;
                ConfirmWithServer(ServerEconomy.MigrateUnlocksAsync(VesselUnlockSystem.LocallyBoughtVesselNames(), LocalHangarLevels()));
                return;
            }

            var levels = CurrentProfile.Economy.HangarUpgrades ??= new List<HangarUpgradeLevel>();
            levels.Clear();
            foreach (var h in wallet.hangar ?? new List<ServerHangarLevel>())
                if (h != null && h.level > 0)
                    levels.Add(new HangarUpgradeLevel { Vessel = h.vessel, Element = h.element, Level = h.level });

            VesselUnlockSystem.ApplyServerOwnership(wallet.vessels ?? new List<string>());
        }

        List<ServerHangarLevel> LocalHangarLevels()
        {
            var list = new List<ServerHangarLevel>();
            foreach (var h in CurrentProfile?.Economy?.HangarUpgrades ?? new List<HangarUpgradeLevel>())
                if (h != null && h.Level > 0)
                    list.Add(new ServerHangarLevel { vessel = h.Vessel, element = h.Element, level = h.Level });
            return list;
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
                if (ServerEconomyGate.Enabled) ConfirmWithServer(ServerEconomy.EarnAsync(DebugSource, pending));
                OnCrystalBalanceChanged?.Invoke(CurrentProfile.Economy.CrystalBalance);
                CSDebug.LogVerbose(CSLogChannel.CloudData, $"[PlayerDataService] Applied {pending} pending debug crystals. Balance: {CurrentProfile.Economy.CrystalBalance}");
            }
#endif
        }
    }
}
