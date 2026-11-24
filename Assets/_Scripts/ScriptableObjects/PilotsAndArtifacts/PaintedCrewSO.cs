using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
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
    /// <para><b>A crew of one's own</b> (Garrett, 2026-11-18: "each player of our game has a unique crew of pilots";
    /// 2026-11-19: "lets get everything in place so that each of these players get their own crew"). Every painted
    /// pilot (the fifty and the roster's) is in the pool, marked <see cref="Person.starter"/>. An account is dealt
    /// eighteen of them, one a callsign, once (<see cref="AccountInventory.Crew"/>): a DEALT crew when it claims one
    /// (<see cref="Claim"/>; the seven test-pool crews, unique among themselves, <see cref="Crews"/>), else a random
    /// set picked by a hash of the player's id (<see cref="RandomCrew"/>, Tools/Build/pilot_art/roster.py's
    /// random_crew bit for bit). Only crew members are granted; a starter pilot granted before crews (every painted
    /// pilot, 2026-11-16..19) who is not in the crew leaves it, and the crew pilot on the same callsign takes their
    /// captain's chair and Hangar seats. Who holds which dealt crew is player data and is never in the repository.</para>
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

        /// <summary>A dealt crew: one painted pilot's key a callsign, in callsign order.</summary>
        [Serializable]
        public class Crew
        {
            public List<string> members = new();
            [Tooltip("SHA-256 (hex) of \"cosmic-shore/crew-code/\" + this crew's claim code (CodeKey). The code itself is "
                     + "private and never in the repository; empty claims nothing.")]
            public string codeHash;
        }

        /// <summary>A tester's account that holds a dealt crew without doing anything (Garrett, 2026-11-19: "have it just
        /// work for them"). Only the hash of the player id is kept, never the id or whose account it is.</summary>
        [Serializable]
        public class Account
        {
            [Tooltip("SHA-256 (hex) of \"cosmic-shore/crew-account/\" + the UGS player id (AccountHash).")]
            public string idHash;
            [Tooltip("The dealt crew this account holds, from 0 (an index into crews).")]
            public int crew;
        }

        [Tooltip("One row per painted pilot, in the order a crew is granted them. This order is the pool's: a random crew "
                 + "picks from it by index, so append only.")]
        [SerializeField] List<Person> people = new();

        [Tooltip("The dealt crews, in the order roster.py deals them. Never reorder: an account that claimed one keeps "
                 + "its index.")]
        [SerializeField] List<Crew> crews = new();

        [Tooltip("The testers' accounts and the dealt crew each holds. Written by Tools/Build/author_pilot_scene_assets.py "
                 + "from Docs/Pilots/art/roster/crew_accounts.json (hashes only).")]
        [SerializeField] List<Account> accounts = new();

        public IReadOnlyList<Person> People => people;
        public IReadOnlyList<Account> Accounts => accounts;
        public IReadOnlyList<Crew> Crews => crews;

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
        public bool Grant(AccountInventory inventory, long nowMs) => Grant(inventory, nowMs, null);

        /// <summary>
        /// <see cref="Grant(AccountInventory,long)"/> for a player: an account with no crew yet is dealt the random set
        /// <paramref name="playerId"/> picks (<see cref="RandomCrew"/>), then only its crew is granted and every starter
        /// pilot outside it leaves (<see cref="Dismiss"/>). With no id and no crew, every starter pilot is granted (the
        /// build before crews).
        /// </summary>
        public bool Grant(AccountInventory inventory, long nowMs, string playerId)
        {
            if (inventory == null || people == null) return false;
            // A tester's account holds its dealt crew with nothing to do: the first read after the list names it claims
            // it (a random crew dealt before is replaced). Claim grants again with no id, so this asks once.
            int listed = CrewForAccount(playerId);
            if (listed >= 0 && inventory.ClaimedCrew != listed + 1 && Claim(inventory, listed, nowMs)) return true;
            inventory.Crew ??= new List<string>();
            bool dealt = false;
            if (inventory.Crew.Count == 0 && !string.IsNullOrEmpty(playerId))
            {
                inventory.Crew.AddRange(RandomCrew(playerId));
                dealt = inventory.Crew.Count > 0;
            }
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
                if (!inventory.InCrew(p.key)) continue;
                if (inventory.PaintedGranted.Contains(p.key) || inventory.PaintedPilot(p.key) != null) continue;
                var pilot = Mint(p, inventory, nowMs);
                if (pilot == null) continue;
                inventory.PaintedGranted.Add(p.key);
                changed = true;
            }

            if (Dismiss(inventory)) changed = true;

            foreach (var pilot in inventory.Pilots)
                if (Paint(pilot)) changed = true;
            return changed || dealt;
        }

        /// <summary>
        /// Deals the dealt crew <paramref name="index"/> (from 0) to <paramref name="inventory"/>, replacing whatever crew
        /// it had, and grants it. False for an index with no crew.
        /// </summary>
        public bool Claim(AccountInventory inventory, int index, long nowMs)
        {
            if (inventory == null || crews == null || index < 0 || index >= crews.Count || crews[index]?.members == null) return false;
            inventory.Crew = new List<string>(crews[index].members);
            inventory.ClaimedCrew = index + 1;
            // A pilot of the new crew dismissed with the old one comes back.
            inventory.PaintedGranted?.RemoveAll(k => inventory.Crew.Contains(k) && inventory.PaintedPilot(k) == null);
            Grant(inventory, nowMs, null);
            return true;
        }

        /// <summary>
        /// Starter pilots outside the account's crew leave it (none while no crew is dealt). Before one goes, the crew's
        /// painted pilot on the same callsign takes their captain's chair and their Hangar seats, so nothing the player
        /// set up is left empty. Recruits (any other origin) stay.
        /// </summary>
        public static bool Dismiss(AccountInventory inventory)
        {
            if (inventory?.Pilots == null || inventory.Crew == null || inventory.Crew.Count == 0) return false;
            var leaving = new List<PilotRecord>();
            foreach (var pilot in inventory.Pilots)
                if (pilot != null && pilot.Origin == StarterOrigin && !string.IsNullOrEmpty(pilot.PaintedKey) && !inventory.InCrew(pilot.PaintedKey))
                    leaving.Add(pilot);
            foreach (var pilot in leaving)
            {
                PilotRecord heir = null;
                foreach (var other in inventory.Pilots)
                    if (other != null && other != pilot && inventory.InCrew(other.PaintedKey) && !string.IsNullOrEmpty(other.PaintedKey)
                        && other.CallsignOrTemplate() == pilot.CallsignOrTemplate())
                    {
                        heir = other;
                        break;
                    }
                if (heir != null)
                {
                    if (inventory.CaptainId == pilot.Id) inventory.CaptainId = heir.Id;
                    foreach (var h in inventory.Hangar)
                        if (h != null && h.PilotId == pilot.Id) h.PilotId = heir.Id;
                }
                inventory.RemovePilot(pilot.Id);
            }
            return leaving.Count > 0;
        }

        /// <summary>A typed claim code as it is hashed: upper case, letters and digits only ("ab12-cd34" is "AB12CD34").
        /// </summary>
        public static string CodeKey(string typed)
        {
            if (string.IsNullOrEmpty(typed)) return string.Empty;
            var sb = new StringBuilder(typed.Length);
            foreach (char c in typed)
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
            return sb.ToString();
        }

        /// <summary>The hash a crew's <see cref="Crew.codeHash"/> holds for <paramref name="typed"/>.</summary>
        public static string CodeHash(string typed) => Sha256Hex("cosmic-shore/crew-code/" + CodeKey(typed));

        /// <summary>The hash an <see cref="Account.idHash"/> holds for a UGS player id.</summary>
        public static string AccountHash(string playerId) => Sha256Hex("cosmic-shore/crew-account/" + (playerId ?? string.Empty));

        /// <summary>The dealt crew (from 0) listed for this player id, else -1.</summary>
        public int CrewForAccount(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || accounts == null || accounts.Count == 0 || crews == null) return -1;
            string hash = AccountHash(playerId);
            foreach (var a in accounts)
                if (a != null && a.crew >= 0 && a.crew < crews.Count
                    && string.Equals(a.idHash, hash, StringComparison.OrdinalIgnoreCase))
                    return a.crew;
            return -1;
        }

        static string Sha256Hex(string text)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>The dealt crew (from 0) whose claim code is <paramref name="typed"/>, else -1. A code is at least eight
        /// letters or digits, so a short display name never asks the question.</summary>
        public int CrewForCode(string typed)
        {
            if (crews == null || CodeKey(typed).Length < 8) return -1;
            string hash = CodeHash(typed);
            for (int i = 0; i < crews.Count; i++)
                if (crews[i] != null && !string.IsNullOrEmpty(crews[i].codeHash)
                    && string.Equals(crews[i].codeHash, hash, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        /// <summary>
        /// The random crew for a player outside the dealt crews (Garrett, 2026-11-18: "all other players can grab a
        /// random set from that pool"): for each callsign, the starter pilot at index SHA-256("cosmic-shore/crew/{id}/
        /// {CALLSIGN}")[first 12 hex digits] mod the callsign's count, in <see cref="People"/>'s order. The same for a
        /// player every time; it may share pilots with anyone else's. Empty for an empty id.
        /// </summary>
        public List<string> RandomCrew(string playerId)
        {
            var crew = new List<string>();
            if (string.IsNullOrEmpty(playerId) || people == null) return crew;
            var pool = new List<string>();
            using var sha = SHA256.Create();
            foreach (var vision in Callsigns.All)
            {
                pool.Clear();
                foreach (var p in people)
                    if (p != null && p.starter && p.callsign == vision && !string.IsNullOrEmpty(p.key)) pool.Add(p.key);
                if (pool.Count == 0) continue;
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes($"cosmic-shore/crew/{playerId}/{Callsigns.WordOf(vision)}"));
                ulong h = 0;
                for (int i = 0; i < 6; i++) h = (h << 8) | hash[i];          // the first 12 hex digits
                crew.Add(pool[(int)(h % (ulong)pool.Count)]);
            }
            return crew;
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
