using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A scale model of a cell whose world is <b>GROWN</b> rather than laid.
    ///
    /// <para>Only three of the arcade's preview cells author an <c>EnvironmentPrefab</c> — the
    /// Boneyard, the Ribcage and the Wildlife cages. The other fourteen have no generator at all:
    /// their arenas are planted by the spawn profile once a match starts, so at the moment a card
    /// is opened there is literally nothing built to sample. That is why those cards showed an
    /// empty frame, and it is data rather than a defect.</para>
    ///
    /// <para>What CAN be shown before anything grows is the <b>planting</b>: how many of each
    /// species, and the band of the cell each one occupies. That is a real, checkable property of
    /// the profile, and for the modes that have one it is also the memorable thing about the arena
    /// — Rampage's cactus belt really is a ring at 0.76–0.94 of the membrane with the core left
    /// open. So this emits one marker per plant, at a planting position drawn the same way the
    /// spawner draws it, and hands the result to the ordinary
    /// <see cref="CellMiniatureBuilder"/>.</para>
    ///
    /// <para><b>It models the PLANTING, not the plants.</b> A grown plant's geometry is emergent —
    /// it depends on how long it has lived and what has eaten it — so a marker stands for "a plant
    /// of this species is here", not for its shape. A cell whose profile plants nothing (the Barren
    /// cell Joust, Scurry and Skim Race run on) correctly yields nothing: that arena IS open
    /// water.</para>
    ///
    /// <para>Fauna are deliberately excluded. They are creatures that move through the arena rather
    /// than part of its shape, and a still model of where they happened to start says nothing
    /// true about it.</para>
    /// </summary>
    public static class ModePreviewPlantingModel
    {
        /// <summary>
        /// The planting of <paramref name="config"/> as lays, or an empty list when the profile
        /// plants nothing.
        /// </summary>
        /// <param name="membraneRadius">The cell's membrane radius — planting bands are fractions
        /// of it, exactly as <c>Flora.ResolvePlantRadius</c> reads them.</param>
        /// <param name="seed">Fixed per card so the same mode always draws the same model. A
        /// preview that reshuffled every time it was opened would read as instability.</param>
        public static List<PrismLay> Build(CellConfigDataSO config, float membraneRadius, int seed)
        {
            var lays = new List<PrismLay>();
            var profile = config ? config.SpawnProfile : null;
            if (!profile || profile.SupportedFloras == null || membraneRadius <= 0f) return lays;

            // Never disturb the global stream: this runs while the menu's own ecology is live, and
            // an ecology is exactly the kind of system whose behaviour depends on its RNG sequence.
            var previousState = Random.state;
            Random.InitState(seed);

            try
            {
                int domainCursor = 0;
                foreach (var flora in profile.SupportedFloras)
                {
                    if (!flora || !flora.FloraPrefab) continue;

                    int count = ResolveCount(profile, flora);
                    if (count <= 0) continue;

                    var band = ResolveBand(flora);
                    float outer = Mathf.Max(0.01f, band.y) * membraneRadius;
                    float inner = Mathf.Clamp(band.x, 0f, band.y) * membraneRadius;

                    // A marker stands for a plant, not for its geometry - see the class summary.
                    // The builder floors shard size for legibility anyway, so this only has to be
                    // in the right ballpark.
                    var scale = Vector3.one * (membraneRadius * MarkerFractionOfMembrane);

                    for (int i = 0; i < count; i++)
                    {
                        var position = Random.onUnitSphere * BandRadius(inner, outer);
                        var rotation = Random.rotationUniform;

                        // Flora take a random playable domain at spawn (CellLifeSpawnerBase
                        // .PickRandomDomain), so cycling the triad is what the real planting looks
                        // like - and it gives the model the same three-colour composition.
                        var domain = PlayableDomains[domainCursor++ % PlayableDomains.Length];

                        lays.Add(new PrismLay(new SpawnPoint(position, rotation, scale), domain));
                    }
                }
            }
            finally
            {
                Random.state = previousState;
            }

            CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                $"[ModePreview] Planting model for '{(config ? config.CellName : "?")}': " +
                $"{lays.Count} plants across {profile.SupportedFloras.Count} species.");

            return lays;
        }

        /// <summary>
        /// Where this species plants in THIS cell, as (inner, outer) fractions of the membrane.
        ///
        /// <para>The prefab's own fractions are only the species' default. <b>The band is a CELL
        /// fact</b> — "coral fills this arena from the nucleus out to 0.95 R" is a layout decision,
        /// not part of what coral is — so a cell states it with
        /// <see cref="FloraConfigurationSO.PlantRadiusCellFractionMaxOverride"/> and its inner
        /// twin, applied at spawn through <c>Flora.ApplyVariantTuning</c> and winning over both the
        /// prefab and the rolled element variant. Rampage's whole cactus belt lives there: read the
        /// prefab alone and every species comes back at the default shell, so the model would draw
        /// a belt the mode does not plant.</para>
        ///
        /// <para>The rolled per-element variant sits between the two and is deliberately skipped —
        /// it is a property of an element this model has not rolled, and the cell override wins
        /// over it anyway wherever a cell has bothered to state one.</para>
        /// </summary>
        static Vector2 ResolveBand(FloraConfigurationSO flora)
        {
            var band = flora.FloraPrefab.PlantingBandFractions;

            // -1 is the established "keep what you have" sentinel on both overrides.
            if (flora.PlantRadiusCellFractionMaxOverride >= 0f)
                band.y = Mathf.Clamp01(flora.PlantRadiusCellFractionMaxOverride);
            if (flora.PlantRadiusCellFractionMinOverride >= 0f)
                band.x = Mathf.Clamp01(flora.PlantRadiusCellFractionMinOverride);

            return band;
        }

        /// <summary>
        /// A seed derived from a name, stable across sessions and machines.
        ///
        /// <para><c>string.GetHashCode</c> is explicitly not guaranteed stable between runs, and a
        /// preview that reshuffled its arena every launch would read as instability in the arena
        /// rather than in the seed.</para>
        /// </summary>
        public static int StableSeed(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;

            unchecked
            {
                int hash = 17;
                foreach (char c in key) hash = hash * 31 + c;
                return hash;
            }
        }

        /// <summary>
        /// How many of a species the cell plants: its authored seed count through the profile's
        /// scale, falling back to the population floor for a species that seeds none but breeds
        /// toward one.
        /// </summary>
        static int ResolveCount(SpawnProfileSO profile, FloraConfigurationSO flora)
        {
            int authored = flora.InitialSpawnCount > 0 ? flora.InitialSpawnCount : flora.PopulationSize;
            if (authored <= 0) return 0;

            int scaled = profile.ScaleFloraPopulation(authored);

            // A cap on the MODEL, not on the cell: a species seeding thousands would swamp both the
            // vertex budget and the silhouette, and past a few hundred markers the band reads
            // exactly the same.
            return Mathf.Clamp(scaled, 0, MaxMarkersPerSpecies);
        }

        /// <summary>
        /// Volume-uniform across the shell, the same draw <c>CellLifeSpawnerBase.RandomBandRadius</c>
        /// makes. A uniform-in-radius draw would crowd the inner edge — measured at 63% of a
        /// population inside the innermost quarter-volume — so a model that used one would show a
        /// band the cell does not actually plant.
        /// </summary>
        static float BandRadius(float inner, float outer)
        {
            float i3 = inner * inner * inner;
            float o3 = outer * outer * outer;
            return Mathf.Pow(Random.Range(i3, o3), 1f / 3f);
        }

        static readonly Domains[] PlayableDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        /// <summary>Marker size relative to the membrane. Small: the BAND is what reads.</summary>
        const float MarkerFractionOfMembrane = 0.02f;

        /// <summary>Past this the band looks identical and the vertices are wasted.</summary>
        const int MaxMarkersPerSpecies = 400;
    }
}
