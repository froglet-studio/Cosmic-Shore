using UnityEngine;
using StarWriter.Core;

public class FakeCrystal : Crystal
{
    Teams team;
    [HideInInspector] public Teams Team { get => team; set => team = value; }

    protected override void Collide(Collider other)
    {
        if (!IsShip(other.gameObject) && !IsProjectile(other.gameObject))
            return;

        Ship ship = IsShip(other.gameObject) ? other.GetComponent<ShipGeometry>().Ship : other.GetComponent<Projectile>().Ship;
        if (ship.Team == Team)
            return;

        PerformCrystalImpactEffects(crystalProperties, ship);

        Explode(ship);

        PlayExplosionAudio();

        Destroy(gameObject);
    }
}