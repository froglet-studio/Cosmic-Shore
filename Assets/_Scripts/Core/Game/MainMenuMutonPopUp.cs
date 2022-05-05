using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class MainMenuMutonPopUp : MonoBehaviour
{

    public float sphereRadius = 100;
    [SerializeField]
    GameObject aiShip;

    [SerializeField]
    GameObject spentMutonPrefab;

    [SerializeField]
    Vector3 displacement = Vector3.zero;

    [SerializeField]
    public float intensityAmount = 10f;

    public float lifeTimeIncrease = 20;

    void Start()
    {
        //transform.position = Random.insideUnitSphere * sphereRadius + displacement;
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
        transform.position = UnityEngine.Random.onUnitSphere * sphereRadius;

        //reset ship
        StarWriter.Core.Input.AiShipController controllerScript = other.GetComponent<StarWriter.Core.Input.AiShipController>();
        controllerScript.lerpAmount = .2f;

        //grow tail
        MainMenuTrailSpawner trailScript = other.GetComponent<MainMenuTrailSpawner>();
        trailScript.lifeTime += lifeTimeIncrease;
    }
}
