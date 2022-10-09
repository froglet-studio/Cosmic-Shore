using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core.Input;

public class MainMenuMutonPopUp : MonoBehaviour
{
    [SerializeField]
    GameObject spentMutonPrefab;

    [SerializeField]
    GameObject MutonContainer;

    public float lifeTimeIncrease = 20;
    public float sphereRadius = 100;

    [SerializeField]
    GameObject Muton;

    List<Collision> collisions;

    [SerializeField]
    Material material;

    Material tempMaterial;


    void Start()
    {
        collisions = new List<Collision>();
    }

    void OnCollisionEnter(Collision collision)
    {
        collisions.Add(collision);
    }

    private void Update()
    {
        if (collisions.Count > 0)
        {
            Collide(collisions[0]);
            collisions.Clear();
        }
    }

    private void Collide(Collision collision)
    {
        var other = collision.collider;

        //check if a ship
        if (other.transform.parent.parent.GetComponent<AiShipController>() != null)
        {
            //make an exploding muton
            var spentMuton = Instantiate<GameObject>(spentMutonPrefab);
            spentMuton.transform.position = transform.position;
            spentMuton.transform.localEulerAngles = transform.localEulerAngles;
            tempMaterial = new Material(material);
            spentMuton.GetComponent<Renderer>().material = tempMaterial;

            // animate it
            GameObject ship = other.transform.parent.parent.gameObject;



            if (ship == GameObject.FindWithTag("red"))
            {
                StartCoroutine(spentMuton.GetComponent<Impact>().ImpactCoroutine(
                    ship.transform.forward * ship.GetComponent<AiShipController>().speed/10, tempMaterial, "red"));
            }
            else
            {
                StartCoroutine(spentMuton.GetComponent<Impact>().ImpactCoroutine(
                        ship.transform.forward * ship.GetComponent<AiShipController>().speed/10, tempMaterial, "blue"));
            }


            // move old muton
            StartCoroutine(Muton.GetComponent<FadeIn>().FadeInCoroutine());
            transform.SetPositionAndRotation(UnityEngine.Random.onUnitSphere * sphereRadius, UnityEngine.Random.rotation); //use "on sphere" to avoid the logo

            // reset ship aggression
            StarWriter.Core.Input.AiShipController controllerScript = ship.GetComponent<StarWriter.Core.Input.AiShipController>();
            controllerScript.defaultLerp = .2f;

            // grow tail
            MainMenuTrailSpawner trailScript = ship.GetComponent<MainMenuTrailSpawner>();
            trailScript.lifeTime += lifeTimeIncrease;
        }
    }
}
