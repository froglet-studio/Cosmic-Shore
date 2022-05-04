using System;
using System.Collections.Generic;
using UnityEngine;

public class MutonPopUp : MonoBehaviour, ICollidable
{
    float sphereRadius = 100;

    [SerializeField]
    GameObject spentMutonPrefab;

    [SerializeField]
    public float intensityAmount = 10f;

    public delegate void PopUpCollision(float amount, string uuid);
    public static event PopUpCollision OnMutonPopUpCollision;


    private void OnTriggerEnter(Collider other)
    {
        spentMutonPrefab.transform.position = transform.position;
        spentMutonPrefab.transform.localEulerAngles = transform.localEulerAngles;        
        Instantiate<GameObject>(spentMutonPrefab);
        transform.position = UnityEngine.Random.insideUnitSphere * sphereRadius;
        OnMutonPopUpCollision(intensityAmount, other.gameObject.GetComponent<Player>().PlayerUUID);
        //TODO Decay brokenSphere and clean up
        //Collide();
    }

    public void Collide()
    {
        //TODO play SFX sound
        //Destroy(this);
        //TODO Respawn
    }
}
