using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Player : MonoBehaviour, ICollidable, IDamagable
{
    private string playerName = default;
    [SerializeField]
    private string playerUUID = "admin";
    [SerializeField]
    private float maxIntesity = 100f;
    [SerializeField]
    private float currentIntesity;


    public string PlayerName { get => playerName; set => playerName = value; }
   
    public string PlayerUUID { get => playerUUID; set => playerUUID = value; }

    public float CurrentIntesity { get => currentIntesity; set => currentIntesity = value; }

    private void OnEnable()
    {
        Trail.OnTrailCollision += GainIntesity;
        MutonPopUp.OnMutonPopUpCollision += GainIntesity;
    }

    private void OnDisable()
    {
        Trail.OnTrailCollision -= LoseIntesity;
        MutonPopUp.OnMutonPopUpCollision -= LoseIntesity;
    }



    // Start is called before the first frame update
    void Start()
    {
        CurrentIntesity = maxIntesity;
    }

    private void ChangePlayerName(string name)
    {
        playerName = name;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Collide()
    {
        TakeDamage(1f);
    }

    public void TakeDamage(float amount)
    {
        Debug.Log("You have taken " + amount + " damage.");
    }

    public void Respawn(Vector3 point)
    {
        //TODO get ship ref
        //TODO Get Respawn point for a list of available points
        //TODO return the ship to a respawn point
        //TODO Set rotation

    }

    private void LoseIntesity(float amount, string uuid)
    {
        CurrentIntesity -= amount;
        if (CurrentIntesity <= 0)
        {
            CurrentIntesity = 0;
            //TODO loss conditional met

        }
    }

    private void GainIntesity(float amount, string uuid)
    {
        CurrentIntesity += amount;
        if (CurrentIntesity >= 100)
        {
            CurrentIntesity = 100;

        }
    }

}
