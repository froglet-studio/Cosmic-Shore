using UnityEngine;
using System.Collections.Generic;
using StarWriter.Core.Input;
using StarWriter.Core.Audio;

public class FlowCrystal : MonoBehaviour
{
    public struct MutonDetails
    {
        public float fuelAmount;
        public int scoreAmount;
        public float tailLengthIncreaseAmount;
    }

    #region Events
    public delegate void OnCollision(GameObject ship, MutonDetails mutonDetails);
    public static event OnCollision OnMutonCollision;

    public delegate void PopUpCollision(string uuid, float amount);
    public static event PopUpCollision OnMutonPopUpCollision;

    public delegate void OnCollisionIncreaseScore(string uuid, int amount);
    public static event OnCollisionIncreaseScore AddToScore;

    public delegate void MutonMove();
    public static event MutonMove OnMutonMove;
    #endregion

    #region Inspector Fields
    [SerializeField] MutonDetails mutonDetails;
    [SerializeField] float lifeTimeIncrease;
    [SerializeField] float fuelAmount = 0.07f;
    [SerializeField] public float sphereRadius = 100;

    [SerializeField] int scoreBonus = 1;

    [SerializeField] GameObject spentMutonPrefab;  
    [SerializeField] GameObject Muton;    
    [SerializeField] Material material;
    #endregion

    [SerializeField] FlowFieldData field;

    Material tempMaterial;
    List<Collider> collisions;

    const float TWO_PI = 6.28f;

    int eccentricAnomaly;

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

        // TODO: let's refactor this so we're not locked into this ship structure
        GameObject ship = other.transform.parent.parent.gameObject;

        if (AddToScore != null)
            AddToScore(ship.GetComponent<Player>().PlayerUUID, scoreBonus);

        if (ship == GameObject.FindWithTag("Player"))
        {
            // Player Collision
            // Muton animation and haptics
            StartCoroutine(spentMuton.GetComponent<Impact>().ImpactCoroutine(
                ship.transform.forward * ship.GetComponent<ShipData>().speed, tempMaterial, "Player"));
            HapticController.PlayMutonCollisionHaptics();

            // Update fuel bar and currentScore
            OnMutonPopUpCollision(ship.GetComponent<Player>().PlayerUUID, fuelAmount); // excess Fuel flows into currentScore


            OnMutonCollision?.Invoke(ship, mutonDetails);
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
                controllerScript.lerp = controllerScript.defaultLerp;
                controllerScript.throttle = controllerScript.defaultThrottle;
            }
            else
            {
                StartCoroutine(spentMuton.GetComponent<Impact>().ImpactCoroutine(
                     ship.transform.forward * ship.GetComponent<AiShipController>().speed, tempMaterial, "blue"));
                
                //reset ship aggression
                AiShipController controllerScript = ship.GetComponent<AiShipController>();
                controllerScript.lerp = controllerScript.defaultLerp;
                controllerScript.throttle = controllerScript.defaultThrottle;
            }
        }

        // Play SFX sound
        AudioSource audioSource = GetComponent<AudioSource>();
        AudioSystem.Instance.PlaySFXClip(audioSource.clip, audioSource);

        // Move the muton
        StartCoroutine(Muton.GetComponent<FadeIn>().FadeInCoroutine());
        eccentricAnomaly++;
        float stops = 8;

        var aspectRatio = (float)field.fieldHeight / (float)field.fieldWidth;
        transform.SetPositionAndRotation(new Vector3((field.fieldWidth - field.fieldThickness) * Mathf.Cos(eccentricAnomaly * TWO_PI / stops),
                                                     (field.fieldHeight - field.fieldThickness*aspectRatio) * Mathf.Sin(eccentricAnomaly * TWO_PI / stops), 
                                                     0), UnityEngine.Random.rotation);
        //OnMutonMove();

        // Grow tail
        TrailSpawner trailScript = ship.GetComponent<TrailSpawner>();
        trailScript.trailLength += lifeTimeIncrease;
    }
}
