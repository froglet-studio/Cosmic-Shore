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

        [Header("Sensing")]
        [Tooltip("Optional override for the cell's mass-SENSING radius — prism registration " +
                 "(ContainsPosition) and the density grids fauna seek mass with — independent of " +
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