using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class DomainAssigner
{
    private static List<Domains> availableDomains = new ();

    /// <summary>
    /// Picks a unique random team from all Domains (excluding None, Unassigned, Blue).
    /// If all are already assigned, logs an error and returns Domains.Unassigned.
    /// </summary>
    public static Domains GetAvailableDomain()
    {
        // If no teams left, return error
        if (availableDomains.Count == 0)
        {
            Initialize();
        }

        // Pick a random unassigned team
        int idx = UnityEngine.Random.Range(0, availableDomains.Count);
        var chosen = availableDomains[idx];

        // Mark it as used
        availableDomains.Remove(chosen);

        Debug.Log($"[DomainAssigner] ✅ Assigned unique domain: {chosen}");
        return chosen;
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
