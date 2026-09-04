using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>Which crystal a microscene lays at a pickup point.</summary>
    public enum CrystalKind
    {
        /// <summary>Skimmable single-element pickup (Charge/Mass/Space/Time) - buffs the skimmed element.</summary>
        Elemental = 0,
        /// <summary>Body-collected any-domain jackpot - fuel + speed buff. Rarer, the "big" reward.</summary>
        Omni = 1,
    }

    public readonly struct CrystalDrop
    {
        public readonly Vector3 LocalPosition;
        public readonly CrystalKind Kind;

        public CrystalDrop(Vector3 localPosition, CrystalKind kind)
        {
            LocalPosition = localPosition;
            Kind = kind;
        }
    }

    /// <summary>
    /// Per-prism structural metadata a recipe stamps alongside each geometry point: which
    /// substructure the point belongs to (gate #2, strand #1, tree #4…) and how far along that
    /// structure's own path it sits (0 = root/entry, 1 = tip/exit). This is what lets the painter
    /// colour and theme *with* the superstructure - alternate whole gates, run a domain gradient up
    /// a strand, put danger on the tips, armour the frame - instead of sprinkling per-index confetti.
    /// </summary>
    public readonly struct PointMeta
    {
        public readonly ushort Structure;
        public readonly float T;

        public PointMeta(ushort structure, float t)
        {
            Structure = structure;
            T = t;
        }
    }

    /// <summary>
    /// The plan for one microscene on the freestyle conveyor, in two layers:
    ///
    ///   • GEOMETRY (<see cref="PrismPoints"/> / <see cref="Metas"/> / <see cref="CrystalPoints"/>) -
    ///     pure position/rotation/scale produced by a <see cref="MicroscenePatterns"/> recipe, plus
    ///     per-point structural metadata (substructure id + t-along-path, stamped by
    ///     <see cref="CloseStructure"/>). The recipe knows nothing about domain, kind, or crystal
    ///     type - only shape - so the hand-tuned flyability of each recipe stays untouched by theming.
    ///   • THEMED OUTPUT (<see cref="Prisms"/> / <see cref="Crystals"/>) - produced by
    ///     <see cref="MicroscenePainter"/> from a <see cref="MicroscenePalette"/>: per-prism
    ///     domain (incl. neutral Blue) + <see cref="PrismKind"/> (plain / danger / shielded /
    ///     supershielded) painted along the structural metadata under a coherent per-scene style,
    ///     per-scene scale moods (uniform / long-axis stretch / per-structure taper), and a
    ///     mostly-elemental / occasionally-omni crystal mix. The conveyor consumes THESE.
    ///
    /// Generation is deterministic per seed (instance-local <see cref="System.Random"/> only, never
    /// the global <see cref="UnityEngine.Random"/>) and safe to run incrementally.
    /// </summary>
    public sealed class MicroscenePlan
    {
        public string RecipeName;

        // ── Geometry (recipe output - pure shape + structural metadata) ──────
        public readonly List<SpawnPoint> PrismPoints = new();
        public readonly List<PointMeta> Metas = new();
        public readonly List<Vector3> CrystalPoints = new();

        // ── Themed output (painter output - domain + kind + crystal type) ────
        public readonly List<PrismLay> Prisms = new();
        public readonly List<CrystalDrop> Crystals = new();

        public int FloraCount;
        public int FaunaCount;

        ushort _nextStructure;

        /// <summary>Substructures stamped so far (== the id the NEXT <see cref="CloseStructure"/> uses).</summary>
        public int StructureCount => _nextStructure;

        /// <summary>
        /// Stamp every not-yet-tagged prism point as one substructure, with t assigned linearly in
        /// emission order (primitives emit along their own path, so order ≈ path position). Recipes
        /// call this after each gate / strand / tree / wall; anything left untagged when the plan
        /// finalizes is swept into a single trailing structure, so partial tagging degrades
        /// gracefully instead of desyncing the metadata.
        /// </summary>
        public void CloseStructure()
        {
            int start = Metas.Count, end = PrismPoints.Count;
            int len = end - start;
            if (len <= 0) return;

            ushort id = _nextStructure++;
            for (int i = 0; i < len; i++)
                Metas.Add(new PointMeta(id, len > 1 ? i / (float)(len - 1) : 0.5f));
        }
    }
}
