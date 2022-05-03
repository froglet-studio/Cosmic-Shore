using System;
using System.Collections.Generic;
using UnityEngine;

public class MutonPopUp : MonoBehaviour, ICollidable
{
    float sphereRadius = 100;

    [SerializeField]
    GameObject spentMutonPrefab;

    [SerializeField]
    public float intensityAmountloss = 10f;

    public static event Action<float, string> OnMutonPopUpCollision;


    private void OnTriggerEnter(Collider other)
    {
        spentMutonPrefab.transform.position = transform.position;
        spentMutonPrefab.transform.localEulerAngles = transform.localEulerAngles;        
        Instantiate<GameObject>(spentMutonPrefab);
        transform.position = UnityEngine.Random.insideUnitSphere * sphereRadius;
        OnMutonPopUpCollision(intensityAmountloss, other.gameObject.GetComponent<Player>().PlayerUUID);
        //TODO Decay brokenSphere and clean up
        //Collide();
    }

    public void Collide()
    {
        //TODO play SFX sound
        Destroy(this);
        //TODO Respawn
    }
}
