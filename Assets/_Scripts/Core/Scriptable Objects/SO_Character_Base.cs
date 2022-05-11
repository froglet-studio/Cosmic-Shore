using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Character_Base : ScriptableObject
{
    [SerializeField]
    private string characterName;
    [SerializeField]
    private string uniqueUserID;

    public string CharacterName { get => characterName; set => characterName = value; }
    public string UniqueUserID { get => uniqueUserID; set => uniqueUserID = value; }
}
