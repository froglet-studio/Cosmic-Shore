using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// What a pilot chose on a card's launch panel the last time they LAUNCHED it - the record
    /// the panel re-seeds itself from the next time that card opens.
    ///
    /// <para>One record per <see cref="GameModes"/>: a card is the unit the player remembers a
    /// setup for (Scarab Scramble at intensity 3 with two bots on Ruby is a different memory
    /// from Rampage at 1), and the arcade and arena grids are the same modal pointed at two
    /// rosters, so one key serves both.</para>
    ///
    /// <para>Two halves, written by two different authorities. The HOST TERMS (intensity, domain
    /// count, the AI placements) are decided by the launch authority and written only on a
    /// launch it performed; the PILOT CHOICE (own domain, own hull) is every player's own and is
    /// written on every ready press, host and guest alike. A guest therefore never overwrites
    /// the host terms it merely watched (<see cref="WithPilotChoice"/> keeps them), and a host
    /// re-launching writes both (<see cref="WithHostTerms"/>).</para>
    ///
    /// <para>The record is a WISH, not a command: every field is re-validated against the card,
    /// the player's unlocks and the party on the ground when it is applied
    /// (<see cref="LaunchPreferenceRules"/>), so a saved intensity 4 on a mode the player has
    /// since not unlocked, or three saved bots on a card that now seats two, degrade to the
    /// legal nearest rather than to an error.</para>
    /// </summary>
    [Serializable]
    public struct LaunchPreference
    {
        public GameModes GameMode;

        /// <summary>The intensity the host launched at. 0 = never written.</summary>
        public int Intensity;

        /// <summary>The domain count the host launched with. 0 = never written.</summary>
        public int DomainCount;

        /// <summary>
        /// The bots the host PLACED, in placement order (Add AI mode). Empty is a real answer -
        /// a launch with no placed bots - which is why <see cref="HasHostTerms"/> exists rather
        /// than reading emptiness as "unknown".
        /// </summary>
        public List<Domains> AIDomains;

        /// <summary>The local pilot's own domain pick. <see cref="Domains.Blue"/> = never written.</summary>
        public Domains Domain;

        /// <summary>The local pilot's own hull. <see cref="VesselClassType.Random"/> = never written.</summary>
        public VesselClassType Vessel;

        /// <summary>True once a launch authority has written the host half.</summary>
        public bool HasHostTerms;

        /// <summary>True once any pilot has written their own half.</summary>
        public bool HasPilotChoice;

        public static LaunchPreference Empty(GameModes mode) => new()
        {
            GameMode  = mode,
            AIDomains = new List<Domains>(),
            Domain    = Domains.Blue,
            Vessel    = VesselClassType.Random,
        };

        /// <summary>This record with the host half replaced. The pilot half is written too,
        /// because the host is also a pilot and just pressed Start.</summary>
        public readonly LaunchPreference WithHostTerms(int intensity, int domainCount,
                                                       IList<Domains> aiDomains,
                                                       Domains domain, VesselClassType vessel)
        {
            var next = WithPilotChoice(domain, vessel);
            next.Intensity    = intensity;
            next.DomainCount  = domainCount;
            next.AIDomains    = aiDomains != null ? new List<Domains>(aiDomains) : new List<Domains>();
            next.HasHostTerms = true;
            return next;
        }

        /// <summary>This record with only the pilot half replaced; the host half survives.</summary>
        public readonly LaunchPreference WithPilotChoice(Domains domain, VesselClassType vessel)
        {
            var next = this;
            next.AIDomains      = AIDomains != null ? new List<Domains>(AIDomains) : new List<Domains>();
            next.Domain         = domain;
            next.Vessel         = vessel;
            next.HasPilotChoice = true;
            return next;
        }
    }

    /// <summary>
    /// The pure half of applying a <see cref="LaunchPreference"/>: every rule that turns a saved
    /// wish into a value the panel may legally seed. Kept free of Unity objects so the edit-mode
    /// suite can hold each rule (<c>HomeHubPreferenceTests</c>), and so the modal applies a
    /// preference through the same clamps a live press goes through rather than a second copy.
    /// </summary>
    public static class LaunchPreferenceRules
    {
        /// <summary>
        /// The intensity to open on. A never-written record opens on the card's minimum, exactly
        /// as before; a written one is clamped to the card's range AND to what this player has
        /// unlocked - a saved 4 on a mode whose 3 and 4 are still locked opens on 2, never on a
        /// dimmed button that the row would then draw as selected.
        /// </summary>
        public static int ResolveIntensity(int saved, int cardMin, int cardMax, int maxUnlocked)
        {
            int ceiling = Mathf.Min(cardMax, maxUnlocked);
            if (saved <= 0) return Mathf.Clamp(cardMin, cardMin, ceiling);
            return Mathf.Clamp(saved, cardMin, Mathf.Max(cardMin, ceiling));
        }

        /// <summary>
        /// The AI placements to restore: only real playable domains (never the Blue sentinel), in
        /// their saved order, cut to the seats the card still has free above the humans present.
        /// A party that grew since the last launch simply gets fewer of its bots back.
        /// </summary>
        public static List<Domains> ResolveAiPlacements(IList<Domains> saved, int freeSeats)
        {
            var result = new List<Domains>();
            if (saved == null || freeSeats <= 0) return result;

            foreach (var d in saved)
            {
                if (result.Count >= freeSeats) break;
                if (d == Domains.Blue) continue;
                if (Array.IndexOf(GameDataSOActiveDomains, d) < 0) continue;
                result.Add(d);
            }
            return result;
        }

        /// <summary>
        /// The domain count to restore. It must cover every placed bot's domain (a saved Gold
        /// placement needs all three), sit inside the card's own window, and never fall below
        /// the mode's minimum. A never-written record answers the caller's default.
        /// </summary>
        public static int ResolveDomainCount(int saved, int fallback, int minForGame, int max,
                                             IList<Domains> placements)
        {
            int prefix = DomainPrefixCount(placements);
            int floor  = Mathf.Max(minForGame, prefix);
            int wanted = saved > 0 ? saved : fallback;
            return Mathf.Clamp(Mathf.Max(wanted, floor), minForGame, Mathf.Max(minForGame, max));
        }

        /// <summary>
        /// The pilot's own domain to restore, or <see cref="Domains.Jade"/> - the value the commit
        /// already put every human on - when the saved pick is unwritten or outside the match's
        /// active prefix. A pick outside the prefix is not restored silently: the tile it would
        /// light is dimmed, and a highlighted dimmed tile is a promise the spawn would break.
        /// </summary>
        public static Domains ResolvePilotDomain(Domains saved, int domainCount)
        {
            if (saved == Domains.Blue) return Domains.Jade;
            int index = Array.IndexOf(GameDataSOActiveDomains, saved);
            if (index < 0 || index >= domainCount) return Domains.Jade;
            return saved;
        }

        /// <summary>How many of the Jade -> Ruby -> Gold prefix the placements need.</summary>
        public static int DomainPrefixCount(IList<Domains> placements)
        {
            int need = 0;
            if (placements == null) return 0;
            foreach (var d in placements)
            {
                int index = Array.IndexOf(GameDataSOActiveDomains, d);
                if (index >= 0) need = Mathf.Max(need, index + 1);
            }
            return need;
        }

        // The playable set in prefix order - GameDataSO's own, never a second copy of it. Blue
        // is the "no team" sentinel and is absent from it.
        static Domains[] GameDataSOActiveDomains => CosmicShore.Utility.GameDataSO.ActiveDomains;
    }
}
