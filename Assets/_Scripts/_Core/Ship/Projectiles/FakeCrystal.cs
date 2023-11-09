using _Scripts.Environment.FlowField;
using StarWriter.Core;
using UnityEngine;

namespace _Scripts._Core.Ship.Projectiles
{
    public class FakeCrystal : Crystal
    {
        [SerializeField] Material blueCrystalMaterial;
        //[SerializeField] GameObject CrystalGeometry;
        public bool isplayer = false;

        protected override void Start()
        {
            base.Start();
            if (isplayer) GetComponentInChildren<MeshRenderer>().material = blueCrystalMaterial;
        }


        protected override void Collide(Collider other)
        {
            if (!IsShip(other.gameObject) && !IsProjectile(other.gameObject))
                return;

            StarWriter.Core.Ship ship = IsShip(other.gameObject) ? other.GetComponent<ShipGeometry>().Ship : other.GetComponent<Projectile>().Ship;
        
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
}