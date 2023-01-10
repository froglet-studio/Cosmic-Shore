using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core;
using StarWriter.Core.Input;

public class MainMenuCrystal : MonoBehaviour
{
    [SerializeField] GameObject spentCrystalPrefab;
    [SerializeField] GameObject Container;
    [SerializeField] GameObject Crystal;
    [SerializeField] Material material;

    public float lifeTimeIncrease = 20;
    public float sphereRadius = 100;

    List<Collision> collisions;
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
            tempMaterial = new Material(material);

            //make an exploding Crystal
            var spentCrystal = Instantiate(spentCrystalPrefab);
            spentCrystal.GetComponent<Renderer>().material = tempMaterial;
            spentCrystal.transform.position = transform.position;
            spentCrystal.transform.localEulerAngles = transform.localEulerAngles;
            spentCrystal.transform.parent = Container.transform;

            // animate it
            GameObject ship = other.transform.parent.parent.gameObject;

            var shipId = ship == GameObject.FindWithTag("red") ? "red" : "blue";
            StartCoroutine(spentCrystal.GetComponent<Impact>().ImpactCoroutine(
                ship.transform.forward * ship.GetComponent<ShipData>().speed/10, tempMaterial, shipId));

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