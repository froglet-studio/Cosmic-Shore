using UnityEngine;
using System.Collections.Generic;
using StarWriter.Core.Input;
using StarWriter.Core.Audio;

public class MutonPopUp : MonoBehaviour
{
    #region Events
    public delegate void PopUpCollision(string uuid, float amount);
    public static event PopUpCollision OnMutonPopUpCollision;

    public delegate void OnCollisionIncreaseScore(string uuid, int amount);
    public static event OnCollisionIncreaseScore AddToScore;

    public delegate void MutonMove();
    public static event MutonMove OnMutonMove;
    #endregion

    #region Inspector Fields
    [SerializeField] float lifeTimeIncrease;
    [SerializeField] float fuelAmount = 0.07f;
    [SerializeField] public float sphereRadius = 100;

    [SerializeField] int scoreBonus = 1;

    [SerializeField] GameObject spentMutonPrefab;  
    [SerializeField] GameObject Muton;    
    [SerializeField] Material material;
    #endregion

    Material tempMaterial;
    List<Collider> collisions;

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

    private void Collide(Collider other)
    {
        // make an exploding muton
        var spentMuton = Instantiate<GameObject>(spentMutonPrefab);
        spentMuton.transform.position = transform.position;
        spentMuton.transform.localEulerAngles = transform.localEulerAngles;
        tempMaterial = new Material(material);
        spentMuton.GetComponent<Renderer>().material = tempMaterial;

        GameObject ship = other.transform.parent.parent.gameObject;

        if (ship == GameObject.FindWithTag("Player"))
        {
            // Player Collision
            // Muton animation and haptics
            StartCoroutine(spentMuton.GetComponent<Impact>().ImpactCoroutine(
                ship.transform.forward * ship.GetComponent<InputController>().speed, tempMaterial, "Player"));
            HapticController.PlayMutonCollisionHaptics();

            // Update fuel bar and currentScore
            OnMutonPopUpCollision(ship.GetComponent<Player>().PlayerUUID, fuelAmount); // excess Fuel flows into currentScore
            if (AddToScore != null)
                AddToScore(ship.GetComponent<Player>().PlayerUUID, scoreBonus);
        }
        else
        {
            // AI collision
            if (ship == GameObject.FindWithTag("red"))
            {
                StartCoroutine(spentMuton.GetComponent<Impact>().ImpactCoroutine(
                    ship.transform.forward * ship.GetComponent<AiShipController>().speed, tempMaterial, "red"));
                
                //reset ship aggression
                AiShipController controllerScript = ship.GetComponent<AiShipController>();
                controllerScript.lerpAmount = .2f;
            }
            else
            {
                StartCoroutine(spentMuton.GetComponent<Impact>().ImpactCoroutine(
                     ship.transform.forward * ship.GetComponent<AiShipController>().speed, tempMaterial, "blue"));
                
                //reset ship aggression
                AiShipController controllerScript = ship.GetComponent<AiShipController>();
                controllerScript.lerpAmount = .2f;
            }
        }

        // Play SFX sound
        AudioSource audioSource = GetComponent<AudioSource>();
        audioSource.PlayOneShot(audioSource.clip);

        // Move the muton
        StartCoroutine(Muton.GetComponent<FadeIn>().FadeInCoroutine());
        transform.SetPositionAndRotation(UnityEngine.Random.insideUnitSphere * sphereRadius, UnityEngine.Random.rotation);
        OnMutonMove();

        // Grow tail
        TrailSpawner trailScript = ship.GetComponent<TrailSpawner>();
        trailScript.trailLength += lifeTimeIncrease;
    }
}
