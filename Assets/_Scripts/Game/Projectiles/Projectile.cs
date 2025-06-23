using System.Collections;
using System.Collections.Generic;
using CosmicShore.Game.IO;
using CosmicShore.Core;
using UnityEngine;


namespace CosmicShore.Game.Projectiles
{
    public class Projectile : MonoBehaviour
    {
        public Vector3 Velocity;
        public bool ImpactOnEnd;
        public float Inertia = 1;
        
        [HideInInspector] public Vector3 InitialScale;
        [SerializeField] protected List<TrailBlockImpactEffects> trailBlockImpactEffects;
        [SerializeField] List<ShipImpactEffects> shipImpactEffects;
        [SerializeField] List<CrystalImpactEffects> crystalImpactEffects;
        [SerializeField] protected List<TrailBlockImpactEffects> endEffects;

        public float ProjectileTime;

        [SerializeField] bool spike = false;
        [SerializeField] float growthRate = 1.0f;
        [SerializeField] bool friendlyFire = false;

        MeshRenderer meshRenderer;

        protected PoolManager poolManager;
        public Teams Team { get; private set; }
        public IShipStatus ShipStatus { get; private set; }

        private void Awake()
        {  
            InitialScale = transform.localScale;
        }

        void Start()
        {
            poolManager = GetComponentInParent<PoolManager>();
            /*Ship = poolManager.Ship;
            shipStatus = Ship.ShipStatus;
            if (Ship == null)
            {
                Debug.LogWarning("Projectile script found no valid IShip reference.");
                Ship = GetComponentInParent<IShip>();
                if (Ship == null)
                {
                    Debug.LogError("Projectile script requires a valid IShip reference.");
                    return;
                }
            }*/
            if (spike) 
            {
                meshRenderer = gameObject.GetComponent<MeshRenderer>();
                meshRenderer.material = ThemeManager.Instance.GetTeamSpikeMaterial(Team);
                meshRenderer.material.SetFloat("_Opacity", .5f);
            }
        }

        public virtual void Initialize(Teams team, IShipStatus shipStatus, float charge) // Later remove [float charge] from here.
        {
            Team = team;
            ShipStatus = shipStatus;

            if (TryGetComponent(out Gun gun) && ShipStatus != null)
            {
                gun.Initialize(ShipStatus);
            }
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            HandleCollision(other);
        }

        void HandleCollision(Collider other)
        {
            if (other.TryGetComponent<TrailBlock>(out var trailBlock))
            {
                if (!friendlyFire && trailBlock.Team == Team)
                    return;

                PerformTrailImpactEffects(trailBlock.TrailBlockProperties);
            }
            if (other.TryGetComponent<ShipGeometry>(out var shipGeometry))
            {
                if (shipGeometry.Ship.ShipStatus.Team == Team)
                    return;

                PerformShipImpactEffects(shipGeometry);
            }
        }

        protected virtual void PerformTrailImpactEffects(TrailBlockProperties trailBlockProperties)
        {
            foreach (TrailBlockImpactEffects effect in trailBlockImpactEffects)
            {
                switch (effect)
                {
                    case TrailBlockImpactEffects.DeactivateTrailBlock:
                        trailBlockProperties.trailBlock.Damage(Velocity * Inertia, ShipStatus.Team, ShipStatus.PlayerName);
                        break;
                    case TrailBlockImpactEffects.Steal:
                        trailBlockProperties.trailBlock.Steal(ShipStatus.PlayerName, Team);
                        break;
                    case TrailBlockImpactEffects.Shield:
                        trailBlockProperties.trailBlock.ActivateShield(.5f);
                        break;
                    case TrailBlockImpactEffects.Stop:

                        if (!trailBlockProperties.trailBlock.GetComponent<Boid>()) Stop();
                        else poolManager.ReturnToPool(gameObject, gameObject.tag);
                        break;
                    case TrailBlockImpactEffects.Fire:
                        GetComponent<LoadedGun>().FireGun();
                        break;
                    case TrailBlockImpactEffects.Explode:
                        Debug.LogWarning("Non - implemented TrailExplode");
                        // ((ExplodableProjectile)this).Detonate();         -> better to override the method in child ExplodableProjectile class.
                        break;

                }
            }
        }

