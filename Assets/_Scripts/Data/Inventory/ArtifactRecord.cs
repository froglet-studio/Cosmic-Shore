using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// ONE unique artifact in an account's hoard (Docs/Pilots/INVENTORY.md §2). No two are the same: each is rolled from
    /// its own 64-bit seed (<see cref="ArtifactGenerator"/>) on top of a payload kind (<see cref="ArchetypeId"/>), and:
    /// <list type="bullet">
    /// <item>accepts exactly ONE element, <see cref="Element"/> (Garrett, 2026-11-07: "only accept one element"),</item>
    /// <item>improves exactly ONE thing with it, the kind's track for that element (<see cref="Aspect"/>),</item>
    /// <item>accepts any number of those crystals (<see cref="Invested"/>, unbounded) with diminishing returns
    /// (<see cref="ArtifactCurve"/>),</item>
    /// <item>keeps the other three tracks at the levels it was rolled with, forever.</item>
    /// </list>
    ///
    /// <para><b>The record is a snapshot.</b> Every rolled trait is stored, not re-derived, so changing the generator's
    /// tables later never rewrites an artifact someone already owns; <see cref="GeneratorVersion"/> says which tables
    /// rolled it. Upgrades live on the artifact (ARTIFACTS.md §4): a stolen artifact keeps every crystal fed to it.</para>
    ///
    /// <para>Public fields and no properties, so Newtonsoft writes exactly these into the cloud record; computed answers
    /// are methods.</para>
    /// </summary>
    [Serializable]
    public class ArtifactRecord
    {
        /// <summary><c>artifact.&lt;kind&gt;#&lt;seed hex&gt;</c> (<see cref="UniqueId"/>). What squad slots save.</summary>
        public string Id;
        /// <summary>The payload kind (<c>ArtifactSO.Id</c>) this artifact transforms payloads like.</summary>
        public string ArchetypeId;
        public int GeneratorVersion;

        /// <summary>Name as rolled, printable ASCII.</summary>
        public string Name;
        /// <summary>The one element it accepts: Mass, Space, Time or Charge.</summary>
        public Element Element;
        /// <summary>The one thing it improves, as the player reads it (the kind's track label for <see cref="Element"/>).</summary>
        public string Aspect;

        /// <summary>The attuned track's level before any crystal (0..<see cref="Ceiling"/>).</summary>
        public float BaseLevel;
        /// <summary>The level the attuned track approaches and never reaches (at most 10).</summary>
        public float Ceiling;
        /// <summary>Crystals that take the attuned track half the way from <see cref="BaseLevel"/> to <see cref="Ceiling"/>.</summary>
        public float HalfCrystals;
        /// <summary>The fixed levels of the four tracks; the attuned element's entry is ignored (it is curve-driven).</summary>
        public float FixedMass, FixedSpace, FixedTime, FixedCharge;

        /// <summary>Every crystal ever fed to it. Unbounded.</summary>
        public long Invested;

        /// <summary>Look, for the art pass: hue 0..1, facet count (3-9), glyph index.</summary>
        public float Hue;
        public int Facets;
        public int Glyph;
        public string Lore;

        /// <summary>Where it came from: starter, voyage, salvage, legacy, dev.</summary>
        public string Origin;
        public long MintedAtMs;

        /// <summary>The template part of the id (the kind), robust to a record whose <see cref="ArchetypeId"/> is blank.</summary>
        public string Kind() => string.IsNullOrEmpty(ArchetypeId) ? UniqueId.TemplateOf(Id) : ArchetypeId;

        /// <summary>The attuned track's level now.</summary>
        public float AttunedLevel() => ArtifactCurve.Level(Invested, BaseLevel, Ceiling, HalfCrystals);

        /// <summary>The attuned track's level after <paramref name="more"/> crystals.</summary>
        public float LevelAfter(long more) => ArtifactCurve.Level(SafeAdd(Invested, more), BaseLevel, Ceiling, HalfCrystals);

        /// <summary>All four levels now: the attuned one from the curve, the rest as rolled.</summary>
        public ArtifactLevels Levels() =>
            new ArtifactLevels(FixedMass, FixedSpace, FixedTime, FixedCharge).With(Element, AttunedLevel());

        /// <summary>The serial players read ("9F3C-01D2").</summary>
        public string Serial() => UniqueId.SerialOf(Id);

        /// <summary>True when this artifact takes <paramref name="crystal"/>: its own element, or Omni (which raises any
        /// one track, ARTIFACTS.md §4).</summary>
        public bool Accepts(Element crystal) => crystal == Element || crystal == Element.Omni;

        public static long SafeAdd(long a, long b)
        {
            if (b <= 0) return a;
            return a > long.MaxValue - b ? long.MaxValue : a + b;
        }

        /// <summary>A copy (records are mutable; the inventory hands out copies only where it must not be aliased).</summary>
        public ArtifactRecord Clone() => (ArtifactRecord)MemberwiseClone();
    }
}
