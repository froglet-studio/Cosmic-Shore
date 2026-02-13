using CosmicShore.App.Systems.Audio;
using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Soap;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using UnityEngine;


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
        #region Inspector Fields
        [SerializeField]
        CellRuntimeDataSO cellData;
        
        [SerializeField] 
        public CrystalProperties crystalProperties;
        
        [SerializeField] float sphereRadius = 100;
        public float SphereRadius => sphereRadius;

        [SerializeField] protected GameObject SpentCrystalPrefab;

        [SerializeField] protected List<CrystalModelData> crystalModels;
        [SerializeField] protected bool allowVesselImpactEffect = true;
        [SerializeField] bool allowRespawnOnImpact;

        [Header("Data Containers")]
        [SerializeField] protected ThemeManagerDataContainerSO _themeManagerData;

        #endregion
        
        public List<CrystalModelData> CrystalModels => crystalModels;
        
        Material tempMaterial;
        public CrystalManager CrystalManager { get; protected set; }
        public bool IsExploding { get; private set; }

        protected virtual void Start()
        {
            crystalProperties.crystalValue = crystalProperties.fuelAmount * transform.lossyScale.x;
        }

        public void InjectDependencies(CrystalManager cm) => CrystalManager = cm;
        
        public bool CanBeCollected(Domains shipDomain) => ownDomain == Domains.None || ownDomain == shipDomain;

        public struct ExplodeParams
        {
            public Vector3 Course;
            public float Speed;
            public FixedString64Bytes PlayerName;
        }

        public void NotifyManagerToExplodeCrystal(ExplodeParams explodeParams) =>
            CrystalManager.ExplodeCrystal(Id, explodeParams);
        
        public void Respawn()
        {
            if (!allowRespawnOnImpact)
            {
                DestroyCrystal();
                return;
            }

            CrystalManager.RespawnCrystal(Id);
        }

        public void DestroyCrystal()
        {
            cellData.TryRemoveItem(this);
            Destroy(gameObject);
        }
        
        public void DeactivateModels()
        {
            foreach (var model in crystalModels)
            {
                model.model.SetActive(true);
                model.model.GetComponent<FadeIn>().StartFadeIn();
            }
        }

        public void MoveToNewPos(Vector3 newPos)
        {
            transform.SetPositionAndRotation(newPos, Quaternion.identity);
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
        
        public void Explode(ExplodeParams explodeParams)
        {
            if (IsExploding)
                return;           
            
            IsExploding = true;
            WaitForImpact().Forget();
            
            foreach (var modelData in crystalModels)
            {
                var model = modelData.model;

                var spentCrystal = Instantiate(SpentCrystalPrefab);
                spentCrystal.transform.SetPositionAndRotation(transform.position, transform.rotation);
                spentCrystal.transform.localScale = transform.lossyScale;
                
                tempMaterial = new Material(modelData.explodingMaterial);
                spentCrystal.GetComponent<Renderer>().material = tempMaterial;

                if (crystalProperties.Element == Element.Space && modelData.spaceCrystalAnimator != null)
                {
                    var spentAnimator = spentCrystal.GetComponent<SpaceCrystalAnimator>();
                    var thisAnimator = model.GetComponent<SpaceCrystalAnimator>();
                    spentAnimator.timer = thisAnimator.timer;
                }
                spentCrystal.GetComponent<Impact>()?.HandleImpact(
                    explodeParams.Course * explodeParams.Speed, tempMaterial, explodeParams.PlayerName.ToString());
            }
            
            PlayExplosionAudio();
        }

        void PlayExplosionAudio()
        {
            AudioSource audioSource = GetComponent<AudioSource>();
            AudioSystem.Instance.PlaySFXClip(audioSource.clip, audioSource);
        }

        public void ActivateCrystal()
        {
            transform.parent = cellData.Cell.transform;
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

        public void ChangeDomain(Domains newDomain, float duration = -1)
        {
            if (ownDomain == newDomain)
                return;
            if (newDomain == Domains.None)
            {
                for (int i = 0; i < crystalModels.Count; i++)
                {
                    StartCoroutine(LerpCrystalMaterialCoroutine(crystalModels[i].model, crystalModels[i].defaultMaterial, 1));
                }
                ownDomain = newDomain;
                return;
            }
            ownDomain = newDomain;
            for (int i = 0; i < crystalModels.Count; i++)
            {
                StartCoroutine(LerpCrystalMaterialCoroutine(crystalModels[i].model, _themeManagerData.GetTeamCrystalMaterial(ownDomain, i), 1));
            }
            if (duration != -1) StartCoroutine(DecayingTheftCoroutine(duration));
        }

        IEnumerator DecayingTheftCoroutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            ChangeDomain(Domains.None);
        }

        protected IEnumerator LerpCrystalMaterialCoroutine(GameObject model, Material targetMaterial, float lerpDuration = 2f)
        {
            for (int i = 0; i < crystalModels.Count; i++)
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
                crystalModels[i].explodingMaterial = targetMaterial;
                Destroy(tempMaterial);
            }
        }
        
        /// <summary>
        /// This is to forbid multiple impacts due to multiple vessel colliders
        /// </summary>
        async UniTask WaitForImpact()
        {
            await UniTask.WaitForSeconds(0.5f);
            IsExploding = false;
        }
    }
}