        protected virtual void PerformEndEffects()
        {
            foreach (TrailBlockImpactEffects effect in endEffects)
            {
                switch (effect)
                {
                    case TrailBlockImpactEffects.Stop:
                        Stop();
                        poolManager.ReturnToPool(gameObject, gameObject.tag);
                        break;
                    case TrailBlockImpactEffects.Fire:
                        GetComponent<LoadedGun>().FireGun();
                        break;
                    case TrailBlockImpactEffects.Explode:
                        Debug.Log("EndExplode");
                        // ((ExplodableProjectile)this).Detonate();         -> better to override the method in child ExplodableProjectile class.
                        break;
                }
            }
        }

        protected virtual void PerformShipImpactEffects(ShipGeometry shipGeometry)
        {
            foreach (ShipImpactEffects effect in shipImpactEffects)
            {
                switch (effect)
                {
                    case ShipImpactEffects.TrailSpawnerCooldown:
                        shipGeometry.Ship.ShipStatus.TrailSpawner.PauseTrailSpawner();
                        shipGeometry.Ship.ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(10);
                        break;
                    case ShipImpactEffects.PlayHaptics:
                        if (!shipGeometry.Ship.ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.ShipCollision);//.PlayShipCollisionHaptics();
                        break;
                    case ShipImpactEffects.SpinAround:
                        shipGeometry.Ship.Transform.localRotation = Quaternion.LookRotation(Velocity);
                        break;
                    case ShipImpactEffects.Knockback:
                        shipGeometry.Ship.ShipStatus.ShipTransformer.ModifyVelocity(Velocity * 100,2);
                        break;
                    case ShipImpactEffects.Stun:
                        shipGeometry.Ship.ShipStatus.ShipTransformer.ModifyThrottle(.1f, 10);
                        break;
                    case ShipImpactEffects.Charm:
                        shipGeometry.Ship.ShipStatus.TrailSpawner.Charm(ShipStatus, 7);
                        break;
                }
            }
        }

        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties)
        {
            foreach (CrystalImpactEffects effect in crystalImpactEffects)
            {
                switch (effect)
                {
                    case CrystalImpactEffects.PlayHaptics:
                        if (!ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.CrystalCollision);//.PlayCrystalImpactHaptics();
                        break;
                    //case CrystalImpactEffects.AreaOfEffectExplosion:
                    //    var AOEExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
                    //    AOEExplosion.Ship = this;
                    //    AOEExplosion.SetPositionAndRotation(transform.position, transform.rotation);
                    //    AOEExplosion.MaxScale = Mathf.Max(minExplosionScale, ResourceSystem.CurrentAmmo * maxExplosionScale);
                    //    break;
                    case CrystalImpactEffects.StealCrystal:
                        crystalProperties.crystal.Steal(Team, 7f);
                        break;
                    case CrystalImpactEffects.GainFullAmmo:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(0, ShipStatus.ResourceSystem.Resources[0].MaxAmount); // Move to single system
                        break;
                }
            }
        }

        public void LaunchProjectile(float projectileTime)
        {
            if (spike)
            {
                transform.localScale = new Vector3(.4f, .4f, 2);
                GetComponent<MeshRenderer>().material.SetFloat("_Opacity", .5f);
            }

            if (moveCoroutine != null)
                StopCoroutine(moveCoroutine);

            moveCoroutine = StartCoroutine(MoveProjectileCoroutine(projectileTime));
        }

        Coroutine moveCoroutine;

        public IEnumerator MoveProjectileCoroutine(float projectileTime)
        {
            var elapsedTime = 0f;
            while (elapsedTime < projectileTime)
            {
                // Calculate movement for this frame
                Vector3 moveDistance = Velocity * Time.deltaTime * Mathf.Cos(elapsedTime * Mathf.PI / (2 * projectileTime));
                Vector3 nextPosition = transform.position + moveDistance;

                // Only check for raycasting collisions if spike is true
                if (spike)
                {
                    if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, moveDistance.magnitude))
                    {
                        transform.position = hit.point;
                        HandleCollision(hit.collider);
                    }

                    var stretchedPoint = new Vector3(.5f, .5f, elapsedTime * growthRate);
                    var percentRemaining = elapsedTime / projectileTime;
                    if (percentRemaining > .9) meshRenderer.material.SetFloat("_Opacity", 1 - Mathf.Pow(percentRemaining, 4));
                    //transform.localScale = stretchedPoint;
                }

                // Move the projectile to the next position
                transform.position = nextPosition;

                elapsedTime += Time.deltaTime;
                yield return null;
            }
            if (ImpactOnEnd) PerformEndEffects();
            poolManager.ReturnToPool(gameObject, gameObject.tag);
        }

        public void Stop() 
        {
            if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        }

    }
}