using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Holds the account's live <see cref="AccountInventory"/> and keeps it saved (Docs/Pilots/INVENTORY.md §4). Every
    /// read and write of pilots and artifacts reaches it through <see cref="PilotLoadoutStore"/>.
    ///
    /// <para><b>Where the inventory lives.</b> Once <see cref="UGSDataService"/> has loaded the account, the inventory is
    /// the <c>ACCOUNT_INVENTORY</c> record (<see cref="InventoryRepository"/>): saved to the cloud with the same debounce,
    /// offline snapshot and failed-load protection as every other record. Before that (an editor scene opened straight
    /// into play, a test, no sign-in) it is a stand-in read from this machine's last snapshot of the same key and written
    /// back to it; when the cloud record arrives, the cloud wins, as with every other record.</para>
    ///
    /// <para><b>Preparing an inventory</b> happens once per record, the first time it is read:</para>
    /// <list type="number">
    /// <item><b>Starter crew and hoard</b> (once per account): one pilot per callsign and one artifact of every kind that
    /// is not found on voyages, so nothing playable before unique pilots and artifacts is lost (§6, open question 1).</item>
    /// <item><b>Device import</b> (once per device and account): the PlayerPrefs this device kept before the inventory
    /// (Hangar seats, slots, voyage finds and old track levels, whose spent crystals become crystals fed).</item>
    /// <item><b>Squad adoption</b>: saved squads that name bare callsign or kind ids point at the crew's pilots and the
    /// hoard's artifacts (<see cref="AccountInventory.AdoptSquads"/>).</item>
    /// </list>
    /// </summary>
    public static class AccountInventoryStore
    {
        /// <summary>The inventory changed (any pilot, artifact, captain or Hangar pick).</summary>
        public static event Action Changed;

        static AccountInventory _fallback;
        static AccountInventory _prepared;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Changed = null;
            _fallback = null;
            _prepared = null;
            // A voyage's job board reads the player's real crew: a seated pilot brings their own faction's work.
            VoyageContent.CrewSource = CrewPersonFor;
        }

        /// <summary>The voyage's view of a crew pilot (<see cref="VoyageContent.CrewSource"/>), or null when the crew has
        /// no pilot for that id.</summary>
        public static CrewPerson CrewPersonFor(string pilotId)
        {
            var p = string.IsNullOrEmpty(pilotId) ? null : Current.ResolvePilot(pilotId);
            return p == null ? null : new CrewPerson { PilotId = p.Id, Name = p.DisplayName(), Faction = p.FactionType, Story = p.Backstory };
        }

        static InventoryRepository Repo
        {
            get
            {
                var data = UGSDataService.Instance;
                var repo = data ? data.InventoryRepo : null;
                return repo != null && repo.IsLoaded ? repo : null;
            }
        }

        /// <summary>The account's inventory, prepared (starter, device import, squads). Never null.</summary>
        public static AccountInventory Current
        {
            get
            {
                var repo = Repo;
                AccountInventory inventory;
                if (repo != null)
                {
                    repo.Data.Inventory ??= new AccountInventory();
                    inventory = repo.Data.Inventory;
                }
                else
                {
                    if (_fallback == null)
                    {
                        _fallback = LocalCloudDataCache.TryLoad<InventoryCloudData>(UGSKeys.Inventory)?.Inventory ?? new AccountInventory();
                        _fallback.Normalize();
                    }
                    inventory = _fallback;
                }

                if (!ReferenceEquals(inventory, _prepared))
                {
                    _prepared = inventory;
                    Prepare(inventory);
                }
                return inventory;
            }
        }

        /// <summary>Saves the inventory (debounced to the cloud, or to this machine's snapshot) and raises
        /// <see cref="Changed"/>. Call after every mutation.</summary>
        public static void Save()
        {
            var repo = Repo;
            if (repo != null) repo.MarkDirty();
            else if (_fallback != null) LocalCloudDataCache.Save(UGSKeys.Inventory, new InventoryCloudData { Inventory = _fallback });
            Changed?.Invoke();
        }

        // ── Preparing ───────────────────────────────────────────────────────

        static void Prepare(AccountInventory inventory)
        {
            bool changed = false;
            var catalog = PilotArtifactCatalogSO.Load();
            long now = UniqueId.NowMs();

            if (!inventory.StarterGranted)
            {
                GrantStarter(inventory, catalog, now);
                inventory.StarterGranted = true;
                changed = true;
            }

            // The starter crew are the painted pilots (PaintedCrewSO): repainted on every read, so a crew saved
            // before 2026-11-12 (rolled people under the painted pictures) and an edited story both catch up.
            var painted = PaintedCrewSO.Load();
            if (painted && inventory.Pilots != null)
                foreach (var pilot in inventory.Pilots)
                    if (PaintedCrewSO.IsPaintable(pilot) && painted.Paint(pilot))
                        changed = true;

            if (!inventory.DeviceImportDone)
            {
                ImportDevice(inventory, catalog, now);
                inventory.DeviceImportDone = true;
                changed = true;
            }

            var squads = UGSDataService.Instance ? UGSDataService.Instance.SquadRepo : null;
            if (squads != null && squads.IsLoaded && squads.Data?.Book != null && inventory.AdoptSquads(squads.Data.Book))
                squads.MarkDirty();

            if (changed)
            {
                // Saved without raising Changed: preparing happens inside a read, and a listener re-reading here would
                // re-enter it.
                var repo = Repo;
                if (repo != null && ReferenceEquals(repo.Data.Inventory, inventory)) repo.MarkDirty();
                else LocalCloudDataCache.Save(UGSKeys.Inventory, new InventoryCloudData { Inventory = inventory });
            }
        }

        /// <summary>One pilot per callsign and one artifact of every free kind: everything the game offered before
        /// pilots and artifacts were unique stays playable.</summary>
        public static void GrantStarter(AccountInventory inventory, PilotArtifactCatalogSO catalog, long now)
        {
            foreach (var vision in Callsigns.All)
                if (!inventory.HasCallsign(vision))
                    inventory.MintPilot(vision, FactionType.None, PaintedCrewSO.StarterOrigin, now);

            if (!catalog) return;
            foreach (var kind in catalog.Artifacts)
                if (kind && !kind.FoundOnVoyages && inventory.CountOfKind(kind.Id) == 0)
                    inventory.MintArtifact(kind.ToArchetype(), "starter", now);
        }

        // The keys PilotLoadoutStore wrote before the inventory existed (2026-10-27..11-07). Read once, never written.
        static string LegacySeatKey(VesselClassType vessel) => $"pilots.seat.{(int)vessel}";
        static string LegacySlotsKey(VesselClassType vessel) => $"artifacts.slots.{(int)vessel}";
        static string LegacyFoundKey(string kindId) => $"artifacts.found.{kindId}";
        static string LegacyTrackKey(string kindId, Element element) => $"artifacts.track.{kindId}.{(int)element}";

        static void ImportDevice(AccountInventory inventory, PilotArtifactCatalogSO catalog, long now)
        {
            if (catalog)
                foreach (var kind in catalog.Artifacts)
                {
                    if (!kind) continue;
                    // A voyage find this device recorded: the hoard gets one of that kind.
                    if (kind.FoundOnVoyages && PlayerPrefs.GetInt(LegacyFoundKey(kind.Id), 0) > 0 && inventory.CountOfKind(kind.Id) == 0)
                        inventory.MintArtifact(kind.ToArchetype(), "legacy", now);

                    // Crystals spent on the old four tracks are crystals fed to the kind's first artifact: crystals are
                    // crystals, and the player keeps what they paid for.
                    long spent = 0;
                    foreach (var e in ArtifactLevels.TrackElements)
                    {
                        int level = Mathf.Clamp(PlayerPrefs.GetInt(LegacyTrackKey(kind.Id, e), 0), 0, ArtifactTrack.MaxLevel);
                        var track = kind.Track(e);
                        if (track == null) continue;
                        for (int l = 0; l < level; l++) spent += track.CostFrom(l);
                    }
                    if (spent > 0 && inventory.FirstOfKind(kind.Id) is { } first) first.Invested = ArtifactRecord.SafeAdd(first.Invested, spent);
                }

            foreach (VesselClassType vessel in Enum.GetValues(typeof(VesselClassType)))
            {
                if (vessel == VesselClassType.Any || vessel == VesselClassType.Random) continue;
                string seat = PlayerPrefs.GetString(LegacySeatKey(vessel), string.Empty);
                if (seat.Length > 0 && inventory.ResolvePilot(seat) is { } pilot) inventory.SetHangarPilot(vessel, pilot.Id);

                string slots = PlayerPrefs.GetString(LegacySlotsKey(vessel), string.Empty);
                int cut = slots.IndexOf(',');
                string slotted = cut < 0 ? slots : slots.Substring(0, cut);
                if (slotted.Length > 0 && inventory.ResolveArtifact(slotted, TakenByHangar(inventory)) is { } artifact)
                    inventory.SetHangarArtifact(vessel, artifact.Id);
            }
        }

        static HashSet<string> TakenByHangar(AccountInventory inventory)
        {
            var taken = new HashSet<string>();
            foreach (var h in inventory.Hangar)
                if (h != null && !string.IsNullOrEmpty(h.ArtifactId)) taken.Add(h.ArtifactId);
            return taken;
        }
    }
}
