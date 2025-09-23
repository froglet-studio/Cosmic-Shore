using System.Collections;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class AOEExplosion : ElementalShipComponent
    {
        protected const float PI_OVER_TWO = Mathf.PI / 2;

        [Header("Explosion Settings")]
        [SerializeField] protected float ExplosionDuration = 2f;
        [SerializeField] protected float ExplosionDelay = 0.2f;

        /*[SerializeField] private bool affectSelf = false;
        [SerializeField] private bool destructive = true;
        [SerializeField] private bool devastating = false;
        [SerializeField] bool shielding = false;*/

        protected Vector3 MaxScaleVector;
        protected float Inertia = 70;
        protected float speed;
        protected Vector3 SpawnPosition;
        protected Quaternion SpawnRotation;

        // Material and Team
        public Material Material { get; protected set; }
        public Domains Domain { get; protected set; }
        public IVessel Vessel { get; protected set; }
        public bool AnonymousExplosion { get; protected set; }
        public float MaxScale { get; protected set; } = 200f;
        
        public virtual void Initialize(InitializeStruct initStruct)
        {
            SpawnPosition = initStruct.SpawnPosition;
            SpawnRotation = initStruct.SpawnRotation;
            
            AnonymousExplosion = initStruct.AnnonymousExplosion;
            Vessel = initStruct.Vessel;
            if (Vessel == null)
            {
                Debug.LogError("Vessel is not initialized in AOEExplosion!");
                return;
            }

            Domain = initStruct.OwnDomain;
            if (Domain == Domains.Unassigned)
                Domain = Vessel.VesselStatus.Domain;

            MaxScale = initStruct.MaxScale;
            MaxScaleVector = new Vector3(MaxScale, MaxScale, MaxScale);
            speed = MaxScale / ExplosionDuration;

            Material = initStruct.OverrideMaterial != null ? initStruct.OverrideMaterial : Vessel.VesselStatus.AOEExplosionMaterial;
            if (Material == null)
                Material = new Material(Vessel.VesselStatus.AOEExplosionMaterial);
        }

        public void Detonate() => StartCoroutine(ExplodeCoroutine());

        public Vector3 CalculateImpactVector(Vector3 impacteePosition) =>
            impacteePosition - transform.position.normalized * speed * Inertia ;
        
        // Deprecated - Moved to R_ExplosionImpactor.cs
        /*protected virtual void OnTriggerEnter(Collider other)
        {
            var impactVector = (other.transform.position - transform.position).normalized * speed * Inertia ;

            if (other.TryGetComponent<TrailBlock>(out var trailBlock))
            {
                if ((trailBlock.Team != Team || affectSelf) && trailBlock.PrismProperties.IsSuperShielded)
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
                    trailBlock.Damage(impactVector, Vessel.VesselStatus.Team, Vessel.VesselStatus.Player.PlayerName, devastating);
            }
            else if (other.TryGetComponent<IVesselStatus>(out var vesselStatus))
            {
                if (vesselStatus.Team == Team && !affectSelf)
                    return;

                PerformShipImpactEffects(vesselStatus, impactVector);
            }
            else
            {
                Debug.Log("AOEExplosion.OnTriggerEnter - not a vessel or a trail block: " + other.name);
            }
        }*/

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

        // Deprecated - New Impact Effect System has been implemented. Remove it once all tested.
        /*protected virtual void PerformShipImpactEffects(IVesselStatus vesselStatus, Vector3 impactVector)
        {
            var castedEffects = _shipImpactEffects.Cast<R_IImpactEffect>();

            ShipHelper.ExecuteImpactEffect(
                castedEffects,
                new ImpactEffectData(vesselStatus, null, impactVector)
            );
        }

        public virtual void SetPositionAndRotation(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }*/



        public struct InitializeStruct
        {
            public Domains OwnDomain;
            public bool AnnonymousExplosion;
            public IVessel Vessel;
            public Material OverrideMaterial;
            public float MaxScale;
            public Vector3 SpawnPosition;
            public Quaternion SpawnRotation;
        }
    }
}
