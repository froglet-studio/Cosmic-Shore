using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The player's pilots and artifacts (Docs/Pilots/CALLSIGNS.md, Docs/Pilots/ARTIFACTS.md): which pilot sits in each
    /// vessel's seat, which artifacts the player holds, which one is slotted on each vessel, the player's four track
    /// levels on each artifact, and the elemental crystals they spend to raise them. Ids are the <c>PilotSO.Id</c> /
    /// <c>ArtifactSO.Id</c> strings, which players save and nobody renames.
    ///
    /// <para><b>Crystals are the profile's.</b> The four elemental balances and the Omni balance are the player's
    /// cloud profile (<see cref="CosmicShore.UI.PlayerDataService"/>, Shombith's section 97), so a raise spends the same
    /// crystals the Hangar and the Converter show. <b>Track levels are a device-local stand-in</b> (PlayerPrefs): they
    /// belong on the profile beside the Hangar's levels, and that is the backend, so it goes through Shombith
    /// (Docs/Pilots/ARTIFACTS.md §10). Everything that reads or raises goes through this class, so the move is one
    /// class.</para>
    ///
    /// <para><b>2026-10-27 (Garrett):</b> built on Shombith's store; the found RANKS of the numeric artifacts are gone
    /// with them. Every artifact is still free to hold (Shombith, 2026-10-26).</para>
    /// </summary>
    public static class PilotLoadoutStore
    {
        /// <summary>A seat changed: the vessel and the pilot id now in it (empty = no pilot, so no sight).</summary>
        public static event Action<VesselClassType, string> PilotChanged;
        /// <summary>A vessel's slotted artifact changed (Any: what the player holds changed).</summary>
        public static event Action<VesselClassType> ArtifactsChanged;
        /// <summary>A track level or a crystal balance changed.</summary>
        public static event Action UpgradesChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            PilotChanged = null;
            ArtifactsChanged = null;
            UpgradesChanged = null;
        }

        const char Separator = ',';
        static string SeatKey(VesselClassType vessel) => $"pilots.seat.{(int)vessel}";
        static string SlotsKey(VesselClassType vessel) => $"artifacts.slots.{(int)vessel}";
        static string HeldKey(string artifactId) => $"artifacts.found.{artifactId}";
        static string TrackKey(string artifactId, Element element) => $"artifacts.track.{artifactId}.{(int)element}";

        // ---- the seat ----

        public static string GetPilot(VesselClassType vessel) => PlayerPrefs.GetString(SeatKey(vessel), string.Empty);

        public static void SetPilot(VesselClassType vessel, string pilotId)
        {
            pilotId ??= string.Empty;
            if (pilotId.Length == 0) PlayerPrefs.DeleteKey(SeatKey(vessel));
            else PlayerPrefs.SetString(SeatKey(vessel), pilotId);
            PlayerPrefs.Save();
            PilotChanged?.Invoke(vessel, pilotId);
        }

        // ---- held artifacts ----

        /// <summary>Every artifact is free (Shombith, 2026-10-26: "all of these can be unlocked for free not buy it").</summary>
        public const bool EverythingIsFree = true;

        /// <summary>True when the player holds this artifact. Everything is free, so every artifact is held unless a
        /// record says otherwise (a find written by <see cref="Grant"/>, a loss by <see cref="Forget"/>).</summary>
        public static bool Holds(string artifactId) =>
            !string.IsNullOrEmpty(artifactId) && (EverythingIsFree || PlayerPrefs.GetInt(HeldKey(artifactId), 0) > 0);

        /// <summary>Records a find (an artifact stowed home from a voyage).</summary>
        public static void Grant(string artifactId)
        {
            if (string.IsNullOrEmpty(artifactId)) return;
            PlayerPrefs.SetInt(HeldKey(artifactId), 1);
            PlayerPrefs.Save();
            ArtifactsChanged?.Invoke(VesselClassType.Any);
        }

        /// <summary>Forgets a find (an artifact lost with a fallen Ark; dev tooling).</summary>
        public static void Forget(string artifactId)
        {
            if (string.IsNullOrEmpty(artifactId)) return;
            PlayerPrefs.DeleteKey(HeldKey(artifactId));
            PlayerPrefs.Save();
            ArtifactsChanged?.Invoke(VesselClassType.Any);
        }

        // ---- the slot ----

        /// <summary>The vessel's slotted artifact ids, in slot order, into <paramref name="into"/> (cleared first).</summary>
        public static void GetSlotted(VesselClassType vessel, List<string> into)
        {
            into.Clear();
            Split(PlayerPrefs.GetString(SlotsKey(vessel), string.Empty), into);
        }

        /// <summary>The vessel's first slotted artifact id, or empty.</summary>
        public static string GetSlottedArtifact(VesselClassType vessel)
        {
            var saved = PlayerPrefs.GetString(SlotsKey(vessel), string.Empty);
            int cut = saved.IndexOf(Separator);
            return cut < 0 ? saved : saved.Substring(0, cut);
        }

        /// <summary>Slots the artifact if there is room and it is held, or takes it out if it is slotted. True when it
        /// ends up slotted. With one slot, slotting another artifact replaces the one there.</summary>
        public static bool ToggleSlot(VesselClassType vessel, string artifactId, int slotCount)
        {
            var slotted = new List<string>();
            GetSlotted(vessel, slotted);
            if (slotCount == 1 && slotted.Count == 1 && slotted[0] != artifactId && Holds(artifactId)) slotted.Clear();
            bool now = Toggle(slotted, artifactId, slotCount, Holds(artifactId));
            PlayerPrefs.SetString(SlotsKey(vessel), string.Join(Separator.ToString(), slotted));
            PlayerPrefs.Save();
            ArtifactsChanged?.Invoke(vessel);
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

        // ---- upgrades: track levels and crystals (Docs/Pilots/ARTIFACTS.md §4) ----

        /// <summary>The player's level on <paramref name="artifactId"/>'s <paramref name="element"/> track, 0 to 10.</summary>
        public static int TrackLevel(string artifactId, Element element) =>
            string.IsNullOrEmpty(artifactId) ? 0
                : Mathf.Clamp(PlayerPrefs.GetInt(TrackKey(artifactId, element), 0), 0, ArtifactTrack.MaxLevel);

        /// <summary>The player's four levels on an artifact.</summary>
        public static ArtifactLevels Levels(string artifactId) => new(
            TrackLevel(artifactId, Element.Mass), TrackLevel(artifactId, Element.Space),
            TrackLevel(artifactId, Element.Time), TrackLevel(artifactId, Element.Charge));

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

        /// <summary>Sets an artifact's four track levels back to 0 (dev tooling).</summary>
        public static void ResetTracks(string artifactId)
        {
            if (string.IsNullOrEmpty(artifactId)) return;
            foreach (var e in new[] { Element.Mass, Element.Space, Element.Time, Element.Charge })
                PlayerPrefs.DeleteKey(TrackKey(artifactId, e));
            PlayerPrefs.Save();
            UpgradesChanged?.Invoke();
        }

        static bool Spend(Element element, int amount)
        {
            var data = CosmicShore.UI.PlayerDataService.Instance;
            if (!data) return false;
            const string source = "artifact track";
            return element == Element.Omni ? data.TrySpendCrystals(amount, source) : data.TrySpendElementalCrystals(element, amount, source);
        }

        /// <summary>Why a raise was refused, or <see cref="Raised"/>.</summary>
        public enum RaiseResult
        {
            Raised = 0,
            AtTop = 1,
            NotEnoughCrystals = 2,
            NoSuchTrack = 3,
        }

        /// <summary>
        /// The spend rule, pure: raising a track from <paramref name="level"/> costs its next level's price, paid
        /// entirely in the track's own crystals, or entirely in Omni when the player picks it (§4: Omni raises any one
        /// track, the player's pick). <paramref name="balance"/> is the balance of whichever crystal pays.
        /// </summary>
        public static RaiseResult Price(ArtifactTrack track, int level, int balance, out int cost)
        {
            cost = 0;
            if (track == null) return RaiseResult.NoSuchTrack;
            cost = track.CostFrom(level);
            if (cost <= 0) return RaiseResult.AtTop;
            return balance >= cost ? RaiseResult.Raised : RaiseResult.NotEnoughCrystals;
        }

        /// <summary>Spend crystals to raise <paramref name="artifact"/>'s <paramref name="element"/> track one level,
        /// paying in that element's crystals, or in Omni when <paramref name="payWithOmni"/>.</summary>
        public static RaiseResult TryRaise(ArtifactSO artifact, Element element, bool payWithOmni = false)
        {
            if (!artifact) return RaiseResult.NoSuchTrack;
            var payer = payWithOmni ? Element.Omni : element;
            int level = TrackLevel(artifact.Id, element);
            var result = Price(artifact.Track(element), level, Crystals(payer), out int cost);
            if (result != RaiseResult.Raised) return result;
            if (!Spend(payer, cost)) return RaiseResult.NotEnoughCrystals;
            PlayerPrefs.SetInt(TrackKey(artifact.Id, element), level + 1);
            PlayerPrefs.Save();
            UpgradesChanged?.Invoke();
            return RaiseResult.Raised;
        }

        static void Split(string saved, List<string> into)
        {
            if (string.IsNullOrEmpty(saved)) return;
            foreach (var part in saved.Split(Separator))
                if (part.Length > 0 && !into.Contains(part)) into.Add(part);
        }
    }
}
