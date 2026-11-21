using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The voyage lobby's lines to the party before anyone sets sail (Docs/Voyages/PARTY.md §7), Netcode named messages
    /// beside <see cref="VoyageCourseLink"/>'s, so no networked object is added:
    /// <list type="bullet">
    /// <item><b>The lobby</b> (host to every friend, reliable): whether the host's Voyage window is open, the voyage it has
    /// open, the page it is on and its difficulty, the Ark's one domain and the friends' votes for it. A friend's menu
    /// follows it into the same voyage, as a friend follows the host into an arcade card (Shombith, 2026-11-21).</item>
    /// <item><b>A domain vote</b> (a friend to the host, reliable): the domain the friend would like the Ark to fly. The
    /// host picks; a vote only shows on the host's Ark page.</item>
    /// </list>
    /// </summary>
    public static class VoyageLobbyLink
    {
        const string LobbyMessage = "cosmicshore.voyage.lobby";
        const string VoteMessage = "cosmicshore.voyage.domainvote";

        /// <summary>No vote yet (no <see cref="Domains"/> member is 0).</summary>
        public const Domains NoVote = 0;

        static CustomMessagingManager s_registeredOn;
        static VoyageLobbyState s_latest;
        static readonly Dictionary<ulong, Domains> s_votes = new();
        static int s_votesVersion;
        static Domains s_myVote = NoVote;
        static Domains s_arkDomain = NoVote;
        const string ArkDomainPref = "Voyages.ArkDomain";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_registeredOn = null;
            s_latest = null;
            s_votes.Clear();
            s_votesVersion = 0;
            s_myVote = NoVote;
            s_arkDomain = NoVote;
        }

        /// <summary>The host's lobby as this friend last heard it (null on the host, solo, or before the first one).</summary>
        public static VoyageLobbyState Latest => s_latest;

        /// <summary>The domain this friend last voted for (<see cref="NoVote"/>: none yet).</summary>
        public static Domains MyVote => s_myVote;

        /// <summary>Changes whenever a friend's vote reaches the host (its Ark page redraws on it).</summary>
        public static int VotesVersion => s_votesVersion;

        /// <summary>
        /// Register the handlers on the live session if they are not (call every so often: a session that stops and starts
        /// again has a new messaging manager). A new session forgets the last lobby and every vote.
        /// </summary>
        public static void EnsureListening()
        {
            var nm = NetworkManager.Singleton;
            var messaging = nm != null && nm.IsListening ? nm.CustomMessagingManager : null;
            if (messaging == null)
            {
                if (s_registeredOn != null) Forget();
                return;
            }
            if (ReferenceEquals(s_registeredOn, messaging)) return;
            Forget();
            messaging.RegisterNamedMessageHandler(LobbyMessage, OnLobby);
            messaging.RegisterNamedMessageHandler(VoteMessage, OnVote);
            s_registeredOn = messaging;
        }

        static void Forget()
        {
            s_registeredOn = null;
            s_latest = null;
            s_votes.Clear();
            s_votesVersion++;
            s_myVote = NoVote;
        }

        /// <summary>The host tells the party where its Voyage window stands. Does nothing solo or off the host.</summary>
        public static void Send(VoyageLobbyState state)
        {
            var nm = NetworkManager.Singleton;
            if (state == null || !VoyageCourseLink.HostWithParty || nm.CustomMessagingManager == null) return;
            string json = JsonUtility.ToJson(state);
            var writer = new FastBufferWriter(json.Length * 2 + 64, Allocator.Temp, int.MaxValue);
            try
            {
                writer.WriteValueSafe(json);
                nm.CustomMessagingManager.SendNamedMessageToAll(LobbyMessage, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
            finally
            {
                writer.Dispose();
            }
        }

        /// <summary>A friend votes for the Ark's domain. Does nothing on the host or solo.</summary>
        public static void Vote(Domains domain)
        {
            var nm = NetworkManager.Singleton;
            if (!VoyageCourseLink.IsFollower || nm.CustomMessagingManager == null) return;
            s_myVote = domain;
            var writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
            try
            {
                writer.WriteValueSafe((int)domain);
                nm.CustomMessagingManager.SendNamedMessage(VoteMessage, NetworkManager.ServerClientId, writer,
                    NetworkDelivery.ReliableSequenced);
            }
            finally
            {
                writer.Dispose();
            }
        }

        /// <summary>The friends' votes for <paramref name="domain"/> that the host holds, counting only friends still in
        /// the session.</summary>
        public static int VotesFor(Domains domain)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || !nm.IsServer) return 0;
            int n = 0;
            var connected = nm.ConnectedClientsIds;
            foreach (var pair in s_votes)
            {
                if (pair.Value != domain) continue;
                for (int i = 0; i < connected.Count; i++)
                    if (connected[i] == pair.Key) { n++; break; }
            }
            return n;
        }

        /// <summary>
        /// The Ark's one domain (Shombith, 2026-11-21: "one single domain for the Ark"): the host's pick on the host and
        /// solo, the host's as last heard on a friend's machine. The voyage reads it for the Ark's hull and model, every
        /// person's vessel (<see cref="MenuServerPlayerVesselInitializer"/> in the voyage scene) and the home harbour
        /// (<see cref="VoyageRun.LiveDomain(GameDataSO)"/>). Remembered on the device between sessions; Jade at first.
        /// </summary>
        public static Domains ArkDomain
        {
            get
            {
                if (VoyageCourseLink.IsFollower && s_latest != null && IsPlayable((Domains)s_latest.Domain))
                    return (Domains)s_latest.Domain;
                if (s_arkDomain == NoVote)
                {
                    var saved = (Domains)PlayerPrefs.GetInt(ArkDomainPref, (int)Domains.Jade);
                    s_arkDomain = IsPlayable(saved) ? saved : Domains.Jade;
                }
                return s_arkDomain;
            }
        }

        /// <summary>The host (or a solo pilot) picks the Ark's domain. Refused on a friend's machine (they vote instead,
        /// <see cref="Vote"/>) and for Blue, the no-team sentinel.</summary>
        public static bool SetArkDomain(Domains domain)
        {
            if (!VoyageCourseLink.IsLeader || !IsPlayable(domain)) return false;
            s_arkDomain = domain;
            PlayerPrefs.SetInt(ArkDomainPref, (int)domain);
            return true;
        }

        /// <summary>Jade, Ruby or Gold: a domain a team flies.</summary>
        public static bool IsPlayable(Domains domain) => GameDataSO.IsActiveDomain(domain, GameDataSO.ActiveDomains.Length);

        static void OnLobby(ulong sender, FastBufferReader reader)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out string json);
            try
            {
                s_latest = JsonUtility.FromJson<VoyageLobbyState>(json);
                VoyagePartySeats.Adopt(s_latest?.Seats);
            }
            catch (ArgumentException e)
            {
                CSDebug.LogWarning($"[Voyage] The host's lobby would not read ({e.Message}) - not followed.");
            }
        }

        static void OnVote(ulong sender, FastBufferReader reader)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || sender == nm.LocalClientId) return;
            try
            {
                reader.ReadValueSafe(out int domain);
                var d = (Domains)domain;
                if (!IsPlayable(d)) return;
                if (s_votes.TryGetValue(sender, out var known) && known == d) return;
                s_votes[sender] = d;
                s_votesVersion++;
            }
            catch (OverflowException)
            {
                // A short message: no vote.
            }
        }
    }

    /// <summary>The host's Voyage window as the party sees it (<see cref="VoyageLobbyLink"/>).</summary>
    [Serializable]
    public sealed class VoyageLobbyState
    {
        /// <summary>True while the host's Voyage window is open.</summary>
        public bool Open;
        /// <summary>The open voyage's seat key (<see cref="VoyageSeatRecord.JobKey"/> or the escort's); empty for none.</summary>
        public string Key;
        /// <summary>The page the host is on (the board's Page: 0 the HyperSea, 1 the voyage, 2 your Ark).</summary>
        public int Page;
        /// <summary>The difficulty the host picked (1-9).</summary>
        public int Difficulty;
        /// <summary>The Ark's one domain, the host's pick (<see cref="Domains"/>).</summary>
        public int Domain;
        /// <summary>The friends' votes, per domain: Jade, Ruby, Gold.</summary>
        public int VotesJade, VotesRuby, VotesGold;
        /// <summary>Every seat the host knows, its own among them (<see cref="VoyagePartySeats.Snapshot"/>), so each friend's
        /// Ark page shows the host's pilot and the other friends' (Shombith, 2026-11-21).</summary>
        public List<VoyageLobbySeat> Seats = new();

        /// <summary>True when this state asks a friend's menu to move from <paramref name="other"/> (another voyage, page,
        /// difficulty or open state).</summary>
        public bool MovesFrom(VoyageLobbyState other) =>
            other == null || Open != other.Open || Key != other.Key || Page != other.Page || Difficulty != other.Difficulty;

        public override bool Equals(object obj) =>
            obj is VoyageLobbyState o && Open == o.Open && Key == o.Key && Page == o.Page &&
            Difficulty == o.Difficulty && Domain == o.Domain && VotesJade == o.VotesJade && VotesRuby == o.VotesRuby &&
            VotesGold == o.VotesGold && SameSeats(Seats, o.Seats);

        static bool SameSeats(List<VoyageLobbySeat> a, List<VoyageLobbySeat> b)
        {
            int n = a?.Count ?? 0;
            if (n != (b?.Count ?? 0)) return false;
            for (int i = 0; i < n; i++)
                if (!VoyageLobbySeat.Same(a[i], b[i])) return false;
            return true;
        }

        public override int GetHashCode() => HashCode.Combine(Open, Key, Page, Difficulty, Domain, VotesJade, VotesRuby, VotesGold);

        /// <summary>The votes the state carries for <paramref name="domain"/>.</summary>
        public int VotesFor(Domains domain) => domain switch
        {
            Domains.Jade => VotesJade,
            Domains.Ruby => VotesRuby,
            Domains.Gold => VotesGold,
            _ => 0,
        };
    }

    /// <summary>One seat in the host's lobby (<see cref="VoyageLobbyState.Seats"/>): who sits it and what they picked.</summary>
    [Serializable]
    public sealed class VoyageLobbySeat
    {
        public ulong ClientId;
        public string PlayerName;
        public int SlotVessel, SlotElement;
        public string PilotId, ArtifactId, PilotName;

        public static bool Same(VoyageLobbySeat a, VoyageLobbySeat b) =>
            ReferenceEquals(a, b) || a != null && b != null && a.ClientId == b.ClientId && a.PlayerName == b.PlayerName &&
            a.SlotVessel == b.SlotVessel && a.SlotElement == b.SlotElement && a.PilotId == b.PilotId &&
            a.ArtifactId == b.ArtifactId && a.PilotName == b.PilotName;
    }
}
