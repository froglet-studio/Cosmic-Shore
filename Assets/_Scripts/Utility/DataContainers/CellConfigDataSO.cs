using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "CellConfigData", menuName = "Cosmic Shore/Cells/Cell Config Data")]
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
    }
}