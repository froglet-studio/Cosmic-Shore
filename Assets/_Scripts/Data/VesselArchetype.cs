using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// A vessel's ROLE (Shombith, 2026-11-26, the archetypes board https://claude.ai/artifact/Duwb9xJ7XdAykxMKAnMyFe):
    /// Cultivators increase allied prism volume, Destroyers destroy enemy prisms, Guardians protect allied prisms. A
    /// role is the family of three <see cref="VesselArchetype"/>s. The fleet's table is
    /// <c>VesselArchetypesSO</c> (Resources/Vessels/VesselArchetypes, Docs/Vessels/ARCHETYPES.md); the Hangar, the
    /// ecology page and voyages all read it. Values are fixed for serialization.
    /// </summary>
    public enum VesselRole
    {
        None = 0,
        Cultivator = 1,
        Destroyer = 2,
        Guardian = 3,
    }

    /// <summary>
    /// A vessel's ARCHETYPE, the nine sub-types of the three roles (Docs/Vessels/archetypes.json, the board's copy). Each
    /// vessel has a primary, a secondary and sometimes a tertiary archetype. An archetype is never a kit change (CLAUDE.md, a game mode never changes the vessel).
    /// Values are fixed for serialization; the three of a role are consecutive.
    /// </summary>
    public enum VesselArchetype
    {
        None = 0,
        Pumper = 1,
        Builder = 2,
        Burster = 3,
        Sapper = 4,
        Specialist = 5,
        Nuke = 6,
        Harraser = 7,
        Zoner = 8,
        Enhancer = 9,
    }

    /// <summary>The rules archetypes are read by: the role of an archetype, how well a vessel meets a seat, and a
    /// squad's fit for an ask of archetype seats (voyages ask for three).</summary>
    public static class VesselArchetypes
    {
        /// <summary>A vessel on its primary archetype meets the seat fully.</summary>
        public const float PrimaryMeets = 1f;
        /// <summary>A vessel on its secondary or tertiary archetype meets the seat half.</summary>
        public const float LesserMeets = 0.5f;

        /// <summary>The role <paramref name="a"/> belongs to.</summary>
        public static VesselRole RoleOf(VesselArchetype a) => a switch
        {
            VesselArchetype.Pumper or VesselArchetype.Builder or VesselArchetype.Burster => VesselRole.Cultivator,
            VesselArchetype.Sapper or VesselArchetype.Specialist or VesselArchetype.Nuke => VesselRole.Destroyer,
            VesselArchetype.Harraser or VesselArchetype.Zoner or VesselArchetype.Enhancer => VesselRole.Guardian,
            _ => VesselRole.None,
        };

        /// <summary>How well a vessel whose archetypes are <paramref name="archetypes"/> (primary first) meets a seat
        /// asking for <paramref name="seat"/>: 1 on its primary, 0.5 on a secondary or tertiary, else 0.</summary>
        public static float Meets(IReadOnlyList<VesselArchetype> archetypes, VesselArchetype seat)
        {
            if (archetypes == null || seat == VesselArchetype.None) return 0f;
            for (int i = 0; i < archetypes.Count; i++)
                if (archetypes[i] == seat) return i == 0 ? PrimaryMeets : LesserMeets;
            return 0f;
        }

        /// <summary>
        /// The squad's fit for an ask: each vessel fills at most one seat and each seat takes at most one vessel, assigned
        /// for the best total (so 0 to the seat count, in halves). <paramref name="vessels"/> holds each squad vessel's
        /// archetypes, primary first (null or empty for a vessel with none).
        /// </summary>
        public static float Fit(IReadOnlyList<VesselArchetype> seats, IReadOnlyList<IReadOnlyList<VesselArchetype>> vessels)
        {
            if (seats == null || seats.Count == 0 || vessels == null || vessels.Count == 0) return 0f;
            var used = new bool[vessels.Count];
            return Best(seats, 0, vessels, used);
        }

        // Seats are few (three) and squads small (three), so the exhaustive assignment is cheap and exact.
        static float Best(IReadOnlyList<VesselArchetype> seats, int seat, IReadOnlyList<IReadOnlyList<VesselArchetype>> vessels,
            bool[] used)
        {
            if (seat >= seats.Count) return 0f;
            float best = Best(seats, seat + 1, vessels, used); // the seat left empty
            for (int v = 0; v < vessels.Count; v++)
            {
                if (used[v]) continue;
                float meets = Meets(vessels[v], seats[seat]);
                if (meets <= 0f) continue;
                used[v] = true;
                best = Math.Max(best, meets + Best(seats, seat + 1, vessels, used));
                used[v] = false;
            }
            return best;
        }
    }
}
