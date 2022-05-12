using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Player : MonoBehaviour
{
    [SerializeField]
    private string playerUUID;

    [SerializeField]
    SO_Character_Base playerSO;
   
    public string PlayerUUID { get => playerUUID; } 

    

    

    void Start()
    {
        playerUUID = playerSO.UniqueUserID;
    }

    
}
