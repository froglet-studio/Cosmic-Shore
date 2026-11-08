using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Core
{
    /// <summary>
    /// The player's pilots and artifacts (Docs/Pilots/INVENTORY.md): the crew of unique pilots, the Ark's hoard of unique
    /// artifacts, which pilot sits in each vessel's seat and which artifact is in its slot outside a squad, and feeding an
    /// artifact the crystals it accepts. Everything that reads or changes them goes through this class; the data is the
    /// account's <see cref="AccountInventory"/> (<see cref="AccountInventoryStore"/>, the <c>ACCOUNT_INVENTORY</c> record).
    ///
    /// <para><b>Ids.</b> A pilot's id is its callsign's id plus '#' and a seed, an artifact's its kind's id plus '#' and a
    /// seed (<see cref="UniqueId"/>); the catalog finds the callsign or kind from either (<see cref="PilotArtifactCatalogSO"/>).
    /// A bare callsign or kind id is a legacy id from before 2026-11-07 and resolves to the crew's pilot of that callsign
    /// or the hoard's first artifact of that kind.</para>
    ///
    /// <para><b>Crystals are the profile's.</b> The four elemental balances and the Omni balance are the player's cloud
    /// profile (<see cref="CosmicShore.UI.PlayerDataService"/>, Shombith's section 97), so a feed spends the same crystals
    /// the Hangar and the Converter show.</para>
    ///
    /// <para><b>2026-11-07 (Garrett):</b> every pilot is a unique person and every artifact unique, attuned to one element
    /// it accepts without limit. The device-local PlayerPrefs this class kept since 2026-10-27 (seats, slots, finds,
    /// four track levels per artifact) are folded into the inventory once and no longer written.</para>
    /// </summary>
    public static class PilotLoadoutStore
    {
        /// <summary>A seat changed: the vessel and the pilot id now in it (empty = no pilot, so no sight).</summary>
        public static event Action<VesselClassType, string> PilotChanged;
        /// <summary>A vessel's slotted artifact changed (Any: what the player holds changed).</summary>
        public static event Action<VesselClassType> ArtifactsChanged;
        /// <summary>An artifact was fed, or a crystal balance changed.</summary>
        public static event Action UpgradesChanged;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            PilotChanged = null;
            ArtifactsChanged = null;
            UpgradesChanged = null;
        }

        static AccountInventory Inv => AccountInventoryStore.Current;

        // ── The crew ───────────────────────────────────────────────────────

        /// <summary>Every pilot in the crew, in the order they joined, into <paramref name="into"/> (cleared first).</summary>
        public static void GetCrew(List<PilotRecord> into)
        {
            into.Clear();
            foreach (var p in Inv.Pilots)
                if (p != null) into.Add(p);
        }

        /// <summary>The crew's pilot a seat or slot id names (a unique id held, or a legacy callsign id); see
        /// <see cref="AccountInventory.ResolvePilot"/>.</summary>
        public static bool TryGetPilot(string id, out PilotRecord pilot)
        {
            pilot = string.IsNullOrEmpty(id) ? null : Inv.ResolvePilot(id);
            return pilot != null;
        }

        /// <summary>The pilot promoted to captain, or null.</summary>
        public static PilotRecord Captain => Inv.Captain();

        /// <summary>Promotes a crew pilot to captain: the player joins their faction (VOYAGES.md §6.2.1).</summary>
        public static bool PromoteToCaptain(string pilotId)
        {
            if (!Inv.PromoteToCaptain(pilotId)) return false;
            AccountInventoryStore.Save();
            return true;
        }

        /// <summary>Recruits a new pilot on <paramref name="callsign"/> (a voyage reward, an emissary, dev tooling). Null
        /// when the crew already has that callsign.</summary>
        public static PilotRecord Recruit(PilotVision callsign, FactionType factionType = FactionType.None, string origin = "recruit")
        {
            var pilot = Inv.MintPilot(callsign, factionType, origin, UniqueId.NowMs());
            if (pilot == null) return null;
            AccountInventoryStore.Save();
            PilotChanged?.Invoke(VesselClassType.Any, pilot.Id);
            return pilot;
        }

        // ── The seat (outside a squad) ─────────────────────────────────────

        /// <summary>The pilot id seated on <paramref name="vessel"/>, or empty.</summary>
        public static string GetPilot(VesselClassType vessel) => Inv.HangarFor(vessel)?.PilotId ?? string.Empty;

        /// <summary>Seats a crew pilot on <paramref name="vessel"/>; empty clears the seat. A legacy callsign id seats the
        /// crew's pilot of that callsign.</summary>
        public static void SetPilot(VesselClassType vessel, string pilotId)
        {
            string id = string.IsNullOrEmpty(pilotId) ? string.Empty : Inv.ResolvePilot(pilotId)?.Id ?? string.Empty;
            if (GetPilot(vessel) == id || !Inv.SetHangarPilot(vessel, id)) return;
            AccountInventoryStore.Save();
            PilotChanged?.Invoke(vessel, id);
        }

        // ── The hoard ──────────────────────────────────────────────────────

        /// <summary>Every artifact in the hoard, in the order they arrived, into <paramref name="into"/> (cleared first).</summary>
        public static void GetHoard(List<ArtifactRecord> into)
        {
            into.Clear();
            foreach (var a in Inv.Artifacts)
                if (a != null) into.Add(a);
        }

        /// <summary>The held artifact <paramref name="id"/> names (a unique id held, or a legacy kind id).</summary>
        public static bool TryGetArtifact(string id, out ArtifactRecord artifact)
        {
            artifact = string.IsNullOrEmpty(id) ? null : Inv.ResolveArtifact(id);
            return artifact != null;
        }

        /// <summary>True when the hoard holds this artifact (for a legacy kind id: any artifact of that kind).</summary>
        public static bool Holds(string artifactId) => TryGetArtifact(artifactId, out _);

        /// <summary>
        /// Puts an artifact in the hoard: a voyage find stowed home, a wreck's salvage. A unique id already held changes
        /// nothing (the squad's own artifact coming home); a unique id from elsewhere is re-rolled from its seed (its
        /// crystals fed stay with whoever lost it until the backend moves records between accounts); a bare kind id mints
        /// a brand-new artifact of that kind. Returns the held artifact, or null for an unknown kind.
        /// </summary>
        public static ArtifactRecord Grant(string artifactId, string origin = "voyage")
        {
            if (string.IsNullOrEmpty(artifactId)) return null;
            var held = Inv.Artifact(artifactId);
            if (held != null) return held;
            var catalog = PilotArtifactCatalogSO.Load();
            if (!catalog || !catalog.TryGetArtifact(artifactId, out var kindSO)) return null;
            var kind = kindSO.ToArchetype();
            long now = UniqueId.NowMs();
            ArtifactRecord granted;
            if (UniqueId.IsUnique(artifactId))
            {
                granted = ArtifactGenerator.FromId(artifactId, kind, origin, now);
                if (granted == null || Inv.TryAddArtifact(granted) != AccountInventory.AddResult.Added) return null;
            }
            else granted = Inv.MintArtifact(kind, origin, now);
            if (granted == null) return null;
            AccountInventoryStore.Save();
            ArtifactsChanged?.Invoke(VesselClassType.Any);
            return granted;
        }

        /// <summary>Takes an artifact out of the hoard (lost with a fallen Ark; dev tooling).</summary>
        public static void Forget(string artifactId)
        {
            var held = Inv.ResolveArtifact(artifactId);
            if (held == null || !Inv.RemoveArtifact(held.Id)) return;
            AccountInventoryStore.Save();
            ArtifactsChanged?.Invoke(VesselClassType.Any);
        }

        // ── The slot (outside a squad) ─────────────────────────────────────

        /// <summary>The vessel's slotted artifact ids into <paramref name="into"/> (cleared first): one slot per vessel.</summary>
        public static void GetSlotted(VesselClassType vessel, List<string> into)
        {
            into.Clear();
            string id = GetSlottedArtifact(vessel);
            if (id.Length > 0) into.Add(id);
        }

        /// <summary>The vessel's slotted artifact id, or empty.</summary>
        public static string GetSlottedArtifact(VesselClassType vessel) => Inv.HangarFor(vessel)?.ArtifactId ?? string.Empty;

        /// <summary>The vessel's one slot set to <paramref name="artifactId"/> (empty, or one not held, clears it). An
        /// artifact rides one vessel at a time, so it leaves any other vessel's slot.</summary>
        public static void SetSlottedArtifact(VesselClassType vessel, string artifactId)
        {
            if (vessel == VesselClassType.Any) return;
            string id = TryGetArtifact(artifactId, out var held) ? held.Id : string.Empty;
            if (GetSlottedArtifact(vessel) == id || !Inv.SetHangarArtifact(vessel, id)) return;
            AccountInventoryStore.Save();
            ArtifactsChanged?.Invoke(vessel);
        }

        /// <summary>Slots the artifact if it is held (replacing the one there), or takes it out if it is slotted. True when
        /// it ends up slotted.</summary>
        public static bool ToggleSlot(VesselClassType vessel, string artifactId, int slotCount)
        {
            var slotted = new List<string>();
            GetSlotted(vessel, slotted);
            if (slotCount == 1 && slotted.Count == 1 && slotted[0] != artifactId && Holds(artifactId)) slotted.Clear();
            bool now = Toggle(slotted, artifactId, slotCount, Holds(artifactId));
            SetSlottedArtifact(vessel, slotted.Count > 0 ? slotted[0] : string.Empty);
            return now;
        }

        /// <summary>The slot rule, pure: a slotted id comes out; an unslotted one goes in when it may and a slot is free.
        /// True when the id ends up slotted.</summary>
        public static bool Toggle(List<string> slotted, string id, int slotCount, bool canSlot)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (slotted.Remove(id)) return false;
            if (!canSlot || slotted.Count >= slotCount) return false;
            slotted.Add(id);
            return true;
        }

        // ── Levels and feeding (Docs/Pilots/INVENTORY.md §2.3) ─────────────

        /// <summary>
        /// The four track levels of the artifact <paramref name="artifactId"/> names: a held one's own; a unique id the
        /// player does not hold (a friend's, a peer's) as rolled from its seed, with nothing fed; anything else all zero.
        /// </summary>
        public static ArtifactLevels Levels(string artifactId)
        {
            if (TryGetArtifact(artifactId, out var held)) return held.Levels();
            var catalog = PilotArtifactCatalogSO.Load();
            if (catalog && catalog.TryGetArtifact(artifactId, out var kind)
                && ArtifactGenerator.FromId(artifactId, kind.ToArchetype()) is { } rolled)
                return rolled.Levels();
            return default;
        }

        /// <summary>The player's crystals of <paramref name="element"/> on their profile (Mass, Space, Time and Charge
        /// are the elemental balances, Omni the omni-crystal balance). 0 before the profile is loaded.</summary>
        public static int Crystals(Element element)
        {
            var data = CosmicShore.UI.PlayerDataService.Instance;
            if (!data) return 0;
            return element == Element.Omni ? data.GetCrystalBalance() : data.GetElementalCrystalsHeld(element);
        }

        /// <summary>Credit crystals to the profile (dev tooling; a match pays them out by the owner's rule).</summary>
        public static void AddCrystals(Element element, int amount, string source)
        {
            var data = CosmicShore.UI.PlayerDataService.Instance;
            if (!data || amount <= 0) return;
            if (element == Element.Omni) data.AddCrystals(amount, source);
            else data.AddElementalCrystals(element, amount, source);
            UpgradesChanged?.Invoke();
        }

        /// <summary>Empties what an artifact was fed, back to the level it was rolled at (dev tooling; no refund).</summary>
        public static void ResetFed(string artifactId)
        {
            if (!TryGetArtifact(artifactId, out var held) || held.Invested == 0) return;
            held.Invested = 0;
            AccountInventoryStore.Save();
            UpgradesChanged?.Invoke();
        }

        /// <summary>Why a feed was refused, or <see cref="Fed"/>.</summary>
        public enum FeedOutcome
        {
            Fed = 0,
            NotEnoughCrystals = 1,
            NoSuchArtifact = 2,
            WrongElement = 3,
            NothingToFeed = 4,
        }

        /// <summary>
        /// The feed rule, pure: <paramref name="artifact"/> takes <paramref name="crystals"/> of <paramref name="payer"/>
        /// when it accepts that crystal (its own element, or Omni) and the balance covers it.
        /// </summary>
        public static FeedOutcome Check(ArtifactRecord artifact, Element payer, long crystals, int balance)
        {
            if (artifact == null) return FeedOutcome.NoSuchArtifact;
            if (!artifact.Accepts(payer)) return FeedOutcome.WrongElement;
            if (crystals <= 0) return FeedOutcome.NothingToFeed;
            return balance >= crystals ? FeedOutcome.Fed : FeedOutcome.NotEnoughCrystals;
        }

        /// <summary>
        /// Spends <paramref name="crystals"/> of the artifact's own element (or of Omni when <paramref name="payWithOmni"/>)
        /// and feeds them to it. There is no limit; each crystal adds a little less than the one before
        /// (<see cref="ArtifactCurve"/>). <paramref name="result"/> carries the level before and after.
        /// </summary>
        public static FeedOutcome TryFeed(string artifactId, int crystals, bool payWithOmni, out AccountInventory.FeedResult result)
        {
            result = default;
            if (!TryGetArtifact(artifactId, out var artifact)) return FeedOutcome.NoSuchArtifact;
            var payer = payWithOmni ? Element.Omni : artifact.Element;
            var outcome = Check(artifact, payer, crystals, Crystals(payer));
            if (outcome != FeedOutcome.Fed) return outcome;
            if (!Spend(payer, crystals)) return FeedOutcome.NotEnoughCrystals;
            result = Inv.Feed(artifact.Id, payer, crystals);
            AccountInventoryStore.Save();
            UpgradesChanged?.Invoke();
            return FeedOutcome.Fed;
        }

        static bool Spend(Element element, int amount)
        {
            var data = CosmicShore.UI.PlayerDataService.Instance;
            if (!data) return false;
            const string source = "artifact feed";
            return element == Element.Omni ? data.TrySpendCrystals(amount, source) : data.TrySpendElementalCrystals(element, amount, source);
        }
    }
}
