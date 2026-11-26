using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The fleet's vessel ROLES and ARCHETYPES (Shombith's board of 2026-11-26,
    /// https://claude.ai/artifact/Duwb9xJ7XdAykxMKAnMyFe, copied to Docs/Vessels/archetypes.json; Docs/Vessels/ARCHETYPES.md).
    /// The ONE copy every reader uses: the Hangar, the ecology page and voyages. Holds each role's and archetype's words,
    /// each vessel's archetypes (primary first, then secondary, then tertiary), and the counter graph. An archetype never
    /// changes a vessel's kit (CLAUDE.md, a game mode never changes the vessel).
    /// <para>Written by <c>Tools/Build/author_vessel_archetypes.py</c> (its <c>--check</c> is the drift gate) into
    /// <c>Resources/Vessels/VesselArchetypes</c>. The vessel and counter tables are the generator's; the names and lines
    /// are a person's: the generator seeds them from the board once and keeps them on every re-run.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "VesselArchetypes", menuName = "ScriptableObjects/Vessels/Vessel Archetypes")]
    public class VesselArchetypesSO : ScriptableObject
    {
        /// <summary>Where the shipped table lives, under Resources.</summary>
        public const string ResourcePath = "Vessels/VesselArchetypes";

        static VesselArchetypesSO s_loaded;

        /// <summary>The shipped table (<c>Resources/Vessels/VesselArchetypes</c>), or null when the asset is missing.</summary>
        public static VesselArchetypesSO Load()
        {
            if (!s_loaded) s_loaded = Resources.Load<VesselArchetypesSO>(ResourcePath);
            return s_loaded;
        }

        /// <summary>One role: its name and its goal.</summary>
        [Serializable]
        public class RoleEntry
        {
            public VesselRole Role;
            [Tooltip("The role's name as players read it. Empty shows nothing.")]
            public string Name;
            [Tooltip("The role's goal, one line (the board's). Empty shows nothing.")]
            public string Line;
        }

        /// <summary>One archetype: its name and what it does.</summary>
        [Serializable]
        public class ArchetypeEntry
        {
            public VesselArchetype Archetype;
            [Tooltip("The archetype's name as players read it. Empty shows nothing.")]
            public string Name;
            [Tooltip("What the archetype does, one line (the board's). Empty shows nothing.")]
            public string Line;
        }

        /// <summary>A vessel's archetypes, primary first, then secondary, then tertiary.</summary>
        [Serializable]
        public class VesselEntry
        {
            public VesselClassType Vessel;
            public List<VesselArchetype> Archetypes = new();
        }

        /// <summary>One arrow of the board's graph: <see cref="From"/> counters <see cref="To"/>.</summary>
        [Serializable]
        public class Counter
        {
            public VesselArchetype From;
            public VesselArchetype To;
        }

        /// <summary>A vessel that holds an archetype, and at which rank (0 primary, 1 secondary, 2 tertiary).</summary>
        public readonly struct Holder
        {
            public readonly VesselClassType Vessel;
            public readonly int Rank;
            public Holder(VesselClassType vessel, int rank) { Vessel = vessel; Rank = rank; }
        }

        /// <summary>Rank of a primary archetype in <see cref="ArchetypesOf"/>.</summary>
        public const int Primary = 0;
        /// <summary>Rank of a secondary archetype.</summary>
        public const int Secondary = 1;
        /// <summary>Rank of a tertiary archetype.</summary>
        public const int Tertiary = 2;

        [Header("Words (seeded from the board once; yours to edit)")]
        [SerializeField] List<RoleEntry> roles = new();
        [SerializeField] List<ArchetypeEntry> archetypes = new();

        [Header("Tables (written by Tools/Build/author_vessel_archetypes.py)")]
        [SerializeField] List<VesselEntry> vessels = new();
        [SerializeField] List<Counter> counters = new();

        static readonly IReadOnlyList<VesselArchetype> None = Array.Empty<VesselArchetype>();

        /// <summary>The vessel's archetypes, primary first (index 0 primary, 1 secondary, 2 tertiary); empty when the board
        /// gives it none.</summary>
        public IReadOnlyList<VesselArchetype> ArchetypesOf(VesselClassType vessel)
        {
            for (int i = 0; i < vessels.Count; i++)
                if (vessels[i].Vessel == vessel) return vessels[i].Archetypes;
            return None;
        }

        /// <summary>The vessel's primary archetype, or None.</summary>
        public VesselArchetype PrimaryOf(VesselClassType vessel)
        {
            var list = ArchetypesOf(vessel);
            return list.Count > 0 ? list[0] : VesselArchetype.None;
        }

        /// <summary>The vessel's role: the role of its primary archetype, or None.</summary>
        public VesselRole RoleOf(VesselClassType vessel) => VesselArchetypes.RoleOf(PrimaryOf(vessel));

        /// <summary>Every vessel that holds <paramref name="archetype"/>, with its rank, in table order.</summary>
        public List<Holder> VesselsOf(VesselArchetype archetype)
        {
            var result = new List<Holder>();
            for (int i = 0; i < vessels.Count; i++)
            {
                int rank = vessels[i].Archetypes.IndexOf(archetype);
                if (rank >= 0) result.Add(new Holder(vessels[i].Vessel, rank));
            }
            return result;
        }

        /// <summary>The archetypes <paramref name="archetype"/> counters (the board's arrows out of it).</summary>
        public List<VesselArchetype> Counters(VesselArchetype archetype)
        {
            var result = new List<VesselArchetype>();
            for (int i = 0; i < counters.Count; i++)
                if (counters[i].From == archetype) result.Add(counters[i].To);
            return result;
        }

        /// <summary>The archetypes that counter <paramref name="archetype"/> (the board's arrows into it).</summary>
        public List<VesselArchetype> CounteredBy(VesselArchetype archetype)
        {
            var result = new List<VesselArchetype>();
            for (int i = 0; i < counters.Count; i++)
                if (counters[i].To == archetype) result.Add(counters[i].From);
            return result;
        }

        /// <summary>The role's name as players read it; empty when unauthored.</summary>
        public string RoleName(VesselRole role) => FindRole(role)?.Name ?? string.Empty;
        /// <summary>The role's goal line; empty when unauthored.</summary>
        public string RoleLine(VesselRole role) => FindRole(role)?.Line ?? string.Empty;
        /// <summary>The archetype's name as players read it; empty when unauthored.</summary>
        public string ArchetypeName(VesselArchetype archetype) => FindArchetype(archetype)?.Name ?? string.Empty;
        /// <summary>What the archetype does, one line; empty when unauthored.</summary>
        public string ArchetypeLine(VesselArchetype archetype) => FindArchetype(archetype)?.Line ?? string.Empty;

        /// <summary>The vessels the table covers, in table order.</summary>
        public IEnumerable<VesselClassType> Vessels
        {
            get { for (int i = 0; i < vessels.Count; i++) yield return vessels[i].Vessel; }
        }

        RoleEntry FindRole(VesselRole role)
        {
            for (int i = 0; i < roles.Count; i++) if (roles[i].Role == role) return roles[i];
            return null;
        }

        ArchetypeEntry FindArchetype(VesselArchetype archetype)
        {
            for (int i = 0; i < archetypes.Count; i++) if (archetypes[i].Archetype == archetype) return archetypes[i];
            return null;
        }
    }
}
