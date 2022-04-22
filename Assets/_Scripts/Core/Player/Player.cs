using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Player : MonoBehaviour
{
    private string playerName = default;

    public string PlayerName { get => playerName; set => playerName = value; }

    // Start is called before the first frame update
    void Start()
    {
       
    }

    private void ChangePlayerName(string name)
    {
        playerName = name;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
