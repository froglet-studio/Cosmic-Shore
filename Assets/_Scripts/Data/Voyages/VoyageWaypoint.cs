using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// The waypoint a voyage waits at (P13 "voyage chapters", Loop Atlas retention build, 2026-11-21; named apart from the
    /// saga chapters): a voyage closed at sea after the Ark reached a world
    /// of its leg is not given up. It waits with its job and what is aboard, and the next sitting sails it on from that
    /// world (<see cref="VoyageState.HasWaypoint"/>, <see cref="VoyageRules.Resume(VoyageState, VoyageContent, Domains, long)"/>).
    /// </summary>
    public static class VoyageWaypoint
    {
        /// <summary>A leg begins: its worlds are the waypoint's route, none reached yet (P13, voyage chapters).</summary>
        public static void Begin(VoyageState state, IReadOnlyList<int> route)
        {
            if (state == null) return;
            state.WaypointRoute ??= new List<int>();
            state.WaypointRoute.Clear();
            if (route != null) state.WaypointRoute.AddRange(route);
            state.WaypointReached = -1;
        }

        /// <summary>The Ark reached world <paramref name="routeIndex"/> of the leg: the voyage can pick up from it.
        /// Only forward: a world behind the furthest reached changes nothing.</summary>
        public static bool Mark(VoyageState state, int routeIndex)
        {
            if (state == null || !state.AtSea || state.WaypointRoute == null) return false;
            if (routeIndex < 0 || routeIndex >= state.WaypointRoute.Count || routeIndex <= state.WaypointReached) return false;
            state.WaypointReached = routeIndex;
            return true;
        }

        /// <summary>The worlds still to sail from the waypoint reached (the reached world first). Empty when none waits.</summary>
        public static List<int> Remaining(VoyageState state)
        {
            var rest = new List<int>();
            if (state == null || !state.HasWaypoint()) return rest;
            for (int i = state.WaypointReached; i < state.WaypointRoute.Count; i++) rest.Add(state.WaypointRoute[i]);
            return rest;
        }

        /// <summary>No waypoint waits.</summary>
        public static void Clear(VoyageState state)
        {
            if (state == null) return;
            state.WaypointRoute?.Clear();
            state.WaypointReached = -1;
        }
    }
}
