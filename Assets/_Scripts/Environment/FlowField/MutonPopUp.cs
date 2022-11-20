using UnityEngine;
using System.Collections.Generic;
using StarWriter.Core.Input;
using StarWriter.Core.Audio;
using System.Collections;

public class MutonPopUp : MonoBehaviour
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

    Material tempMaterial;
    List<Collider> collisions;
    [SerializeField] bool warp = false;


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
                    ship.transform.forward * ship.GetComponent<ShipData>().speed, tempMaterial, "red"));
                
                // reset ship aggression
                AiShipController controllerScript = ship.GetComponent<AiShipController>();
                controllerScript.lerp = controllerScript.defaultLerp;
                controllerScript.throttle = controllerScript.defaultThrottle;
            }
            else
            {
                StartCoroutine(spentMuton.GetComponent<Impact>().ImpactCoroutine(
                     ship.transform.forward * ship.GetComponent<ShipData>().speed, tempMaterial, "blue"));
                
                // reset ship aggression
                AiShipController controllerScript = ship.GetComponent<AiShipController>();
                controllerScript.lerp = controllerScript.defaultLerp;
                controllerScript.throttle = controllerScript.defaultThrottle;
            }
        }

        // Spawn AOE explosion


        // Play SFX sound
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null) Debug.LogWarning("WTF, audioSource is null");
        if (AudioSystem.Instance == null) Debug.LogWarning("WTF, AudioSystem.Instance is null");
        AudioSystem.Instance.PlaySFXClip(audioSource.clip, audioSource);

        // Move the muton
        StartCoroutine(Muton.GetComponent<FadeIn>().FadeInCoroutine());
        if (warp) transform.SetPositionAndRotation(UnityEngine.Random.onUnitSphere * sphereRadius, UnityEngine.Random.rotation);
        else
        {
            transform.SetPositionAndRotation(UnityEngine.Random.insideUnitSphere * sphereRadius, UnityEngine.Random.rotation);
            OnMutonMove(); //TODO understand why this throws a null exception when the if statement isn't removed
        }
        

        // Grow tail
        TrailSpawner trailScript = ship.GetComponent<TrailSpawner>();
        trailScript.trailLength += lifeTimeIncrease;
    }

    private void ExecuteEffect(GameObject ship)
    {
        StartCoroutine(ExecuteEffectCoroutine());
    }

    private IEnumerator ExecuteEffectCoroutine()
    {
        yield return null;
    }
}
