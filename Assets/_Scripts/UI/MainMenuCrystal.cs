using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core.Input;
using UnityEngine.Serialization;

public class MainMenuCrystal : MonoBehaviour
{
    [SerializeField] GameObject spentCrystalPrefab;

    [SerializeField] GameObject Container;

    public float lifeTimeIncrease = 20;
    public float sphereRadius = 100;

    [SerializeField] GameObject Crystal;

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
        if (other.transform.parent.parent.GetComponent<AIPilot>() != null)
        {
            //make an exploding Crystal
            var spentCrystal = Instantiate<GameObject>(spentCrystalPrefab);
            spentCrystal.transform.position = transform.position;
            spentCrystal.transform.localEulerAngles = transform.localEulerAngles;
            tempMaterial = new Material(material);
            spentCrystal.GetComponent<Renderer>().material = tempMaterial;

            // animate it
            GameObject ship = other.transform.parent.parent.gameObject;



            if (ship == GameObject.FindWithTag("red"))
            {
                StartCoroutine(spentCrystal.GetComponent<Impact>().ImpactCoroutine(
                    ship.transform.forward * ship.GetComponent<ShipData>().speed/10, tempMaterial, "red"));
            }
            else
            {
                StartCoroutine(spentCrystal.GetComponent<Impact>().ImpactCoroutine(
                        ship.transform.forward * ship.GetComponent<ShipData>().speed/10, tempMaterial, "blue"));
            }


            // move old Crystal
            StartCoroutine(Crystal.GetComponent<FadeIn>().FadeInCoroutine());
            transform.SetPositionAndRotation(UnityEngine.Random.onUnitSphere * sphereRadius, UnityEngine.Random.rotation); //use "on sphere" to avoid the logo

            // reset ship aggression
            StarWriter.Core.Input.AIPilot controllerScript = ship.GetComponent<StarWriter.Core.Input.AIPilot>();
            controllerScript.defaultLerp = .2f;

            // grow tail
            MainMenuTrailSpawner trailScript = ship.GetComponent<MainMenuTrailSpawner>();
            trailScript.lifeTime += lifeTimeIncrease;
        }
    }
}
