using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Mass-Space VOLUME BOOKS (Garrett, 2026-11-18). A pilot's score is their MASS contribution, VC - the volume
    /// they increased in their own domain (laid, grown, restored, stolen, shielded) - plus their SPACE contribution, VD
    /// - the volume they reduced from the opposing domains (destroyed, or stolen: a steal counts as both). Added up a
    /// team is every effect its pilots had on volume, so with two teams the difference between the team scores equals
    /// the difference between their remaining volume at the end, which <see cref="SetCounted"/> measures from the live
    /// prisms. Two ways to one number: a gap is volume that moved by something no pilot did (fauna eating it, a pilot
    /// destroying their own team's, shrinking) or an event the books missed. The winner may not be the team in
    /// control, but every pilot sees what they did for it.
    ///
    /// <para><see cref="Line.Lost"/> is the third column the check needs: the pilot's own volume that went, by any
    /// cause. Created less Lost is what the books say the pilot still owns, the per-pilot form of the same check.</para>
    ///
    /// <para>The server writes it (<c>StatsManager</c>, <c>Player</c>'s report RPCs); a mode that shows it sends the
    /// lines to its clients at the end (<c>BroodRushController</c>). It never changes a score and no legacy stat
    /// reads it, so every mode that scores on VolumeCreated or HostileVolumeDestroyed keeps its numbers.</para>
    /// </summary>
    public sealed class VolumeLedger
    {
        public struct Line
        {
            /// <summary>VC, the mass contribution: volume this pilot increased in their own domain.</summary>
            public float Created;
            /// <summary>VD, the space contribution: volume this pilot reduced from an opposing domain.</summary>
            public float Reduced;
            /// <summary>Volume of this pilot's that went, by any cause (destroyed, stolen from them, shrunk, unshielded).</summary>
            public float Lost;
            /// <summary>Super-shielded prisms of an opposing domain this pilot destroyed: the specialist's measure.</summary>
            public int SuperShieldsBroken;
            /// <summary>The volume this pilot still owned at the end, measured from the live prisms.</summary>
            public float Counted;
            /// <summary>False until <see cref="SetCounted"/> measured this pilot.</summary>
            public bool HasCount;

            /// <summary>The pilot's score: VC + VD.</summary>
            public float Total => Created + Reduced;

            /// <summary>What the books say this pilot still owns.</summary>
            public float Remaining => Created - Lost;

            public static Line operator +(Line a, Line b) => new()
            {
                Created = a.Created + b.Created,
                Reduced = a.Reduced + b.Reduced,
                Lost = a.Lost + b.Lost,
                SuperShieldsBroken = a.SuperShieldsBroken + b.SuperShieldsBroken,
                Counted = a.Counted + b.Counted,
                HasCount = a.HasCount || b.HasCount,
            };
        }

        readonly Dictionary<string, Line> _lines = new();

        public int Count => _lines.Count;
        public IEnumerable<KeyValuePair<string, Line>> Lines => _lines;

        public void Clear() => _lines.Clear();

        public bool TryGet(string name, out Line line) => _lines.TryGetValue(name ?? string.Empty, out line);

        public Line Get(string name) => TryGet(name, out var line) ? line : default;

        /// <summary>Overwrites a pilot's line: a client filling the books the server sent.</summary>
        public void Set(string name, Line line)
        {
            if (string.IsNullOrEmpty(name)) return;
            _lines[name] = line;
        }

        /// <summary>A change in a pilot's own volume: growth is created, shrinking (a negative delta) lost.</summary>
        public void AddDelta(string name, float delta)
        {
            if (delta > 0f) AddCreated(name, delta);
            else if (delta < 0f) AddLost(name, -delta);
        }

        public void AddCreated(string name, float volume)
        {
            if (string.IsNullOrEmpty(name) || volume <= 0f) return;
            var line = Get(name);
            line.Created += volume;
            _lines[name] = line;
        }

        public void AddLost(string name, float volume)
        {
            if (string.IsNullOrEmpty(name) || volume <= 0f) return;
            var line = Get(name);
            line.Lost += volume;
            _lines[name] = line;
        }

        /// <summary>VD: <paramref name="volume"/> of an opposing domain this pilot destroyed or stole.</summary>
        public void AddReduced(string name, float volume, bool superShielded = false)
        {
            if (string.IsNullOrEmpty(name)) return;
            var line = Get(name);
            if (volume > 0f) line.Reduced += volume;
            if (superShielded) line.SuperShieldsBroken++;
            _lines[name] = line;
        }

        /// <summary>
        /// One prism of <paramref name="victim"/>'s went because of <paramref name="actor"/> (null: no pilot - fauna,
        /// the arena). The victim always loses it; the actor's VD gains it only when they are on another team, since
        /// reducing your own domain is no contribution to space.
        /// </summary>
        public void BookLoss(string victim, Domains victimDomain, string actor, Domains actorDomain, float volume,
            bool superShielded = false)
        {
            AddLost(victim, volume);
            if (!string.IsNullOrEmpty(actor) && actor != victim && actorDomain != victimDomain)
                AddReduced(actor, volume, superShielded);
        }

        public void SetCounted(string name, float volume)
        {
            if (string.IsNullOrEmpty(name)) return;
            var line = Get(name);
            line.Counted = volume;
            line.HasCount = true;
            _lines[name] = line;
        }

        /// <summary>
        /// The second way to the number: measures what every rostered pilot still owns from <paramref name="owned"/>
        /// (owner name, volume including its booked shell) and writes it as their <see cref="Line.Counted"/>. A pilot
        /// who owns nothing counts 0. Prisms of no rostered pilot (flora, fauna, the arena) are not in the books.
        /// </summary>
        public void CountOwned(ICollection<string> roster, IEnumerable<KeyValuePair<string, float>> owned)
        {
            if (roster == null) return;
            var sums = new Dictionary<string, float>();
            foreach (var n in roster)
                if (!string.IsNullOrEmpty(n)) sums[n] = 0f;
            if (owned != null)
                foreach (var kv in owned)
                    if (kv.Key != null && sums.TryGetValue(kv.Key, out var v)) sums[kv.Key] = v + kv.Value;
            foreach (var kv in sums) SetCounted(kv.Key, kv.Value);
        }

        /// <summary>
        /// <see cref="CountOwned"/> over every live prism in the spatial index (server, at the match's end). Each
        /// prism counts its box (<c>prismProperties.volume</c>, what its events booked) and its booked shell.
        /// </summary>
        public void CountLive(PrismSpatialIndex index, ICollection<string> roster)
        {
            if (index == null || roster == null) return;
            var live = new List<Prism>(index.LiveCount);
            index.CopyLivePrisms(live);
            var owned = new List<KeyValuePair<string, float>>(live.Count);
            for (int i = 0; i < live.Count; i++)
            {
                var p = live[i];
                if (!p || p.prismProperties == null) continue;
                owned.Add(new KeyValuePair<string, float>(p.PlayerName,
                    Mathf.Max(p.prismProperties.volume, 0f) + p.BookedShieldVolume));
            }
            CountOwned(roster, owned);
        }

        /// <summary>The team's line: its pilots' lines added.</summary>
        public Line Sum(IEnumerable<string> names)
        {
            var total = default(Line);
            if (names == null) return total;
            foreach (var n in names)
                if (TryGet(n, out var line)) total += line;
            return total;
        }

        /// <summary>
        /// The volume a shield adds on top of its prism's box: the octahedral shell is 4.5 boxes, the stellated
        /// super-shield 13.5 (<see cref="OctahedronMeshGenerator.SHIELD_TO_BOX_VOLUME_RATIO"/>,
        /// <see cref="StellatedOctahedronMeshGenerator.SUPER_SHIELD_TO_BOX_VOLUME_RATIO"/>, the same ratios the
        /// shields' physics mass uses), so the extra is the ratio less the one box the prism already booked.
        /// </summary>
        public static float ShieldExtra(float boxVolume, bool shielded, bool superShielded)
        {
            if (boxVolume <= 0f) return 0f;
            if (superShielded) return boxVolume * (StellatedOctahedronMeshGenerator.SUPER_SHIELD_TO_BOX_VOLUME_RATIO - 1f);
            if (shielded) return boxVolume * (OctahedronMeshGenerator.SHIELD_TO_BOX_VOLUME_RATIO - 1f);
            return 0f;
        }

        /// <summary>
        /// Do the books agree with the count? Float sums of thousands of prisms drift, so they agree within half a
        /// unit or a tenth of a percent of the count, whichever is larger.
        /// </summary>
        public static bool Balances(float books, float counted) =>
            Mathf.Abs(books - counted) <= Mathf.Max(0.5f, 0.001f * Mathf.Abs(counted));
    }
}
