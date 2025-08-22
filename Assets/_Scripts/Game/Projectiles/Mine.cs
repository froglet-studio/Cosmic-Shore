using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    public class Mine : MonoBehaviour
    {
        [SerializeField] private Material blueCrystalMaterial;
        [SerializeField] public CrystalProperties crystalProperties;
        [SerializeField] private float explodeAfterSeconds = 20f;
        [SerializeField] private bool verbose = true;
        [SerializeField] protected List<CrystalModelData> crystalModels;
        [SerializeField] private Collider collider;
        [SerializeField] protected GameObject SpentCrystalPrefab;

        public bool isplayer;

        private bool _explosionNullified;
        private Coroutine _explodeRoutine;
        Material _tempMaterial;

        private void Start()
        {
            if (isplayer && blueCrystalMaterial != null)
            {
                var r = GetComponentInChildren<MeshRenderer>();
                if (r != null) r.material = blueCrystalMaterial;
            }

            if (_explodeRoutine != null) StopCoroutine(_explodeRoutine);
            _explodeRoutine = StartCoroutine(ExplodeCountdown());
        }

        public void NullifyDelayedExplosion(IShipStatus shipStatus)
        {
            _explosionNullified = true;
            if (_explodeRoutine != null)
            {
                StopCoroutine(_explodeRoutine);
                _explodeRoutine = null;
            }
            Explode(shipStatus);
            if (verbose) Debug.Log("[Mine] Delayed explosion nullified.");
        }

        private IEnumerator ExplodeCountdown()
        {
            float t = 0f;
            while (t < explodeAfterSeconds)
            {
                if (_explosionNullified) yield break;
                t += Time.deltaTime;
                yield return null;
            }

            if (_explosionNullified) yield break;

            if (verbose) Debug.Log("[Mine] Delayed explosion triggered (log only).");
            _explodeRoutine = null;
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

        private void Explode(IShipStatus shipStatus)
        {
            collider.enabled = false;

            foreach (var modelData in crystalModels)
            {
                var model = modelData.model;

                _tempMaterial = new Material(modelData.explodingMaterial);
                var spentCrystal = Instantiate(SpentCrystalPrefab);
                spentCrystal.transform.SetPositionAndRotation(transform.position, transform.rotation);
                spentCrystal.GetComponent<Renderer>().material = _tempMaterial;
                spentCrystal.transform.localScale = transform.lossyScale;

                if (crystalProperties.Element == Element.Space && modelData.spaceCrystalAnimator != null)
                {
                    var spentAnimator = spentCrystal.GetComponent<SpaceCrystalAnimator>();
                    var thisAnimator = model.GetComponent<SpaceCrystalAnimator>();
                    spentAnimator.timer = thisAnimator.timer;
                }
                
                spentCrystal.GetComponent<Impact>()?.HandleImpact(shipStatus.Course * shipStatus.Speed, _tempMaterial, shipStatus.Player.PlayerName);
            }
        }
    }
}