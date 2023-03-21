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

        if (ship.Team == Team)
        {
            return;
        }

        ship.PerformFakeCrystalImpactEffects(crystalProperties);


        //
        // Do the crystal stuff that always happens (ship independent)
        //

        // Make an exploding Crystal
        tempMaterial = new Material(material);
        var spentCrystal = Instantiate(SpentCrystalPrefab);
        spentCrystal.transform.position = transform.position;
        spentCrystal.transform.localEulerAngles = transform.localEulerAngles;
        spentCrystal.GetComponent<Renderer>().material = tempMaterial;

        spentCrystal.GetComponent<Impact>().StartImpactCoroutine(
            ship.transform.forward * ship.GetComponent<ShipData>().Speed, tempMaterial, ship.Player.PlayerName);

        // Play SFX sound
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null) Debug.LogWarning("WTF, audioSource is null");                      // TODO: remove this debug if not seen _again_ by 2/12/23
        if (AudioSystem.Instance == null) Debug.LogWarning("WTF, AudioSystem.Instance is null");    // TODO: remove this debug if not seen _again_ by 2/12/23
        AudioSystem.Instance.PlaySFXClip(audioSource.clip, audioSource);
        Destroy(transform.gameObject);
    }

    public virtual void SetPositionAndRotation(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
    }

}
