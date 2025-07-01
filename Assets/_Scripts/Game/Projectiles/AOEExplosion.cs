using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game.IO;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class AOEExplosion : ElementalShipComponent
    {
        [HideInInspector] public float speed;

        protected const float PI_OVER_TWO = Mathf.PI / 2;
        protected Vector3 MaxScaleVector;
        protected float Inertia = 70;
        

        [Header("Explosion Settings")]
        [SerializeField] protected float ExplosionDuration = 2f;
        [SerializeField] protected float ExplosionDelay = 0.2f;

        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        List<ScriptableObject> _shipImpactEffects;


        [SerializeField] private bool affectSelf = false;
        [SerializeField] private bool destructive = true;
        [SerializeField] private bool devastating = false;
        [SerializeField] bool shielding = false;

        protected GameObject container;

        // Material and Team
        public Material Material { get; private set; }
        public Teams Team { get; private set; }
        public IShip Ship { get; private set; }
        public bool AnonymousExplosion { get; private set; }
        public float MaxScale { get; private set; } = 200f;

        public void Initialize(InitializeStruct initStruct)
        {
            Team = initStruct.OwnTeam;
            AnonymousExplosion = initStruct.AnnonymousExplosion;
            Ship = initStruct.Ship;
            MaxScale = initStruct.MaxScale;

            if (Ship == null)
            {
                Debug.LogError("Ship is not initialized in AOEExplosion!");
                return;
            }

            Material = initStruct.OverrideMaterial != null ? initStruct.OverrideMaterial : Ship.ShipStatus.AOEExplosionMaterial;
        }

        public virtual void InitializeAndDetonate(IShip ship)
        {
            Ship = ship;
            InitializeProperties();
            StartCoroutine(ExplodeCoroutine());
        }

        public void Detonate() => StartCoroutine(ExplodeCoroutine());

        private void InitializeProperties()
        {
            speed = MaxScale / ExplosionDuration;
            if (container == null) container = new GameObject("AOEContainer");

            if (Team == Teams.Unassigned)
                Team = Ship.ShipStatus.Team;
            if (Material == null)
                Material = new Material(Ship.ShipStatus.AOEExplosionMaterial);

            // SetParent with false to take container's world position
            //transform.SetParent(container.transform, worldPositionStays: false);
            MaxScaleVector = new Vector3(MaxScale, MaxScale, MaxScale);
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            var impactVector = (other.transform.position - transform.position).normalized * speed * Inertia ;

            if (other.TryGetComponent<TrailBlock>(out var trailBlock))
            {
                if ((trailBlock.Team != Team || affectSelf) && trailBlock.TrailBlockProperties.IsSuperShielded)
                {
                    trailBlock.DeactivateShields();
                    Destroy(gameObject);    // TODO: This seems wrong...
                } 
                if ((trailBlock.Team == Team && !affectSelf) || !destructive)
                {
                    if (shielding && trailBlock.Team == Team)
                        trailBlock.ActivateShield();
                    else 
                        trailBlock.ActivateShield(2f);
                    return;
                }

                if (AnonymousExplosion)
                    trailBlock.Damage(impactVector, Teams.None, "🔥GuyFawkes🔥", devastating);
                else
                    trailBlock.Damage(impactVector, Ship.ShipStatus.Team, Ship.ShipStatus.Player.PlayerName, devastating);
            }
            else if (other.TryGetComponent<IShipStatus>(out var shipStatus))
            {
                if (shipStatus.Team == Team && !affectSelf)
                    return;

                PerformShipImpactEffects(shipStatus, impactVector);
            }
            else
            {
                Debug.Log("AOEExplosion.OnTriggerEnter - not a ship or a trail block: " + other.name);
            }
        }

        protected virtual IEnumerator ExplodeCoroutine()
        {
            yield return new WaitForSeconds(ExplosionDelay);

            if (TryGetComponent<MeshRenderer>(out var meshRenderer))
                meshRenderer.material = Material;

            var elapsedTime = 0f;
            while (elapsedTime < ExplosionDuration)
            {
                elapsedTime += Time.deltaTime;
                var easing = Mathf.Sin((elapsedTime / ExplosionDuration) * PI_OVER_TWO);
                transform.localScale = Vector3.Lerp(Vector3.zero, MaxScaleVector, easing);
                Material.SetFloat("_Opacity", 1 - easing);
                yield return null;
            }

            Destroy(gameObject);
        }

        protected virtual void PerformShipImpactEffects(IShipStatus shipStatus, Vector3 impactVector)
        {
            foreach (var effect in _shipImpactEffects)
            {
                if (effect is IImpactEffect impactEffect)
                {
                    impactEffect.Execute(new ImpactContext
                    {
                        ShipStatus = shipStatus,
                        ImpactVector = impactVector,
                        OwnTeam = Team
                    });
                }
                else
                {
                    Debug.LogWarning($"Impact effect {effect.name} does not implement IImpactEffect interface.");
                }
            }
        }

        public virtual void SetPositionAndRotation(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }



        public struct InitializeStruct
        {
            public Teams OwnTeam;
            public bool AnnonymousExplosion;
            public IShip Ship;
            public Material OverrideMaterial;
            public float MaxScale;
        }
    }
}
