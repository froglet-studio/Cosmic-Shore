using CosmicShore.App.Systems.Audio;
using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] public CrystalProperties crystalProperties;
        [SerializeField] public float sphereRadius = 100;

        [SerializeField] protected GameObject SpentCrystalPrefab;

        [SerializeField] protected List<CrystalModelData> crystalModels;
        [SerializeField] protected bool allowVesselImpactEffect = true;
        [SerializeField] bool allowRespawnOnImpact;

        [Header("Data Containers")]
        [SerializeField] ThemeManagerDataContainerSO _themeManagerData;
        #endregion

        
        // TODO - Remove this from here, as it is added to R_CrystalImpactor.cs
        [Header("Crystal Effects")]
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))]
        List<ScriptableObject> _crystalImpactEffects;

        Material tempMaterial;
        Vector3 origin = Vector3.zero;

        protected virtual void Start()
        {
            crystalProperties.crystalValue = crystalProperties.fuelAmount * transform.lossyScale.x;
        }

         // Moved to R_CrystalImpactor.cs
         /*protected virtual void OnTriggerEnter(Collider other)
         {
             Collide(other);
         }*/

         // Deprecated - New Impact Effect System has been implemented. Remove it once all tested.
         /*protected void PerformCrystalImpactEffects(CrystalProperties crystalProperties, IShip ship)
         {
             var castedEffects = _crystalImpactEffects.Cast<IImpactEffect>();
             var impactEffectData = new ImpactEffectData(ship.ShipStatus, ship.ShipStatus,
                 ship.ShipStatus.Course * ship.ShipStatus.Speed);

             ShipHelper.ExecuteImpactEffect(castedEffects, impactEffectData, crystalProperties);
         }*/

        public virtual void ExecuteCommonVesselImpact(IShip ship)
        {
            if (OwnTeam != Teams.None && OwnTeam != ship.ShipStatus.Team)
                return;

            if (allowVesselImpactEffect)
            {
                // TODO - This class should not modify AIPilot's properties directly.
                /*if (ship.ShipStatus.AIPilot != null)
                {
                    AIPilot aiPilot = ship.ShipStatus.AIPilot;

                    aiPilot.aggressiveness = aiPilot.defaultAggressiveness;
                    aiPilot.throttle = aiPilot.defaultThrottle;
                }*/
            }

            // TODO - Add Event channels here rather than calling singletons directly.
            if (StatsManager.Instance != null)
                StatsManager.Instance.CrystalCollected(ship, crystalProperties);

            // TODO - Handled from R_CrystalImpactor.cs
            // PerformCrystalImpactEffects(crystalProperties, ship);

            Explode(ship);
            PlayExplosionAudio();

            // Move the Crystal
            if (allowRespawnOnImpact)
            {
                foreach (var model in crystalModels)
                    model.model.GetComponent<FadeIn>().StartFadeIn();

                transform.SetPositionAndRotation(UnityEngine.Random.insideUnitSphere * sphereRadius + origin, UnityEngine.Random.rotation);
                cell.UpdateItem();
            }
            else
            {
                cell.TryRemoveItem(this);
                Destroy(gameObject);
            }
        }
        
        // Deprecated - Remove later
        /*protected virtual void Collide(Collider other)
        {
            if (!other.TryGetComponent(out IVesselCollider shipCollider))
                return;

            var ship = shipCollider.Ship;
            
            if (OwnTeam != Teams.None && OwnTeam != ship.ShipStatus.Team)
                return;

            if (allowVesselImpactEffect)
            {
                // Deprecated - New Impact Effect System has been implemented. Remove it once all tested.
                // ship.PerformCrystalImpactEffects(crystalProperties);

                // TODO - This class should not modify AIPilot's properties directly.
                /*if (ship.ShipStatus.AIPilot != null)
                {
                    AIPilot aiPilot = ship.ShipStatus.AIPilot;

                    aiPilot.aggressiveness = aiPilot.defaultAggressiveness;
                    aiPilot.throttle = aiPilot.defaultThrottle;
                }#1#
            }

            // TODO - Add Event channels here rather than calling singletons directly.
            if (StatsManager.Instance != null)
                StatsManager.Instance.CrystalCollected(ship, crystalProperties);

            PerformCrystalImpactEffects(crystalProperties, ship);

            Explode(ship);
            
            PlayExplosionAudio();

            // Move the Crystal
            if (allowRespawnOnImpact)
            {
                foreach (var model in crystalModels)
                    model.model.GetComponent<FadeIn>().StartFadeIn();

                transform.SetPositionAndRotation(UnityEngine.Random.insideUnitSphere * sphereRadius + origin, UnityEngine.Random.rotation);
                cell.UpdateItem();
            }
            else
            {
                cell.TryRemoveItem(this);
                Destroy(gameObject);
            }
        }*/

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

        protected void Explode(IShip ship)
        {
            for (int i = 0; i < crystalModels.Count; i++)
            {
                var modelData = crystalModels[i];
                var model = modelData.model;

                tempMaterial = new Material(modelData.explodingMaterial);
                var spentCrystal = Instantiate(SpentCrystalPrefab);
                spentCrystal.transform.position = transform.position;
                spentCrystal.transform.localEulerAngles = transform.localEulerAngles;
                spentCrystal.GetComponent<Renderer>().material = tempMaterial;
                spentCrystal.transform.localScale = transform.lossyScale;

                if (crystalProperties.Element == Element.Space && modelData.spaceCrystalAnimator != null)
                {
                    var spentAnimator = spentCrystal.GetComponent<SpaceCrystalAnimator>();
                    var thisAnimator = model.GetComponent<SpaceCrystalAnimator>();
                    spentAnimator.timer = thisAnimator.timer;
                }
                var shipStatus = ship.ShipStatus;
                spentCrystal.GetComponent<Impact>()?.HandleImpact(shipStatus.Course * shipStatus.Speed, tempMaterial, ship.ShipStatus.Player.PlayerName);
            }
        }

        protected void PlayExplosionAudio()
        {
            AudioSource audioSource = GetComponent<AudioSource>();
            AudioSystem.Instance.PlaySFXClip(audioSource.clip, audioSource);
        }

        public void SetOrigin(Vector3 origin)
        {
            this.origin = origin;
        }

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

        public void Steal(Teams team, float duration)
        {
            OwnTeam = team;
            foreach (var modelData in crystalModels)
            {
                StartCoroutine(LerpCrystalMaterialCoroutine(modelData.model, _themeManagerData.GetTeamCrystalMaterial(team), 1));
            }
            StartCoroutine(DecayingTheftCoroutine(duration));
        }

        IEnumerator DecayingTheftCoroutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            OwnTeam = Teams.None;
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