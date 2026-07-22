using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Utility;
using System;

namespace CosmicShore.Gameplay
{
    [System.Serializable]
    public class MineModelData
    {
        public GameObject model;
        public Material defaultMaterial;
        public Material explodingMaterial;
        public Material inactiveMaterial;
    }

    public class Mine : MonoBehaviour
    {
        [Inject] AudioSystem audioSystem;
        [SerializeField] private Material blueMineMaterial;
        [SerializeField] private float explodeAfterSeconds = 20f;
        [SerializeField] protected List<MineModelData> mineModels;
        [FormerlySerializedAs("collider")] [SerializeField] private Collider mineCollider;
        [SerializeField] protected GameObject SpentMinePrefab;

        public bool isplayer;

        private bool _explosionNullified;
        private Coroutine _explodeRoutine;

        private void Start()
        {
            if (isplayer && blueMineMaterial != null)
            {
                var r = GetComponentInChildren<MeshRenderer>();
                if (r != null) r.material = blueMineMaterial;
            }

            if (_explodeRoutine != null) StopCoroutine(_explodeRoutine);
            _explodeRoutine = StartCoroutine(ExplodeCountdown());
        }

        public void NullifyDelayedExplosion(Vector3 velocity)
        {
            _explosionNullified = true;
            if (_explodeRoutine != null)
            {
                StopCoroutine(_explodeRoutine);
                _explodeRoutine = null;
            }
            Explode(velocity);
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

            Explode(Vector3.one);
            _explodeRoutine = null;
        }

        private void Explode(Vector3 velocity)
        {
            mineCollider.enabled = false;
            audioSystem.PlayGameplaySFX(GameplaySFXCategory.MineExplode, transform.position);
            foreach (var modelData in mineModels)
            {
                // Impact assigns the shared exploding material and animates its
                // shader state via MaterialPropertyBlock — no per-explosion clone.
                var impact = SpentCrystalPoolManager.GetPooledOrInstantiate(
                    SpentMinePrefab, transform.position, transform.rotation);
                if (!impact) continue;

                impact.transform.localScale = transform.lossyScale;
                impact.HandleImpact(velocity, modelData.explodingMaterial, "");
            }
            // Invoke(nameof(DestroyMine));
            DestroyMine();
         }

        private void DestroyMine()
        {
            CSDebug.Log("Mine Exploding");
            Destroy(gameObject);
        }
    }
}
