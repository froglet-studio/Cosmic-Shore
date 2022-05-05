using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Player : MonoBehaviour//,ICollidable, IDamagable
{
    //private string playerName = default;
    [SerializeField]
    private string playerUUID;
    //[SerializeField]
    //private float maxIntensity = 100f;
    //[SerializeField]
    //private float currentIntensity;


    //public string PlayerName { get => playerName; set => playerName = value; }
   
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
        //CurrentIntesity = maxIntensity;
    }

    //private void ChangePlayerName(string name)
    //{
    //    playerName = name;
    //}

    

    //public void Collide(Collider other)
    //{
    //    //TakeDamage(1f);
    //}

    //public void TakeDamage(float amount)
    //{
    //    Debug.Log("You have taken " + amount + " damage.");
    //}

    //public void Respawn(Vector3 point)
    //{
    //    //TODO get ship ref
    //    //TODO Get Respawn point for a list of available points
    //    //TODO return the ship to a respawn point
    //    //TODO Set rotation

    //}


    //private void GainIntesity(float amount, string uuid)
    //{
    //    CurrentIntesity += amount;
    //}

}
