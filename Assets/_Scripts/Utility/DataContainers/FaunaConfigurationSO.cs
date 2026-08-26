using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace CosmicShore.Utility
{
    [CreateAssetMenu(
        fileName = "FaunaConfiguration",
        menuName = "ScriptableObjects/DataContainers/" + nameof(FaunaConfigurationSO))]
    public class FaunaConfigurationSO : ScriptableObject
    {
        public Fauna FaunaPrefab;
        public int InitialSpawnCount;
        [Tooltip("SEED FLOOR for this species: the periodic spawner only tops the live population " +
                 "back up to this count (bootstrap + extinction recovery). Above the floor the " +
                 "population is driven by REPRODUCTION and bounded by starvation/predation - the " +
                 "food web, not the spawner. See Docs/ECOSYSTEM.md §6.")]
        [Min(1)] public int PopulationSize = 4;
        public float SpawnProbability;

        [Header("Reproduction (the population driver - see Docs/ECOSYSTEM.md §6)")]
        [Tooltip("Feeds (prism consumes; kills for predators) an individual needs per birth. " +
                 "0 = this species does not reproduce; the seed floor is then its only source. " +
                 "Lower = prey converts to population faster (steeper Lotka-Volterra upswing).")]
        [Min(0)] public int FeedsPerOffspring = 0;
        [Tooltip("Offspring spawned per birth.")]
        [Min(1)] public int OffspringPerBirth = 1;
        [Tooltip("Minimum seconds between births per individual. Throttles bursts when an " +
                 "individual is gorging on a dense buildup.")]
        [Min(0f)] public float ReproductionCooldownSeconds = 10f;
        [Tooltip("Hard per-cell cap on this species' live population. A PERFORMANCE backstop, " +
                 "not the primary control (starvation is) - size it to what the frame budget " +
                 "tolerates, not to where you want the population to sit. 0 = uncapped.")]
        [Min(0)] public int MaxLivePopulation = 0;

        [Header("Deployment behavior")]
        [Tooltip("Staged release: this species may seed only once its cell's " +
                 "Cell.FaunaReleaseTier reaches this value. 0 (the default, and what every " +
                 "shipped biome authors) means 'released from the start' - a cell's tier " +
                 "defaults to int.MaxValue, so nothing changes unless a mode stages it. " +
                 "Ribcage holds its brood closed until the leader cracks 25% of the cage " +
                 "(tier 0 = the grazer swarm) and adds the predator at 50% (tier 1). " +
                 "Gating PRODUCTION is allowed by the conserved-mass law; culling is not.")]
        [Min(0)] public int ReleaseTier = 0;

        [Header("Spatial band - this species' own pen (0 = unpenned, every shipped biome)")]
        [Tooltip("INNER radius (world units from the cell centre) of the annulus this species " +
                 "is penned to. 0 = no inner wall. See BandOuterRadius.")]
        [Min(0f)] public float BandInnerRadius = 0f;
        [Tooltip("OUTER radius of this species' band. 0 (the default, and what every shipped " +
                 "biome authors) means NO BAND - the species roams the whole cell exactly as " +
                 "before. Set both to pen a species to a shell: Wildlife Liberation stacks three " +
                 "nested cages and gives each tier of creature the band of its own cage, so the " +
                 "tadpole swarm rides the outer shell and the kaiju stays in the core.\n\n" +
                 "This GENERALIZES Cell.FaunaContainmentRadius (one radius, whole cell) to an " +
                 "annulus authored PER SPECIES, and carries the same contract: a spatial DIET + " +
                 "STEERING rule, never a wall. Nothing is teleported, no collider is added and " +
                 "nothing is culled for crossing it - mass outside the band is simply not food " +
                 "and every goal is clamped back in. The two compose (cell pen first, then band), " +
                 "and offspring inherit their parent's band because they bind the same config.")]
        [Min(0f)] public float BandOuterRadius = 0f;

        [Tooltip("0-1: pulls this species' roaming goal toward the cell centre so it spends " +
                 "more time on the central canopy (the gyroids around the nucleus). Herbivores " +
                 "only — predators hunt prey, not places. Leave 0 (default) for deployments " +
                 "that must range far from centre (e.g. the Skim Race track-cleanup swarm).")]
        [Range(0f, 1f)] public float CenterFocusBias = 0f;

        [Header("Elemental contract - one base prefab, FOUR data-defined variants")]
        [Tooltip("The element this species config spawns as. None = keep the prefab-authored " +
                 "crystal element (the legacy per-element prefab-variant path). Setting it lets " +
                 "ONE base prefab serve all four element variants: the heart crystal is " +
                 "provisioned from ElementalCrystalSet at lineage assignment.")]
        public Element Element = Element.None;

        [Tooltip("Per-variant expression applied on top of the base prefab at AssignLineage - " +
                 "everything that used to force a prefab variant per element (behavior numbers, " +
                 "body scale/material, audio loop). Leave Enabled off to keep the prefab as " +
                 "authored. Inventory source: the Mass/Space/Time tadpole prefab diff " +
                 "(Docs/ECOSYSTEM.md §3).")]
        public FaunaVariantTuning Variant = new();

        [Header("Variant spread - one config spans all four of a species' elements")]
        [Tooltip("Roll the ELEMENT per spawn instead of hatching this config's single element. " +
                 "The element's identity (body prism shape, starvation clock, flocking numbers, " +
                 "material, audio) comes from the palette below, so a rolled element behaves as " +
                 "authored - not just a recoloured heart. Empty palette = roll the element alone " +
                 "and keep this config's own Variant tuning. Population, reproduction and the " +
                 "seed floor stay on THIS config, so spreading elements does not multiply the " +
                 "creature count (or the collider budget).")]
        public bool SpreadElements = false;

        [Tooltip("Per-element sibling configs that define each element's identity (normally the " +
                 "four canonical assets in _SO_Assets/Lifeforms for this species). Only Element " +
                 "and Variant are read from them.")]
        public List<FaunaConfigurationSO> ElementPalette = new();

        // THERE IS NO LEVEL, and there is no field here for one. A creature is its species
        // and its element; the four elemental variations ARE its whole variation, and each
        // states its own body scale, prism shape, survival numbers, flocking, audio and HEART
        // SIZE exactly once (Docs/ECOSYSTEM.md §40, which retires §33 and §17's level half).
        // A rolled spawn level (the retired LifeformLevelSpread) and an earned one
        // (the retired FeedsPerLevel) are BOTH gone: the first handed a creature a life it had
        // not lived, and the second made "how big is this thing" a hidden per-individual
        // history the player could not read off the species. Element spread below is untouched
        // - an element is an identity a creature is born with.

        /// <summary>
        /// What a single hatch of this species is: which element it carries and the variant
        /// block expressing that element. Pass <paramref name="inherit"/> (a parent's pick) so
        /// offspring keep the lineage's identity instead of rolling a fresh one.
        /// </summary>
        public LifeformVariantPick<FaunaVariantTuning> RollVariant(
            LifeformVariantPick<FaunaVariantTuning>? inherit = null)
        {
            if (inherit.HasValue) return inherit.Value;

            var element = Element;
            var tuning = Variant;

            if (SpreadElements)
            {
                var sibling = RollPaletteSibling();
                if (sibling)
                {
                    element = sibling.Element;
                    tuning = sibling.Variant;
                }
                else
                {
                    element = CosmicShore.ScriptableObjects.ElementalCrystalSetSO.RandomElement();
                }
            }

            return new LifeformVariantPick<FaunaVariantTuning>(element, tuning);
        }

        FaunaConfigurationSO RollPaletteSibling()
        {
            if (ElementPalette is not { Count: > 0 }) return null;

            for (int attempt = 0; attempt < ElementPalette.Count; attempt++)
            {
                var candidate = ElementPalette[Random.Range(0, ElementPalette.Count)];
                if (candidate && candidate.Element != CosmicShore.Data.Element.None) return candidate;
            }
            return null;
        }
    }

    /// <summary>
    /// The data that used to differ between per-element prefab VARIANTS of the same species,
    /// hoisted into config so ONE base prefab serves all of them. Sentinels keep the prefab's
    /// authored value: floats -1 (scale 0), object refs null, Forager = KeepPrefab. Captured
    /// from the real Mass/Space/Time tadpole diff: body scale (0.4/0.7/0.4), spindle material,
    /// starvation (90/30/30), cohesion radius (50/20/20), behavior tick (1.5/3/3), graze radius
    /// (45/15/15), goal weight (3/0.3/0.3), speed band (10-15/10-15/15-20), forager (on/off/off),
    /// FMOD loop (Mass Tadpole / none / Time Tadpole) + attenuation (0-200 / - / -).
    /// </summary>
    [System.Serializable]
    public class FaunaVariantTuning
    {
        public enum TriState { KeepPrefab = 0, On = 1, Off = 2 }

        [Tooltip("Master switch - off means this block changes nothing (legacy prefab-variant path).")]
        public bool Enabled = false;

        [Header("Body")]
        [Tooltip("Uniform root scale for this variant - the creature's size, full stop. 0 = " +
                 "keep the prefab's authored scale.")]
        [Min(0f)] public float BaseBodyScale = 0f;
        [Tooltip("Target scale of each body HealthPrism - the per-element PRISM shape (the " +
                 "Mass/Time tadpoles author 0.8 x 0.8 x 7 long tail prisms; Space keeps the " +
                 "spindle default). Zero = keep the prefab's authored prism scale.")]
        public Vector3 BodyPrismScale = Vector3.zero;
        [Tooltip("Material applied to the body's Spindle renderers (the per-element body look). " +
                 "None = keep the prefab's authored material.")]
        public Material BodyMaterial;

        [Header("Survival / feeding")]
        [Tooltip("Seconds without feeding before starvation. -1 = keep prefab.")]
        public float StarvationSeconds = -1f;
        [Tooltip("Forager creatures hunt the cell's densest sensed mass and can starve; " +
                 "KeepPrefab leaves the authored flag.")]
        public TriState Forager = TriState.KeepPrefab;

        [Header("Boid flocking (applies to Boid-based species; -1 = keep prefab)")]
        public float CohesionRadius = -1f;
        public float BehaviorUpdateRate = -1f;
        public float TrailBlockInteractionRadius = -1f;
        public float GoalWeight = -1f;
        public float MinSpeed = -1f;
        public float MaxSpeed = -1f;

        [Header("Heart")]
        [Tooltip("WORLD scale this variant's heart (its elemental crystal) renders at - sized " +
                 "to suit THIS creature, so a piranha's heart is a piranha's and a shark's is a " +
                 "shark's. 0 = keep the platform default on ElementalCrystalSet.\n\n" +
                 "This is a GAMEPLAY number as well as a visual one: the collect reward and the " +
                 "live domain fauna buff both read the heart's world scale, so a bigger " +
                 "creature's heart is worth more to whoever takes it. The whole authored band " +
                 "must therefore stay under ElementalCrystalSetSO.MaxSafeHeartWorldScale - past " +
                 "it two visibly different hearts pay the same. Authored by " +
                 "Tools/Build/author_lifeform_heart_sizes.py, which measures the body and fails " +
                 "the build on an overshoot; do not hand-edit one in isolation.")]
        [Min(0f)] public float HeartWorldScale = 0f;

        [Header("Audio")]
        [Tooltip("When on, the variant's loop event (below) replaces the prefab emitter's - an " +
                 "empty reference silences the loop (the Space tadpole ships silent).")]
        public bool OverrideAudio = false;
        public FMODUnity.EventReference AudioLoopEvent;
        [Tooltip("FMOD attenuation override distances; -1 leaves the emitter's authored values.")]
        public float AudioMinDistance = -1f;
        public float AudioMaxDistance = -1f;
    }
}
