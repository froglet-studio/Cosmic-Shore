using CosmicShore.App.Systems.Audio;
using CosmicShore.Core;
using CosmicShore.Game.IO;
using CosmicShore.Game.Projectiles;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Game.AI;

namespace CosmicShore.Environment.FlowField
{
    public class Crystal : NodeItem
    {
        #region Events
        public delegate void CrystalMove();
        public static event CrystalMove OnCrystalMove;
        #endregion

        #region Inspector Fields
        [SerializeField] public CrystalProperties crystalProperties;
        [SerializeField] public float sphereRadius = 100;
        [SerializeField] protected GameObject SpentCrystalPrefab;
        [SerializeField] protected GameObject CrystalModel; 
        [SerializeField] protected Material explodingMaterial;
        [SerializeField] protected Material defaultMaterial;
        [SerializeField] protected Material inactiveCrystalMaterial;
        [SerializeField] protected bool shipImpactEffects = true;
        #endregion

        [Header("Optional Crystal Effects")]
        #region Optional Fields
        [SerializeField] List<CrystalImpactEffects> crystalImpactEffects;
        [SerializeField] GameObject AOEPrefab;
        [SerializeField] float maxExplosionScale;
        [SerializeField] Material AOEExplosionMaterial;
        #endregion

        Vector3 origin = Vector3.zero;

        protected Material tempMaterial;
        List<Collider> collisions;

        protected virtual void Awake()
        {
            collisions = new List<Collider>();
        }

        protected virtual void Start()
        {
            AddSelfToNode();
        }

        protected virtual void OnEnable()
        {
            //AddSelfToNode();
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

        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties, Ship ship)
        {
            foreach (CrystalImpactEffects effect in crystalImpactEffects)
            {
                switch (effect)
                {
                    case CrystalImpactEffects.PlayFakeCrystalHaptics:   // TODO: P1 need to merge haptics and take an enum to determine which on to play
                        if (!ship.ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.FakeCrystalCollision);//.PlayFakeCrystalImpactHaptics();
                        break;
                    case CrystalImpactEffects.ReduceSpeed:
                        ship.ShipTransformer.ModifyThrottle(.1f, 3);  // TODO: Magic numbers
                        break;
                    case CrystalImpactEffects.AreaOfEffectExplosion:
                        var AOEExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
                        AOEExplosion.Material = AOEExplosionMaterial;
                        AOEExplosion.Team = Team;
                        AOEExplosion.Ship = ship;
                        AOEExplosion.SetPositionAndRotation(transform.position, transform.rotation);
                        AOEExplosion.MaxScale = maxExplosionScale;
                        AOEExplosion.AnonymousExplosion = true;
                        break;
                    case CrystalImpactEffects.IncrementLevel: // TODO: add amount based on crystal scale
                        ship.ResourceSystem.IncrementLevel(crystalProperties.Element);
                        break;
                }
            }
        }

        protected virtual void Collide(Collider other)
        {
            Ship ship;
            Projectile projectile;
            if (IsShip(other.gameObject))
            {
                ship = other.GetComponent<ShipGeometry>().Ship;
                if (Team == Teams.None || Team == ship.Team)
                {
                    if (shipImpactEffects)
                    {
                        ship.PerformCrystalImpactEffects(crystalProperties);
                        if (ship.TryGetComponent<AIPilot>(out var aiPilot))
                        {
                            aiPilot.aggressiveness = aiPilot.defaultAggressiveness;
                            aiPilot.throttle = aiPilot.defaultThrottle;
                        }
                    }
                    else if (StatsManager.Instance != null)
                        StatsManager.Instance.CrystalCollected(ship, crystalProperties);
                }
                else return;
            }
            else if (IsProjectile(other.gameObject))
            {
                ship = other.GetComponent<Projectile>().Ship;
                projectile = other.GetComponent<Projectile>();
                if (Team == Teams.None || Team == ship.Team)
                {
                    if (shipImpactEffects)
                    {
                        projectile.PerformCrystalImpactEffects(crystalProperties);
                        if (ship.TryGetComponent<AIPilot>(out var aiPilot))
                        {
                            aiPilot.aggressiveness = aiPilot.defaultAggressiveness;
                            aiPilot.throttle = aiPilot.defaultThrottle;
                        }
                    }
                    else if (StatsManager.Instance != null)
                        StatsManager.Instance.CrystalCollected(ship, crystalProperties);
                }
                else return;
            } 
            else return;

            //
            // Do the crystal stuff that always happens (ship/projectile independent)
            //
            PerformCrystalImpactEffects(crystalProperties, ship);

            Explode(ship);

            PlayExplosionAudio();

            // Move the Crystal
            StartCoroutine(CrystalModel.GetComponent<FadeIn>().FadeInCoroutine());
            transform.SetPositionAndRotation(Random.insideUnitSphere * sphereRadius + origin, UnityEngine.Random.rotation);
            OnCrystalMove?.Invoke();

            UpdateSelfWithNode();
        }

