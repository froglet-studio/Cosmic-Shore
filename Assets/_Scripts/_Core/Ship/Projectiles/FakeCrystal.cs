using UnityEngine;
using System.Collections.Generic;
using StarWriter.Core;
using StarWriter.Core.Audio;

public class FakeCrystal : Crystal
{
    Teams team;
    [HideInInspector] public Teams Team { get => team; set => team = value; }

    protected override void Collide(Collider other)
    {
        if (!IsShip(other.gameObject) && !IsProjectile(other.gameObject))
            return;

        Ship ship = IsShip(other.gameObject) ? other.GetComponent<ShipGeometry>().Ship : other.GetComponent<Projectile>().Ship;

        //
        // Do the ship specific crystal stuff
        //

        if (ship.Team == Team)
            return;

        PerformCrystalImpactEffects(crystalProperties, ship);


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
        AudioSystem.Instance.PlaySFXClip(audioSource.clip, audioSource);

        Destroy(transform.gameObject);
    }

    public virtual void SetPositionAndRotation(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
    }

}
