using System.Collections.Generic;
using UnityEngine;
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
                 "prepopulated (Garland: 4,502 prisms, composed for the home-screen camera), and no " +
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

        [Header("Phase Thresholds")]
        [Tooltip("Per-biome up/down prism-count thresholds that drive phase transitions. "
               + "The gap between Up and Down for each phase is the hysteresis band.")]
        public CellPhaseThresholds PhaseThresholds = CellPhaseThresholds.Default;
    }
}