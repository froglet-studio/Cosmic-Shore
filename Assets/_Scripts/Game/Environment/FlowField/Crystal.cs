using CosmicShore.App.Systems.Audio;
using CosmicShore.Core;
using CosmicShore.Game.IO;
using CosmicShore.Game.Projectiles;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Game.AI;
using CosmicShore.Utility.ClassExtensions;
using System;
using CosmicShore.Game;

namespace CosmicShore.Environment.FlowField
{

    [System.Serializable]
    public struct CrystalModelData
    {
        public GameObject model;
        public Material defaultMaterial;
        public Material explodingMaterial;
        public Material inactiveMaterial;
        public SpaceCrystalAnimator spaceCrystalAnimator;
    }

    public class Crystal : NodeItem
    {
        #region Events
        public static Action OnCrystalMove;
        #endregion

        #region Inspector Fields
        [SerializeField] public CrystalProperties crystalProperties;
        [SerializeField] public float sphereRadius = 100;

        [SerializeField] protected GameObject SpentCrystalPrefab;

        [SerializeField] protected List<CrystalModelData> crystalModels;
        [SerializeField] protected bool shipImpactEffects = true;
        [SerializeField] bool RespawnOnImpact;
        [SerializeField] HapticType ImpactHapticType;

        [Header("Data Containers")]
        [SerializeField] ThemeManagerDataContainerSO _themeManagerData;
        #endregion

        [Header("Crystal Effects")]
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        List<ScriptableObject> _crystalImpactEffects;

        Vector3 origin = Vector3.zero;

        protected Material tempMaterial;
        List<Collider> collisions;

        /// <summary>
        /// 
        /// </summary>
        private static int _layerName;

        protected virtual void Awake()
        {
            collisions = new List<Collider>();
            
            // Initialized Crystal game object layer, assign it to "Crystals"
            _layerName = LayerMask.NameToLayer("Crystals");
            gameObject.layer = _layerName;
        }


        protected virtual void Start()
        {
            crystalProperties.crystalValue = crystalProperties.fuelAmount * transform.lossyScale.x;
            AddSelfToNode();
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            collisions.Add(other);
        }

        protected virtual void Update()
        {
            if (collisions.Count > 0 && collisions[0] != null)
                Collide(collisions[0]);

            collisions.Clear();
        }

        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties, IShip ship)
        {
            foreach (IImpactEffect effect in _crystalImpactEffects)
            {
                effect.Execute(new ImpactContext
                {
                    CrystalProperties = crystalProperties,
                    ShipStatus = ship.ShipStatus
                });
            }
        }

        /*public void PerformCrystalImpactEffects(CrystalProperties crystalProperties, IShip ship)
        {
            foreach (CrystalImpactEffects effect in crystalImpactEffects)
            {
                switch (effect)
                {
                    case CrystalImpactEffects.PlayHaptics:
                        if (!ship.ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(ImpactHapticType);
                        break;
                    case CrystalImpactEffects.ReduceSpeed:
                        ship.ShipStatus.ShipTransformer.ModifyThrottle(.1f, 3);  // TODO: Magic numbers
                        break;
                    case CrystalImpactEffects.AreaOfEffectExplosion:
                        var AOEExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
                        AOEExplosion.Initialize(new AOEExplosion.InitializeStruct
                        {
                            OwnTeam = OwnTeam,
                            Ship = ship,
                            OverrideMaterial = AOEExplosionMaterial,
                            MaxScale = maxExplosionScale,
                            AnnonymousExplosion = true
                        });
                        AOEExplosion.SetPositionAndRotation(transform.position, transform.rotation);
                        AOEExplosion.Detonate();
                        break;
                    case CrystalImpactEffects.AdjustLevel:
                        ship.ShipStatus.ResourceSystem.AdjustLevel(crystalProperties.Element, crystalProperties.crystalValue);
                        break;
                }
            }
        }*/

        protected virtual void Collide(Collider other)
        {
            IShip ship;

            if (other.gameObject.IsLayer("Ships"))
            {
                if (!other.TryGetComponent(out ship))
                    return;

                if (OwnTeam == Teams.None || OwnTeam == ship.ShipStatus.Team)
                {
                    if (shipImpactEffects)
                    {
                        ship.PerformCrystalImpactEffects(crystalProperties);
                        if (ship.ShipStatus.AIPilot != null)
                        {
                            AIPilot aiPilot = ship.ShipStatus.AIPilot;

                            aiPilot.aggressiveness = aiPilot.defaultAggressiveness;
                            aiPilot.throttle = aiPilot.defaultThrottle;
                        }
                    }

                    // TODO - Add Event channels here rather than calling singletons directly.
                    if (StatsManager.Instance != null)
                        StatsManager.Instance.CrystalCollected(ship, crystalProperties);
                }
                else return;
            }
            //else if (other.gameObject.IsLayer("Projectiles"))
            //{
            //    ship = other.GetComponent<Projectile>().Ship;
            //    projectile = other.GetComponent<Projectile>();
            //    if (Team == Teams.None || Team == ship.Team)
            //    {
            //        if (shipImpactEffects)
            //        {
            //            projectile.PerformCrystalImpactEffects(crystalProperties);
            //            if (ship.TryGetComponent<AIPilot>(out var aiPilot))
            //            {
            //                aiPilot.aggressiveness = aiPilot.defaultAggressiveness;
            //                aiPilot.throttle = aiPilot.defaultThrottle;
            //            }
            //        }
                    
            //        if (StatsManager.Instance != null)
            //            StatsManager.Instance.CrystalCollected(ship, crystalProperties);
            //    }
            //    else return;
            //}
            else return;

            //
            // Do the crystal stuff that always happens (ship/projectile independent)
            //
            PerformCrystalImpactEffects(crystalProperties, ship);

            Explode(ship);
            
            PlayExplosionAudio();

            // Move the Crystal
            if (RespawnOnImpact)
            {
                foreach (var model in crystalModels)
                    model.model.GetComponent<FadeIn>().StartFadeIn();

                transform.SetPositionAndRotation(UnityEngine.Random.insideUnitSphere * sphereRadius + origin, UnityEngine.Random.rotation);
                OnCrystalMove?.Invoke();

                UpdateSelfWithNode();  //TODO: check if we need to remove elmental crystals from the node
            }
            else
            {
                RemoveSelfFromNode();
                Destroy(gameObject);
            }
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
            transform.parent = NodeControlManager.Instance.GetNearestNode(transform.position).transform;
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