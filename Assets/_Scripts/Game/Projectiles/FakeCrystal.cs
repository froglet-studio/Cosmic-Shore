using CosmicShore.Environment.FlowField;
using CosmicShore.Core;
using CosmicShore.Utility.ClassExtensions;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class FakeCrystal : Crystal
    {
        [SerializeField] Material blueCrystalMaterial;

        public bool isplayer;

        protected override void Start()
        {
            base.Start();
            if (isplayer) GetComponentInChildren<MeshRenderer>().material = blueCrystalMaterial;
        }

        protected override void Collide(Collider other)
        {
            if (!other.gameObject.IsLayer("Ships") && !other.gameObject.IsLayer("Projectiles"))
                return;

            var ship = other.gameObject.IsLayer("Ships") ? other.GetComponent<ShipGeometry>().Ship : other.GetComponent<Projectile>().Ship;
        
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