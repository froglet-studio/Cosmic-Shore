using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class MutonPopUp : MonoBehaviour//, ICollidable
{

    public float sphereRadius = 100;
    [SerializeField]
    GameObject aiShip;

    [SerializeField]
    GameObject spentMutonPrefab;

    [SerializeField]
    public float intensityAmount = 10f;

    public float lifeTimeIncrease = 20;


    public delegate void PopUpCollision(float amount, string uuid);
    public static event PopUpCollision OnMutonPopUpCollision;

    void Start()
    {
        transform.position = Random.insideUnitSphere * sphereRadius;
    }



    private void OnTriggerEnter(Collider other)
    {
        //TODO Decay brokenSphere and clean up
        Collide(other);
    }

    public void Collide(Collider other)
    {
        spentMutonPrefab.transform.position = transform.position;
        spentMutonPrefab.transform.localEulerAngles = transform.localEulerAngles;
        Instantiate<GameObject>(spentMutonPrefab);
        transform.position = UnityEngine.Random.insideUnitSphere * sphereRadius;
        OnMutonPopUpCollision(intensityAmount, other.gameObject.GetComponent<Player>().PlayerUUID);

        //grow tail
        TrailSpawner trailScript = other.GetComponent<TrailSpawner>();
        trailScript.lifeTime += lifeTimeIncrease;

        //make ai harder
        if (other.gameObject.GetComponent<Player>().PlayerUUID == "admin")
        {
            StarWriter.Core.Input.AiShipController aiControllerScript = aiShip.GetComponent<StarWriter.Core.Input.AiShipController>();
            aiControllerScript.lerpAmount += .005f;
            aiControllerScript.defaultThrottle += .05f;
        }
        

        //GameObject  
        //TODO play SFX sound
        //Destroy(this);
        //TODO Respawn
    }
}
