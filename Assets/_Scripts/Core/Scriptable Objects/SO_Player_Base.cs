using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Player", menuName = "Create SO/Player")]
public class SO_Player_Base : ScriptableObject
{
    [SerializeField]
    private string playerName;
    [SerializeField]
    private float userID;

    public string PlayerName { get => playerName; set => playerName = value; }
    public float UserID { get => userID; set => userID = value; }
}
