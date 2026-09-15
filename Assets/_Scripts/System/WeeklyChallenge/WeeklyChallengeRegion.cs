using System;
using System.Globalization;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    /// <summary>
    /// Which regional leaderboard this player belongs to.
    ///
    /// <para><b>UGS Leaderboards has no concept of a region.</b> A board is a board; every score on
    /// it is global. So "regional" is not a filter the service can apply — it can only ever mean
    /// <i>a second board that only players in that region submit to</i>, authored per region on
    /// <c>WeeklyChallengeCatalogSO.regionalLeaderboards</c> and created in the dashboard alongside
    /// the world board with the same ascending / keep-best / weekly-archive settings.</para>
    ///
    /// <para><b>Why not filter the world page client-side?</b> Because it silently produces an
    /// empty board. The world fetch returns the global top N; filtering that to one region shows
    /// only the regional players who were already globally ranked, so a region with no top-N player
    /// sees nothing and reads it as "the leaderboard is broken". A separate board ranks a region
    /// against itself, which is the whole point of the tab.</para>
    ///
    /// <para><b>How the key is resolved, in priority order.</b> The first source that answers wins,
    /// and each is more authoritative than the next:</para>
    /// <list type="number">
    /// <item><see cref="Publish"/> — whatever the networking layer knows. This is the one that is
    /// actually right: the Relay/Multiplayer session picks a region by measured latency, so it
    /// reflects where the player really connects, not where their operating system thinks they
    /// are. Nothing publishes it today; the hook exists so that when the party layer surfaces its
    /// chosen region it becomes a one-line change rather than a redesign.</item>
    /// <item>The device's own country (<see cref="RegionInfo.CurrentRegion"/>) mapped through the
    /// authored table. Good enough to put a player on the right board, and wrong for anyone on a
    /// VPN or an imported console — which is the honest limitation of doing this without the
    /// network layer's answer.</item>
    /// <item>Nothing. The Regional tab then reports no board rather than guessing, because putting
    /// a player on the wrong region's board is worse than not showing one.</item>
    /// </list>
    ///
    /// <para>The result is CACHED for the session: the device's region does not change mid-session,
    /// and re-deriving it per fetch would allocate a <see cref="RegionInfo"/> per tab press.</para>
    /// </summary>
    public static class WeeklyChallengeRegion
    {
        static string _published;
        static string _cached;
        static bool _resolved;

        /// <summary>
        /// Tell the resolver which region this session actually connected through. Call it from
        /// the networking layer once a Relay/session region is known; it outranks the device
        /// locale. Passing null or empty clears it back to the locale answer.
        /// </summary>
        public static void Publish(string regionKey)
        {
            string trimmed = string.IsNullOrWhiteSpace(regionKey) ? null : regionKey.Trim().ToLowerInvariant();
            if (trimmed == _published) return;

            _published = trimmed;
            _resolved = false;   // re-derive on the next ask

            CSDebug.LogVerbose(CSLogChannel.WeeklyChallenge,
                $"[WeeklyChallengeRegion] Connection region published as '{trimmed ?? "(none)"}'.");
        }

        /// <summary>
        /// This player's region key, or null when it cannot be determined. Compared
        /// case-insensitively against the keys authored on the catalog.
        /// </summary>
        public static string Current
        {
            get
            {
                if (_resolved) return _cached;
                _resolved = true;
                _cached = _published ?? FromDeviceCountry();
                return _cached;
            }
        }

        /// <summary>Editor/test seam: forget the cached answer so the next ask re-derives it.</summary>
        public static void Invalidate() => _resolved = false;

        /// <summary>
        /// The device's two-letter ISO country, lowercased — <c>us</c>, <c>gb</c>, <c>sg</c>. It is
        /// deliberately NOT mapped onto a coarse region here: the mapping from country to board is
        /// authored data (a board may cover one country or twenty), and burying it in code would
        /// mean a new region needs a build.
        /// </summary>
        static string FromDeviceCountry()
        {
            try
            {
                var region = RegionInfo.CurrentRegion;
                return region != null ? region.TwoLetterISORegionName.ToLowerInvariant() : null;
            }
            catch (Exception ex)
            {
                // Some platforms return an invariant culture with no region at all.
                CSDebug.LogVerbose(CSLogChannel.WeeklyChallenge,
                    $"[WeeklyChallengeRegion] No device region available: {ex.Message}");
                return null;
            }
        }
    }
}
