using UnityEngine;
using StarWriter.Core;

public class FakeCrystal : Crystal
{
    protected override void Collide(Collider other)
    {
        if (!IsShip(other.gameObject) && !IsProjectile(other.gameObject))
            return;

        Ship ship = IsShip(other.gameObject) ? other.GetComponent<ShipGeometry>().Ship : other.GetComponent<Projectile>().Ship;
        
        // TODO: use a different material if the fake crystal is on your team
        if (ship.Team == Team)
            return;

        PerformCrystalImpactEffects(crystalProperties, ship);

        Explode(ship);

        PlayExplosionAudio();

        RemoveSelfFromNode();

        Destroy(gameObject);
    }
}