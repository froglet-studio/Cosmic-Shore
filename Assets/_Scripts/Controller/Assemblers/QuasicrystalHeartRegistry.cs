using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The claim book for quasicrystal hearts: which heart of which colony's lattice already
    /// has (or is being grown toward) a plant. One entry per heart, claimed by the plant that
    /// owns it and released when that plant is destroyed - so a grazed-out star becomes
    /// plantable again, which is what lets neighbours recolonise a hole in the scaffold.
    ///
    /// <para><b>This is an exact integer dictionary, not a spatial hash</b> - the same
    /// property the Schwarz P tile book has (<see cref="SchwarzPTileRegistry"/>) and the
    /// gyroid's float-centre book lacks: a heart is a Z^6 address, so "is this heart taken"
    /// is a dictionary hit with no dedupe radius, no ownership epsilon and no coherence
    /// tolerance, even though the pattern the addresses describe never repeats
    /// (Docs/ECOSYSTEM.md 36).</para>
    ///
    /// <para>Keyed by FRAME as well as heart, because an address only means something
    /// relative to the lattice that minted it. Every plant of one colony shares its
    /// founder's frame by reference; two independent founders hold two frames and never
    /// collide here. Their prisms still cannot overlap - that is
    /// <c>PrismSpatialIndex.TryReserve</c>'s job on every single edge, exactly as for any
    /// two floras that meet (Docs/SPATIAL_INDEX.md).</para>
    ///
    /// <para>Bookkeeping only - no MonoBehaviour, no update loop. This is not a spatial
    /// store of mass; the canonical index of prisms stays <c>PrismSpatialIndex</c>.</para>
    /// </summary>
    public static class QuasicrystalHeartRegistry
    {
        static readonly Dictionary<QuasicrystalLatticeFrame,
            Dictionary<QuasicrystalVertex, AssembledFlora>> claims = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForDomainReload() => claims.Clear();

        public static bool IsClaimed(QuasicrystalLatticeFrame frame, QuasicrystalVertex heart) =>
            frame != null && claims.TryGetValue(frame, out var book) && book.ContainsKey(heart);

        /// <summary>Claims a heart for <paramref name="owner"/>. False when it is already held.</summary>
        public static bool TryClaim(QuasicrystalLatticeFrame frame, QuasicrystalVertex heart, AssembledFlora owner)
        {
            if (frame == null) return false;
            if (!claims.TryGetValue(frame, out var book))
                claims[frame] = book = new Dictionary<QuasicrystalVertex, AssembledFlora>();
            if (book.ContainsKey(heart)) return false;
            book[heart] = owner;
            return true;
        }

        /// <summary>
        /// Re-homes a claim onto the plant that now owns it. A parent claims its daughter's
        /// heart BEFORE spawning her so a sibling cannot race it; the daughter then adopts.
        /// </summary>
        public static void TransferClaim(QuasicrystalLatticeFrame frame, QuasicrystalVertex heart, AssembledFlora newOwner)
        {
            if (frame == null || !claims.TryGetValue(frame, out var book)) return;
            if (book.ContainsKey(heart)) book[heart] = newOwner;
        }

        /// <summary>Releases a claim, but only for the plant that holds it.</summary>
        public static void Release(QuasicrystalLatticeFrame frame, QuasicrystalVertex heart, AssembledFlora owner)
        {
            if (frame == null || !claims.TryGetValue(frame, out var book)) return;
            if (!book.TryGetValue(heart, out var held)) return;
            // `is null or !held` so a destroyed-but-non-null owner can still release during teardown.
            if (held is null || !held || held == owner) book.Remove(heart);
            if (book.Count == 0) claims.Remove(frame);
        }

        /// <summary>Live heart claims across every colony - the colony heartbeat's plant count.</summary>
        public static int ClaimCount
        {
            get
            {
                int n = 0;
                foreach (var book in claims.Values) n += book.Count;
                return n;
            }
        }

        /// <summary>Live heart claims in one colony's lattice.</summary>
        public static int CountFor(QuasicrystalLatticeFrame frame) =>
            frame != null && claims.TryGetValue(frame, out var book) ? book.Count : 0;

        /// <summary>
        /// Drops every claim belonging to <paramref name="cell"/>'s lattices - called where
        /// the cell destroys or abandons its lifeforms. Without it the NEXT world grown in
        /// this cell inherits the dead one's claims and can never re-plant those hearts (the
        /// Cell Selector swaps worlds in the very scene this colony ships in).
        /// </summary>
        public static void Clear(Cell cell)
        {
            if (!cell) return;
            var doomed = new List<QuasicrystalLatticeFrame>();
            foreach (var frame in claims.Keys)
                if (frame != null && frame.Cell == cell) doomed.Add(frame);
            for (int i = 0; i < doomed.Count; i++) claims.Remove(doomed[i]);
        }
    }
}
