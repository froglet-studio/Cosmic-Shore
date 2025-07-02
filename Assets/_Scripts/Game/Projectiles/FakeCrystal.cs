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

            var shipStatus = other.gameObject.IsLayer("Ships") ? other.GetComponent<IShipStatus>() : other.GetComponent<Projectile>().ShipStatus;
        
            if (shipStatus == null)
            {
                Debug.LogError("Ship Status cannot be null!");
                return;
            }

            // TODO: use a different material if the fake crystal is on your team
            if (shipStatus.Team == Team)
                return;

            PerformCrystalImpactEffects(crystalProperties, shipStatus.Ship);

            Explode(shipStatus.Ship);

            PlayExplosionAudio();

            RemoveSelfFromNode();

            Destroy(gameObject);
        }
    }
}