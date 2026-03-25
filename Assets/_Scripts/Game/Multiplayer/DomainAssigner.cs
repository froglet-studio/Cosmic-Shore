using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CosmicShore.Utility;

public static class DomainAssigner
{
    private static List<Domains> availableDomains = new ();
    private static Dictionary<Domains, int> availableDomainsCount = new();

    /// <summary>
    /// Picks a unique random team from all Domains (excluding None, Unassigned, Blue).
    /// If all are already assigned, logs an error and returns Domains.Unassigned.
    /// </summary>
    static Domains GetAvailableDomain()
    {
        int idx = UnityEngine.Random.Range(0, availableDomains.Count);
        var chosen = availableDomains[idx];

        // Mark it as used
        availableDomains.RemoveAt(idx);
        return chosen;
    }
    
    /// <summary>
    /// TEMP Method to assign domains to players based on game modes,
    /// later need to transfer this logic to support all game modes and co-op
    /// with specified player count per domain
    /// </summary>
    public static Domains GetDomainsByGameModes(GameModes gameMode)
    {
        // If no teams left, log a warning and return Unassigned instead of
        // silently re-initializing.  Re-initializing mid-session was the root
        // cause of duplicate / swapped domains in 3-player games because the
        // fresh pool could hand out a domain that was already assigned to
        // another player earlier in the same session.
        if (availableDomains.Count == 0)
        {
            CSDebug.LogWarning("[DomainAssigner] No domains left in pool. " +
                             "Call Initialize() before assigning domains for a new session.");
            return Domains.Unassigned;
        }

        // Considering in co-op modes, all local users will be assigned to Jade Domain
        return gameMode is GameModes.Multiplayer2v2CoOpVsAI or GameModes.MultiplayerWildlifeBlitzGame ? Domains.Jade : GetAvailableDomain();
    }

    /// <summary>
    /// Assigns a specific preferred domain to a player, removing it from the
    /// available pool so no other player receives the same domain.
    /// Returns the preferred domain on success, or falls back to a random
    /// available domain if the preferred one was already taken.
    /// </summary>
    public static Domains GetPreferredDomain(Domains preferred, GameModes gameMode)
    {
        if (preferred == Domains.Unassigned || preferred == Domains.None)
            return GetDomainsByGameModes(gameMode);

        int idx = availableDomains.IndexOf(preferred);
        if (idx >= 0)
        {
            availableDomains.RemoveAt(idx);
            return preferred;
        }

        // Preferred domain was already taken — fall back to random
        CSDebug.LogWarning($"[DomainAssigner] Preferred domain {preferred} unavailable, assigning random.");
        return GetDomainsByGameModes(gameMode);
    }

    /// <summary>
    /// Clears all assigned teams (use when restarting or resetting game).
    /// </summary>
    public static void Initialize()
    {
        availableDomains.Clear();
        // Get all valid teams (excluding reserved ones)
        availableDomains = Enum.GetValues(typeof(Domains))
            .Cast<Domains>()
            .Where(t => t is not (Domains.None or Domains.Unassigned or Domains.Blue))
            .ToList();
        CSDebug.Log("[DomainAssigner] 🔄 Cleared assigned domains cache.");
    }
}
