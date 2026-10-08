using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Switchyard's own map of itself: where the rails start and end, which burr each one
    /// launches into, and what colour each burr is wearing right now.
    ///
    /// <para><b>Why this exists at all.</b> The arena's shape is closed-form arithmetic, so
    /// anything that needs it could in principle recompute it - and everything that tried to
    /// would need the spawnable's serialized fields AND the container transform it was parented
    /// under. This component is attached to that container by
    /// <see cref="SpawnableSwitchyard"/> as it lays the yard, so every consumer asks the object
    /// that IS the arena and gets world positions that are correct by construction, including
    /// after a re-parent or a cell swap. Two consumers today: the HUD's objective arrow
    /// (<see cref="HijackObjectiveProvider"/>) and the mode's AI loop
    /// (<see cref="HijackController"/>).</para>
    ///
    /// <para>Positions are stored in the container's LOCAL space and resolved through its
    /// transform on read - a snapshot of world positions taken during the build would be wrong
    /// the moment anything moved the container, which is exactly the class of bug the Cell's
    /// "read <c>ExpectedNucleusWorldRadius</c>, not the spawned one" rule records.</para>
    ///
    /// <para>The registry is a plain static list in the <c>Crystal.Active</c> shape: every peer
    /// builds its own arena locally (closed form, so they agree), so the GEOMETRY needs no
    /// replication. It is cleared on disable, so a cell swap or a scene reload cannot leave a
    /// consumer pointing at a destroyed yard.</para>
    ///
    /// <para><b>OWNERSHIP and SHIELD STATE are replicated, and every read here goes through
    /// them.</b> The shape is identical on every peer but who owns each prism, and whether it is
    /// armoured, is not - a steal or a shield is a local call on whichever machine ran it. While
    /// a Hijack match is live, <see cref="HijackController"/> binds its server-authoritative
    /// table (<see cref="Ownership"/>, a <see cref="HijackOwnershipLedger"/>) to this yard, which
    /// keeps every yard prism SHOWING the table's owner and armour, and <see cref="StateAt"/>
    /// answers from that table instead of the local prism. Outside a match (the mode preview's
    /// satellite arena) nothing is bound and the reads fall back to the prism's own state.</para>
    ///
    /// <para><b>Burr colour is READ LIVE, never stored.</b> The whole mode is players flipping
    /// this mass back and forth, so a cached domain would be a lie within seconds. A burr reports
    /// the domain of the majority of its own prisms, sampled from the trail it was laid into.</para>
    /// </summary>
    public class HijackYard : MonoBehaviour
    {
        /// <summary>One spiny cluster: the thing a rail launches you at.</summary>
        public readonly struct Burr
        {
            public readonly Vector3 LocalCentre;
            public readonly float Radius;
            public readonly bool Big;
            /// <summary>The colour it was LAID in - the starting owner, not the live one. Ask
            /// <see cref="HijackYard.HostileMassAt"/> for what it is worth to a pilot now.</summary>
            public readonly Domains PaintedDomain;
            /// <summary>The trail this burr's prisms live in - the live census's source.</summary>
            public readonly Trail Trail;

            public Burr(Vector3 localCentre, float radius, bool big, Domains painted, Trail trail)
            {
                LocalCentre = localCentre; Radius = radius; Big = big;
                PaintedDomain = painted; Trail = trail;
            }
        }

        /// <summary>One open ribbon, plus the burr its far end aims at.</summary>
        public readonly struct Rail
        {
            public readonly Vector3 LocalStart;
            public readonly Vector3 LocalEnd;
            /// <summary>Index into <see cref="Burrs"/> of the cluster the far end launches into -
            /// the arena's whole reward for reaching the end of a rail.</summary>
            public readonly int TargetBurr;
            public readonly Trail Trail;

            public Rail(Vector3 localStart, Vector3 localEnd, int targetBurr, Trail trail)
            {
                LocalStart = localStart; LocalEnd = localEnd; TargetBurr = targetBurr; Trail = trail;
            }
        }

        readonly List<Burr> _burrs = new(18);
        readonly List<Rail> _rails = new(24);

        static readonly List<HijackYard> s_active = new(2);

        /// <summary>Every live yard. Normally exactly one; a list because the mode preview can
        /// stand a satellite arena beside the menu's own cell.</summary>
        public static IReadOnlyList<HijackYard> Active => s_active;

        /// <summary>The yard to reason about, or null before the arena has been laid.</summary>
        public static HijackYard Current => s_active.Count > 0 ? s_active[s_active.Count - 1] : null;

        public IReadOnlyList<Burr> Burrs => _burrs;
        public IReadOnlyList<Rail> Rails => _rails;

        /// <summary>The match's replicated ownership table, bound by
        /// <see cref="HijackController"/> while a match is live; null otherwise.</summary>
        internal HijackOwnershipLedger Ownership { get; set; }

        /// <summary>
        /// Who owns the <paramref name="index"/>th prism of trail <paramref name="slot"/>, as the
        /// match's replicated table says, or the prism's own domain when no table is bound or the
        /// table has not seen this prism yet.
        /// </summary>
        Domains DomainAt(int slot, int index, Prism prism)
        {
            var table = Ownership;
            if (table != null && table.TryRead(slot, index, out var owner)) return owner;
            return prism.Domain;
        }

        /// <summary>
        /// Owner AND armour of the <paramref name="index"/>th prism of trail
        /// <paramref name="slot"/>, from the replicated table, or the prism's own state when no
        /// table is bound or the table has not seen this prism yet.
        /// </summary>
        void StateAt(int slot, int index, Prism prism, out Domains domain,
                     out HijackOwnershipLedger.Shield shield)
        {
            var table = Ownership;
            if (table != null && table.TryReadState(slot, index, out domain, out shield)) return;
            domain = prism.Domain;
            shield = HijackOwnershipLedger.LocalShield(prism);
        }

        /// <summary>
        /// Is this prism worth anything to <paramref name="domain"/>? Hostile, and not
        /// super-shielded - <c>PrismTeamManager.Steal</c> refuses a super-shielded prism outright,
        /// so it is not loot however many times it is ridden. A plain shield still counts: the
        /// first pass breaks it, the next one takes the prism.
        /// </summary>
        bool IsLootFor(int slot, int index, Prism prism, Domains domain)
        {
            StateAt(slot, index, prism, out var owner, out var shield);
            return owner != domain && shield != HijackOwnershipLedger.Shield.SuperShielded;
        }

        /// <summary>
        /// The replicated owner of any prism in this yard - the same answer on every peer. A
        /// prism that is not one of the yard's own (or with no table bound) reports its local
        /// domain.
        /// </summary>
        public Domains DomainOf(Prism prism)
        {
            if (!prism) return Domains.Blue;
            var table = Ownership;
            if (table == null) return prism.Domain;
            int slot = table.SlotOf(prism.Trail);
            int index = slot >= 0 ? prism.Trail.GetBlockIndex(prism) : -1;
            return index >= 0 ? DomainAt(slot, index, prism) : prism.Domain;
        }

        /// <summary>
        /// Fraction (0-1) of a rail's prisms that <paramref name="domain"/> owns, read from the
        /// replicated table - the AI's "how much of this run is already a fast one" term.
        /// </summary>
        public float OwnFractionOfRail(int rail, Domains domain)
        {
            var list = _rails[rail].Trail?.TrailList;
            if (list == null || list.Count == 0) return 0f;

            int slot = HijackOwnershipLedger.RailSlot(this, rail);
            int own = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var prism = list[i];
                if (prism && DomainAt(slot, i, prism) == domain) own++;
            }
            return own / (float)list.Count;
        }

        void OnEnable()
        {
            if (!s_active.Contains(this)) s_active.Add(this);
        }

        void OnDisable() => s_active.Remove(this);

        internal int AddBurr(Vector3 localCentre, float radius, bool big, Domains painted, Trail trail)
        {
            _burrs.Add(new Burr(localCentre, radius, big, painted, trail));
            return _burrs.Count - 1;
        }

        internal void AddRail(Vector3 localStart, Vector3 localEnd, int targetBurr, Trail trail) =>
            _rails.Add(new Rail(localStart, localEnd, targetBurr, trail));

        public Vector3 WorldPoint(Vector3 local) => transform.TransformPoint(local);

        public Vector3 BurrCentre(int index) => WorldPoint(_burrs[index].LocalCentre);

        /// <summary>
        /// How many prisms in this burr are worth STEALING to <paramref name="domain"/> right
        /// now - the only measure of a burr that means anything in this mode, since the painted
        /// colour is just where it started. A destroyed prism counts: it is restored and then
        /// taken on the same ride hop (<c>GunVesselTransformer.ApplyPrismscapePayoff</c>). A
        /// super-shielded one does not (<see cref="IsLootFor"/>).
        /// </summary>
        public int HostileMassAt(int index, Domains domain)
        {
            var trail = _burrs[index].Trail;
            var list = trail?.TrailList;
            if (list == null) return 0;

            int slot = HijackOwnershipLedger.BurrSlot(index);
            int hostile = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var prism = list[i];
                if (prism && IsLootFor(slot, i, prism, domain)) hostile++;
            }
            return hostile;
        }

        /// <summary>
        /// Is there anything left in this burr for <paramref name="domain"/> to take? The same
        /// question as <see cref="HostileMassAt"/> without the count, and it EARLY-OUTS on the
        /// first hostile prism - which matters because the answer is normally yes and the caller
        /// is a HUD readout, not a scoring path.
        /// </summary>
        public bool HasHostileMass(int index, Domains domain)
        {
            var list = _burrs[index].Trail?.TrailList;
            if (list == null) return false;

            int slot = HijackOwnershipLedger.BurrSlot(index);
            for (int i = 0; i < list.Count; i++)
            {
                var prism = list[i];
                if (prism && IsLootFor(slot, i, prism, domain)) return true;
            }
            return false;
        }

        /// <summary>
        /// Nearest burr holding at least one prism this pilot could take, or -1.
        ///
        /// <para>DISTANCE FIRST, then hostility. A burr is up to 1,143 prisms and there are 18 of
        /// them, so asking all 18 "have you got anything?" is ~20k prism reads; asking only the
        /// ones that could still WIN is normally two or three, and each of those early-outs on
        /// its first hostile prism. The full walk only happens for a burr the pilot has already
        /// emptied, which is the rare case and the one where the answer has to be exact.</para>
        /// </summary>
        public int NearestHostileBurr(Vector3 from, Domains domain)
        {
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _burrs.Count; i++)
            {
                float sqr = (BurrCentre(i) - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                if (!HasHostileMass(i, domain)) continue;
                bestSqr = sqr;
                best = i;
            }
            return best;
        }
    }
}
