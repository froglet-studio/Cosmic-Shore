using System.Collections;
using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    public class FakeCrystal : Crystal
    {
        [SerializeField] private Material blueCrystalMaterial;

        [SerializeField] private float explodeAfterSeconds = 20f;
        [SerializeField] private bool verbose = true;

        private bool _explosionNullified;
        private Coroutine _explodeRoutine;
        
        public bool isplayer;

        protected override void Start()
        {
            base.Start();
            if (isplayer) 
                GetComponentInChildren<MeshRenderer>().material = blueCrystalMaterial;
            
            if (_explodeRoutine != null) StopCoroutine(_explodeRoutine);
            _explodeRoutine = StartCoroutine(ExplodeCountdown());
        }
        
        public void NullifyDelayedExplosion()
        {
            _explosionNullified = true;
            if (_explodeRoutine != null)
            {
                StopCoroutine(_explodeRoutine);
                _explodeRoutine = null;
            }
            if (verbose) Debug.Log("[FakeCrystal] Delayed explosion nullified.");
        }

        private IEnumerator ExplodeCountdown()
        {
            var t = 0f;
            while (t < explodeAfterSeconds)
            {
                if (_explosionNullified) yield break;
                t += Time.deltaTime;
                yield return null;
            }

            if (_explosionNullified) yield break;

            Debug.Log("[FakeCrystal] Delayed explosion triggered (log only).");
            _explodeRoutine = null;
            
            // cell.TryRemoveItem(this);
            // Destroy(gameObject);
        }

        // public override void ExecuteCommonVesselImpact(IShip ship)
        // {
        //     // TODO: use a different material if the fake crystal is on your team
        //     if (ship.ShipStatus.Team == OwnTeam)
        //         return;
        //
        //     // TODO - Handled from R_CrystalImpactor.cs
        //     // PerformCrystalImpactEffects(crystalProperties, shipStatus.Ship);
        //     
        //     Explode(ship);
        //     PlayExplosionAudio();
        //     cell.TryRemoveItem(this);
        //     Destroy(gameObject);
        // }
    }
}
