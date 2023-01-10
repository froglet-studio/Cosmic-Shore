using UnityEngine;
using System.Collections.Generic;
using StarWriter.Core;
using StarWriter.Core.Audio;

public class Crystal : MonoBehaviour
{
    #region Events
    public delegate void CrystalMove();
    public static event CrystalMove OnCrystalMove;
    #endregion

    #region Inspector Fields
    [SerializeField] CrystalProperties crystalProperties;
    [SerializeField] public float sphereRadius = 100;
    [SerializeField] GameObject SpentCrystalPrefab;
    [SerializeField] GameObject CrystalModel; 
    [SerializeField] Material material;
    #endregion

    Material tempMaterial;
    List<Collider> collisions;
    [SerializeField] bool surface = false;

    public Teams Team { get => Teams.None; set => Debug.LogError("Someone tried to set the team type for a crystal"); }

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

    void Collide(Collider other)
    {
        if (!IsShip(other.gameObject))
            return;

        // TODO: let's refactor this so we're not locked into this playerGO structure
        //GameObject playerGO = other.transform.parent.parent.gameObject;
        GameObject playerGO = other.transform.parent.parent.gameObject;

        //
        // Do the ship specific crystal stuff
        //

        var ship = other.GetComponent<ShipGeometry>().Ship;
        ship.PerformCrystalImpactEffects(crystalProperties);

        //
        // Do the crystal stuff that always happens (ship independent)
        //

        // Make an exploding Crystal
        tempMaterial = new Material(material);
        var spentCrystal = Instantiate<GameObject>(SpentCrystalPrefab);
        spentCrystal.transform.position = transform.position;
        spentCrystal.transform.localEulerAngles = transform.localEulerAngles;
        spentCrystal.GetComponent<Renderer>().material = tempMaterial;
        
        StartCoroutine(spentCrystal.GetComponent<Impact>().ImpactCoroutine(
            ship.transform.forward * ship.GetComponent<ShipData>().speed, tempMaterial, ship.Player.PlayerName));

        // Play SFX sound
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null) Debug.LogWarning("WTF, audioSource is null");                      // TODO: remove this debug if not seen by 12/12/22
        if (AudioSystem.Instance == null) Debug.LogWarning("WTF, AudioSystem.Instance is null");    // TODO: remove this debug if not seen by 12/12/22
        AudioSystem.Instance.PlaySFXClip(audioSource.clip, audioSource);

        // Move the Crystal
        StartCoroutine(CrystalModel.GetComponent<FadeIn>().FadeInCoroutine());
        if (surface) 
            transform.SetPositionAndRotation(UnityEngine.Random.onUnitSphere * sphereRadius, UnityEngine.Random.rotation);
        else
        {
            transform.SetPositionAndRotation(UnityEngine.Random.insideUnitSphere * sphereRadius, UnityEngine.Random.rotation);
            OnCrystalMove?.Invoke(); // TODO: understand why this throws a null exception when the if statement isn't removed // TODO: understand the previous 'TODO' comment... does it mean to say "if statement is removed"
        }
    }

    bool IsShip(GameObject go)
    {
        return go.layer == LayerMask.NameToLayer("Ships");
    }
}