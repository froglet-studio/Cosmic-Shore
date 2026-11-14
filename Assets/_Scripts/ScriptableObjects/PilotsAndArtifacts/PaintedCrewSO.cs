using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The PAINTED pilots as people: per callsign, the pilot its scene shows (Docs/Pilots/PORTRAITS.md §8). A crew's
    /// starter pilot on a callsign IS that callsign's painted pilot (Garrett, 2026-11-12: the stories must match the
    /// art), so <see cref="Paint"/> gives a starter record this person's name, animals, faction, home and story, and
    /// the profile page, the crew list and a voyage's jobs all read the person in the picture.
    ///
    /// <para>Written by <c>Tools/Build/author_pilot_scene_assets.py</c> from <c>Docs/Pilots/art/scenes/people.json</c>,
    /// whose stories are the authored text (short paragraphs about the wider world and the work the pilot's faction
    /// brings); its <c>--check</c> fails on a hand edit. Under Resources, so the inventory reads it without a scene
    /// reference (<see cref="Load"/>).</para>
    /// </summary>
    public class PaintedCrewSO : ScriptableObject
    {
        public const string ResourcePath = "Pilots/PaintedCrew";

        [Serializable]
        public class Person
        {
            [Tooltip("The callsign whose painted pilot this is.")]
            public PilotVision callsign;
            public string given;
            public string family;
            public AnimalSpecies speciesOne;
            public AnimalSpecies speciesTwo;
            public FactionType factionType;
            [Tooltip("One of FactionCatalog's founding names of the faction type.")]
            public string factionName;
            public string homeworld;
            [Tooltip("The story on the profile page: short paragraphs, a blank line between two. Empty shows nothing.")]
            [TextArea(4, 16)]
            public string story;
        }

        [Tooltip("One row per callsign with a painted pilot, in callsign order.")]
        [SerializeField] List<Person> people = new();

        public IReadOnlyList<Person> People => people;

        static PaintedCrewSO _loaded;

        /// <summary>The asset under Resources, or null when this build has none.</summary>
        public static PaintedCrewSO Load()
        {
            if (!_loaded) _loaded = Resources.Load<PaintedCrewSO>(ResourcePath);
            return _loaded;
        }

        /// <summary>The painted pilot of <paramref name="callsign"/>. A quiet probe: false when it has none.</summary>
        public bool TryGet(PilotVision callsign, out Person person)
        {
            person = null;
            if (people == null) return false;
            foreach (var candidate in people)
            {
                if (candidate == null || candidate.callsign != callsign) continue;
                person = candidate;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Makes <paramref name="pilot"/> its callsign's painted pilot: name, animals, faction, home and story. Its id,
        /// callsign, origin and chronicle are kept, so a squad that seats it and the story it grew stay. True when a
        /// field changed; false when the callsign has no painted pilot or the record already matches.
        /// </summary>
        public bool Paint(PilotRecord pilot)
        {
            if (pilot == null || !TryGet(pilot.Vision(), out var p)) return false;
            string factionId = string.IsNullOrEmpty(p.factionName) ? string.Empty : FactionCatalog.IdOf(p.factionType, p.factionName);
            string story = p.story ?? string.Empty;
            bool changed = pilot.GivenName != p.given || pilot.FamilyName != p.family
                           || pilot.SpeciesOne != p.speciesOne || pilot.SpeciesTwo != p.speciesTwo
                           || pilot.FactionType != p.factionType || pilot.FactionId != factionId
                           || pilot.FactionName != p.factionName || pilot.Homeworld != p.homeworld
                           || pilot.Backstory != story;
            if (!changed) return false;
            pilot.GivenName = p.given;
            pilot.FamilyName = p.family;
            pilot.SpeciesOne = p.speciesOne;
            pilot.SpeciesTwo = p.speciesTwo;
            pilot.FactionType = p.factionType;
            pilot.FactionId = factionId;
            pilot.FactionName = p.factionName;
            pilot.Homeworld = p.homeworld;
            pilot.Backstory = story;
            return true;
        }

        /// <summary>The origin of the pilots <see cref="Paint"/> applies to (AccountInventoryStore.GrantStarter).</summary>
        public const string StarterOrigin = "starter";

        /// <summary>The pilots <see cref="Paint"/> applies to: the starter crew. A recruit is a rolled person of their
        /// own and keeps their own record.</summary>
        public static bool IsPaintable(PilotRecord pilot) => pilot != null && pilot.Origin == StarterOrigin;
    }
}
