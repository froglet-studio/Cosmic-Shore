using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
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
        [SerializeField] private Material blueMineMaterial;
        [SerializeField] private float explodeAfterSeconds = 20f;
        [SerializeField] protected List<MineModelData> mineModels;
        [SerializeField] private Collider collider;
        [SerializeField] protected GameObject SpentMinePrefab;

        public bool isplayer;

        private bool _explosionNullified;
        private Coroutine _explodeRoutine;
        Material _tempMaterial;
        private Vector3 _velocity;

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
            collider.enabled = false;
            foreach (var modelData in mineModels)
            {
                _tempMaterial = new Material(modelData.explodingMaterial);
                var spentCrystal = Instantiate(SpentMinePrefab);
                spentCrystal.transform.SetPositionAndRotation(transform.position, transform.rotation);
                spentCrystal.GetComponent<Renderer>().material = _tempMaterial;
                spentCrystal.transform.localScale = transform.lossyScale;

                spentCrystal.GetComponent<Impact>()?.HandleImpact(velocity, _tempMaterial, "");
            }
            // Invoke(nameof(DestroyMine));
            DestroyMine();
         }

        private void DestroyMine()
        {
            Debug.Log("Mine Exploding");
            Destroy(gameObject);
        }
    }
}