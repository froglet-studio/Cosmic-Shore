using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Data
{
    /// <summary>
    /// What a pilot or an artifact needs from the vessel it is put on: optionally one vessel class,
    /// plus every <see cref="VesselKeyword"/> it names (Docs/PILOTS_AND_ARTIFACTS.md §4).
    ///
    /// <para><b>One rule for both kinds of content.</b> A pilot is usually written for one hull, so
    /// it binds <see cref="Vessel"/>; a generalised pilot (one that shields a trail, say) leaves it at
    /// <see cref="VesselClassType.Any"/> and is offered on every hull meeting its keywords. Artifacts
    /// use the same shape, so a parameter that only one hull has can be scoped to that hull without
    /// minting a single-vessel keyword. Both halves must hold.</para>
    ///
    /// <para>A hull that is not concrete (<see cref="VesselClassType.Random"/>, or
    /// <see cref="VesselClassType.Any"/>) meets nothing: its kit is not known until the match picks
    /// one, so a slot left on Random is offered no pilot or artifact until then.</para>
    /// </summary>
    [Serializable]
    public class VesselRequirement
    {
        [Tooltip("The one vessel class this is for. Any = every class whose keywords match.")]
        public VesselClassType Vessel = VesselClassType.Any;

        [Tooltip("Every keyword the vessel must declare. Empty = no keyword is needed.")]
        public List<VesselKeyword> Keywords = new();

        public VesselRequirement() { }

        public VesselRequirement(VesselClassType vessel, params VesselKeyword[] keywords)
        {
            Vessel = vessel;
            Keywords = new List<VesselKeyword>(keywords ?? Array.Empty<VesselKeyword>());
        }

        /// <summary>True when this is written for one vessel class rather than for any hull.</summary>
        public bool IsBoundToClass() => Vessel != VesselClassType.Any;

        /// <summary>
        /// True when a <paramref name="vesselClass"/> hull declaring <paramref name="satisfied"/>
        /// meets this requirement. Pure and allocation-free; safe to call while filling a picker.
        /// </summary>
        public bool IsMetBy(VesselClassType vesselClass, IReadOnlyList<VesselKeyword> satisfied)
        {
            if (!IsConcrete(vesselClass)) return false;
            if (Vessel != VesselClassType.Any && Vessel != vesselClass) return false;
            if (Keywords == null) return true;

            for (int i = 0; i < Keywords.Count; i++)
            {
                var keyword = Keywords[i];
                if (keyword == VesselKeyword.None) continue;
                if (!Contains(satisfied, keyword)) return false;
            }
            return true;
        }

        /// <summary>True for a real hull: not <see cref="VesselClassType.Random"/> or <see cref="VesselClassType.Any"/>.</summary>
        public static bool IsConcrete(VesselClassType vesselClass) =>
            vesselClass != VesselClassType.Random && vesselClass != VesselClassType.Any;

        static bool Contains(IReadOnlyList<VesselKeyword> keywords, VesselKeyword keyword)
        {
            if (keywords == null) return false;
            for (int i = 0; i < keywords.Count; i++)
                if (keywords[i] == keyword) return true;
            return false;
        }
    }
}
