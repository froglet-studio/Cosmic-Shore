using UnityEngine;
using System.Collections.Generic;
using CosmicShore;
using System;

[CreateAssetMenu(fileName = "New Cell Type", menuName = "Cosmic Shore/Cell Type")]
public class SO_CellType : ScriptableObject
{
    [Header("AppSHell Properties")]
    public string CellName;
    public string Description;
    public Sprite Icon;

    [Header("Cell Properties")]
    public float Difficulty;

    [Header("Visual Properties")]
    public GameObject MembranePrefab;
    public GameObject NucleusPrefab;
    public SnowChanger CytoplasmPrefab;

    [Header("Mechanical Properties")]
    public List<CellModifier> CellModifiers = new List<CellModifier>();

    [Header("Flora and Fauna")]
    public List<Flora> SupportedFlora = new List<Flora>();
    public List<Population> SupportedFauna = new List<Population>();
}
