using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Player : MonoBehaviour
{
    [SerializeField]
    private string playerUUID;
   
    public string PlayerUUID { get => playerUUID; set => playerUUID = value; }

    //public float CurrentIntesity { get => currentIntensity; set => currentIntensity = value; }

    //private void OnEnable()
    //{
    //    Trail.OnTrailCollision += GainIntesity;
    //    MutonPopUp.OnMutonPopUpCollision += GainIntesity;
    //}

    //private void OnDisable()
    //{
    //    Trail.OnTrailCollision -= GainIntesity;
    //    MutonPopUp.OnMutonPopUpCollision -= GainIntesity;
    //}

    // Start is called before the first frame update
    void Start()
    {
        // CurrentIntesity = maxIntensity;
    }

    //private void ChangePlayerName(string name)
    //{
    //    playerName = name;
    //}

    //public void Collide(Collider other)
    //{
    //    TakeDamage(1f);
    //}

    //private void GainIntesity(float amount, string uuid)
    //{
    //    CurrentIntesity += amount;
    //}
}
