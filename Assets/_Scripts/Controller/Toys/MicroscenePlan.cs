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
    /// The plan for one microscene on the freestyle conveyor, in two layers:
    ///
    ///   • GEOMETRY (<see cref="PrismPoints"/> / <see cref="CrystalPoints"/>) - pure
    ///     position/rotation/scale produced by a <see cref="MicroscenePatterns"/> recipe. The recipe
    ///     knows nothing about domain, kind, or crystal type - only shape - so the hand-tuned
    ///     flyability of each recipe stays untouched by theming.
    ///   • THEMED OUTPUT (<see cref="Prisms"/> / <see cref="Crystals"/>) - produced by
    ///     <c>MicroscenePatterns.ApplyTheming</c> from a <see cref="MicroscenePalette"/>: per-prism
    ///     domain (incl. neutral Blue) + <see cref="PrismKind"/> (plain / danger / shielded /
    ///     supershielded) under a coherent per-scene style, a per-scene scale mood, and a
    ///     mostly-elemental / occasionally-omni crystal mix. The conveyor consumes THESE.
    ///
    /// Generation is deterministic per seed (instance-local <see cref="System.Random"/> only, never
    /// the global <see cref="UnityEngine.Random"/>) and safe to run incrementally.
    /// </summary>
    public sealed class MicroscenePlan
    {
        public string RecipeName;

        // ── Geometry (recipe output - pure shape) ────────────────────────────
        public readonly List<SpawnPoint> PrismPoints = new();
        public readonly List<Vector3> CrystalPoints = new();

        // ── Themed output (Finalize output - domain + kind + crystal type) ───
        public readonly List<PrismLay> Prisms = new();
        public readonly List<CrystalDrop> Crystals = new();

        public int FloraCount;
        public int FaunaCount;
    }
}