        protected void Explode(Ship ship)
        {
            tempMaterial = new Material(explodingMaterial);
            var spentCrystal = Instantiate(SpentCrystalPrefab);
            spentCrystal.transform.position = transform.position;
            spentCrystal.transform.localEulerAngles = transform.localEulerAngles;
            spentCrystal.GetComponent<Renderer>().material = tempMaterial;

            spentCrystal.GetComponent<Impact>().HandleImpact(
                ship.transform.forward * ship.GetComponent<ShipStatus>().Speed, tempMaterial, ship.Player.PlayerName);
        }

        protected void PlayExplosionAudio()
        {
            AudioSource audioSource = GetComponent<AudioSource>();
            AudioSystem.Instance.PlaySFXClip(audioSource.clip, audioSource);
        }

        // TODO: P1 move to static ObjectResolver class
        protected bool IsShip(GameObject go)
        {
            return go.layer == LayerMask.NameToLayer("Ships");
        }

        // TODO: P1 move to static ObjectResolver class
        protected bool IsProjectile(GameObject go)
        {
            return go.layer == LayerMask.NameToLayer("Projectiles");
        }

        public void SetOrigin(Vector3 origin)
        {
            this.origin = origin;
        }

        public void ActivateCrystal() // TODO: handle this with crystal.Activate()
        {
            transform.parent = NodeControlManager.Instance.transform;
            gameObject.GetComponent<SphereCollider>().enabled = true;
            enabled = true;
            GetComponentInChildren<Renderer>().material = inactiveCrystalMaterial; // TODO: make a crytal material set that this pulls from using the element
            if (lerpCrystalMaterialCoroutine != null) StopCoroutine(lerpCrystalMaterialCoroutine);
            lerpCrystalMaterialCoroutine = StartCoroutine(LerpCrystalMaterialCoroutine(defaultMaterial));
        }

        public void Steal(Teams team, float duration)
        {
            Team = team;
            CrystalModel.GetComponent<Renderer>().material = Hangar.Instance.GetTeamCrystalMaterial(team);
            if (lerpCrystalMaterialCoroutine != null) StopCoroutine(lerpCrystalMaterialCoroutine);
            lerpCrystalMaterialCoroutine = StartCoroutine(LerpCrystalMaterialCoroutine(Hangar.Instance.GetTeamCrystalMaterial(team)));
            StartCoroutine(DecayingTheftCoroutine(duration));
        }

        IEnumerator DecayingTheftCoroutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            Team = Teams.None;
            CrystalModel.GetComponent<Renderer>().material = defaultMaterial;
            if (lerpCrystalMaterialCoroutine != null) StopCoroutine(lerpCrystalMaterialCoroutine);
            lerpCrystalMaterialCoroutine = StartCoroutine(LerpCrystalMaterialCoroutine(defaultMaterial));

        }

        Coroutine lerpCrystalMaterialCoroutine;
        IEnumerator LerpCrystalMaterialCoroutine(Material targetMaterial, float lerpDuration = 3f)
        {
            Material tempMaterial = new Material(CrystalModel.GetComponent<Renderer>().material);
            CrystalModel.GetComponent<Renderer>().material = tempMaterial;

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

                yield return null;//new WaitForSeconds(.05f);
            }

            CrystalModel.GetComponent<Renderer>().material = targetMaterial;
        }

    }
}