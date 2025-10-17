using CosmicShore.App.Systems.Audio;
using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game
{
    [System.Serializable]
    public class CrystalModelData
    {
        public GameObject model;
        public Material defaultMaterial;
        public Material explodingMaterial;
        public Material inactiveMaterial;
        public SpaceCrystalAnimator spaceCrystalAnimator;
    }

    public class Crystal : CellItem
    {
        const int MinimumSpaceBetweenCurrentAndLastSpawnPos = 100;
        
        #region Inspector Fields
        [SerializeField] 
        public CrystalProperties crystalProperties;
        
        [SerializeField] float sphereRadius = 100;
        public float SphereRadius => sphereRadius;

        [SerializeField] protected GameObject SpentCrystalPrefab;

        [SerializeField] protected List<CrystalModelData> crystalModels;
        [SerializeField] protected bool allowVesselImpactEffect = true;
        [SerializeField] bool allowRespawnOnImpact;

        [Header("Data Containers")]
        [SerializeField] ThemeManagerDataContainerSO _themeManagerData;

        [SerializeField] private Collider collider;

        #endregion
        
        public List<CrystalModelData> CrystalModels => crystalModels;
        
        Material tempMaterial;
        Vector3 _lastSpawnPosition;
        public Vector3 Origin { get; private set; } = Vector3.zero;

        protected virtual void Start()
        {
            crystalProperties.crystalValue = crystalProperties.fuelAmount * transform.lossyScale.x;
        }

        public bool CanBeCollected(Domains shipDomain) => ownDomain == Domains.None || ownDomain == shipDomain;

        public void Respawn()
        {
            if (!allowRespawnOnImpact)
            {
                // cell?.TryRemoveItem(this);
                CrystalManager.Instance.TryRemoveItem(this);
                Destroy(gameObject);
                return;
            }

            DeactivateModels();
            ChangeSpawnPosition();
            // cell.UpdateItem();
            CrystalManager.Instance.UpdateItem();
        }
        
        void DeactivateModels()
        {
            foreach (var model in crystalModels)
            {
                model.model.SetActive(true);
                model.model.GetComponent<FadeIn>().StartFadeIn();
            }
        }

        void ChangeSpawnPosition()
        {
            Vector3 spawnPos;
            do
            {
                spawnPos = Random.insideUnitSphere * SphereRadius + Origin;
            } while (Vector3.SqrMagnitude(_lastSpawnPosition - spawnPos) <= MinimumSpaceBetweenCurrentAndLastSpawnPos);
            
            transform.SetPositionAndRotation(spawnPos, Random.rotation);
            collider.enabled = true;
            // Origin = transform.position;
            _lastSpawnPosition = spawnPos;
        }

        public void Vacuum(Vector3 newPosition, float vaccumAmount)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                newPosition,
                vaccumAmount * Time.deltaTime / transform.lossyScale.x);
        }

        //the following is a public method that can be called to grow the crystal
        public void GrowCrystal(float duration, float targetScale)
        {
            StartCoroutine(Grow(duration, targetScale));
        }

        // the following grow coroutine is used to grow the crystal when it changes size
        IEnumerator Grow(float duration, float targetScale)
        {
            float elapsedTime = 0.0f;
            Vector3 startScale = transform.localScale;
            Vector3 targetScaleVector = new Vector3(targetScale, targetScale, targetScale);

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);

                transform.localScale = Vector3.Lerp(startScale, targetScaleVector, t);

                yield return null;
            }

            transform.localScale = targetScaleVector;
        }

        public void Explode(IVesselStatus vesselStatus)
        {
            collider.enabled = false;
            
            for (var i = 0; i < crystalModels.Count; i++)
            {
                var modelData = crystalModels[i];
                var model = modelData.model;

                tempMaterial = new Material(modelData.explodingMaterial);
                var spentCrystal = Instantiate(SpentCrystalPrefab);
                /*spentCrystal.transform.position = transform.position;
                spentCrystal.transform.localEulerAngles = transform.localEulerAngles;*/
                spentCrystal.transform.SetPositionAndRotation(transform.position, transform.rotation);
                spentCrystal.GetComponent<Renderer>().material = tempMaterial;
                spentCrystal.transform.localScale = transform.lossyScale;

                if (crystalProperties.Element == Element.Space && modelData.spaceCrystalAnimator != null)
                {
                    var spentAnimator = spentCrystal.GetComponent<SpaceCrystalAnimator>();
                    var thisAnimator = model.GetComponent<SpaceCrystalAnimator>();
                    spentAnimator.timer = thisAnimator.timer;
                }
                spentCrystal.GetComponent<Impact>()?.HandleImpact(vesselStatus.Course * vesselStatus.Speed, tempMaterial, vesselStatus.Player.Name);
            }
        }

        public void PlayExplosionAudio()
        {
            AudioSource audioSource = GetComponent<AudioSource>();
            AudioSystem.Instance.PlaySFXClip(audioSource.clip, audioSource);
        }

        public void SetOrigin(Vector3 o) => Origin = o; 

        public void ActivateCrystal()
        {
            transform.parent = CellControlManager.Instance.GetNearestCell(transform.position).transform;
            gameObject.GetComponent<SphereCollider>().enabled = true;
            enabled = true;

            for (int i = 0; i < crystalModels.Count; i++)
            {
                var modelData = crystalModels[i];
                var model = modelData.model;

                model.GetComponent<Renderer>().material = modelData.inactiveMaterial;
                StartCoroutine(LerpCrystalMaterialCoroutine(model, modelData.defaultMaterial));
            }
        }

        public void Steal(Domains domain, float duration)
        {
            ownDomain = domain;
            foreach (var modelData in crystalModels)
            {
                StartCoroutine(LerpCrystalMaterialCoroutine(modelData.model, _themeManagerData.GetTeamCrystalMaterial(domain), 1));
            }
            StartCoroutine(DecayingTheftCoroutine(duration));
        }

        IEnumerator DecayingTheftCoroutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            ownDomain = Domains.None;
            foreach (var modelData in crystalModels)
            {
                StartCoroutine(LerpCrystalMaterialCoroutine(modelData.model, modelData.defaultMaterial, 1));
            }
        }

        IEnumerator LerpCrystalMaterialCoroutine(GameObject model, Material targetMaterial, float lerpDuration = 2f)
        {
            Renderer renderer = model.GetComponent<Renderer>();
            Material tempMaterial = new Material(renderer.material);
            renderer.material = tempMaterial;

            Color startColor1 = tempMaterial.GetColor("_BrightCrystalColor");
            Color startColor2 = tempMaterial.GetColor("_DullCrystalColor");

            Color targetColor1 = targetMaterial.GetColor("_BrightCrystalColor");
            Color targetColor2 = targetMaterial.GetColor("_DullCrystalColor");

            float elapsedTime = 0.0f;

            while (elapsedTime < lerpDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / lerpDuration);

                tempMaterial.SetColor("_BrightCrystalColor", Color.Lerp(startColor1, targetColor1, t));
                tempMaterial.SetColor("_DullCrystalColor", Color.Lerp(startColor2, targetColor2, t));

                yield return null;
            }

            renderer.material = targetMaterial;
            Destroy(tempMaterial);
        }
    }
}

