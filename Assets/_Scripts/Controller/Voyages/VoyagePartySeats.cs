using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The seats a party's friends picked in the host's voyage lobby, as each friend's machine reports them
    /// (<see cref="VoyageCourseLink.Report"/>, Docs/Voyages/PARTY.md §3). A friend's hull already reaches the host through
    /// their player's replicated hull; their pilot, element and artifact live on their own machine and reach the host only
    /// this way, so the host's Ark page can show them. A friend publishes on every change and again every few seconds (a
    /// host that opened its page late still hears it); the host keeps the last one per client.
    /// </summary>
    public static class VoyagePartySeats
    {
        /// <summary>A friend's seat as the host last heard it.</summary>
        public sealed class FriendSeat
        {
            public ulong ClientId;
            public string PlayerName;
            public SquadSlot Slot;
            /// <summary>The pilot's name as the friend's crew knows them (a person's own name, else the callsign's).</summary>
            public string PilotName;
            public float HeardAt;
        }

        static readonly Dictionary<ulong, FriendSeat> s_seats = new();
        static int s_version;
        static string s_lastSent;
        static float s_nextResend;
        const float ResendSeconds = 3f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_seats.Clear();
            s_version = 0;
            s_lastSent = null;
            s_nextResend = 0f;
        }

        /// <summary>Changes whenever a friend's seat changes (a page redraws on it).</summary>
        public static int Version => s_version;

        /// <summary>The seat <paramref name="clientId"/> last reported while connected now, or null.</summary>
        public static FriendSeat Of(ulong clientId)
        {
            if (!s_seats.TryGetValue(clientId, out var seat)) return null;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && nm.IsListening && nm.IsServer)
                foreach (var id in nm.ConnectedClientsIds)
                    if (id == clientId) return seat;
            // Gone from the session (a client id can come back as someone else): forget it.
            s_seats.Remove(clientId);
            s_version++;
            return null;
        }

        /// <summary>The host heard a friend's seat.</summary>
        internal static void Store(ulong clientId, VoyagePartyReport report)
        {
            if (report == null) return;
            var slot = new SquadSlot((VesselClassType)report.SlotVessel, (Element)report.SlotElement,
                string.IsNullOrEmpty(report.PilotId) ? null : report.PilotId,
                string.IsNullOrEmpty(report.ArtifactId) ? null : report.ArtifactId);
            if (s_seats.TryGetValue(clientId, out var known) && known.Slot.Vessel == slot.Vessel && known.Slot.Element == slot.Element &&
                known.Slot.PilotId == slot.PilotId && known.Slot.ArtifactId == slot.ArtifactId && known.PilotName == report.PilotName)
            {
                known.HeardAt = Time.unscaledTime;
                return;
            }
            s_seats[clientId] = new FriendSeat
            {
                ClientId = clientId,
                PlayerName = report.PlayerName,
                Slot = slot,
                PilotName = report.PilotName,
                HeardAt = Time.unscaledTime,
            };
            s_version++;
        }

        /// <summary>
        /// A friend's machine tells the host the seat it picked: at once when it changed, else every few seconds. Does
        /// nothing off a friend's machine. <paramref name="pilotName"/> is the pilot as this crew names them.
        /// </summary>
        public static void Publish(SquadSlot slot, string playerName, string pilotName, bool force = false)
        {
            if (!VoyageCourseLink.IsFollower) return;
            VoyageCourseLink.EnsureListening();
            var report = new VoyagePartyReport
            {
                Kind = VoyagePartyReport.ReportKind.Seat,
                PlayerName = playerName ?? string.Empty,
                Vessel = (int)slot.Vessel,
                SlotVessel = (int)slot.Vessel,
                SlotElement = (int)slot.Element,
                PilotId = slot.PilotId ?? string.Empty,
                PilotName = pilotName ?? string.Empty,
                ArtifactId = slot.ArtifactId ?? string.Empty,
            };
            string sig = JsonUtility.ToJson(report);
            if (!force && sig == s_lastSent && Time.unscaledTime < s_nextResend) return;
            s_lastSent = sig;
            s_nextResend = Time.unscaledTime + ResendSeconds;
            VoyageCourseLink.Report(report);
        }
    }
}

namespace CosmicShore.Gameplay
{
    /// <summary>The party's humans as this machine sees them (Docs/Voyages/PARTY.md): who else is flying the voyage.</summary>
    public static class VoyageParty
    {
        /// <summary>The seats the host sends with its course: itself in <paramref name="mySeat"/>, each connected friend in
        /// the next seat (<see cref="VoyageRules.SeatClients"/>), -1 for an AI seat. Solo: just the local seat.</summary>
        public static System.Collections.Generic.List<long> SeatClients(int mySeat)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var friends = new System.Collections.Generic.List<long>();
            long me = 0;
            if (nm != null && nm.IsListening && nm.IsServer)
            {
                me = (long)nm.LocalClientId;
                foreach (var id in nm.ConnectedClientsIds)
                    if (id != nm.LocalClientId) friends.Add((long)id);
            }
            return VoyageRules.SeatClients(mySeat, me, friends);
        }

        /// <summary>
        /// Every other human's vessel in this session (not this machine's pilot, not an AI), with the client id that flies
        /// it, into <paramref name="into"/>.
        /// </summary>
        public static void OtherHumans(GameDataSO gameData, System.Collections.Generic.List<(IVesselStatus status, long client)> into)
        {
            into.Clear();
            var players = gameData ? gameData.Players : null;
            if (players == null) return;
            foreach (var p in players)
            {
                if (p == null || p is Object uo && !uo) continue;
                if (p.IsLocalUser || p.IsInitializedAsAI) continue;
                var vessel = p.Vessel;
                if (vessel == null || vessel is Object vo && !vo) continue;
                var status = vessel.VesselStatus;
                if (status == null || !status.Transform) continue;
                into.Add((status, (long)p.OwnerClientNetId));
            }
        }
    }
}
