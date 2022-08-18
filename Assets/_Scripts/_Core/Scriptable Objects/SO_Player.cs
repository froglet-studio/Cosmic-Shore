using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This SO contains all Player only fields and properties
/// </summary>
/// 
[CreateAssetMenu(fileName = "New Player", menuName = "Create SO/Player")]
public class SO_Player : SO_Character_Base
{
    [SerializeField]
    private SO_Ship_Base shipPrefab;
    [SerializeField]
    private SO_Trail_Base trailPrefab;

    public SO_Ship_Base ShipPrefab { get => shipPrefab; set => shipPrefab = value; }
    public SO_Trail_Base TrailPrefab { get => trailPrefab; set => trailPrefab = value; }
}
