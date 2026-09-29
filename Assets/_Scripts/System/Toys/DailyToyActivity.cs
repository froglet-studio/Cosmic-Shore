using System;
using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    /// <summary>
    /// ONE activity a day, drawn from the toys themselves - the Toy Box's answer to the arcade's
    /// weekly challenge, and the same two rules underneath it.
    ///
    /// <para><b>The definition is DERIVED, only the claim is STORED.</b> Today's activity is a pure
    /// function of the UTC date over the live toys' own activity options, so it resolves on a cold
    /// launch, offline, and identically on every device - no server round trip and nothing to
    /// author. What is stored is the one thing that genuinely differs per player: whether they have
    /// already been paid for today (<see cref="PlayerDataService.UnlockReward"/>, which is
    /// cloud-merged, so a reinstall or a second device cannot re-earn the same day).</para>
    ///
    /// <para><b>The pool is the LIVE toys, never an authored list.</b> A
    /// <c>DailyActivityCatalog</c> naming "Connect the Dots &gt; Rainbow" would be exactly the
    /// second authority on what a toy does that the whole Toy Box is built to avoid
    /// (<c>Docs/HomeHub/ARCHITECTURE.md</c> §4.1.1) - and it would go out of step the first time a
    /// painting was renamed. A painting added tomorrow is in the draw for free.</para>
    ///
    /// <para><b>What counts as an ACTIVITY is the toy's own declaration, not a list of toys
    /// here.</b> <see cref="ToyShellOption.RequiresFreestyle"/> already means "this only means
    /// anything with the player at the stick" - a painting, a wander, a voyage - which is precisely
    /// the difference between an activity and a setting. So the same one flag divides this button
    /// from the shuffle beside it (<see cref="ToyShuffle"/>, which takes the settings), and neither
    /// needed a new field or a per-toy exception.</para>
    ///
    /// <para><b>Paid for TRYING, not for finishing.</b> A painting has no pass mark, a wander has no
    /// end, so there is nothing here to complete - the reward lands when the player actually STARTS
    /// the day's activity (<see cref="TryClaim"/>, called from the apply, not from the press), which
    /// is the honest reading of "have a go at this".</para>
    ///
    /// <para><b>No service object, no subscription.</b> Everything here is derived on demand and
    /// cached against the day key plus the registry's current surface list, so a cell swap that
    /// rebuilds the whole toybox invalidates it by identity rather than by an event. That also means
    /// there is no static event whose ordering against
    /// <see cref="ToyShellRegistry"/>'s own reset has to be got right.</para>
    /// </summary>
    public static class DailyToyActivity
    {
        /// <summary>
        /// The day's activity: which live toy offers it, and the path the Toy Box's detail window
        /// walks to select it. Check <see cref="IsValid"/> before use.
        /// </summary>
        public readonly struct Pick
        {
            public readonly IToyShellSurface Surface;
            public readonly ToyShellOption Option;

            /// <summary>The option's path for <c>ToyConfigureModal.BindToPath</c> - one label
            /// today, because the pool is a toy's top layer (see <see cref="Candidates"/>).</summary>
            public readonly IReadOnlyList<string> Path;

            /// <summary>The toy's display name, e.g. "Connect the Dots".</summary>
            public readonly string ToyName;

            /// <summary>The activity's own name, e.g. "Rainbow".</summary>
            public readonly string ActivityName;

            public Pick(IToyShellSurface surface, ToyShellOption option, string toyName)
            {
                Surface = surface;
                Option = option;
                ToyName = toyName ?? "";
                ActivityName = option != null ? option.Label ?? "" : "";
                Path = option != null ? new[] { option.Label ?? "" } : Array.Empty<string>();
            }

            public bool IsValid => Surface != null && Option != null && Option.Apply != null;
        }

        // ── The day ────────────────────────────────────────────────────────────

        /// <summary>
        /// The key of the UTC day an instant falls in.
        ///
        /// <para><b>InvariantCulture is load-bearing.</b> A bare <c>ToString("yyyy-MM-dd")</c>
        /// formats through <c>CurrentCulture</c>, whose CALENDAR is not always Gregorian, and Unity
        /// takes CurrentCulture from the device locale - so the same instant keys as
        /// <c>2026-09-24</c> on en-US and <c>1448-04-12</c> on ar-SA. Those players would draw a
        /// different activity from everyone around them and file their claim under a key nothing
        /// else reads. The weekly challenge paid for this once already
        /// (<see cref="WeeklyChallengeCatalogSO.WeekKeyFor"/>); it is the same trap one feature
        /// over.</para>
        ///
        /// <para>Deliberately NOT <c>WeeklyChallengeCatalogSO.DayKeyFor</c>: the Toy Box's day must
        /// not depend on the arcade's catalog asset being present. Only the HASH is shared, because
        /// the platform-independence argument for it is the same one and the project should have
        /// exactly one of those.</para>
        /// </summary>
        public static string DayKeyFor(DateTime utc) =>
            utc.ToUniversalTime().Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>Today's key.</summary>
        public static string TodayKey => DayKeyFor(DateTime.UtcNow);

        /// <summary>Time until the next activity - the next UTC midnight. Never negative.</summary>
        public static TimeSpan TimeUntilTomorrow
        {
            get
            {
                var now = DateTime.UtcNow;
                var span = now.Date.AddDays(1) - now;
                return span > TimeSpan.Zero ? span : TimeSpan.Zero;
            }
        }

        // ── The draw ───────────────────────────────────────────────────────────

        static readonly List<ToyShellOption> _optionScratch = new();
        static readonly List<Pick> _candidates = new();
        static readonly List<IToyShellSurface> _cachedSurfaces = new();
        static string _cachedDayKey = "";
        static bool _cached;

        /// <summary>
        /// Today's activity, or an invalid <see cref="Pick"/> when no toy is offering one - the
        /// toybox has not been built yet, or a cell swap has just torn it down. Callers draw
        /// "unavailable" rather than a live-looking button that does nothing.
        /// </summary>
        public static Pick Today
        {
            get
            {
                EnsureCandidates();
                if (_candidates.Count == 0) return default;

                uint hash = WeeklyChallengeCatalogSO.HashPeriodKey(_cachedDayKey);
                return _candidates[(int)(hash % (uint)_candidates.Count)];
            }
        }

        /// <summary>
        /// Every activity the live toys are currently offering, in a STABLE order: registry
        /// placement order, then each toy's own option order. That ordering is what makes the draw
        /// the same on every machine - a hash over an unstable order would hand two players
        /// different activities on the same date.
        /// </summary>
        public static IReadOnlyList<Pick> Candidates
        {
            get
            {
                EnsureCandidates();
                return _candidates;
            }
        }

        /// <summary>
        /// Rebuild the candidate list when the day rolled over or the toybox changed. Cached
        /// against the surface LIST rather than its count, so a cell swap that rebuilds the same
        /// seven toys still invalidates.
        /// </summary>
        static void EnsureCandidates()
        {
            string dayKey = TodayKey;
            var surfaces = ToyShellRegistry.Surfaces;

            if (_cached && dayKey == _cachedDayKey && SameSurfaces(surfaces))
                return;

            _cached = true;
            _cachedDayKey = dayKey;
            _cachedSurfaces.Clear();
            for (int i = 0; i < surfaces.Count; i++) _cachedSurfaces.Add(surfaces[i]);

            _candidates.Clear();
            for (int i = 0; i < surfaces.Count; i++)
            {
                var surface = surfaces[i];
                if (surface == null) continue;

                // Deliberately NOT filtered on ShellAvailable. A toy that cannot answer for a
                // moment (mid cell-swap, mid vessel-swap) still OFFERS the same activities, and
                // dropping it would shorten the candidate list, move the hash, and change today's
                // activity for as long as the swap lasted - then change it back. A toy with
                // nothing to offer contributes nothing anyway, because its own BuildShellOptions
                // says so (the gallery's is empty when its gallery is), which is the toy's own
                // declaration rather than a second gate here.
                //
                // Only the TOP LAYER, and that is a stated limit rather than an oversight: every
                // toy that offers an activity today offers it flat (the gallery's sixteen canvases,
                // the Wanderway's one "Wander", the Arkway's one "Set sail"), while walking every
                // BRANCH to look for a nested one would pay the Spawn Matrix's whole species
                // enumeration to draw a menu button. A toy that nests its activities is not in this
                // pool; widening it is one recursive call, at that cost.
                _optionScratch.Clear();
                try
                {
                    surface.BuildShellOptions(_optionScratch);
                }
                catch (Exception e)
                {
                    // One toy that throws while describing itself must not take the button with it.
                    CSDebug.LogError($"[DailyToyActivity] '{NameOf(surface)}' threw while building " +
                                     $"its shell options, so it is not in today's draw: {e}");
                    continue;
                }

                string toyName = NameOf(surface);
                for (int j = 0; j < _optionScratch.Count; j++)
                {
                    var option = _optionScratch[j];
                    if (option == null || option.Apply == null) continue;
                    if (!option.RequiresFreestyle) continue;      // a setting, not an activity

                    // A branch is not an activity, and an AppliesOnSelect row is one
                    // ToyConfigureModal.BindToPath deliberately will not select (nothing changes
                    // the world because a window was opened) - so drawing one would put an
                    // un-startable activity on the button. No shipped option is both, but the pool
                    // must only ever hold what the press can actually arm.
                    if (option.IsBranch || option.AppliesOnSelect) continue;
                    _candidates.Add(new Pick(surface, option, toyName));
                }
            }
        }

        static bool SameSurfaces(IReadOnlyList<IToyShellSurface> surfaces)
        {
            if (surfaces.Count != _cachedSurfaces.Count) return false;
            for (int i = 0; i < surfaces.Count; i++)
                if (!ReferenceEquals(surfaces[i], _cachedSurfaces[i])) return false;
            return true;
        }

        static string NameOf(IToyShellSurface surface)
        {
            var definition = surface?.ShellDefinition;
            return definition ? definition.DisplayName : "";
        }

        // ── The claim ──────────────────────────────────────────────────────────

        /// <summary>
        /// The reward id today's claim is filed under. Carries the DAY, so yesterday's claim can
        /// never satisfy today's - and it is a reward id rather than a local file because
        /// <c>UnlockedRewardIds</c> is cloud-merged, which is what stops a reinstall re-earning it.
        /// </summary>
        public static string RewardIdFor(string dayKey) => "toy-activity:" + dayKey;

        /// <summary>True once today's activity has been started and paid for.</summary>
        public static bool ClaimedToday
        {
            get
            {
                var data = PlayerDataService.Instance;
                return data != null && data.IsRewardUnlocked(RewardIdFor(TodayKey));
            }
        }

        /// <summary>Crystals today's first start pays.</summary>
        public static int RewardCrystals => ToyboxDailyActivityConfigSO.EffectiveRewardCrystals;

        /// <summary>
        /// Pay for today, once. Returns the crystals granted - 0 when today is already claimed,
        /// when no profile is loaded yet, or when the reward is authored at 0.
        ///
        /// <para>The unlock is recorded BEFORE the crystals are added, so a failure between the two
        /// costs the player a reward rather than handing them an unbounded one: an id that is
        /// filed but unpaid is a bug worth one reward, an id that is paid but unfiled is a bug
        /// worth every reward.</para>
        /// </summary>
        public static int TryClaim()
        {
            var data = PlayerDataService.Instance;
            if (data == null)
            {
                CSDebug.LogVerbose(CSLogChannel.ToyBox,
                    "[DailyToyActivity] No PlayerDataService, so today's activity pays nothing. " +
                    "The activity itself still ran.");
                return 0;
            }

            string id = RewardIdFor(TodayKey);
            if (data.IsRewardUnlocked(id)) return 0;

            data.UnlockReward(id);

            int amount = RewardCrystals;
            if (amount > 0) data.AddCrystals(amount, "daily_toy_activity");

            CSDebug.LogVerbose(CSLogChannel.ToyBox,
                $"[DailyToyActivity] Claimed {id} for {amount} crystals.");
            return amount;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _cached = false;
            _cachedDayKey = "";
            _cachedSurfaces.Clear();
            _candidates.Clear();
            _optionScratch.Clear();
        }
    }
}
