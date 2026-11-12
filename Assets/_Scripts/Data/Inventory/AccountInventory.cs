using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// Everything unique an account owns (Docs/Pilots/INVENTORY.md §4): its crew of pilots, the artifacts its Ark hoards,
    /// its captain, and what each vessel flies outside a squad (the Hangar). Saved squads (<see cref="SquadBook"/>) point
    /// into it by id. Persisted as <c>InventoryCloudData.Inventory</c> under the <c>ACCOUNT_INVENTORY</c> cloud key.
    ///
    /// <para>Pure data with no engine dependency, so every rule runs in an edit-mode test or headless, like
    /// <see cref="SquadBook"/>. Every mutation is a method here that refuses what would break an invariant, and
    /// <see cref="Normalize"/> repairs anything a hand-edited, partial or older record can carry. The invariants:</para>
    /// <list type="bullet">
    /// <item>every id is unique and well formed (<see cref="UniqueId"/>);</item>
    /// <item>the crew holds at most one pilot per callsign (VOYAGES.md §6.2.1);</item>
    /// <item>every artifact accepts exactly one track element and its crystals fed are never negative;</item>
    /// <item>the captain, and every Hangar seat and slot, names something the account holds.</item>
    /// </list>
    /// </summary>
    [Serializable]
    public class AccountInventory
    {
        /// <summary>The most artifacts a hoard keeps (a guard on the cloud record's size, about 700 bytes each, not a
        /// design limit; raise it with the backend).</summary>
        public const int MaxArtifacts = 1000;

        public List<PilotRecord> Pilots = new();
        public List<ArtifactRecord> Artifacts = new();
        /// <summary>The pilot promoted to captain: the player's faction is theirs (VOYAGES.md §6.2.1). Empty for none.</summary>
        public string CaptainId;
        /// <summary>Per vessel, the pilot and artifact it flies when no squad is deployed (the Hangar tabs).</summary>
        public List<HangarLoadout> Hangar = new();
        /// <summary>True once the starter crew and hoard were granted (<c>AccountInventoryStore</c>).</summary>
        public bool StarterGranted;
        /// <summary>True once this device's pre-inventory PlayerPrefs (Hangar seats, slots, track levels) were folded in.</summary>
        public bool DeviceImportDone;

        // ── Pilots ──────────────────────────────────────────────────────────

        /// <summary>The pilot with this exact id, or null.</summary>
        public PilotRecord Pilot(string id)
        {
            if (string.IsNullOrEmpty(id) || Pilots == null) return null;
            foreach (var p in Pilots)
                if (p != null && p.Id == id) return p;
            return null;
        }

        /// <summary>The crew's pilot carrying <paramref name="callsignId"/> (<c>pilot.callsign.volt</c>), or null.</summary>
        public PilotRecord PilotWithCallsign(string callsignId)
        {
            if (string.IsNullOrEmpty(callsignId) || Pilots == null) return null;
            foreach (var p in Pilots)
                if (p != null && p.CallsignOrTemplate() == callsignId) return p;
            return null;
        }

        public bool HasCallsign(PilotVision vision) => PilotWithCallsign(Callsigns.IdOf(vision)) != null;

        public enum AddResult { Added = 0, Invalid = 1, DuplicateId = 2, CallsignTaken = 3, Full = 4 }

        /// <summary>Adds <paramref name="pilot"/> to the crew. Refused when its id is malformed or held, or the crew
        /// already has a pilot with its callsign.</summary>
        public AddResult TryAddPilot(PilotRecord pilot)
        {
            Pilots ??= new List<PilotRecord>();
            if (pilot == null || !UniqueId.TrySplit(pilot.Id, out string template, out _) || !Callsigns.TryParse(template, out _))
                return AddResult.Invalid;
            if (string.IsNullOrEmpty(pilot.CallsignId)) pilot.CallsignId = template;
            if (pilot.CallsignId != template) return AddResult.Invalid;
            if (Pilot(pilot.Id) != null) return AddResult.DuplicateId;
            if (PilotWithCallsign(template) != null) return AddResult.CallsignTaken;
            Pilots.Add(pilot);
            return AddResult.Added;
        }

        /// <summary>
        /// Rolls and adds a new pilot on <paramref name="callsign"/>, re-rolling (up to 8 seeds) until the person's name is
        /// new to this crew. Null when the crew already has the callsign. <paramref name="seeds"/> feeds the seeds (tests);
        /// null draws fresh ones.
        /// </summary>
        public PilotRecord MintPilot(PilotVision callsign, FactionType factionType, string origin, long nowMs, Func<ulong> seeds = null)
        {
            if (callsign == PilotVision.None || HasCallsign(callsign)) return null;
            seeds ??= SeededRandom.NewSeed;
            PilotRecord pilot = null;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                pilot = PilotGenerator.Roll(callsign, seeds(), factionType, origin, nowMs);
                if (pilot != null && Pilot(pilot.Id) == null && !NameTaken(pilot.DisplayName())) break;
            }
            if (pilot == null || TryAddPilot(pilot) != AddResult.Added) return null;
            pilot.AddChronicle("Joined the crew.");
            return pilot;
        }

        bool NameTaken(string name)
        {
            foreach (var p in Pilots)
                if (p != null && p.DisplayName() == name) return true;
            return false;
        }

        /// <summary>Discontinues a pilot (VOYAGES.md §2: "discontinue a pilot"): out of the crew, out of the captain's
        /// chair and out of every Hangar seat. Saved squads that named them fall back when read.</summary>
        public bool RemovePilot(string id)
        {
            var p = Pilot(id);
            if (p == null) return false;
            Pilots.Remove(p);
            if (CaptainId == id) CaptainId = null;
            foreach (var h in Hangar)
                if (h != null && h.PilotId == id) h.PilotId = null;
            return true;
        }

        // ── Captain and factions ────────────────────────────────────────────

        public PilotRecord Captain() => Pilot(CaptainId);

        /// <summary>Promotes a crew pilot to captain, which is how the player joins that pilot's faction (§6.2.1).</summary>
        public bool PromoteToCaptain(string id)
        {
            var p = Pilot(id);
            if (p == null) return false;
            if (CaptainId == id) return true;
            CaptainId = id;
            p.AddChronicle(string.IsNullOrEmpty(p.FactionName) ? "Promoted to captain." : "Promoted to captain; the crew now flies for " + p.FactionName + ".");
            return true;
        }

        /// <summary>The player's own faction: the captain's. Empty with no captain.</summary>
        public string PrimaryFactionId() => Captain()?.FactionId ?? string.Empty;

        /// <summary>Every other faction the crew's pilots belong to (allies: their jobs, not their membership).</summary>
        public void AllyFactionIds(List<string> into)
        {
            into.Clear();
            string primary = PrimaryFactionId();
            foreach (var p in Pilots)
                if (p != null && !string.IsNullOrEmpty(p.FactionId) && p.FactionId != primary && !into.Contains(p.FactionId))
                    into.Add(p.FactionId);
        }

        // ── Artifacts ───────────────────────────────────────────────────────

        public ArtifactRecord Artifact(string id)
        {
            if (string.IsNullOrEmpty(id) || Artifacts == null) return null;
            foreach (var a in Artifacts)
                if (a != null && a.Id == id) return a;
            return null;
        }

        /// <summary>The hoard's first artifact of <paramref name="kindId"/> not in <paramref name="except"/>, or null.</summary>
        public ArtifactRecord FirstOfKind(string kindId, ICollection<string> except = null)
        {
            if (string.IsNullOrEmpty(kindId) || Artifacts == null) return null;
            foreach (var a in Artifacts)
                if (a != null && a.Kind() == kindId && (except == null || !except.Contains(a.Id))) return a;
            return null;
        }

        public int CountOfKind(string kindId)
        {
            int n = 0;
            if (Artifacts != null)
                foreach (var a in Artifacts)
                    if (a != null && a.Kind() == kindId) n++;
            return n;
        }

        /// <summary>Adds an artifact to the hoard. Refused for a malformed or held id, an element it cannot be attuned
        /// to, or a full hoard.</summary>
        public AddResult TryAddArtifact(ArtifactRecord artifact)
        {
            Artifacts ??= new List<ArtifactRecord>();
            if (artifact == null || !UniqueId.TrySplit(artifact.Id, out string kind, out _) || !ArtifactLevels.IsTrackElement(artifact.Element))
                return AddResult.Invalid;
            if (string.IsNullOrEmpty(artifact.ArchetypeId)) artifact.ArchetypeId = kind;
            if (artifact.ArchetypeId != kind) return AddResult.Invalid;
            if (Artifact(artifact.Id) != null) return AddResult.DuplicateId;
            if (Artifacts.Count >= MaxArtifacts) return AddResult.Full;
            if (artifact.Invested < 0) artifact.Invested = 0;
            Artifacts.Add(artifact);
            return AddResult.Added;
        }

        /// <summary>Rolls and adds a new artifact of <paramref name="kind"/>. Null when the kind cannot roll one or the
        /// hoard is full.</summary>
        public ArtifactRecord MintArtifact(ArtifactArchetype kind, string origin, long nowMs, Func<ulong> seeds = null)
        {
            seeds ??= SeededRandom.NewSeed;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var a = ArtifactGenerator.Roll(kind, seeds(), origin, nowMs);
                if (a == null) return null;
                var result = TryAddArtifact(a);
                if (result == AddResult.Added) return a;
                if (result != AddResult.DuplicateId) return null;
            }
            return null;
        }

        /// <summary>Takes an artifact out of the hoard (lost with a fallen Ark, given away, dev tooling). Hangar slots
        /// that carried it are cleared; saved squads that named it fall back to none when read.</summary>
        public bool RemoveArtifact(string id)
        {
            var a = Artifact(id);
            if (a == null) return false;
            Artifacts.Remove(a);
            foreach (var h in Hangar)
                if (h != null && h.ArtifactId == id) h.ArtifactId = null;
            return true;
        }

        public enum FeedStatus { Fed = 0, NoSuchArtifact = 1, WrongElement = 2, NothingToFeed = 3 }

        /// <summary>What a feed did: the attuned level before and after, and the crystals fed in total.</summary>
        public readonly struct FeedResult
        {
            public readonly FeedStatus Status;
            public readonly float Before;
            public readonly float After;
            public readonly long Invested;

            public FeedResult(FeedStatus status, float before, float after, long invested)
            {
                Status = status;
                Before = before;
                After = after;
                Invested = invested;
            }

            public bool CrossedTwist => Before < ArtifactLevels.TwistLevel && After >= ArtifactLevels.TwistLevel;
        }

        /// <summary>
        /// Feeds <paramref name="crystals"/> of <paramref name="crystal"/> to an artifact. Only its own element (or Omni)
        /// is accepted, and there is no limit: every crystal raises its one track a little less than the one before
        /// (<see cref="ArtifactCurve"/>). Spending the crystals is the caller's (the profile holds them).
        /// </summary>
        public FeedResult Feed(string artifactId, Element crystal, long crystals)
        {
            var a = Artifact(artifactId);
            if (a == null) return new FeedResult(FeedStatus.NoSuchArtifact, 0, 0, 0);
            float before = a.AttunedLevel();
            if (!a.Accepts(crystal)) return new FeedResult(FeedStatus.WrongElement, before, before, a.Invested);
            if (crystals <= 0) return new FeedResult(FeedStatus.NothingToFeed, before, before, a.Invested);
            a.Invested = ArtifactRecord.SafeAdd(a.Invested, crystals);
            return new FeedResult(FeedStatus.Fed, before, a.AttunedLevel(), a.Invested);
        }

        // ── The Hangar (one pilot and one artifact per vessel outside a squad) ──

        public HangarLoadout HangarFor(VesselClassType vessel)
        {
            if (Hangar == null) return null;
            foreach (var h in Hangar)
                if (h != null && h.Vessel == vessel) return h;
            return null;
        }

        HangarLoadout EnsureHangar(VesselClassType vessel)
        {
            Hangar ??= new List<HangarLoadout>();
            var h = HangarFor(vessel);
            if (h != null) return h;
            h = new HangarLoadout { Vessel = vessel };
            Hangar.Add(h);
            return h;
        }

        /// <summary>Seats a held pilot on <paramref name="vessel"/> (empty clears the seat). False for a pilot not held.</summary>
        public bool SetHangarPilot(VesselClassType vessel, string pilotId)
        {
            if (!string.IsNullOrEmpty(pilotId) && Pilot(pilotId) == null) return false;
            EnsureHangar(vessel).PilotId = string.IsNullOrEmpty(pilotId) ? null : pilotId;
            return true;
        }

        /// <summary>Slots a held artifact on <paramref name="vessel"/> (empty clears the slot). An artifact rides one
        /// vessel at a time, so it leaves any other Hangar slot. False for an artifact not held.</summary>
        public bool SetHangarArtifact(VesselClassType vessel, string artifactId)
        {
            if (!string.IsNullOrEmpty(artifactId) && Artifact(artifactId) == null) return false;
            if (!string.IsNullOrEmpty(artifactId))
                foreach (var h in Hangar)
                    if (h != null && h.Vessel != vessel && h.ArtifactId == artifactId) h.ArtifactId = null;
            EnsureHangar(vessel).ArtifactId = string.IsNullOrEmpty(artifactId) ? null : artifactId;
            return true;
        }

        // ── Squad slots point into the inventory ────────────────────────────

        /// <summary>
        /// The held pilot a squad slot's <paramref name="slotPilotId"/> names: the pilot itself when held; for a legacy
        /// slot that saved a bare callsign id, or a unique id this crew does not hold (a friend's pilot), the crew's
        /// pilot with that callsign. Null when the crew has none.
        /// </summary>
        public PilotRecord ResolvePilot(string slotPilotId)
        {
            var held = Pilot(slotPilotId);
            if (held != null) return held;
            string template = UniqueId.TemplateOf(slotPilotId);
            return Callsigns.IsCallsignId(template) ? PilotWithCallsign(template) : null;
        }

        /// <summary>
        /// The held artifact a squad slot's <paramref name="slotArtifactId"/> names: the artifact itself when held; for a
        /// legacy slot that saved a bare kind id, the hoard's first artifact of that kind not in
        /// <paramref name="except"/>. Null otherwise (a unique id not held is gone: lost, or someone else's).
        /// </summary>
        public ArtifactRecord ResolveArtifact(string slotArtifactId, ICollection<string> except = null)
        {
            var held = Artifact(slotArtifactId);
            if (held != null) return held;
            return UniqueId.IsUnique(slotArtifactId) ? null : FirstOfKind(slotArtifactId, except);
        }

        /// <summary>
        /// Points every squad the player MADE at this inventory: a legacy callsign id becomes the crew's pilot of that
        /// callsign, a legacy kind id the first artifact of that kind no other slot of the squad took, and an artifact no
        /// longer held is dropped. A joined squad is left alone (its ids are its owner's). True when a slot changed.
        /// </summary>
        public bool AdoptSquads(SquadBook book)
        {
            if (book?.Squads == null) return false;
            bool changed = false;
            var taken = new HashSet<string>();
            foreach (var squad in book.Squads)
            {
                if (squad == null || squad.IsJoined()) continue;
                taken.Clear();
                for (int seat = 0; seat < SavedSquad.SlotCount; seat++)
                {
                    var slot = squad.GetSlot(seat);
                    var pilot = string.IsNullOrEmpty(slot.PilotId) ? null : ResolvePilot(slot.PilotId);
                    string pilotId = pilot?.Id;
                    var artifact = string.IsNullOrEmpty(slot.ArtifactId) ? null : ResolveArtifact(slot.ArtifactId, taken);
                    string artifactId = artifact != null && !taken.Contains(artifact.Id) ? artifact.Id : null;
                    if (artifactId != null) taken.Add(artifactId);
                    // A pilot id the crew cannot resolve is kept: the slot falls back when read, and FillPilots repairs it.
                    string newPilot = pilotId ?? slot.PilotId;
                    if (newPilot == slot.PilotId && artifactId == (string.IsNullOrEmpty(slot.ArtifactId) ? null : slot.ArtifactId)) continue;
                    slot.PilotId = newPilot;
                    slot.ArtifactId = artifactId;
                    squad.SetSlot(seat, slot);
                    changed = true;
                }
            }
            return changed;
        }

        // ── Repair ──────────────────────────────────────────────────────────

        /// <summary>
        /// Repairs the inventory after a load: missing lists, null and malformed entries, repeated ids, a second pilot on
        /// one callsign (the first is kept), an artifact attuned to no track element, negative or broken curve numbers,
        /// text the UI font cannot draw, a captain or Hangar pick that names nothing held. Idempotent.
        /// </summary>
        public void Normalize()
        {
            Pilots ??= new List<PilotRecord>();
            Artifacts ??= new List<ArtifactRecord>();
            Hangar ??= new List<HangarLoadout>();

            var ids = new HashSet<string>();
            var callsigns = new HashSet<string>();
            Pilots.RemoveAll(p =>
            {
                if (p == null || !UniqueId.TrySplit(p.Id, out string template, out _) || !Callsigns.TryParse(template, out _)) return true;
                if (!ids.Add(p.Id) || !callsigns.Add(template)) return true;
                p.CallsignId = template;
                p.GivenName = PilotGenerator.Printable(p.GivenName);
                p.FamilyName = PilotGenerator.Printable(p.FamilyName);
                p.Backstory = PilotGenerator.PrintableText(p.Backstory);
                p.Chronicle ??= new List<string>();
                p.Chronicle.RemoveAll(string.IsNullOrWhiteSpace);
                if (p.Chronicle.Count > PilotRecord.MaxChronicle) p.Chronicle.RemoveRange(0, p.Chronicle.Count - PilotRecord.MaxChronicle);
                return false;
            });

            ids.Clear();
            Artifacts.RemoveAll(a =>
            {
                if (a == null || !UniqueId.TrySplit(a.Id, out string kind, out _) || !ArtifactLevels.IsTrackElement(a.Element)) return true;
                if (!ids.Add(a.Id)) return true;
                a.ArchetypeId = kind;
                a.Name = PilotGenerator.Printable(a.Name);
                if (a.Invested < 0) a.Invested = 0;
                if (!(a.Ceiling <= ArtifactLevels.MaxLevel)) a.Ceiling = ArtifactLevels.MaxLevel;   // also NaN
                if (!(a.BaseLevel >= 0f)) a.BaseLevel = 0f;
                if (a.BaseLevel > a.Ceiling) a.BaseLevel = a.Ceiling;
                if (!(a.HalfCrystals > 0f)) a.HalfCrystals = ArtifactGenerator.HalfMax;
                return false;
            });
            if (Artifacts.Count > MaxArtifacts) Artifacts.RemoveRange(MaxArtifacts, Artifacts.Count - MaxArtifacts);

            if (!string.IsNullOrEmpty(CaptainId) && Pilot(CaptainId) == null) CaptainId = null;

            var vessels = new HashSet<VesselClassType>();
            var slotted = new HashSet<string>();
            Hangar.RemoveAll(h => h == null || !vessels.Add(h.Vessel));
            foreach (var h in Hangar)
            {
                if (!string.IsNullOrEmpty(h.PilotId) && Pilot(h.PilotId) == null) h.PilotId = null;
                if (!string.IsNullOrEmpty(h.ArtifactId) && (Artifact(h.ArtifactId) == null || !slotted.Add(h.ArtifactId))) h.ArtifactId = null;
            }
        }
    }

    /// <summary>What one vessel flies outside a squad: its seated pilot and slotted artifact (ids into the inventory).</summary>
    [Serializable]
    public class HangarLoadout
    {
        public VesselClassType Vessel;
        public string PilotId;
        public string ArtifactId;
    }
}
