using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This So contains all character base fields and properties
/// </summary>

public class SO_Character_Base : ScriptableObject
{
    [SerializeField]
    private string characterName;
    [SerializeField]
    private string uniqueUserID;
    [SerializeField]
    private Color characterColor;
    
    public string CharacterName { get => characterName; set => characterName = value; }
    public string UniqueUserID { get => uniqueUserID; set => uniqueUserID = value; }
    public Color CharacterColor { get => characterColor; set => characterColor = value; }
}
