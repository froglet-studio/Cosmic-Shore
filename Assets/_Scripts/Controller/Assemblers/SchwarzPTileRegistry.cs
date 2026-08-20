using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The claim book for Schwarz P tiles: which tile of which colony's lattice already has
    /// (or is being grown toward) a plant. One entry per tile, claimed by the plant that owns
    /// it and released when that plant is destroyed - so a grazed-out tile becomes plantable
    /// again, which is what lets neighbours recolonise a hole in the surface.
    ///
    /// <para><b>This is an exact integer dictionary, not a spatial hash.</b> The gyroid's
    /// equivalent (<see cref="GyroidOctagonRegistry"/>) has to bin float centres and compare
    /// them against a dedupe radius, because an octagon is DISCOVERED by measuring a ring of
    /// danger prisms and two colonies' lattices drift apart. A Schwarz P tile is an exact
    /// integer partition of space within its colony's frame (Docs/ECOSYSTEM.md §34), so
    /// "is this tile taken" is a dictionary hit. There is no dedupe radius, no ownership
    /// epsilon and no ring-coherence tolerance to tune, and the whole misalignment defect
    /// class the gyroid spent five playtests on cannot arise here - two plants either agree
    /// on an integer or belong to different frames.</para>
    ///
    /// <para>Keyed by FRAME as well as tile, because a tile key only means something relative
    /// to the lattice that minted it. Every plant of one colony shares its founder's frame by
    /// reference (a daughter inherits it), so within a colony keys are directly comparable;
    /// two independent founders in the same cell hold two frames and simply never collide in
    /// this book. Their prisms still cannot overlap - that is
    /// <c>PrismSpatialIndex.TryReserve</c>'s job on every single site, exactly as it is for
    /// any two floras that meet (Docs/SPATIAL_INDEX.md).</para>
    ///
    /// <para>Bookkeeping only - no MonoBehaviour, no update loop. This is not a spatial store
    /// of mass; the canonical index of prisms stays <c>PrismSpatialIndex</c>.</para>
    /// </summary>
    public static class SchwarzPTileRegistry
    {
        static readonly Dictionary<SchwarzPSurfaceFrame, Dictionary<Vector3Int, AssembledFlora>> claims = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForDomainReload() => claims.Clear();

        public static bool IsClaimed(SchwarzPSurfaceFrame frame, Vector3Int tile) =>
            frame != null && claims.TryGetValue(frame, out var book) && book.ContainsKey(tile);

        /// <summary>Claims a tile for <paramref name="owner"/>. False when it is already held.</summary>
        public static bool TryClaim(SchwarzPSurfaceFrame frame, Vector3Int tile, AssembledFlora owner)
        {
            if (frame == null) return false;
            if (!claims.TryGetValue(frame, out var book))
                claims[frame] = book = new Dictionary<Vector3Int, AssembledFlora>();
            if (book.ContainsKey(tile)) return false;
            book[tile] = owner;
            return true;
        }

        /// <summary>
        /// Re-homes a claim onto the plant that now owns it. A parent claims its daughter's
        /// tile BEFORE spawning her so a sibling cannot race it; the daughter then adopts.
        /// </summary>
        public static void TransferClaim(SchwarzPSurfaceFrame frame, Vector3Int tile, AssembledFlora newOwner)
        {
            if (frame == null || !claims.TryGetValue(frame, out var book)) return;
            if (book.ContainsKey(tile)) book[tile] = newOwner;
        }

        /// <summary>Releases a claim, but only for the plant that holds it.</summary>
        public static void Release(SchwarzPSurfaceFrame frame, Vector3Int tile, AssembledFlora owner)
        {
            if (frame == null || !claims.TryGetValue(frame, out var book)) return;
            if (!book.TryGetValue(tile, out var held)) return;
            // `is null or !held` so a destroyed-but-non-null owner can still release during teardown.
            if (held is null || !held || held == owner) book.Remove(tile);
            if (book.Count == 0) claims.Remove(frame);
        }

        /// <summary>Live tile claims across every colony - the colony heartbeat's plant count.</summary>
        public static int ClaimCount
        {
            get
            {
                int n = 0;
                foreach (var book in claims.Values) n += book.Count;
                return n;
            }
        }

        /// <summary>Live tile claims in one colony's lattice.</summary>
        public static int CountFor(SchwarzPSurfaceFrame frame) =>
            frame != null && claims.TryGetValue(frame, out var book) ? book.Count : 0;

        /// <summary>
        /// Drops every claim belonging to <paramref name="cell"/>'s lattices - called where the
        /// cell destroys or abandons its lifeforms. Without it the NEXT world grown in this cell
        /// inherits the dead one's claims and can never re-plant those tiles (the Cell Selector
        /// swaps worlds in the very scene this colony ships in).
        /// </summary>
        public static void Clear(Cell cell)
        {
            if (!cell) return;
            var doomed = new List<SchwarzPSurfaceFrame>();
            foreach (var frame in claims.Keys)
                if (frame != null && frame.Cell == cell) doomed.Add(frame);
            for (int i = 0; i < doomed.Count; i++) claims.Remove(doomed[i]);
        }
    }
}
