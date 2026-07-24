using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turns a recipe's pure geometry into themed <see cref="MicroscenePlan.Prisms"/> /
    /// <see cref="MicroscenePlan.Crystals"/>. Painting is STRUCTURAL: every scheme keys off the
    /// plan's substructure metadata (<see cref="PointMeta"/>: which gate/strand/tree, t-along-path)
    /// or spatial coordinates (flight-z, angle around the axis, port/starboard side), never bare
    /// point indices - so colour and kind land as deliberate features of the construction:
    ///
    ///   • DOMAIN - alternating whole gates, gradients that shift as you fly through, pinwheel
    ///     sectors, candy-stripes, port/starboard splits, sparse accents, neutral-Blue veins, mono.
    ///   • KIND - a whole substructure of danger (a gate of fire to thread or deliberately skim for
    ///     the Squirrel's danger boost), danger tips on arm/blade ends, shielded ribs armouring one
    ///     frame, a supershielded keystone guarding the crystal, loose sprinkles, all-plain.
    ///     Danger prisms are dangerous to EVERY domain (locked design) - on the belt they are pure
    ///     risk/reward furniture. Shielded/supershielded carry an always-on convex MeshCollider, so
    ///     the palette's hard caps are enforced unconditionally at the end of every paint.
    ///   • SCALE MOODS - a uniform grand/delicate mood, a long-axis stretch (wiry vs. chunky), and
    ///     per-structure taper (root-thick tips or outward flares) riding the structure-t.
    ///
    /// Deterministic per rng (instance-local <see cref="System.Random"/> only).
    /// </summary>
    public static class MicroscenePainter
    {
        enum DomainScheme { Mono = 0, PerStructure = 1, Gradient = 2, Accent = 3, Radial = 4, Stripe = 5, Mirror = 6, NeutralVein = 7 }
        enum KindScheme { AllPlain = 0, DangerAccent = 1, DangerStructure = 2, DangerTips = 3, ShieldAccent = 4, ShieldFrame = 5, Landmark = 6 }

        static readonly Domains[] DefaultDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        public static void Paint(MicroscenePlan plan, System.Random rng, MicroscenePalette pal)
        {
            pal ??= MicroscenePalette.Default;
            var domains = pal.PlayableDomains is { Length: > 0 } ? pal.PlayableDomains : DefaultDomains;

            int n = plan.PrismPoints.Count;
            var domainOf = AssignDomains(plan, rng, pal, domains, n);
            var kindOf = AssignKinds(plan, rng, pal, n);
            EnforceKindCaps(kindOf, pal);

            // Scale moods - rolled per scene, applied per prism (uniform × longest-axis stretch ×
            // structure taper). All factors are strictly positive by construction.
            float mood = rng.NextDouble() < pal.ScaleMoodChance ? PrismGeometry.Range(rng, pal.ScaleMoodMin, pal.ScaleMoodMax) : 1f;
            float stretch = rng.NextDouble() < pal.StretchMoodChance ? PrismGeometry.Range(rng, pal.StretchMin, pal.StretchMax) : 1f;
            float taperTip = rng.NextDouble() < pal.TaperChance ? PrismGeometry.Range(rng, pal.TaperTipMin, pal.TaperTipMax) : 1f;

            plan.Prisms.Clear();
            for (int i = 0; i < n; i++)
            {
                var p = plan.PrismPoints[i];
                Vector3 scale = p.Scale * mood;

                if (!Mathf.Approximately(stretch, 1f))
                {
                    // Stretch each prism along its own longest local axis, whichever that is per
                    // family (strand z, trunk y, plate faces) - the scene reads wiry or chunky as
                    // one gesture without breaking any prism's proportional identity.
                    if (scale.x >= scale.y && scale.x >= scale.z) scale.x *= stretch;
                    else if (scale.y >= scale.z) scale.y *= stretch;
                    else scale.z *= stretch;
                }

                if (!Mathf.Approximately(taperTip, 1f))
                    scale *= Mathf.Lerp(1f, taperTip, MetaAt(plan, i).T);

                plan.Prisms.Add(new PrismLay(new SpawnPoint(p.Position, p.Rotation, scale), domainOf[i], kindOf[i]));
            }

            plan.Crystals.Clear();
            foreach (var pos in plan.CrystalPoints)
            {
                var kind = rng.NextDouble() < pal.OmniCrystalChance ? CrystalKind.Omni : CrystalKind.Elemental;
                plan.Crystals.Add(new CrystalDrop(pos, kind));
            }
        }

        static PointMeta MetaAt(MicroscenePlan plan, int i) =>
            i < plan.Metas.Count ? plan.Metas[i] : new PointMeta(0, 0.5f);

        // ── Domain painting ──────────────────────────────────────────────────

        static Domains[] AssignDomains(MicroscenePlan plan, System.Random rng, MicroscenePalette pal,
            Domains[] domains, int count)
        {
            var result = new Domains[count];
            if (count == 0) return result;

            var scheme = (DomainScheme)WeightedIndex(rng,
                pal.MonoWeight, pal.BandedWeight, pal.GradientWeight, pal.AccentWeight,
                pal.RadialWeight, pal.StripeWeight, pal.MirrorWeight, pal.NeutralVeinWeight);

            // Every multi-domain scheme draws from a per-scene shuffled order, so "gate 1 is Jade"
            // isn't a fixed rule of the belt.
            var order = Shuffled(rng, domains);

            switch (scheme)
            {
                case DomainScheme.PerStructure:
                {
                    // Each substructure one domain, cycling the shuffled order - alternating gates,
                    // per-strand ribbons, per-tree groves.
                    for (int i = 0; i < count; i++)
                        result[i] = order[MetaAt(plan, i).Structure % order.Length];
                    break;
                }
                case DomainScheme.Gradient:
                {
                    // Bands along the flight axis - the scene changes colour as you fly through it.
                    float zMin = float.MaxValue, zMax = float.MinValue;
                    for (int i = 0; i < count; i++)
                    {
                        float z = plan.PrismPoints[i].Position.z;
                        if (z < zMin) zMin = z;
                        if (z > zMax) zMax = z;
                    }
                    float span = Mathf.Max(0.001f, zMax - zMin);
                    int bands = order.Length;
                    for (int i = 0; i < count; i++)
                    {
                        float zn = (plan.PrismPoints[i].Position.z - zMin) / span;
                        result[i] = order[Mathf.Min(bands - 1, (int)(zn * bands))];
                    }
                    break;
                }
                case DomainScheme.Accent:
                {
                    var baseDomain = order[0];
                    var accent = order[order.Length > 1 ? 1 : 0];
                    for (int i = 0; i < count; i++)
                        result[i] = rng.NextDouble() < pal.AccentChance ? accent : baseDomain;
                    break;
                }
                case DomainScheme.Radial:
                {
                    // Pinwheel sectors around the flight axis - hoops, turbines, and rosettes come
                    // out as wedges of colour.
                    float offset = PrismGeometry.Range(rng, 0f, Mathf.PI * 2f);
                    int sectors = order.Length;
                    for (int i = 0; i < count; i++)
                    {
                        var pos = plan.PrismPoints[i].Position;
                        float a = Mathf.Repeat(Mathf.Atan2(pos.y, pos.x) + offset, Mathf.PI * 2f);
                        result[i] = order[Mathf.Min(sectors - 1, (int)(a / (Mathf.PI * 2f) * sectors))];
                    }
                    break;
                }
                case DomainScheme.Stripe:
                {
                    // Candy-stripe: domains alternate point-by-point ALONG each substructure (path
                    // order), so hoops and ribbons band like rock sugar.
                    int stripe = Mathf.Max(1, RangeInt(rng, 1, 4)); // run length per colour
                    var ordinal = OrdinalWithinStructure(plan, count);
                    for (int i = 0; i < count; i++)
                        result[i] = order[(ordinal[i] / stripe) % order.Length];
                    break;
                }
                case DomainScheme.Mirror:
                {
                    // Port/starboard (or above/below) split about the flight line.
                    bool byX = rng.Next(2) == 0;
                    var d0 = order[0];
                    var d1 = order[order.Length > 1 ? 1 : 0];
                    for (int i = 0; i < count; i++)
                    {
                        var pos = plan.PrismPoints[i].Position;
                        result[i] = (byX ? pos.x : pos.y) >= 0f ? d0 : d1;
                    }
                    break;
                }
                case DomainScheme.NeutralVein:
                {
                    var baseDomain = order[0];
                    for (int i = 0; i < count; i++)
                        result[i] = rng.NextDouble() < pal.BlueVeinChance ? Domains.Blue : baseDomain;
                    break;
                }
                default: // Mono
                {
                    var only = order[0];
                    for (int i = 0; i < count; i++) result[i] = only;
                    break;
                }
            }
            return result;
        }

        // ── Kind painting ────────────────────────────────────────────────────

        static PrismKind[] AssignKinds(MicroscenePlan plan, System.Random rng, MicroscenePalette pal, int count)
        {
            var kinds = new PrismKind[count]; // default Plain
            if (count == 0) return kinds;

            var scheme = (KindScheme)WeightedIndex(rng,
                pal.AllPlainWeight, pal.DangerAccentWeight, pal.DangerStructureWeight,
                pal.DangerTipsWeight, pal.ShieldAccentWeight, pal.ShieldFrameWeight, pal.LandmarkWeight);

            switch (scheme)
            {
                case KindScheme.DangerAccent:
                    Sprinkle(kinds, rng, PrismKind.Danger, Mathf.Min(pal.MaxDanger, Mathf.Max(1, count / 8)));
                    break;

                case KindScheme.DangerStructure:
                {
                    // One whole substructure hot - a gate of fire, a burning strand. Pick among
                    // structures small enough to fit the cap so the read stays complete.
                    if (!TryMarkWholeStructure(plan, rng, kinds, PrismKind.Danger, 3, pal.MaxDanger))
                        MarkStructureTips(plan, rng, kinds, pal.MaxDanger); // fallback: hot tips
                    break;
                }

                case KindScheme.DangerTips:
                    MarkStructureTips(plan, rng, kinds, pal.MaxDanger);
                    break;

                case KindScheme.ShieldAccent:
                    Sprinkle(kinds, rng, PrismKind.Shielded, Mathf.Min(pal.MaxShielded, Mathf.Max(1, count / 16)));
                    break;

                case KindScheme.ShieldFrame:
                    // Evenly spaced shielded ribs along ONE substructure - an armoured frame the
                    // player reads as load-bearing, not noise.
                    MarkStructureRibs(plan, rng, kinds, pal.MaxShielded);
                    break;

                case KindScheme.Landmark:
                {
                    // The keystone: supershield the prism nearest the (first) crystal - the landmark
                    // guards the prize - with a small shielded entourage around it.
                    Vector3 heart = plan.CrystalPoints.Count > 0 ? plan.CrystalPoints[0] : Vector3.zero;
                    MarkKeystone(plan, kinds, heart, pal.MaxSuperShielded, pal.MaxShielded);
                    break;
                }
                // AllPlain: leave every prism plain.
            }
            return kinds;
        }

        /// <summary>Mark every point of one randomly-picked substructure whose size fits
        /// [minSize, maxSize]. Returns false when no structure qualifies.</summary>
        static bool TryMarkWholeStructure(MicroscenePlan plan, System.Random rng, PrismKind[] kinds,
            PrismKind kind, int minSize, int maxSize)
        {
            var sizes = StructureSizes(plan, kinds.Length);
            var candidates = new List<int>();
            for (int s = 0; s < sizes.Length; s++)
                if (sizes[s] >= minSize && sizes[s] <= maxSize)
                    candidates.Add(s);
            if (candidates.Count == 0) return false;

            int pick = candidates[rng.Next(candidates.Count)];
            for (int i = 0; i < kinds.Length; i++)
                if (MetaAt(plan, i).Structure == pick)
                    kinds[i] = kind;
            return true;
        }

        /// <summary>Danger on the far ends (t ≥ 0.78) of every structure big enough to have a
        /// readable tip, up to the cap - hot spire/arm/blade ends.</summary>
        static void MarkStructureTips(MicroscenePlan plan, System.Random rng, PrismKind[] kinds, int cap)
        {
            var sizes = StructureSizes(plan, kinds.Length);
            int marked = 0;
            for (int i = 0; i < kinds.Length && marked < cap; i++)
            {
                var meta = MetaAt(plan, i);
                if (sizes[meta.Structure] >= 4 && meta.T >= 0.78f)
                {
                    kinds[i] = PrismKind.Danger;
                    marked++;
                }
            }
            if (marked == 0) // no structure has a tail - degrade to a light sprinkle
                Sprinkle(kinds, rng, PrismKind.Danger, Mathf.Min(cap, Mathf.Max(1, kinds.Length / 10)));
        }

        /// <summary>Up to <paramref name="cap"/> shielded ribs evenly spaced along one substructure.</summary>
        static void MarkStructureRibs(MicroscenePlan plan, System.Random rng, PrismKind[] kinds, int cap)
        {
            if (cap <= 0) return;
            var sizes = StructureSizes(plan, kinds.Length);
            var candidates = new List<int>();
            for (int s = 0; s < sizes.Length; s++)
                if (sizes[s] >= 3)
                    candidates.Add(s);
            if (candidates.Count == 0)
            {
                Sprinkle(kinds, rng, PrismKind.Shielded, Mathf.Min(cap, 1));
                return;
            }

            int pick = candidates[rng.Next(candidates.Count)];
            int ribs = Mathf.Min(cap, Mathf.Max(2, sizes[pick] / 3));
            // Rib at evenly spaced t stations: mark the first point at-or-past each station.
            int placed = 0;
            float nextStation = 0f;
            float step = 1f / Mathf.Max(1, ribs - 1);
            for (int i = 0; i < kinds.Length && placed < ribs; i++)
            {
                var meta = MetaAt(plan, i);
                if (meta.Structure != pick || meta.T < nextStation) continue;
                kinds[i] = PrismKind.Shielded;
                placed++;
                nextStation += step;
            }
        }

        /// <summary>Supershield the prism nearest <paramref name="heart"/>, plus the next-nearest
        /// few as a shielded entourage.</summary>
        static void MarkKeystone(MicroscenePlan plan, PrismKind[] kinds, Vector3 heart,
            int superCap, int shieldCap)
        {
            if (superCap <= 0 && shieldCap <= 0) return;
            int n = kinds.Length;
            var byDistance = new List<(float dist, int idx)>(n);
            for (int i = 0; i < n; i++)
                byDistance.Add(((plan.PrismPoints[i].Position - heart).sqrMagnitude, i));
            byDistance.Sort((a, b) => a.dist.CompareTo(b.dist));

            int cursor = 0;
            for (int s = 0; s < superCap && cursor < n; s++, cursor++)
                kinds[byDistance[cursor].idx] = PrismKind.SuperShielded;
            int entourage = Mathf.Min(shieldCap, Mathf.Max(1, n / 20));
            for (int s = 0; s < entourage && cursor < n; s++, cursor++)
                kinds[byDistance[cursor].idx] = PrismKind.Shielded;
        }

        /// <summary>
        /// Unconditional collider-budget backstop: whatever the scheme painted, the scene never
        /// exceeds the palette caps (shielded/supershielded ride an always-on convex MeshCollider;
        /// danger is capped for gameplay readability). Overflow demotes to Plain, later points first.
        /// </summary>
        static void EnforceKindCaps(PrismKind[] kinds, MicroscenePalette pal)
        {
            int danger = 0, shielded = 0, super = 0;
            for (int i = 0; i < kinds.Length; i++)
            {
                switch (kinds[i])
                {
                    case PrismKind.Danger when ++danger > pal.MaxDanger: kinds[i] = PrismKind.Plain; break;
                    case PrismKind.Shielded when ++shielded > pal.MaxShielded: kinds[i] = PrismKind.Plain; break;
                    case PrismKind.SuperShielded when ++super > pal.MaxSuperShielded: kinds[i] = PrismKind.Plain; break;
                }
            }
        }

        // ── Shared helpers ───────────────────────────────────────────────────

        static int[] StructureSizes(MicroscenePlan plan, int count)
        {
            int structures = Mathf.Max(1, plan.StructureCount);
            var sizes = new int[structures];
            for (int i = 0; i < count; i++)
            {
                int s = MetaAt(plan, i).Structure;
                if (s < structures) sizes[s]++;
            }
            return sizes;
        }

        /// <summary>Each point's ordinal within its own substructure (path order).</summary>
        static int[] OrdinalWithinStructure(MicroscenePlan plan, int count)
        {
            var ordinal = new int[count];
            var counters = new int[Mathf.Max(1, plan.StructureCount)];
            for (int i = 0; i < count; i++)
            {
                int s = MetaAt(plan, i).Structure;
                if (s >= counters.Length) s = counters.Length - 1;
                ordinal[i] = counters[s]++;
            }
            return ordinal;
        }

        static void Sprinkle(PrismKind[] kinds, System.Random rng, PrismKind kind, int n)
        {
            int placed = 0, guard = 0, cap = kinds.Length * 4;
            while (placed < n && guard++ < cap)
            {
                int idx = rng.Next(kinds.Length);
                if (kinds[idx] != PrismKind.Plain) continue;
                kinds[idx] = kind;
                placed++;
            }
        }

        static Domains[] Shuffled(System.Random rng, Domains[] domains)
        {
            var order = (Domains[])domains.Clone();
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }

        static int RangeInt(System.Random rng, int minInclusive, int maxExclusive) =>
            rng.Next(minInclusive, maxExclusive);

        static int WeightedIndex(System.Random rng, params float[] weights)
        {
            float total = 0f;
            foreach (var w in weights) total += Mathf.Max(0f, w);
            float roll = (float)(rng.NextDouble() * Mathf.Max(0.0001f, total));
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= Mathf.Max(0f, weights[i]);
                if (roll < 0f) return i;
            }
            return weights.Length - 1;
        }
    }
}
