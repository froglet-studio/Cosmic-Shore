using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The single door every reward comes through.
    ///
    /// Producers (a scoreboard, a daily challenge, a quest) describe WHAT was earned as a
    /// <see cref="RewardGrant"/> and call <see cref="Grant"/>. This class owns everything that
    /// used to be each producer's problem - the wallet write, dedupe, the failure boundary, and
    /// announcing the result - so a new earn path cannot get any of it subtly wrong, and every
    /// reward the player receives is reported on one channel.
    ///
    /// A static posting surface for the same reason <see cref="GameToastAPI"/> is one: it is
    /// called from gameplay scenes and menu screens alike, and a DI-injected or scene-wired
    /// service would be null in exactly the runtime-spawned objects that need it
    /// (see CLAUDE.md on [Inject] fields in gameplay-spawned prefabs).
    ///
    /// It is NOT the wallet. <c>PlayerDataService</c> remains the sole writer of
    /// <c>ProfileEconomy</c>; this routes to it.
    /// </summary>
    public static class RewardService
    {
        const string ChannelPath = "Channels/RewardGrantedChannel";

        /// <summary>
        /// Prefix for the dedupe keys of once-ever CRYSTAL grants, so they share
        /// <c>UnlockedRewardIds</c> with entitlements without ever colliding with an
        /// entitlement id.
        /// </summary>
        public const string OnceKeyPrefix = "once:";

        static ScriptableEventRewardGranted _channel;
        static bool _channelLoadAttempted;
        static bool _warnedMissingChannel;

        /// <summary>
        /// The most recent grant, and a counter that increments with it.
        ///
        /// A SOAP event reaches only whoever is already listening, and the end-game reward
        /// panel cannot be: the Scoreboard awards the payout while building its cards and
        /// activates the panel afterwards, so a listener parented under that panel has not
        /// had OnEnable yet when the grant is raised. Rather than reorder the end-game flow
        /// around one display, the announcement is also LEFT HERE - a display compares the
        /// sequence against the last one it showed and catches up.
        ///
        /// A sequence rather than a consumed flag, because two displays must be able to
        /// catch up independently; "consuming" it would let whichever woke first hide the
        /// reward from the other.
        /// </summary>
        public static RewardGranted LatestGrant { get; private set; }

        /// <summary>Increments on every announced grant. 0 = nothing granted yet.</summary>
        public static int GrantSequence { get; private set; }

        // Statics survive a play-mode exit when "Reload Domain" is off. Without this reset a
        // null-after-first-attempt channel would never be retried, and the missing-channel
        // warning would be suppressed for the rest of the editor session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _channel = null;
            _channelLoadAttempted = false;
            _warnedMissingChannel = false;
            LatestGrant = default;
            GrantSequence = 0;
        }

        static ScriptableEventRewardGranted Channel
        {
            get
            {
                if (_channel != null) return _channel;
                if (!_channelLoadAttempted)
                {
                    _channelLoadAttempted = true;
                    _channel = Resources.Load<ScriptableEventRewardGranted>(ChannelPath);
                }
                return _channel;
            }
        }

        /// <summary>
        /// Grants a reward and announces it. Returns true when the player's account actually
        /// changed.
        ///
        /// Returns false - without logging an error - for the three ordinary non-events: a
        /// payout of nothing (last place), a once-ever reward already earned, and no profile
        /// service to write to. None of those is a fault.
        /// </summary>
        public static bool Grant(RewardGrant grant)
        {
            if (!grant.IsPayable) return false;

            var profile = PlayerDataService.Instance;
            if (profile == null)
            {
                // Not fail-loud: a reward can legitimately be offered before the profile
                // service exists (an early boot path, a test scene). Losing it silently is what
                // would be wrong, so say so once at warning level and move on.
                CSDebug.LogWarning(
                    $"[RewardService] No PlayerDataService - dropped {Describe(grant)}.");
                return false;
            }

            if (grant.Dedupe == RewardDedupe.Account)
            {
                if (string.IsNullOrEmpty(grant.DedupeKey))
                {
                    CSDebug.LogError(
                        $"[RewardService] {Describe(grant)} asks for account dedupe with no " +
                        "DedupeKey - refusing rather than granting it repeatedly.");
                    return false;
                }

                if (profile.IsRewardUnlocked(grant.DedupeKey))
                    return false;   // already earned; nothing to do and nothing to report
            }

            // The wallet write schedules a cloud save, so it is an external-service boundary.
            // It runs mid-way through building the end-game screen, and a service hiccup must
            // degrade to a lost reward log line - never to a missing scoreboard.
            int previousBalance = profile.GetCrystalBalance();
            int newBalance = previousBalance;

            try
            {
                switch (grant.Kind)
                {
                    case RewardKind.Crystals:
                        newBalance = profile.AddCrystals(grant.Amount, grant.Source);
                        break;

                    case RewardKind.Entitlement:
                        profile.UnlockReward(grant.EntitlementId);
                        break;

                    default:
                        CSDebug.LogError(
                            $"[RewardService] Unhandled RewardKind '{grant.Kind}' - " +
                            $"{Describe(grant)} was not granted.");
                        return false;
                }

                // Marked only after the payout succeeded, so a throw mid-write leaves the
                // reward still owed rather than silently consumed. An entitlement is its own
                // key and UnlockReward already recorded it.
                if (grant.Dedupe == RewardDedupe.Account && grant.Kind != RewardKind.Entitlement)
                    profile.UnlockReward(grant.DedupeKey);
            }
            catch (System.Exception e)
            {
                CSDebug.LogError($"[RewardService] Grant failed for {Describe(grant)}: {e}");
                return false;
            }

            CSDebug.Log($"[RewardService] Granted {Describe(grant)}. " +
                        $"Crystal balance: {previousBalance} -> {newBalance}.");

            Announce(new RewardGranted(grant, previousBalance, newBalance));
            return true;
        }

        /// <summary>Convenience for the common case: a repeatable crystal payout.</summary>
        public static bool GrantCrystals(int amount, string source)
            => Grant(RewardGrant.Crystals(amount, source));

        /// <summary>
        /// A crystal payout that lands at most once per account. <paramref name="key"/> is
        /// namespaced under <see cref="OnceKeyPrefix"/> so it can never collide with an
        /// entitlement id.
        /// </summary>
        public static bool GrantCrystalsOnce(int amount, string source, string key)
            => Grant(RewardGrant.CrystalsOnce(amount, source, OnceKeyPrefix + key));

        static void Announce(RewardGranted granted)
        {
            // Recorded before the raise, so a listener that reacts synchronously and then
            // checks the sequence sees a consistent pair.
            LatestGrant = granted;
            GrantSequence++;

            var channel = Channel;
            if (channel == null)
            {
                // The grant already landed - this only costs the player the notification, so it
                // is a warning rather than an error, and it is said once rather than per game.
                if (!_warnedMissingChannel)
                {
                    _warnedMissingChannel = true;
                    CSDebug.LogWarning(
                        $"[RewardService] Missing channel at Resources/{ChannelPath} - rewards " +
                        "are being granted but nothing will display them.");
                }
                return;
            }

            channel.Raise(granted);
        }

        static string Describe(RewardGrant grant) => grant.Kind switch
        {
            RewardKind.Crystals    => $"{grant.Amount} crystals ({grant.Source})",
            RewardKind.Entitlement => $"entitlement '{grant.EntitlementId}' ({grant.Source})",
            _                      => $"{grant.Kind} ({grant.Source})",
        };
    }
}
