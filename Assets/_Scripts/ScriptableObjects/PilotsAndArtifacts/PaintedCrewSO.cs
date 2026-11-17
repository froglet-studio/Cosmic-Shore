using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The PAINTED pilots as people: one per painted scene (Docs/Pilots/PORTRAITS.md §8), keyed by the scene's word
    /// (<c>VOLT</c>, <c>Astrofoil</c>). A crew's painted pilot IS that person (Garrett, 2026-11-12: the stories must match
    /// the art), so <see cref="Paint"/> gives the record this person's name, animals, faction, home and story, and the
    /// profile page, the crew list and a voyage's jobs all read the person in the picture.
    ///
    /// <para><b>All fifty, callsigns shared</b> (Garrett, 2026-11-16: "lets get all 50 into the game. it is ok if we
    /// duplicate callsign everything else will be the different"). Eighteen people carry their own word as callsign; the
    /// other thirty-two share one. Every person marked <see cref="Person.starter"/> is granted to every crew
    /// (<see cref="Grant"/>); a person leaves the starter crew by clearing that flag in people.json, which is how crews
    /// move to growing one pilot at a time.</para>
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
            [Tooltip("The painted pilot's word: the scene's name (VOLT, Astrofoil). Saved on the crew's record; never rename.")]
            public string key;
            [Tooltip("The callsign this person carries. More than one person may carry a callsign.")]
            public PilotVision callsign;
            [Tooltip("Granted to every crew. Clear it and new crews (and crews that never had them) no longer get this pilot.")]
            public bool starter;
            public string given;
            [Tooltip("Empty for a pilot known by one name.")]
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

        [Tooltip("One row per painted pilot, in the order a crew is granted them.")]
        [SerializeField] List<Person> people = new();

        public IReadOnlyList<Person> People => people;

        /// <summary>The origin of a painted pilot's record (<see cref="PilotRecord.Origin"/>).</summary>
        public const string StarterOrigin = "starter";

        static PaintedCrewSO _loaded;

        /// <summary>The asset under Resources, or null when this build has none.</summary>
        public static PaintedCrewSO Load()
        {
            if (!_loaded) _loaded = Resources.Load<PaintedCrewSO>(ResourcePath);
            return _loaded;
        }

        /// <summary>The painted pilot <paramref name="key"/>. A quiet probe: false when there is none.</summary>
        public bool TryGet(string key, out Person person)
        {
            person = null;
            if (people == null || string.IsNullOrEmpty(key)) return false;
            foreach (var candidate in people)
            {
                if (candidate == null || candidate.key != key) continue;
                person = candidate;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Makes <paramref name="pilot"/> the painted pilot its <see cref="PilotRecord.PaintedKey"/> names: name, animals,
        /// faction, home and story. Its id, callsign, origin and chronicle are kept, so a squad that seats it and the story
        /// it grew stay. True when a field changed; false for a rolled person, a key with no painted pilot, or a record
        /// that already matches.
        /// </summary>
        public bool Paint(PilotRecord pilot)
        {
            if (pilot == null || !TryGet(pilot.PaintedKey, out var p)) return false;
            string factionId = string.IsNullOrEmpty(p.factionName) ? string.Empty : FactionCatalog.IdOf(p.factionType, p.factionName);
            string family = p.family ?? string.Empty;
            string story = p.story ?? string.Empty;
            bool changed = pilot.GivenName != p.given || pilot.FamilyName != family
                           || pilot.SpeciesOne != p.speciesOne || pilot.SpeciesTwo != p.speciesTwo
                           || pilot.FactionType != p.factionType || pilot.FactionId != factionId
                           || pilot.FactionName != p.factionName || pilot.Homeworld != p.homeworld
                           || pilot.Backstory != story;
            if (!changed) return false;
            pilot.GivenName = p.given;
            pilot.FamilyName = family;
            pilot.SpeciesOne = p.speciesOne;
            pilot.SpeciesTwo = p.speciesTwo;
            pilot.FactionType = p.factionType;
            pilot.FactionId = factionId;
            pilot.FactionName = p.factionName;
            pilot.Homeworld = p.homeworld;
            pilot.Backstory = story;
            return true;
        }

        /// <summary>
        /// Brings <paramref name="inventory"/> up to the painted crew, in three steps, and says whether anything changed:
        /// a starter record saved before painted pilots had keys (2026-11-12..16: one rolled person a callsign, painted by
        /// callsign) becomes its callsign's own painted pilot; every <see cref="Person.starter"/> pilot the crew was never
        /// granted joins it (one discontinued is not granted again, <see cref="AccountInventory.PaintedGranted"/>); and
        /// every painted record is painted again, so an edited story catches up.
        /// </summary>
        public bool Grant(AccountInventory inventory, long nowMs)
        {
            if (inventory == null || people == null) return false;
            inventory.Pilots ??= new List<PilotRecord>();
            inventory.PaintedGranted ??= new List<string>();
            bool changed = false;

            foreach (var pilot in inventory.Pilots)
            {
                if (pilot == null || !string.IsNullOrEmpty(pilot.PaintedKey) || pilot.Origin != StarterOrigin) continue;
                string own = Callsigns.WordOf(pilot.Vision());
                if (!TryGet(own, out _) || inventory.PaintedPilot(own) != null) continue;
                pilot.PaintedKey = own;
                changed = true;
            }
            foreach (var pilot in inventory.Pilots)
                if (pilot != null && !string.IsNullOrEmpty(pilot.PaintedKey) && !inventory.PaintedGranted.Contains(pilot.PaintedKey))
                {
                    inventory.PaintedGranted.Add(pilot.PaintedKey);
                    changed = true;
                }

            foreach (var p in people)
            {
                if (p == null || !p.starter || string.IsNullOrEmpty(p.key) || p.callsign == PilotVision.None) continue;
                if (inventory.PaintedGranted.Contains(p.key) || inventory.PaintedPilot(p.key) != null) continue;
                var pilot = Mint(p, inventory, nowMs);
                if (pilot == null) continue;
                inventory.PaintedGranted.Add(p.key);
                changed = true;
            }

            foreach (var pilot in inventory.Pilots)
                if (Paint(pilot)) changed = true;
            return changed;
        }

        static PilotRecord Mint(Person p, AccountInventory inventory, long nowMs)
        {
            string callsignId = Callsigns.IdOf(p.callsign);
            // The same id in every crew, from the painted pilot's key: a squad or a friend's card names the same person.
            ulong seed = SeedOf(p.key);
            for (int attempt = 0; attempt < 8; attempt++, seed++)
            {
                if (seed == 0) continue;
                var pilot = new PilotRecord
                {
                    Id = UniqueId.Compose(callsignId, seed),
                    CallsignId = callsignId,
                    GeneratorVersion = PilotGenerator.Version,
                    PaintedKey = p.key,
                    Origin = StarterOrigin,
                    MintedAtMs = nowMs,
                };
                if (inventory.Pilot(pilot.Id) != null) continue;
                if (inventory.TryAddPilot(pilot) != AccountInventory.AddResult.Added) return null;
                pilot.AddChronicle("Joined the crew.");
                return pilot;
            }
            return null;
        }

        /// <summary>FNV-1a of the key: a fixed seed per painted pilot.</summary>
        public static ulong SeedOf(string key)
        {
            ulong h = 14695981039346656037UL;
            foreach (char c in key ?? string.Empty)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            return h == 0 ? 1 : h;
        }
    }
}
