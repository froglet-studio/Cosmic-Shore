using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using StarWriter.Core.Input;

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

    List<Collider> collisions;

    [SerializeField]
    Material material;

    Material tempMaterial;

    void Start()
    {
        collisions = new List<Collider>();
    }

    void OnCollisionEnter(Collision collision)
    {
        collisions.Add(collision.collider);
    }

    private void Update()
    {
        if (collisions.Count > 0)
        {
            Collide(collisions[0]);
            collisions.Clear();
        }
    }

    public void Collide(Collider other)
    {
        //check if a ship
        if (other.transform.parent.parent.GetComponent<StarWriter.Core.Input.AiShipController>() != null)
        {
            GameObject ship;
            ship = other.transform.parent.parent.gameObject;
            //make an exploding muton
            var spentMuton = Instantiate<GameObject>(spentMutonPrefab);
            spentMuton.transform.position = transform.position;
            spentMuton.transform.localEulerAngles = transform.localEulerAngles;
            tempMaterial = new Material(material);
            spentMuton.GetComponent<Renderer>().material = tempMaterial;
            //spentMuton.GetComponent<Renderer>().bounds.Expand(1000); doesn't work


            //animate it
            if (ship == GameObject.FindWithTag("Player"))
            {
                StartCoroutine(spentMuton.GetComponent<Impact>().ImpactCoroutine(
                    ship.transform.forward * ship.GetComponent<InputController>().speed, tempMaterial, "Player"));
            }
            else
            {
                if (ship == GameObject.FindWithTag("red"))
                {
                    StartCoroutine(spentMuton.GetComponent<Impact>().ImpactCoroutine(
                        ship.transform.forward * ship.GetComponent<AiShipController>().speed, tempMaterial, "red"));
                }
                else
                {
                    StartCoroutine(spentMuton.GetComponent<Impact>().ImpactCoroutine(
                         ship.transform.forward * ship.GetComponent<AiShipController>().speed, tempMaterial, "blue"));
                }
            }

            //move old muton
            StartCoroutine(Muton.GetComponent<FadeIn>().FadeInCoroutine());
            transform.SetPositionAndRotation(UnityEngine.Random.onUnitSphere * sphereRadius, UnityEngine.Random.rotation); //use "on sphere" to avoid the logo

            //reset ship aggression
            StarWriter.Core.Input.AiShipController controllerScript = ship.GetComponent<StarWriter.Core.Input.AiShipController>();
            controllerScript.lerpAmount = .2f;

            //grow tail
            MainMenuTrailSpawner trailScript = ship.GetComponent<MainMenuTrailSpawner>();
            trailScript.lifeTime += lifeTimeIncrease;
        }
    }
}
