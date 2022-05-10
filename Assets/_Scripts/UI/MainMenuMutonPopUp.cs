using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class MainMenuMutonPopUp : MonoBehaviour
{

    public float sphereRadius = 100;

    [SerializeField]
    GameObject spentMutonPrefab;

    [SerializeField]
    GameObject MutonContainer;

    [SerializeField]
    public float intensityAmount = 10f;

    public float lifeTimeIncrease = 20;

    [SerializeField]
    GameObject Muton;

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
        //check if a ship
        if (other.GetComponent<StarWriter.Core.Input.AiShipController>() != null)
        {
            //create new muton
            var spentMuton = Instantiate<GameObject>(spentMutonPrefab);
            spentMuton.transform.position = transform.position;
            spentMuton.transform.localEulerAngles = transform.localEulerAngles;
            spentMuton.transform.parent = MutonContainer.transform;

            //move old muton
            StartCoroutine(Muton.GetComponent<FadeIn>().FadeInCoroutine());
            transform.SetPositionAndRotation(UnityEngine.Random.onUnitSphere * sphereRadius, UnityEngine.Random.rotation); //use "on sphere" to avoid the logo

            //reset ship aggression
            StarWriter.Core.Input.AiShipController controllerScript = other.GetComponent<StarWriter.Core.Input.AiShipController>();
            controllerScript.lerpAmount = .2f;

            //grow tail
            MainMenuTrailSpawner trailScript = other.GetComponent<MainMenuTrailSpawner>();
            trailScript.lifeTime += lifeTimeIncrease;
        }
    }
}
