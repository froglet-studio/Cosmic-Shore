using UnityEngine;
using System.Collections.Generic;
using StarWriter.Core.Audio;
using System.Collections;

public class MutonPopUp : MonoBehaviour
{
    public struct CrystalProperties
    {
        public float fuelAmount;
        public int scoreAmount;
        public float tailLengthIncreaseAmount;
    }

    #region Events
    public delegate void PopUpCollision(string uuid, float amount);
    public static event PopUpCollision OnMutonPopUpCollision;

    public delegate void OnCollisionIncreaseScore(string uuid, int amount);
    public static event OnCollisionIncreaseScore AddToScore;

    public delegate void MutonMove();
    public static event MutonMove OnMutonMove;
    #endregion

    #region Inspector Fields
    [SerializeField] CrystalProperties crystalProperties;
    [SerializeField] float lifeTimeIncrease;            // TODO: push into CrystalProperties
    [SerializeField] float fuelAmount = 0.07f;          // TODO: verify this is always pulled from CrystalProperties
    [SerializeField] public float sphereRadius = 100;   // TODO: push into CrystalProperties
    [SerializeField] int scoreBonus = 1;                // TODO: push into CrystalProperties

    [SerializeField] GameObject spentMutonPrefab;  
    [SerializeField] GameObject Muton;    
    [SerializeField] Material material;
    #endregion

    Material tempMaterial;
    List<Collider> collisions;
    [SerializeField] bool surface = false;


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
        if (!IsPlayer(other.gameObject))
            return;

        // TODO: let's refactor this so we're not locked into this playerGO structure
        GameObject playerGO = other.transform.parent.parent.gameObject;

        //
        // Do the ship specific crystal stuff
        //

        // TODO: this needs to move into the ship PerformCrystalImpactEffects method...
        if (AddToScore != null)
            AddToScore(playerGO.GetComponent<Player>().PlayerUUID, scoreBonus);

        playerGO.GetComponent<Ship>().PerformCrystalImpactEffects(crystalProperties);

        //
        // Do the crystal stuff that always happens (ship independent)
        //

        // Make an exploding muton
        tempMaterial = new Material(material);
        var spentMuton = Instantiate<GameObject>(spentMutonPrefab);
        spentMuton.transform.position = transform.position;
        spentMuton.transform.localEulerAngles = transform.localEulerAngles;
        spentMuton.GetComponent<Renderer>().material = tempMaterial;
        
        // TODO: this is silliness, let's use the ship's name or something and just forward it transparently into the impact coroutine
        string impactId;
        if (playerGO == GameObject.FindWithTag("Player"))
            impactId = "Player";
        else if (playerGO == GameObject.FindWithTag("red"))
            impactId = "red";
        else
            impactId = "blue";

        StartCoroutine(spentMuton.GetComponent<Impact>().ImpactCoroutine(
            playerGO.transform.forward * playerGO.GetComponent<ShipData>().speed, tempMaterial, impactId));

        // Play SFX sound
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null) Debug.LogWarning("WTF, audioSource is null");                      // TODO: remove this debug if not seen by 12/12/22
        if (AudioSystem.Instance == null) Debug.LogWarning("WTF, AudioSystem.Instance is null");    // TODO: remove this debug if not seen by 12/12/22
        AudioSystem.Instance.PlaySFXClip(audioSource.clip, audioSource);

        // Move the muton
        StartCoroutine(Muton.GetComponent<FadeIn>().FadeInCoroutine());
        if (surface) 
            transform.SetPositionAndRotation(UnityEngine.Random.onUnitSphere * sphereRadius, UnityEngine.Random.rotation);
        else
        {
            transform.SetPositionAndRotation(UnityEngine.Random.insideUnitSphere * sphereRadius, UnityEngine.Random.rotation);
            OnMutonMove(); // TODO: understand why this throws a null exception when the if statement isn't removed // TODO: understand the previous 'TODO' comment... does it mean to say "if statement is removed"
        }
    }

    private bool IsPlayer(GameObject go)
    {
        //return go.transform.parent.parent.GetComponent<Player>() != null;

        return go.layer == LayerMask.NameToLayer("Ships");
    }
}