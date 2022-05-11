using UnityEngine;
using Random = UnityEngine.Random;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using StarWriter.Core.Input;

public class MutonPopUp : MonoBehaviour//, ICollidable
{
    

    [SerializeField]
    GameObject aiShip;

    [SerializeField]
    GameObject spentMutonPrefab;

    [SerializeField]
    float intensityAmount = 10f;
    [SerializeField]
    float MutonIntensityBoost = .1f;
    [SerializeField]
    float scoreBonus = 10f;
    [SerializeField]
    float sphereRadius = 100;
    [SerializeField]
    float lifeTimeIncrease = 20;

    [SerializeField]
    IntensityBar IntensityBar;
 

    [SerializeField]
    TextMeshProUGUI outputText;

    [SerializeField]
    GameObject Muton;

    [SerializeField]
    Material material;

    Material tempMaterial;
   

    List<Collider> collisions;

    public delegate void PopUpCollision(string uuid,float amount);
    public static event PopUpCollision OnMutonPopUpCollision;

    public delegate void OnCollisionIncreaseScore(string uuid, float amount);
    public static event OnCollisionIncreaseScore AddToScore;

    void Start()
    {
        collisions = new List<Collider>();
        //transform.position = Random.insideUnitSphere * sphereRadius;
    }

    //private void OnTriggerEnter(Collider other)
    void OnCollisionEnter(Collision collision)
    {

        //Collide(other);
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
                Quaternion.Inverse(spentMuton.transform.rotation) * ship.transform.forward *
                ship.GetComponent<InputController>().speed, tempMaterial, "Player"));
            HapticController.PlayMutonCollisionHaptics();
        }

        //if (ship == GameObject.FindWithTag("red"))
        //{
        //    StartCoroutine(spentMuton.GetComponent<Impact>().ImpactCoroutine(
        //        Quaternion.Inverse(spentMuton.transform.rotation) * ship.transform.forward *
        //        ship.GetComponent<AiShipController>().speed, tempMaterial, "red"));
        //}
        else
        {
            StartCoroutine(spentMuton.GetComponent<Impact>().ImpactCoroutine(
                Quaternion.Inverse(spentMuton.transform.rotation) * ship.transform.forward *
                ship.GetComponent<AiShipController>().speed, tempMaterial, "blue"));
        }

        //move the muton
        StartCoroutine(Muton.GetComponent<FadeIn>().FadeInCoroutine());
        transform.SetPositionAndRotation(UnityEngine.Random.insideUnitSphere * sphereRadius, UnityEngine.Random.rotation);

        //update intensity bar and score
        OnMutonPopUpCollision(ship.GetComponent<Player>().PlayerUUID, intensityAmount); // excess Intensity flows into score
        if(AddToScore != null) { AddToScore(ship.GetComponent<Player>().PlayerUUID, scoreBonus); }

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
