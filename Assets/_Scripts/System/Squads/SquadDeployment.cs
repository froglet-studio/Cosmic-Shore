using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The squad the Ark has DEPLOYED (Docs/Voyages/SLICE.md §2): which saved squad is out, and which of its three seats
    /// each vessel here is flying. While a squad is deployed, a seated vessel flies its SLOT's cards, not the Hangar's
    /// per-hull choice: its pilot (and so its sight on LB) and its artifact (<see cref="VesselPilotLoadout"/> reads
    /// <see cref="TryGetSeat"/>), and its element (<see cref="SeedElement"/>). The vessel itself is the slot's because
    /// the seat is spawned (or swapped) on that hull.
    ///
    /// <para><b>Runtime only, never saved.</b> The deployment lasts while the voyage runs; recalling it puts every
    /// vessel back on its Hangar choice. The local human holds <see cref="MySeat"/>; an AI wingmate is seated by the
    /// machine that spawned it (the host), which owns it and publishes its pilot and artifact to every peer.</para>
    /// </summary>
    public static class SquadDeployment
    {
        static SavedSquad _squad;
        static int _mySeat;
        static int _version;
        static readonly Dictionary<IVesselStatus, int> s_seats = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _squad = null;
            _mySeat = 0;
            _version = 0;
            s_seats.Clear();
            Changed = null;
        }

        /// <summary>Raised when a squad is deployed or recalled, or a seat changes.</summary>
        public static event Action Changed;

        /// <summary>The deployed squad (a copy), or null.</summary>
        public static SavedSquad Squad => _squad;

        public static bool IsDeployed => _squad != null;

        /// <summary>The seat the local human flies.</summary>
        public static int MySeat => _mySeat;

        /// <summary>Bumped on every change, so a per-frame reader re-resolves only when something moved.</summary>
        public static int Version => _version;

        /// <summary>Deploy <paramref name="squad"/> (a copy is kept) with the local human in <paramref name="mySeat"/>.</summary>
        public static void Deploy(SavedSquad squad, int mySeat)
        {
            if (squad == null) return;
            _squad = SquadMembership.Copy(squad);
            _mySeat = Mathf.Clamp(mySeat, 0, SavedSquad.SlotCount - 1);
            s_seats.Clear();
            Bump();
        }

        /// <summary>The local human now flies <paramref name="seat"/> (taking the helm of another vessel).</summary>
        public static void MoveMe(int seat)
        {
            if (_squad == null) return;
            _mySeat = Mathf.Clamp(seat, 0, SavedSquad.SlotCount - 1);
            Bump();
        }

        /// <summary>Seat a vessel this machine spawned (an AI wingmate) in slot <paramref name="seat"/>.</summary>
        public static void Seat(IVesselStatus vessel, int seat)
        {
            if (vessel == null || _squad == null) return;
            s_seats[vessel] = Mathf.Clamp(seat, 0, SavedSquad.SlotCount - 1);
            Bump();
        }

        /// <summary>Take a vessel out of its seat (despawned, or handed back).</summary>
        public static void Unseat(IVesselStatus vessel)
        {
            if (vessel != null && s_seats.Remove(vessel)) Bump();
        }

        /// <summary>Recall the squad: every vessel flies its Hangar choice again.</summary>
        public static void Recall()
        {
            if (_squad == null && s_seats.Count == 0) return;
            _squad = null;
            s_seats.Clear();
            Bump();
        }

        /// <summary>
        /// The slot <paramref name="vessel"/> flies, as a question: false when no squad is deployed or the vessel is in
        /// no seat (an arena bot, another squad's vessel), which is the ordinary case. The local human is in
        /// <see cref="MySeat"/>; any other vessel only if this machine seated it.
        /// </summary>
        public static bool TryGetSeat(IVesselStatus vessel, out SquadSlot slot, out int seat)
        {
            slot = default;
            seat = -1;
            if (_squad == null || vessel == null) return false;
            if (s_seats.TryGetValue(vessel, out seat))
            {
                slot = _squad.GetSlot(seat);
                return true;
            }
            var player = vessel.Player;
            if (player != null && player.IsLocalPilot && !vessel.IsInitializedAsAI)
            {
                seat = _mySeat;
                slot = _squad.GetSlot(seat);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Start a seated vessel levelled in its slot's element (the squad card's element: "the primary element you
        /// start levelled in", Docs/PILOTS_AND_ARTIFACTS.md §1). Raises the element to <paramref name="level"/>
        /// (normalized, 0.1 a petal) and never lowers it. Elements are simulated on the vessel's owning machine, so the
        /// owner calls this.
        /// </summary>
        public static void SeedElement(IVesselStatus vessel, float level)
        {
            if (!TryGetSeat(vessel, out var slot, out _)) return;
            var resources = vessel.ResourceSystem;
            if (resources == null || slot.Element is Element.None or Element.Omni) return;
            if (resources.GetNormalizedLevel(slot.Element) >= level) return;
            resources.SetElementLevel(slot.Element, level);
        }

        static void Bump()
        {
            _version++;
            Changed?.Invoke();
        }
    }
}
