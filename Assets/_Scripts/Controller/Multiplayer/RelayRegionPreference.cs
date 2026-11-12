using System;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The player's chosen Relay region (Settings > REGION): where the sessions THIS machine hosts are served from.
    ///
    /// <para>A party plays through its host's Relay allocation, and every guest connects to that region. With no choice
    /// (Auto) UGS measures the host's ping and picks the region closest to the HOST, which is the worst case for a
    /// party spread over continents: a US host put an India guest on a US relay at ~1200 ms. Choosing a region in
    /// between (Europe for US + India) shares the distance. Only the host's choice matters, so the setting is read
    /// where a session is CREATED (<see cref="PartySessionService.CreateAsync"/> and the quick-match host in
    /// <c>MultiplayerSetup</c>); a guest's own choice applies to the parties it hosts later.</para>
    ///
    /// <para>Stored in PlayerPrefs, so it is per device and survives restarts. An empty id is Auto.</para>
    /// </summary>
    public static class RelayRegionPreference
    {
        /// <summary>PlayerPrefs key of the chosen region id ("" = Auto).</summary>
        public const string PrefsKey = "CosmicShore.RelayRegion";

        static string _regionId;
        static bool _loaded;

        /// <summary>The chosen Relay region id (for example <c>europe-west4</c>), or "" for Auto.</summary>
        public static string RegionId
        {
            get
            {
                if (!_loaded)
                {
                    _regionId = Normalize(PlayerPrefs.GetString(PrefsKey, string.Empty));
                    _loaded = true;
                }
                return _regionId;
            }
        }

        /// <summary>True when no region is chosen and UGS picks the one closest to the host.</summary>
        public static bool IsAuto => RegionId.Length == 0;

        /// <summary>The region to ask Relay for: null for Auto (Relay's own QoS pick).</summary>
        public static string ForSession => IsAuto ? null : RegionId;

        /// <summary>
        /// The region the party session this machine hosts was created in: null for Auto, and only meaningful while
        /// <see cref="HostedSessionKnown"/>. Compared with <see cref="RegionId"/> to tell whether the hosted session
        /// still has to move (<see cref="HostedSessionIsStale"/>).
        /// </summary>
        public static string HostedSessionRegion { get; private set; }

        /// <summary>True once this machine has created a party session this run.</summary>
        public static bool HostedSessionKnown { get; private set; }

        /// <summary>True when the hosted party session was created in another region than the one now chosen.</summary>
        public static bool HostedSessionIsStale => HostedSessionKnown && Normalize(HostedSessionRegion) != RegionId;

        /// <summary>Raised with the new id ("" = Auto) whenever the choice changes.</summary>
        public static event Action<string> Changed;

        /// <summary>Chooses a region ("" or null = Auto) and stores it. No-op when it is already the choice.</summary>
        public static void Set(string regionId)
        {
            var id = Normalize(regionId);
            if (id == RegionId) return;
            _regionId = id;
            PlayerPrefs.SetString(PrefsKey, id);
            PlayerPrefs.Save();
            CSDebug.LogVerbose(CSLogChannel.Party, $"[RelayRegionPreference] Region set to {(id.Length == 0 ? "Auto" : id)}.");
            Changed?.Invoke(id);
        }

        /// <summary>Records the region a party session this machine hosts was created in (null = Auto).</summary>
        public static void MarkHostedSession(string regionId)
        {
            HostedSessionRegion = string.IsNullOrEmpty(regionId) ? null : Normalize(regionId);
            HostedSessionKnown = true;
        }

        /// <summary>Region ids are lower-case Relay ids; whitespace and case are not part of one.</summary>
        public static string Normalize(string regionId) =>
            string.IsNullOrWhiteSpace(regionId) ? string.Empty : regionId.Trim().ToLowerInvariant();

        /// <summary>
        /// Creates a Relay session in the chosen region, falling back to Auto once when the chosen region is refused
        /// (a region UGS no longer serves must never leave a player without a session). <paramref name="create"/>
        /// builds and sends one create for the region it is given (null = Auto) and returns its session (the SDK's
        /// <c>IHostSession</c>). Returns the session and the region it was created in.
        /// </summary>
        public static async UniTask<(T session, string region)> CreateWithFallbackAsync<T>(
            Func<string, UniTask<T>> create, string label) where T : class
        {
            var region = ForSession;
            if (region == null) return (await create(null), null);
            try
            {
                return (await create(region), region);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[RelayRegionPreference] {label}: creating the session in region '{region}' failed " +
                                   $"({e.GetType().Name}: {e.Message}); creating it in the closest region instead.");
                return (await create(null), null);
            }
        }

        /// <summary>Test hook: forget the cached choice and the hosted-session record.</summary>
        public static void ResetForTests()
        {
            _loaded = false;
            _regionId = null;
            HostedSessionRegion = null;
            HostedSessionKnown = false;
            Changed = null;
        }
    }
}
