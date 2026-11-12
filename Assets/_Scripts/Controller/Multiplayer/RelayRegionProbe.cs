using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Qos;
using Unity.Services.Relay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One Relay region as the REGION tab lists it: its id, UGS's own description, and this player's ping.</summary>
    public readonly struct RelayRegionInfo
    {
        public readonly string Id;
        public readonly string Description;
        /// <summary>Average round trip to the region's QoS server in ms; negative when it was not measured.</summary>
        public readonly int PingMs;

        public RelayRegionInfo(string id, string description, int pingMs)
        {
            Id = id;
            Description = description;
            PingMs = pingMs;
        }

        public bool HasPing => PingMs >= 0;
    }

    /// <summary>
    /// Asks UGS which Relay regions this project can allocate in (<c>RelayService.ListRegionsAsync</c>) and measures
    /// this player's ping to each (<c>QosService.GetSortedRelayQosResultsAsync</c>, the same QoS Relay's Auto uses).
    /// The REGION tab shows both, so a party in the US and India can see that Europe is the fair middle.
    ///
    /// <para>One probe at a time; a result is kept for <see cref="CacheSeconds"/> so opening Settings again does not
    /// measure again. The list call goes through <see cref="UgsRequestPolicy"/>; the ping is best-effort (QoS is not
    /// available on WebGL, and a failed measurement leaves the list without pings).</para>
    /// </summary>
    public static class RelayRegionProbe
    {
        /// <summary>How long a probe's result is reused before the next request measures again.</summary>
        public const float CacheSeconds = 120f;

        static IReadOnlyList<RelayRegionInfo> _cached;
        static float _cachedAt = float.NegativeInfinity;
        static UniTaskCompletionSource<IReadOnlyList<RelayRegionInfo>> _inFlight;

        /// <summary>The last probe's result, or null before the first one finished.</summary>
        public static IReadOnlyList<RelayRegionInfo> Last => _cached;

        /// <summary>True when UGS is up and the player is signed in, which both calls need.</summary>
        public static bool CanProbe =>
            UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;

        /// <summary>
        /// The regions with this player's ping to each, best first. Returns the cached result while it is fresh unless
        /// <paramref name="force"/>; an empty list when UGS is unreachable (the tab then offers its authored regions).
        /// </summary>
        public static async UniTask<IReadOnlyList<RelayRegionInfo>> ProbeAsync(UgsRequestPolicy policy, bool force = false)
        {
            if (!force && _cached != null && Time.realtimeSinceStartup - _cachedAt < CacheSeconds) return _cached;
            if (_inFlight != null) return await _inFlight.Task;
            if (!CanProbe) return Array.Empty<RelayRegionInfo>();

            var flight = _inFlight = new UniTaskCompletionSource<IReadOnlyList<RelayRegionInfo>>();
            IReadOnlyList<RelayRegionInfo> result = Array.Empty<RelayRegionInfo>();
            try
            {
                policy ??= UgsRequestPolicy.CreateDefault();
                var regions = await policy.ExecuteAsync("relay:regions",
                    async () => await RelayService.Instance.ListRegionsAsync().AsMainThread());
                var listed = (regions ?? new List<Unity.Services.Relay.Models.Region>())
                    .Where(r => r != null && !string.IsNullOrEmpty(r.Id))
                    .Select(r => (r.Id, r.Description))
                    .ToList();

                var pings = new List<(string region, int ms)>();
                if (listed.Count > 0)
                {
                    try
                    {
                        var qos = await QosService.Instance
                            .GetSortedRelayQosResultsAsync(listed.Select(r => r.Id).ToList()).AsMainThread();
                        if (qos != null)
                            foreach (var q in qos)
                                if (q != null) pings.Add((q.Region, q.AverageLatencyMs));
                    }
                    catch (Exception e)
                    {
                        CSDebug.LogVerbose(CSLogChannel.Party,
                            $"[RelayRegionProbe] Ping measurement unavailable ({e.GetType().Name}: {e.Message}); listing regions without ping.");
                    }
                }

                result = Merge(listed, pings);
                if (result.Count > 0)
                {
                    _cached = result;
                    _cachedAt = Time.realtimeSinceStartup;
                }
                CSDebug.LogVerbose(CSLogChannel.Party,
                    $"[RelayRegionProbe] {result.Count} region(s): {string.Join(", ", result.Select(r => r.HasPing ? $"{r.Id} {r.PingMs}ms" : r.Id))}.");
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[RelayRegionProbe] Could not list Relay regions ({e.GetType().Name}: {e.Message}).");
            }
            finally
            {
                _inFlight = null;
                flight.TrySetResult(result);
            }
            return result;
        }

        /// <summary>
        /// Joins the listed regions with their pings: one entry per region id (case-insensitive, first listing wins),
        /// measured ones first from lowest ping, then the unmeasured ones by id. A ping for a region that was not
        /// listed is dropped (only listed regions can be allocated in). Pure, so it is tested without UGS.
        /// </summary>
        public static IReadOnlyList<RelayRegionInfo> Merge(
            IEnumerable<(string id, string description)> regions, IEnumerable<(string region, int ms)> pings)
        {
            var best = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (pings != null)
                foreach (var (region, ms) in pings)
                {
                    if (string.IsNullOrEmpty(region) || ms < 0) continue;
                    if (!best.TryGetValue(region, out var had) || ms < had) best[region] = ms;
                }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<RelayRegionInfo>();
            if (regions != null)
                foreach (var (id, description) in regions)
                {
                    var key = RelayRegionPreference.Normalize(id);
                    if (key.Length == 0 || !seen.Add(key)) continue;
                    list.Add(new RelayRegionInfo(key, description ?? string.Empty, best.TryGetValue(key, out var ms) ? ms : -1));
                }

            return list
                .OrderBy(r => r.HasPing ? 0 : 1)
                .ThenBy(r => r.HasPing ? r.PingMs : 0)
                .ThenBy(r => r.Id, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Test hook: forget the cached result.</summary>
        public static void ResetForTests()
        {
            _cached = null;
            _cachedAt = float.NegativeInfinity;
            _inFlight = null;
        }
    }
}
