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
    [SerializeField] protected bool shipImpactEffects = true;
    public Teams Team = Teams.None;
    #endregion

    [Header("Optional Crystal Effects")]
    #region Optional Fields
    [SerializeField] List<CrystalImpactEffects> crystalImpactEffects;
    [SerializeField] GameObject AOEPrefab;
    [SerializeField] float maxExplosionScale;
    [SerializeField] Material AOEExplosionMaterial;
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

    public void PerformCrystalImpactEffects(CrystalProperties crystalProperties, Ship ship)
    {
        if (StatsManager.Instance != null)
            StatsManager.Instance.CrystalCollected(ship, crystalProperties);

        foreach (CrystalImpactEffects effect in crystalImpactEffects)
        {
            switch (effect)
            {
                case CrystalImpactEffects.PlayHaptics:
                    HapticController.PlayCrystalImpactHaptics();
                    break;
                case CrystalImpactEffects.PlayFakeCrystalHaptics:   // TODO: P1 need to merge haptics and take an enum to determine which on to play
                    HapticController.PlayFakeCrystalImpactHaptics();
                    break;
                case CrystalImpactEffects.AreaOfEffectExplosion:
                    var AOEExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
                    AOEExplosion.Material = AOEExplosionMaterial;
                    AOEExplosion.Team = Team;
                    AOEExplosion.Ship = ship;
                    AOEExplosion.SetPositionAndRotation(transform.position, transform.rotation);
                    AOEExplosion.MaxScale = maxExplosionScale;
                    break;
                case CrystalImpactEffects.ReduceSpeed:
                    ship.ShipController.ModifyThrottle(.1f, 10);  // TODO: Magic numbers
                    break;
            }
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
        if (shipImpactEffects)
        {
            ship.PerformCrystalImpactEffects(crystalProperties);
        }

        PerformCrystalImpactEffects(crystalProperties, ship);


        //
        // Do the crystal stuff that always happens (ship independent)
        //
        Explode(ship);

        PlayExplosionAudio();

        // Move the Crystal
        StartCoroutine(CrystalModel.GetComponent<FadeIn>().FadeInCoroutine());
        transform.SetPositionAndRotation(Random.insideUnitSphere * sphereRadius + origin, Random.rotation);
        OnCrystalMove?.Invoke();
    }

    protected void Explode(Ship ship)
    {
        tempMaterial = new Material(material);
        var spentCrystal = Instantiate(SpentCrystalPrefab);
        spentCrystal.transform.position = transform.position;
        spentCrystal.transform.localEulerAngles = transform.localEulerAngles;
        spentCrystal.GetComponent<Renderer>().material = tempMaterial;

        spentCrystal.GetComponent<Impact>().HandleImpact(
            ship.transform.forward * ship.GetComponent<ShipData>().Speed, tempMaterial, ship.Player.PlayerName);
    }

    protected void PlayExplosionAudio()
    {
        AudioSource audioSource = GetComponent<AudioSource>();
        AudioSystem.Instance.PlaySFXClip(audioSource.clip, audioSource);
    }

    // TODO: P1 move to static ObjectResolver class
    protected bool IsShip(GameObject go)
    {
        return go.layer == LayerMask.NameToLayer("Ships");
    }

    // TODO: P1 move to static ObjectResolver class
    protected bool IsProjectile(GameObject go)
    {
        return go.layer == LayerMask.NameToLayer("Projectiles");
    }

    public void SetOrigin(Vector3 origin)
    {
        this.origin = origin;
    }
}