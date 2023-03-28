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
    [SerializeField] protected CrystalProperties crystalProperties;
    [SerializeField] public float sphereRadius = 100;
    [SerializeField] protected GameObject SpentCrystalPrefab;
    [SerializeField] protected GameObject CrystalModel; 
    [SerializeField] protected Material material;
    #endregion

    Vector3 origin = Vector3.zero;

    protected Material tempMaterial;
    List<Collider> collisions;

    protected virtual void Start()
    {
        collisions = new List<Collider>();
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        collisions.Add(other);
    }

    protected virtual void Update()
    {
        if (collisions.Count > 0)
        {
            Collide(collisions[0]);
            collisions.Clear();
        }
    }

    protected virtual void Collide(Collider other)
    {
        Ship ship;
        Vector3 velocity;
        if (IsShip(other.gameObject))
        {
            ship = other.GetComponent<ShipGeometry>().Ship;
            velocity = ship.GetComponent<ShipData>().Course * ship.GetComponent<ShipData>().Speed;
        }
        else if (IsProjectile(other.gameObject))
        {
            ship = other.GetComponent<Projectile>().Ship;
            velocity = other.GetComponent<Projectile>().Velocity;
        }
        else return;

        //
        // Do the ship specific crystal stuff
        //
        ship.PerformCrystalImpactEffects(crystalProperties);


        //
        // Do the crystal stuff that always happens (ship independent)
        //

        // Make an exploding Crystal
        tempMaterial = new Material(material);
        var spentCrystal = Instantiate(SpentCrystalPrefab);
        spentCrystal.transform.position = transform.position;
        spentCrystal.transform.localEulerAngles = transform.localEulerAngles;
        spentCrystal.GetComponent<Renderer>().material = tempMaterial;
        
        spentCrystal.GetComponent<Impact>().HandleImpact(
            ship.transform.forward * ship.GetComponent<ShipData>().Speed, tempMaterial, ship.Player.PlayerName);

        // Play SFX sound
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null) Debug.LogWarning("WTF, audioSource is null");                      // TODO: remove this debug if not seen _again_ by 2/12/23
        if (AudioSystem.Instance == null) Debug.LogWarning("WTF, AudioSystem.Instance is null");    // TODO: remove this debug if not seen _again_ by 2/12/23
        AudioSystem.Instance.PlaySFXClip(audioSource.clip, audioSource);

        // Move the Crystal
        StartCoroutine(CrystalModel.GetComponent<FadeIn>().FadeInCoroutine());
        transform.SetPositionAndRotation(Random.insideUnitSphere * sphereRadius + origin, Random.rotation);
        OnCrystalMove?.Invoke();
    }

    protected virtual bool IsShip(GameObject go)
    {
        return go.layer == LayerMask.NameToLayer("Ships");
    }

    protected virtual bool IsProjectile(GameObject go)
    {
        return go.layer == LayerMask.NameToLayer("Projectiles");
    }

    public void SetOrigin(Vector3 origin)
    {
        this.origin = origin;
    }
}