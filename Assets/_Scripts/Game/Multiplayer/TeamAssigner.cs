using System;
using System.Collections.Generic;
using System.Linq;

public static class TeamAssigner
{
    static HashSet<Domains> assignedTeams = new ();
    
    /// <summary>
    /// Picks a random team from all Teams (excluding None/Unassigned) that isn't already in assignedTeams,
    /// adds it to assignedTeams, and returns it. If none are available, returns Teams.Unassigned.
    /// </summary>
    public static Domains AssignRandomTeam()
    {
        // Get all valid teams (exclude None and Unassigned)
        var allTeams = Enum.GetValues(typeof(Domains))
            .Cast<Domains>()
            .Where(t => t != Domains.None && t != Domains.Unassigned)
            .ToArray();

        // Filter out those already assigned
        var available = allTeams.Where(t => !assignedTeams.Contains(t)).ToArray();

        if (available.Length == 0)
        {
            // no teams left
            return Domains.Unassigned;
        }

        // pick one at random
        int idx = UnityEngine.Random.Range(0, available.Length);
        var chosen = available[idx];

        // mark as assigned
        assignedTeams.Add(chosen);

        return chosen;
    }
    
    public static void ClearCache() => assignedTeams.Clear();
}