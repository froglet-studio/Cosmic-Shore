using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
namespace CosmicShore.Utility
{
    [CreateAssetMenu(fileName = "CellConfigData", menuName = "ScriptableObjects/Cells/Cell Config Data")]
    public class CellConfigDataSO : ScriptableObject
    {
        [Header("AppShell Properties")] public string CellName;
        public string Description;
        public Sprite Icon;

        [Header("Cell Properties")] public float Difficulty;
        public int CellEndGameScore;

        [Header("Visual Properties")] public GameObject MembranePrefab;
        public GameObject NucleusPrefab;
        public SnowChanger CytoplasmPrefab;
        
        [Header("Mechanical Properties")]
        public List<CellModifier> CellModifiers = new();

        [Header("Spawn Profiles")]
        public SpawnProfileSO SpawnProfile;

        [Header("Boot")]
        [Tooltip("This is the world a freestyle scene BOOTS into (CellTypeChoiceOptions.EnvironmentFree). " +
                 "Authored rather than inferred: that mode otherwise picks the first config with no " +
                 "EnvironmentPrefab, which is a claim about what a config CONTAINS standing in for the " +
                 "thing actually wanted - how CHEAP it is to build. A config can be both cheap and " +
                 "prepopulated (Garland: 4,259 prisms, composed for the home-screen camera), and no " +
                 "content predicate can say so. Set it on exactly ONE config per cell; the first one " +
                 "found wins and the environment-free scan is the fallback, so a cell that authors none " +
                 "behaves exactly as before. What it boots is still paid for on EVERY entry to the " +
                 "scene, behind the standard EnvironmentLoadVeil - see Docs/ECOSYSTEM.md §36.10, §48.")]
        public bool BootDefault;

        [Header("Environment")]
        [Tooltip("Optional authored structural environment spawned once with the cell (a SpawnableBase " +
                 "prefab, e.g. SpawnableAtlantis - the Yggdra cell's garden). Spawned through the " +
                 "canonical prism lay path, so big structures stream in budgeted and bloom rather than " +
                 "popping. The environment's mass registers with the cell like any prism (it is prey, " +
                 "territory, and phase-ladder volume), so author PhaseThresholds around the " +
                 "prepopulated baseline - see Docs/ECOSYSTEM.md.")]
        public SpawnableBase EnvironmentPrefab;

        [Tooltip("Intensity passed to EnvironmentPrefab.Spawn(). Structures that scale with intensity " +
                 "honor it; fixed structures (e.g. SpawnableAtlantis) ignore it.")]
        [Min(1)] public int EnvironmentIntensity = 1;

        [Header("Sensing")]
        [Tooltip("Optional override for the cell's mass-SENSING radius - prism registration " +
                 "(ContainsPosition) and the density grids fauna seek mass with - independent of " +
                 "the visual membrane. 0 = use the membrane radius (default). Raise it for a large " +
                 "arena (e.g. the Skim Race track, ~4000 long) so fauna can sense + seek mass " +
                 "across the whole space instead of just the central membrane bubble. " +
                 "See Docs/ECOSYSTEM.md §7.2.")]
        [Min(0f)] public float SenseRadiusOverride = 0f;

        [Header("Stakes")]
        [Tooltip("How big a danger-prism contact bites in this cell (VesselElementalDebuffByDangerPrismEffectSO). " +
                 "Shipped = the effect asset's debuffMagnitude (-0.5 = 5 petals per element); Tuned = its " +
                 "tunedDebuffMagnitude (-0.1 = 1 petal). Applies to the hostile burn and the own-domain " +
                 "temporary debuff alike. Measured outcomes of both: Docs/ELEMENTAL_ECONOMY.md §4.1.")]
        public PetalBurnRule PetalBurnRule = PetalBurnRule.Shipped;

        [Tooltip("Who controls this cell before anybody has claimed it - i.e. the colour its fauna " +
                 "spawn in while the cell's own prism-count leader (the nucleus claim, or the " +
                 "whole-cell volume without a nucleus) is still empty. Unset = the legacy fallbacks " +
                 "(gameData's volume leader, then the local pilot's own domain - a FRIENDLY cell in " +
                 "solo freestyle). OpposingLocalPilot = a domain other than the authority's pilot, " +
                 "so the cell starts hostile and the pilot takes it by claiming the nucleus. Read " +
                 "only until a real leader exists; resolved on the server and replicated by " +
                 "CellNetworkSync. Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md: a biome's STARTING " +
                 "state is authored data.")]
        [SerializeField] InitialControllingDomain initialControllingDomain = InitialControllingDomain.Unset;

        /// <summary>See the field's tooltip and <see cref="CellControlRules"/>.</summary>
        public InitialControllingDomain InitialControllingDomain => initialControllingDomain;

        [Header("Metabolism")]
        [Tooltip("Whether fauna in this cell run the CONSERVED stomach (Docs/ECOLOGY_LOD.md §2): a feed fills the " +
                 "stomach by the volume actually eaten, so a thin trail prism buys proportionally less time than a " +
                 "nominal one. Off (the default, every cell but the Swarm cell) = the shipped rule: ANY feed refills " +
                 "the stomach completely, exactly as the old starvation clock reset. Predation and Nourish are full " +
                 "refills under both rules. Opt-in per cell because it changes how fast every grazer starves on a " +
                 "trail diet (about 3-5x faster on Rhino/Squirrel trail prisms) and has not been play-tested outside " +
                 "the Swarm cell.")]
        [SerializeField] bool conservedFaunaStomach = false;

        /// <summary>See the field's tooltip. Read by <c>Fauna.NotifyFed(float)</c>.</summary>
        public bool ConservedFaunaStomach => conservedFaunaStomach;

        [Header("Phase Thresholds")]
        [Tooltip("Per-biome up/down prism-count thresholds that drive phase transitions. "
               + "The gap between Up and Down for each phase is the hysteresis band.")]
        public CellPhaseThresholds PhaseThresholds = CellPhaseThresholds.Default;
    }
}