using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using FMODUnity;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tuning for the Manta's whole bomb kit (the 2026-11 overhaul, MANTA_STING_KABLOOM.md): skim for ENERGY, spend
    /// energy to plant a bomb, every bomb rides ONE shared fuse that each new bomb resets, and the omni crystal cashes
    /// the board with a multiplier per bomb. One shared asset wired DIRECTLY on <see cref="MantaStingActionExecutor"/>
    /// (Sting is bound to no input event, so <c>CollectBoundActions</c> can never resolve it).
    ///
    /// Who owns what:
    ///  • CHARGE (Sting) - how long the shared fuse runs when a plant resets it (<see cref="fuseSeconds"/> times
    ///    <see cref="fuseScale"/>, read at each reset: <see cref="FuseSecondsFor"/>); Contagion at level 5. Charge
    ///    no longer touches the petals a bloom knocks out of a rival: a petal is a whole unit, too coarse a dial to
    ///    tune (Garrett, 2026-11-10), so the bloom's take is flat (MantaBombDebuffByExplosionEffect.asset).
    ///  • SPACE - the skimmer's size (authored on Manta.prefab's Skimmer.Scale, 20 → 40) and the energy the skim
    ///    gathers; skimming an opposing domain's prism primes it EXPLOSIVE, and at level 5 the skim pops its shield
    ///    first. The space card shows the energy.
    ///  • MASS (Decoy) - a collected elemental crystal leaves a decoy behind, which lasts longer with Mass
    ///    (<see cref="DecoyLifetimeFor"/>); at level 5 the omni crystal leaves one too.
    ///  • TIME - Yawstery (the turn) and the soaring speed, with Soar unlocking at level 5 (not this asset).
    /// </summary>
    [CreateAssetMenu(fileName = "MantaStingConfig",
        menuName = "ScriptableObjects/Vessel Actions/Manta Sting Config")]
    public class MantaStingConfigSO : ScriptableObject
    {
        [Header("Energy (SPACE card) - gathered by skimming, spent to plant")]
        [Tooltip("Most energy the Manta can hold, in BOMBS (one bomb costs 1). 1 makes the loop skim, plant, skim, plant. Planted bombs have no cap.")]
        [FormerlySerializedAs("baseCapacity")]
        [SerializeField, Min(1)] int energyCapacity = 1;

        [Tooltip("Energy gained per unique prism/lifeform/vessel skim tick, in BOMBS. 0.34 is roughly one bomb per " +
                 "three skims. Space grows the skimmer, so a higher Space level reaches more prisms per pass.")]
        [FormerlySerializedAs("chargePerSkim")]
        [SerializeField, Min(0.01f)] float energyPerSkim = 0.34f;

        [Tooltip("Seconds between energy ticks paid by the SAME prism - the skimmer re-enters a prism it is " +
                 "sliding along, and one graze must not milk one prism dry.")]
        [FormerlySerializedAs("perPrismChargeCooldown")]
        [SerializeField, Min(0f)] float perPrismEnergyCooldown = 1.5f;

        [Header("Explosive skim (SPACE)")]
        [Tooltip("Skimming an OPPOSING domain's prism primes it explosive: when it is destroyed the prisms beside it " +
                 "in its prismscape go too (PrismExplosiveChain). Off = the skim only gathers energy.")]
        [SerializeField] bool skimPrimesExplosive = true;

        [Tooltip("How lit (toward white) an explosive prism is drawn on the priming machine, 0-1.")]
        [SerializeField, Range(0f, 1f)] float explosiveLift = 0.55f;

        [Tooltip("Seconds the explosive lift takes to fade in.")]
        [SerializeField, Min(0f)] float explosiveLiftFadeSeconds = 0.25f;

        [Header("Shared fuse (CHARGE card) - one timer for every bomb")]
        [Tooltip("The shared fuse at the RESTING Charge level, seconds. Every new bomb resets it to the full length " +
                 "(this times fuseScale), so every bomb on the board always goes off together. Bloomrush replaces " +
                 "this resting length per intensity through MantaBombRules.FuseSecondsOverride; Charge still scales " +
                 "whichever applies.")]
        [SerializeField, Min(1f)] float fuseSeconds = 20f;

        [Tooltip("CHARGE → the fuse: how many times fuseSeconds a plant resets the shared fuse to, by the planter's " +
                 "Charge at that moment. x1 at rest, Max at Charge 10, extrapolated through the deficit and overcharge " +
                 "bands; the floor keeps a drained Charge from collapsing the fuse. A plant at a higher Charge buys the " +
                 "whole board the longer fuse. Charge used to scale the petals a bloom knocks loose - a petal is a whole " +
                 "unit, too coarse a dial to tune (Garrett, 2026-11-10).")]
        [SerializeField] ElementalFloat fuseScale = ElementalFloat.Multiplier(1f, 1.5f, Element.Charge, 0.5f);

        [Header("Planting")]
        [Tooltip("How much FASTER than the target the Manta must be moving to plant on a VESSEL (u/s). 0 = any " +
                 "graze plants - the accessibility default.")]
        [SerializeField, Min(0f)] float plantSpeedMargin = 0f;

        [Header("Blooms - slow on purpose (many big blooms at once)")]
        [Tooltip("The bloom prefab family. Sphere blast(s); spawned at the carrier's position.")]
        [SerializeField] AOEExplosion[] aoePrefabs;

        [Tooltip("Optional material override for bomb blooms. Empty = the vessel's authored AOE material.")]
        [SerializeField] Material bloomMaterial;

        [Tooltip("Blast MaxScale (sphere diameter) when the shared FUSE runs out, or the Manta hits a prism and " +
                 "sets the board off early. The omni crystal pays kabloomBlastScale x the bomb multiplier.")]
        [SerializeField, Min(1f)] float fuseBlastScale = 70f;

        [Tooltip("Blast MaxScale per bomb when the OMNI crystal cashes the board, before the bomb multiplier.")]
        [SerializeField, Min(1f)] float kabloomBlastScale = 140f;

        [Tooltip("Press your luck: each bomb past the first adds this much to EVERY bloom's size when the omni crystal " +
                 "cashes the board (1 bomb = 1x, 3 bombs = 1 + 2 x this).")]
        [SerializeField, Min(0f)] float omniScalePerExtraBomb = 0.35f;

        [Tooltip("Ceiling on the omni bomb multiplier.")]
        [SerializeField, Min(1f)] float omniScaleMultiplierMax = 3f;

        [Tooltip("Seconds a fuse or prism-impact bloom takes to grow to full size. Slow reads better and spreads the " +
                 "cost of many big blooms over more frames.")]
        [SerializeField, Min(0.1f)] float bloomGrowSeconds = 2.5f;

        [Tooltip("Seconds an omni cash-out bloom takes to grow to full size (they are the big ones).")]
        [SerializeField, Min(0.1f)] float omniBloomGrowSeconds = 4f;

        [Tooltip("The Kabloom's own extra DOMAINED blast at the Manta's position, MaxScale.")]
        [SerializeField, Min(0f)] float kabloomSelfBlastScale = 140f;

        [Header("Contagion (CHARGE level 5)")]
        [Tooltip("With the Charge upgrade, anything caught in a bomb's bloom is itself bombed, free. Radius FRACTION " +
                 "of the blast scale used for the catch test (a blast's reach is MaxScale/2).")]
        [SerializeField, Range(0.1f, 1.5f)] float contagionRadiusFraction = 1f;

        [Header("Kabloom cascade (juice)")]
        [Tooltip("Seconds between consecutive bombs in an omni cash-out, nearest first, so the chain rolls outward " +
                 "from the pilot. A fuse or prism-impact board goes off together (no stagger).")]
        [SerializeField, Range(0f, 0.5f)] float cascadeStaggerSeconds = 0.09f;

        [Tooltip("Ceiling on the total cascade time, seconds.")]
        [SerializeField, Min(0.1f)] float cascadeMaxSeconds = 1.2f;

        [Header("Decoy crystals (MASS)")]
        [Tooltip("The omni crystal prefab a decoy omni is built from (Crystal.prefab). The four elemental decoys " +
                 "come from Resources/ElementalCrystalSet.")]
        [SerializeField] GameObject omniCrystalPrefab;

        [Tooltip("Seconds a decoy waits to be taken at the resting Mass level, before it fades.")]
        [SerializeField, Min(1f)] float decoyLifetimeSeconds = 45f;

        [Tooltip("MASS → decoy lifetime: multiplier at Mass level 10 (1x at rest).")]
        [SerializeField, Min(1f)] float decoyLifetimeAtFullMass = 2f;

        [Tooltip("Floor for the Mass lifetime multiplier.")]
        [SerializeField, Range(0.05f, 1f)] float minDecoyLifetimeMultiplier = 0.5f;

        [Tooltip("What a sprung decoy morphs into: the danger-prism cluster, spawned at the decoy facing along the " +
                 "tricked pilot's course so it lands in their path (AOEDangerBlocks).")]
        [SerializeField] AOEExplosion[] decoyMorphPrefabs;

        [Tooltip("Scale handed to the morph (an AOEDangerHemisphereBlocks lays its own authored cluster).")]
        [SerializeField, Min(1f)] float decoyMorphScale = 60f;

        [Tooltip("The decoy's trigger radius as a fraction of the source crystal's collider (1 = the real pickup's).")]
        [SerializeField, Range(0.25f, 3f)] float decoyTriggerRadiusScale = 1f;

        [Header("Fuse marker (the planter sees their own bombs)")]
        [Tooltip("Draw a halo on each bombed target, visible ONLY to the local human pilot who planted it.")]
        [SerializeField] bool showFuseMarker = true;

        [Tooltip("Marker radius in world units.")]
        [SerializeField, Min(1f)] float markerRadius = 14f;

        [Tooltip("Marker colour while the shared fuse is young.")]
        [SerializeField] Color markerCalmColor = new Color(0.35f, 0.8f, 1f, 1f);

        [Tooltip("Marker colour as the fuse runs out, and the colour a cascading bomb wears before it blooms.")]
        [SerializeField] Color markerCriticalColor = new Color(1f, 0.35f, 0.1f, 1f);

        [Tooltip("Seconds of fuse remaining at which the marker is fully critical.")]
        [SerializeField, Min(0.5f)] float markerCriticalSeconds = 6f;

        [Tooltip("Marker pulses per second at a fresh fuse, and at a critical one.")]
        [SerializeField, Min(0.1f)] float markerCalmPulseHz = 0.9f;
        [SerializeField, Min(0.1f)] float markerCriticalPulseHz = 5f;

        [Tooltip("Seconds the marker takes to bloom in and to fade out.")]
        [SerializeField, Min(0.01f)] float markerFadeSeconds = 0.22f;

        [Header("Audio (FMOD) - one event per beat, EMPTY ships silent")]
        [Tooltip("A skim paid energy. Fires on the local pilot only.")]
        [SerializeField] EventReference skimChargeEvent;

        [Tooltip("The energy crossed a whole bomb - the 'you may plant' beat.")]
        [SerializeField] EventReference bombArmedEvent;

        [Tooltip("A bomb went onto a target (and the shared fuse reset).")]
        [SerializeField] EventReference bombPlantedEvent;

        [Tooltip("The shared fuse ran out - the board the pilot did NOT cash in.")]
        [SerializeField] EventReference fuseExpiredEvent;

        [Tooltip("The Manta hit a prism: the board went off early and the energy was lost.")]
        [SerializeField] EventReference prismImpactDetonationEvent;

        [Tooltip("The omni crystal cashed the board. Fires once for the whole cascade, at the ship.")]
        [SerializeField] EventReference kabloomEvent;

        [Tooltip("One bomb in a cascade blooming. Fires per bomb, at the bomb.")]
        [SerializeField] EventReference cascadeBloomEvent;

        [Tooltip("A rival took one of this Manta's decoys and it morphed into danger prisms.")]
        [SerializeField] EventReference decoySprungEvent;

        public int EnergyCapacity => energyCapacity;
        public float EnergyPerSkim => energyPerSkim;
        public float PerPrismEnergyCooldown => perPrismEnergyCooldown;
        public bool SkimPrimesExplosive => skimPrimesExplosive;
        public float ExplosiveLift => explosiveLift;
        public float ExplosiveLiftFadeSeconds => explosiveLiftFadeSeconds;
        public float FuseSeconds => fuseSeconds;
        public float PlantSpeedMargin => plantSpeedMargin;
        public AOEExplosion[] AoePrefabs => aoePrefabs;
        public Material BloomMaterial => bloomMaterial;
        public float FuseBlastScale => fuseBlastScale;
        public float KabloomBlastScale => kabloomBlastScale;
        public float BloomGrowSeconds => bloomGrowSeconds;
        public float OmniBloomGrowSeconds => omniBloomGrowSeconds;
        public float KabloomSelfBlastScale => kabloomSelfBlastScale;
        public float ContagionRadiusFraction => contagionRadiusFraction;
        public float CascadeStaggerSeconds => cascadeStaggerSeconds;
        public float CascadeMaxSeconds => cascadeMaxSeconds;
        public GameObject OmniCrystalPrefab => omniCrystalPrefab;
        public AOEExplosion[] DecoyMorphPrefabs => decoyMorphPrefabs;
        public float DecoyMorphScale => decoyMorphScale;
        public float DecoyTriggerRadiusScale => decoyTriggerRadiusScale;
        public bool ShowFuseMarker => showFuseMarker;
        public float MarkerRadius => markerRadius;
        // the fuse marker is a halo in the world: as the worn colour set paints it
        public Color MarkerCalmColor => ColorTrace.Retrace(ColorTrace.World, markerCalmColor);
        public Color MarkerCriticalColor => ColorTrace.Retrace(ColorTrace.World, markerCriticalColor);
        public float MarkerCriticalSeconds => markerCriticalSeconds;
        public float MarkerCalmPulseHz => markerCalmPulseHz;
        public float MarkerCriticalPulseHz => markerCriticalPulseHz;
        public float MarkerFadeSeconds => markerFadeSeconds;
        public EventReference SkimChargeEvent => skimChargeEvent;
        public EventReference BombArmedEvent => bombArmedEvent;
        public EventReference BombPlantedEvent => bombPlantedEvent;
        public EventReference FuseExpiredEvent => fuseExpiredEvent;
        public EventReference PrismImpactDetonationEvent => prismImpactDetonationEvent;
        public EventReference KabloomEvent => kabloomEvent;
        public EventReference CascadeBloomEvent => cascadeBloomEvent;
        public EventReference DecoySprungEvent => decoySprungEvent;

        /// <summary>
        /// Per-bomb delay in an omni cascade of <paramref name="count"/> bombs: the authored stagger, compressed so
        /// the whole chain still lands inside the ceiling.
        /// </summary>
        public float CascadeDelayFor(int index, int count)
        {
            if (count <= 1 || cascadeStaggerSeconds <= 0f) return 0f;
            float stagger = Mathf.Min(cascadeStaggerSeconds, cascadeMaxSeconds / (count - 1));
            return index * stagger;
        }

        /// <summary>
        /// The press-your-luck multiplier on every bloom of an omni cash-out of <paramref name="bombCount"/> bombs:
        /// 1x for one bomb, growing by <see cref="omniScalePerExtraBomb"/> per extra bomb, capped.
        /// </summary>
        public float OmniScaleMultiplier(int bombCount) =>
            bombCount <= 1 ? 1f
                : Mathf.Min(omniScaleMultiplierMax, 1f + omniScalePerExtraBomb * (bombCount - 1));

        /// <summary>
        /// CHARGE → the length a plant resets the shared fuse to, seconds: the resting length (a mode's override when one
        /// is set, else <see cref="fuseSeconds"/>) times <see cref="fuseScale"/> at the planter's live Charge. The
        /// executor reads it at each reset, on the one machine that simulates the bombs (no other machine runs the fuse),
        /// so the continuous level is the right one, and it is finer than any petal.
        /// </summary>
        public float FuseSecondsFor(IVesselStatus status) =>
            Mathf.Max(1f, FuseSecondsAtRest * fuseScale.EvaluateLive(status));

        /// <summary>
        /// <see cref="FuseSecondsFor"/> at a normalized Charge level (0 at rest, 1 at Charge 10, -0.5 to 1.5 at the band's
        /// ends): the pure form, for tests and the numbers the docs quote.
        /// </summary>
        public float FuseSecondsAtLevel(float chargeLevel01) =>
            Mathf.Max(1f, FuseSecondsAtRest * fuseScale.EvaluateAtNormalizedLevel(chargeLevel01));

        /// <summary>The fuse at the resting Charge level: the mode's override when one is set, else the authored length.</summary>
        float FuseSecondsAtRest => MantaBombRules.FuseSecondsOverride ?? fuseSeconds;

        /// <summary>MASS → how long one of this vessel's decoys waits to be taken, seconds.</summary>
        public float DecoyLifetimeFor(IVesselStatus status) =>
            decoyLifetimeSeconds * ElementalScaling.Multiplier(status, Element.Mass,
                decoyLifetimeAtFullMass, minDecoyLifetimeMultiplier);
    }

    /// <summary>
    /// Mode-level dials for the Manta bomb system. A minigame that wants a different fuse (Bloomrush's per-intensity
    /// ladder) sets the override at round start and clears it on teardown; the executor reads it each time a bomb
    /// resets the shared fuse. The override replaces the RESTING length only: the planter's Charge still scales it
    /// (<see cref="MantaStingConfigSO.FuseSecondsFor"/>). Static by design, with the mandatory domain-reload reset.
    /// </summary>
    public static class MantaBombRules
    {
        public static float? FuseSecondsOverride;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => FuseSecondsOverride = null;
    }
}
