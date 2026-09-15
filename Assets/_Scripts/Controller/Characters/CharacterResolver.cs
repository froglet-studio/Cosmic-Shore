using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Genome + clade assets + config → <see cref="CharacterBlueprint"/>. Pure and deterministic.
    ///
    /// THE TWO RULES that make a chimera a chimera live here, and only here:
    /// <list type="bullet">
    /// <item><b>Continuous axes blend.</b> Each axis is a weighted mix of the human's rolled
    /// proportions and each clade's authored target, under an effective weight
    /// <c>w^γ</c> (normalised) so a 0.20 clade is still visible when γ &lt; 1. This is the file for
    /// "20% clade isn't enough to see" — <see cref="CharacterGenerationConfigSO.TravelGamma"/>.</item>
    /// <item><b>Categorical traits are BOUGHT by weight and never blended.</b> A clade's ladder is
    /// walked from its signature down; each expressed trait CLAIMS slots; a slot holds one
    /// winner (signature &gt; non-signature &gt; human default, then weight, then clade A). A
    /// trait is placed if it wins its FIRST slot; its other claims only evict competitors. This is
    /// the file for "Corvidae doesn't read" when the cause is the LADDER (what a weight buys) —
    /// the beak itself is <see cref="BeakFeature"/>, the feathers are the covering.</item>
    /// </list>
    /// </summary>
    public static class CharacterResolver
    {
        public static CharacterBlueprint Resolve(CharacterGenome genome, CladeCatalog catalog, CharacterGenerationConfigSO config)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (config == null) throw new ArgumentNullException(nameof(config));
            var human = config.HumanSource != null ? config.HumanSource : catalog.Human;
            if (human == null) throw new InvalidOperationException("CharacterResolver: no human source (CharacterGenerationConfigSO.HumanSource / a CladeSO with IsHuman).");

            var bp = new CharacterBlueprint { Genome = genome, Human = human };
            if (!genome.IsPureHuman)
            {
                bp.CladeA = catalog.Require(genome.CladeA);
                bp.CladeB = catalog.Require(genome.CladeB);
                bp.EffectiveWeights = genome.Weights.IsValid
                    ? genome.Weights
                    : CharacterWeights.Constrain(genome.Weights.Human, genome.Weights.CladeA, genome.Weights.CladeB);
            }
            else
            {
                bp.EffectiveWeights = new CharacterWeights { Human = 1f, CladeA = 0f, CladeB = 0f };
            }

            ResolveShape(bp, config);
            ResolveTraits(bp, config);
            ResolveCoverings(bp);
            bp.Genome.ExpressedTraits = bp.ExpressedTraitIds.ToArray();
            return bp;
        }

        // ------------------------------------------------------------------ continuous

        static void ResolveShape(CharacterBlueprint bp, CharacterGenerationConfigSO config)
        {
            var g = bp.Genome;
            var w = bp.EffectiveWeights;
            float gamma = Mathf.Max(0.05f, config.TravelGamma);
            float eh = Mathf.Pow(Mathf.Max(0f, w.Human), gamma);
            float ea = bp.CladeA ? Mathf.Pow(Mathf.Max(0f, w.CladeA), gamma) : 0f;
            float eb = bp.CladeB ? Mathf.Pow(Mathf.Max(0f, w.CladeB), gamma) : 0f;
            float sum = eh + ea + eb;
            if (sum <= 0f) { eh = 1f; sum = 1f; }
            eh /= sum; ea /= sum; eb /= sum;

            var shape = HeadShape.Neutral;
            var baseShape = g.BaseShape.IsInitialized ? g.BaseShape : HeadShape.Neutral;
            for (int i = 0; i < HeadShape.AxisCount; i++)
            {
                var axis = (HeadAxis)i;
                float v = eh * baseShape[axis];
                if (bp.CladeA) v += ea * bp.CladeA.AxisTargetFor(axis) * config.CladeAxisTravel;
                if (bp.CladeB) v += eb * bp.CladeB.AxisTargetFor(axis) * config.CladeAxisTravel;
                shape[axis] = v;
            }

            // Global proportions the species do not own.
            float flesh = Mathf.Clamp01(g.Fleshiness) - 0.5f;
            shape[HeadAxis.JawWidth] += flesh * 0.35f;
            shape[HeadAxis.CheekboneWidth] -= flesh * 0.30f;
            shape[HeadAxis.NeckThickness] += flesh * 0.40f;
            float age = Mathf.Clamp01(g.Age);
            shape[HeadAxis.LipFullness] -= age * 0.35f;
            shape[HeadAxis.BrowRidge] += age * 0.10f;
            bp.Shape = shape;
        }

        // ------------------------------------------------------------------ categorical

        struct Claim
        {
            public TraitDefinition Trait;
            public CladeSO Source;
            public float Weight;
            public int Priority;   // 3 signature, 2 clade rung, 1 human default
            public int Order;      // 0 human, 1 A, 2 B — the final tie-break
        }

        static bool Beats(in Claim a, in Claim b)
        {
            if (a.Priority != b.Priority) return a.Priority > b.Priority;
            if (Mathf.Abs(a.Weight - b.Weight) > 1e-5f) return a.Weight > b.Weight;
            return a.Order < b.Order;
        }

        public static float RungThreshold(TraitDefinition trait, int index, int count)
        {
            if (trait.MinWeight > 0f) return trait.MinWeight;
            if (trait.Signature) return 0f;
            if (count <= 1) return CharacterWeights.Min;
            return CharacterWeights.Min + (CharacterWeights.Max - CharacterWeights.Min) * (index / (float)count);
        }

        static void ResolveTraits(CharacterBlueprint bp, CharacterGenerationConfigSO config)
        {
            var claims = new List<Claim>();
            void Express(CladeSO source, float weight, int order)
            {
                if (source == null || source.Traits == null) return;
                int n = source.Traits.Length;
                for (int i = 0; i < n; i++)
                {
                    var t = source.Traits[i];
                    if (t == null) continue;
                    bool expressed = source.IsHuman || weight + 1e-4f >= RungThreshold(t, i, n);
                    if (!expressed) continue;
                    claims.Add(new Claim
                    {
                        Trait = t, Source = source, Weight = weight, Order = order,
                        Priority = source.IsHuman ? 1 : (t.Signature ? 3 : 2),
                    });
                }
            }
            Express(bp.Human, bp.EffectiveWeights.Human, 0);
            Express(bp.CladeA, bp.EffectiveWeights.CladeA, 1);
            Express(bp.CladeB, bp.EffectiveWeights.CladeB, 2);

            // Slot winners.
            var winner = new Dictionary<FeatureSlot, Claim>();
            foreach (var c in claims)
            {
                if (c.Trait.Slots == null) continue;
                foreach (var slot in c.Trait.Slots)
                {
                    if (!winner.TryGetValue(slot, out var current) || Beats(c, current)) winner[slot] = c;
                }
            }

            // Placement: a trait lands if it has no slot claims, or it won its first slot.
            foreach (var c in claims)
            {
                var t = c.Trait;
                bool placed = t.Slots == null || t.Slots.Count == 0
                              || (winner.TryGetValue(t.Slots[0], out var win) && ReferenceEquals(win.Trait, t));
                if (!placed) continue;
                bp.ExpressedTraitIds.Add($"{c.Source.Key}.{t.Id}");
                if (t.AxisOverrides != null)
                    foreach (var o in t.AxisOverrides) bp.Shape[o.Axis] = o.Value;
                if (t.Feature == FeatureKind.None) continue;
                bp.Features.Add(new ResolvedFeature
                {
                    TraitId = t.Id, SourceKey = c.Source.Key, Kind = t.Feature,
                    Site = string.IsNullOrEmpty(t.Site) ? FeatureCatalog.DefaultSite(t.Feature) : t.Site,
                    Signature = t.Signature, Weight = c.Weight, Params = t.Params ?? new FeatureParams(),
                    SitePitchDeg = TiltOf(t, 0), SiteYawDeg = TiltOf(t, 1), SiteRollDeg = TiltOf(t, 2),
                });
            }

            // Gear: worn by the genome, never by a clade. Goggles ON claim the Eyes' worn space
            // visually but not the slot — the eyes are still generated under them.
            if (bp.Genome.Gear != GearKind.None)
                bp.Features.Add(new ResolvedFeature
                {
                    TraitId = bp.Genome.Gear.ToString(), SourceKey = "Gear", Kind = FeatureKind.Goggles,
                    Site = FeatureCatalog.DefaultSite(FeatureKind.Goggles), Signature = false, Weight = 0f, Params = new FeatureParams(),
                });

            // Slot-derived facts the painter and assembler read.
            bp.HasBeak = bp.HasFeature(FeatureKind.Beak);
            bp.HasMandibles = bp.HasFeature(FeatureKind.Mandibles);
            bp.HasCompoundEyes = bp.HasFeature(FeatureKind.CompoundEye);
            bp.HasBlowhole = bp.HasFeature(FeatureKind.Blowhole);
            bp.HasHumanNose = winner.TryGetValue(FeatureSlot.Nose, out var nose) && nose.Source.IsHuman;
            bp.HasHumanMouth = winner.TryGetValue(FeatureSlot.Mouth, out var mouth) && mouth.Source.IsHuman;

            // Eye recipe: the Eyes winner's params, else the human's.
            bp.Eye = null;
            if (winner.TryGetValue(FeatureSlot.Eyes, out var eyes) && eyes.Trait.Params != null) bp.Eye = eyes.Trait.Params.Eye;
            if (bp.Eye == null)
            {
                var humanEye = FindTrait(bp.Human, FeatureKind.VertebrateEye);
                bp.Eye = humanEye != null && humanEye.Params != null ? humanEye.Params.Eye : new EyeParams();
            }

            // Keratin: the heaviest keratin-bearing feature names the colour; otherwise pale.
            bp.KeratinA = new Color(0.90f, 0.86f, 0.78f);
            bp.KeratinB = new Color(0.78f, 0.72f, 0.60f);
            float best = -1f;
            foreach (var f in bp.Features)
            {
                Color a, b;
                switch (f.Kind)
                {
                    case FeatureKind.Beak: a = f.Params.Beak.KeratinA; b = f.Params.Beak.KeratinB; break;
                    case FeatureKind.Mandibles: a = f.Params.Mandible.KeratinA; b = f.Params.Mandible.KeratinB; break;
                    case FeatureKind.Antennae: a = f.Params.Antenna.KeratinA; b = f.Params.Antenna.KeratinB; break;
                    default: continue;
                }
                if (f.Weight > best) { best = f.Weight; bp.KeratinA = a; bp.KeratinB = b; }
            }
        }

        static TraitDefinition FindTrait(CladeSO clade, FeatureKind kind)
        {
            if (clade == null || clade.Traits == null) return null;
            foreach (var t in clade.Traits) if (t != null && t.Feature == kind) return t;
            return null;
        }

        static float TiltOf(TraitDefinition t, int component) => component switch
        {
            0 => t.SitePitchDeg,
            1 => t.SiteYawDeg,
            _ => t.SiteRollDeg,
        };

        // ------------------------------------------------------------------ coverings

        static void ResolveCoverings(CharacterBlueprint bp)
        {
            bp.Coverings.Add(new CoveringLayer { SourceKey = bp.Human.Key, Recipe = bp.Human.Covering, Weight = bp.EffectiveWeights.Human, ReachDeg = 180f, IsHuman = true });
            AddCovering(bp, bp.CladeA, bp.EffectiveWeights.CladeA);
            AddCovering(bp, bp.CladeB, bp.EffectiveWeights.CladeB);
        }

        static void AddCovering(CharacterBlueprint bp, CladeSO clade, float weight)
        {
            if (clade == null || clade.Covering == null) return;
            float t = Mathf.InverseLerp(CharacterWeights.Min, CharacterWeights.Max, weight);
            bp.Coverings.Add(new CoveringLayer
            {
                SourceKey = clade.Key, Recipe = clade.Covering, Weight = weight,
                ReachDeg = Mathf.Lerp(clade.Covering.ReachDegAtMin, clade.Covering.ReachDegAtMax, t),
            });
        }
    }
}
