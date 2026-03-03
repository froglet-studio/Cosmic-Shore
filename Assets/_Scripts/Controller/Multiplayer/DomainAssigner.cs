using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public static class DomainAssigner
    {
        public static readonly Domains[] PlayableDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        private static List<Domains> availableDomains = new();

        /// <summary>
        /// Assigns domains to AI players to balance teams given human preferences.
        /// Returns a list of Domains for AI players (length = aiCount).
        /// AI are assigned to the smallest team first (greedy round-robin).
        /// </summary>
        public static List<Domains> GetBalancedAIDomains(List<Domains> humanDomains, int aiCount)
        {
            if (aiCount <= 0)
                return new List<Domains>();

            // Count humans per playable team
            var teamCounts = new Dictionary<Domains, int>();
            foreach (var d in PlayableDomains)
                teamCounts[d] = 0;

            foreach (var d in humanDomains)
            {
                if (teamCounts.ContainsKey(d))
                    teamCounts[d]++;
            }

            // Assign AI to smallest team first (greedy balance)
            var aiDomains = new List<Domains>(aiCount);
            for (int i = 0; i < aiCount; i++)
            {
                var smallest = GetSmallestTeam(teamCounts);
                aiDomains.Add(smallest);
                teamCounts[smallest]++;
            }

            return aiDomains;
        }

        /// <summary>
        /// Returns the domain with the fewest players. Breaks ties deterministically
        /// by preferring the earlier domain in PlayableDomains order.
        /// </summary>
        static Domains GetSmallestTeam(Dictionary<Domains, int> teamCounts)
        {
            var smallest = PlayableDomains[0];
            int smallestCount = teamCounts[smallest];

            for (int i = 1; i < PlayableDomains.Length; i++)
            {
                var d = PlayableDomains[i];
                if (teamCounts[d] < smallestCount)
                {
                    smallest = d;
                    smallestCount = teamCounts[d];
                }
            }

            return smallest;
        }

        /// <summary>
        /// Returns whether a domain is a valid playable team (Jade, Ruby, or Gold).
        /// </summary>
        public static bool IsPlayableDomain(Domains domain)
        {
            return domain is Domains.Jade or Domains.Ruby or Domains.Gold;
        }

        #region Legacy API (backward compatibility)

        /// <summary>
        /// Picks a unique random team from the pool (excluding None, Unassigned, Blue).
        /// If the pool is empty, returns Domains.Unassigned.
        /// Legacy: used by single-player mode and co-op modes.
        /// </summary>
        static Domains GetAvailableDomain()
        {
            if (availableDomains.Count == 0)
            {
                CSDebug.LogWarning("[DomainAssigner] No domains left in legacy pool.");
                return Domains.Unassigned;
            }

            int idx = UnityEngine.Random.Range(0, availableDomains.Count);
            var chosen = availableDomains[idx];
            availableDomains.RemoveAt(idx);
            return chosen;
        }

        /// <summary>
        /// Legacy method to assign domains based on game modes.
        /// Co-op modes return Jade; competitive modes pick from pool.
        /// Prefer GetBalancedAIDomains() for team-based modes with AI backfill.
        /// </summary>
        public static Domains GetDomainsByGameModes(GameModes gameMode)
        {
            if (availableDomains.Count == 0)
            {
                CSDebug.LogWarning("[DomainAssigner] No domains left in pool. " +
                                 "Call Initialize() before assigning domains for a new session.");
                return Domains.Unassigned;
            }

            return gameMode is GameModes.Multiplayer2v2CoOpVsAI or GameModes.MultiplayerWildlifeBlitzGame
                ? Domains.Jade
                : GetAvailableDomain();
        }

        /// <summary>
        /// Resets the legacy unique-domain pool. Call before each session.
        /// </summary>
        public static void Initialize()
        {
            availableDomains.Clear();
            availableDomains = Enum.GetValues(typeof(Domains))
                .Cast<Domains>()
                .Where(t => t is not (Domains.None or Domains.Unassigned or Domains.Blue))
                .ToList();
            CSDebug.Log("[DomainAssigner] Cleared assigned domains cache.");
        }

        #endregion
    }
}
