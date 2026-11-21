using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// One squad mate's four pre-match choices: vessel, element, pilot and artifact
    /// (Docs/PILOTS_AND_ARTIFACTS.md §1). Persisted inside <see cref="SavedSquad"/>.
    ///
    /// <para>Pilot and artifact are stored by their authored id, not by asset reference, because the
    /// record is JSON in cloud save. An empty <see cref="PilotId"/> means an empty seat (no sight);
    /// an empty <see cref="ArtifactId"/> means no artifact is carried. Both are resolved, and checked
    /// against the vessel again, when the squad is read, so a stale id falls back rather than
    /// failing.</para>
    ///
    /// <para>Computed answers are methods, not properties, so Newtonsoft does not write them into
    /// the cloud record.</para>
    ///
    /// <para><see cref="Seat"/>, <see cref="MemberId"/> and <see cref="MemberName"/> say who flies the slot (the Ark's
    /// Squads tab): the maker, an AI wingmate, a seat held open, or the friend who joined it. A slot saved before they
    /// existed reads as an AI seat with nobody in it.</para>
    /// </summary>
    [Serializable]
    public struct SquadSlot
    {
        public VesselClassType Vessel;
        public Element Element;
        public string PilotId;
        public string ArtifactId;
        public SquadSeat Seat;
        /// <summary>The UGS player id of whoever holds a <see cref="SquadSeat.Player"/> seat; empty otherwise.</summary>
        public string MemberId;
        /// <summary>Their display name when the seat was last read, for drawing it offline.</summary>
        public string MemberName;

        public SquadSlot(VesselClassType vessel, Element element, string pilotId = null, string artifactId = null)
        {
            Vessel = vessel;
            Element = element;
            PilotId = pilotId;
            ArtifactId = artifactId;
            Seat = SquadSeat.Ai;
            MemberId = null;
            MemberName = null;
        }

        /// <summary>True when a player (not an AI) flies this slot.</summary>
        public bool IsPlayer() => Seat == SquadSeat.Player && !string.IsNullOrEmpty(MemberId);

        /// <summary>True when the slot is held open for a friend to join.</summary>
        public bool IsOpen() => Seat == SquadSeat.Open;

        /// <summary>This slot held by <paramref name="memberId"/>, keeping the four picks.</summary>
        public SquadSlot WithMember(string memberId, string memberName)
        {
            var slot = this;
            slot.Seat = SquadSeat.Player;
            slot.MemberId = memberId;
            slot.MemberName = memberName;
            return slot;
        }

        /// <summary>This slot as an AI or open seat with nobody in it, keeping the four picks.</summary>
        public SquadSlot WithSeat(SquadSeat seat)
        {
            var slot = this;
            slot.Seat = seat;
            if (seat != SquadSeat.Player)
            {
                slot.MemberId = null;
                slot.MemberName = null;
            }
            return slot;
        }

        /// <summary>The four picks of <paramref name="loadout"/> in this seat (who flies it is kept).</summary>
        public SquadSlot WithLoadout(SquadSlot loadout)
        {
            var slot = this;
            slot.Vessel = loadout.Vessel;
            slot.Element = loadout.Element;
            slot.PilotId = loadout.PilotId;
            slot.ArtifactId = loadout.ArtifactId;
            return slot;
        }

        /// <summary>True when no pilot was picked: the seat is empty, so the slot has no sight (Docs/Pilots/CALLSIGNS.md).</summary>
        public bool UsesDefaultPilot() => string.IsNullOrEmpty(PilotId);

        /// <summary>True when an artifact is carried into the match.</summary>
        public bool CarriesArtifact() => !string.IsNullOrEmpty(ArtifactId);

        /// <summary>
        /// This slot on another hull. Pilot and artifact are cleared: a pilot is chosen per vessel,
        /// and the picks made for the old hull say nothing about what the new one can take. Who flies
        /// the slot is kept.
        /// </summary>
        public SquadSlot WithVessel(VesselClassType vessel)
        {
            var slot = this;
            slot.Vessel = vessel;
            slot.PilotId = null;
            slot.ArtifactId = null;
            return slot;
        }
    }
}
