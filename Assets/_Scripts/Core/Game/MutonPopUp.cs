using UnityEngine;
using Random = UnityEngine.Random;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using StarWriter.Core.Input;

public class MutonPopUp : MonoBehaviour//, ICollidable
{
    [SerializeField]
    float sphereRadius = 100;

    [SerializeField]
    GameObject aiShip;

    [SerializeField]
    GameObject spentMutonPrefab;

    [SerializeField]
    float intensityAmount = 10f;

    [SerializeField]
    IntensityBar IntensityBar;

    [SerializeField]
    float MutonIntensityBoost = .1f;


    [SerializeField]
    float lifeTimeIncrease = 20;

    [SerializeField]
    TextMeshProUGUI outputText;

    [SerializeField]
    GameObject Muton;

    [SerializeField]
    Material material;

    Material tempMaterial;

    int score = 0;

    List<Collider> collisions;

    public delegate void PopUpCollision(float amount, string uuid);
    public static event PopUpCollision OnMutonPopUpCollision;

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

        //move the muton
        StartCoroutine(Muton.GetComponent<FadeIn>().FadeInCoroutine());
        transform.SetPositionAndRotation(UnityEngine.Random.insideUnitSphere * sphereRadius, UnityEngine.Random.rotation);

        //update intensity bar and score
        //OnMutonPopUpCollision(intensityAmount, other.GetComponentInParent<Transform>().GetComponentInParent<Player>().PlayerUUID);

        IntensityBar.IncreaseIntensity(MutonIntensityBoost); // TODO: use events instead
        score++;
        outputText.text = score.ToString("D3");

        //// Grow tail
        //TrailSpawner trailScript = other.GetComponentInParent<Transform>().GetComponent<TrailSpawner>();
        //trailScript.lifeTime += lifeTimeIncrease;

        // Make ai harder
        //if (other.gameObject.GetComponent<Player>().PlayerUUID == "admin")
        //{
        //    StarWriter.Core.Input.AiShipController aiControllerScript = aiShip.GetComponent<StarWriter.Core.Input.AiShipController>();
        //    aiControllerScript.lerpAmount += .001f;
        //    aiControllerScript.defaultThrottle += .01f;
        //}

        //TODO play SFX sound
        //TODO Respawn

    }
}
