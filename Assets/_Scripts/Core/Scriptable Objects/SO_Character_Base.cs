using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Character_Base : ScriptableObject
{
    [SerializeField]
    private string characterName;
    [SerializeField]
    private string uniqueUserID;
    [SerializeField]
    private Color characterColor;
    [SerializeField]
    private SO_Ship_Base shipPrefab;
    [SerializeField]
    private SO_Trail_Base trailPrefab;
    
    

    public string CharacterName { get => characterName; set => characterName = value; }
    public string UniqueUserID { get => uniqueUserID; set => uniqueUserID = value; }
    public Color CharacterColor { get => characterColor; set => characterColor = value; }
    public SO_Ship_Base ShipPrefab { get => shipPrefab; set => shipPrefab = value; }
    public SO_Trail_Base TrailPrefab { get => trailPrefab; set => trailPrefab = value; }
}
