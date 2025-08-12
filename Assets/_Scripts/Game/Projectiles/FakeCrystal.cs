using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    public class FakeCrystal : Crystal
    {
        [SerializeField] Material blueCrystalMaterial;

        public bool isplayer;

        protected override void Start()
        {
            base.Start();
            if (isplayer) 
                GetComponentInChildren<MeshRenderer>().material = blueCrystalMaterial;
        }

        public override void ExecuteCommonVesselImpact(IShip ship)
        {
            // TODO: use a different material if the fake crystal is on your team
            if (ship.ShipStatus.Team == OwnTeam)
                return;

            // TODO - Handled from R_CrystalImpactor.cs
            // PerformCrystalImpactEffects(crystalProperties, shipStatus.Ship);
            
            Explode(ship);
            PlayExplosionAudio();
            cell.TryRemoveItem(this);
            Destroy(gameObject);
        }
    }
}