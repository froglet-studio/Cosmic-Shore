using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
        // If no teams left, return error
        if (availableDomains.Count == 0)
        {
            Initialize();
        }

        // In co-op modes, all players are assigned to Jade Domain (same team)
        return gameMode is GameModes.Multiplayer2v2CoOpVsAI or GameModes.WildlifeBlitz ? Domains.Jade : GetAvailableDomain();
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
        Debug.Log("[DomainAssigner] 🔄 Cleared assigned domains cache.");
    }
}
