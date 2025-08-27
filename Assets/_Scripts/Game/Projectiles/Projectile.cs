using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using UnityEngine;


namespace CosmicShore.Game.Projectiles
{
    public class Projectile : MonoBehaviour
    {
        public Vector3 Velocity;

        [HideInInspector] public Vector3 InitialScale;
        
        [SerializeField]
        ProjectileImpactor projectileImpactor;

        public float Charge { get; private set; }
        

        public float ProjectileTime;

        [SerializeField] bool spike = false;
        [SerializeField] float growthRate = 1.0f;
        [SerializeField] bool friendlyFire = false;

        [Header("Data Containers")]
        [SerializeField] 
        ThemeManagerDataContainerSO _themeManagerData;

        /*[SerializeField] bool _pierceCrystal;
        [SerializeField] bool _pierceShip;
        [SerializeField] bool _piercePrism;*/

        MeshRenderer meshRenderer;

        protected PoolManager _poolManager;
        public Teams OwnTeam { get; private set; }
        public IShipStatus ShipStatus { get; private set; }

        private void Awake()
        {  
            InitialScale = transform.localScale;
        }

        void Start()
        {
            if (spike) 
            {
                meshRenderer = gameObject.GetComponent<MeshRenderer>();
                meshRenderer.material = _themeManagerData.GetTeamSpikeMaterial(OwnTeam);
                meshRenderer.material.SetFloat("_Opacity", .5f);
            }
        }

        public virtual void Initialize(PoolManager poolManager, Teams ownTeam, IShipStatus shipStatus, float charge) // Later remove [float charge] from here.
        {
            OwnTeam = ownTeam;
            ShipStatus = shipStatus;
            _poolManager = poolManager;
            Charge = charge;
            
            if (TryGetComponent(out Gun gun) && ShipStatus != null)
            {
                gun.Initialize(ShipStatus);
            }
        }

        public bool DisallowImpactOnPrism(Teams trailBlockTeam) => !friendlyFire && trailBlockTeam == OwnTeam;

        public bool DisallowImpactOnVessel(Teams vesselTeam) => vesselTeam == OwnTeam;
        
        /*protected virtual void OnTriggerEnter(Collider other)
        { 
            HandleCollision(other);
        }*/

        /*void HandleCollision(Collider other)
        {
            if (other.TryGetComponent<TrailBlock>(out var trailBlock))
            {
                if (!friendlyFire && trailBlock.Team == OwnTeam)
                    return;

                PerformTrailImpactEffects(trailBlock.TrailBlockProperties);

                // if (!_piercePrism) ExecuteStopEffect();
            }
            if (other.TryGetComponent<IShipStatus>(out var shipStatus))
            {
                if (shipStatus.Team == OwnTeam)
                    return;

                PerformShipImpactEffects(shipStatus);

                // if (!_pierceShip) ExecuteStopEffect();
            }
        }*/

        // Deprecated - New Impact Effect System has been implemented. Remove it once all tested.
        /*protected virtual void PerformTrailImpactEffects(TrailBlockProperties trailBlockProperties)
        {
            foreach (TrailBlockImpactEffects effect in trailBlockImpactEffects)
            {
                switch (effect)
                {
                    case TrailBlockImpactEffects.DeactivateTrailBlock:
                        trailBlockProperties.trailBlock.Damage(Velocity * Inertia, ShipStatus.Team, ShipStatus.PlayerName);
                        break;
                    case TrailBlockImpactEffects.Steal:
                        trailBlockProperties.trailBlock.Steal(ShipStatus.PlayerName, OwnTeam);
                        break;
                    case TrailBlockImpactEffects.Shield:
                        trailBlockProperties.trailBlock.ActivateShield(.5f);
                        break;
                    case TrailBlockImpactEffects.Stop:

                        if (!trailBlockProperties.trailBlock.GetComponent<Boid>()) Stop();
                        else _poolManager.ReturnToPool(gameObject, gameObject.tag);
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

            foreach (ITrailBlockImpactEffect effect in _trailBlockImpactEffects.Cast<ITrailBlockImpactEffect>())
            {
                if (effect is null)
                    continue;

                effect.Execute(new ImpactEffectData(ShipStatus, null, Vector3.zero), trailBlockProperties); // TODO : impacted vector is not correct here.
            }
        }*/

        // Deprecated - New Impact Effect System has been implemented. Remove it once all tested.
        /*protected virtual void PerformEndEffects()
        {
            foreach (TrailBlockImpactEffects effect in endEffects)
            {
                switch (effect)
                {
                    case TrailBlockImpactEffects.Stop:
                        Stop();
                        _poolManager.ReturnToPool(gameObject, gameObject.tag);
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

            foreach (IBaseImpactEffect effect in _endEffects.Cast<IBaseImpactEffect>())
            {
                if (effect is null)
                    continue;

                effect.Execute(new ImpactEffectData(ShipStatus, null, Vector3.zero)); // TODO : impacted vector is not correct here.
            }
        }*/

        // Deprecated - New Impact Effect System has been implemented. Remove it once all tested.
        /*protected virtual void PerformShipImpactEffects(IShipStatus shipStatus)
        {
            foreach (ShipImpactEffects effect in shipImpactEffects)
            {
                switch (effect)
                {
                    case ShipImpactEffects.TrailSpawnerCooldown:
                        shipStatus.Ship.ShipStatus.TrailSpawner.PauseTrailSpawner();
                        shipStatus.Ship.ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(10);
                        break;
                    case ShipImpactEffects.PlayHaptics:
                        if (!shipStatus.Ship.ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.ShipCollision);//.PlayShipCollisionHaptics();
                        break;
                    case ShipImpactEffects.SpinAround:
                        shipStatus.Ship.Transform.localRotation = Quaternion.LookRotation(Velocity);
                        break;
                    case ShipImpactEffects.Knockback:
                        shipStatus.Ship.ShipStatus.ShipTransformer.ModifyVelocity(Velocity * 100,2);
                        break;
                    case ShipImpactEffects.Stun:
                        shipStatus.Ship.ShipStatus.ShipTransformer.ModifyThrottle(.1f, 10);
                        break;
                    case ShipImpactEffects.Charm:
                        shipStatus.Ship.ShipStatus.TrailSpawner.Charm(ShipStatus, 7);
                        break;
                }
            }

            foreach (IBaseImpactEffect effect in _endEffects.Cast<IBaseImpactEffect>())
            {
                if (effect is null)
                    continue;

                effect.Execute(new ImpactEffectData(ShipStatus, null, Vector3.zero)); // TODO : impacted vector is not correct here.
            }
        }*/

        // Deprecated - New Impact Effect System has been implemented. Remove it once all tested.
        /*protected virtual void PerformCrystalImpactEffects(CrystalProperties crystalProperties)
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
                        crystalProperties.crystal.Steal(OwnTeam, 7f);
                        break;
                    case CrystalImpactEffects.GainFullAmmo:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(0, ShipStatus.ResourceSystem.Resources[0].MaxAmount); // Move to single system
                        break;
                }
            }

            foreach (ICrystalImpactEffect effect in _crystalImpactEffects.Cast<ICrystalImpactEffect>())
            {
                if (effect is null)
                    continue;

                effect.Execute(new ImpactEffectData(ShipStatus, null, Vector3.zero), crystalProperties); // TODO : impacted vector is not correct here.
            }
        }*/

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
                    // This should be done from R_ProjectileImpactor
                    /*if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, moveDistance.magnitude))
                    {
                        transform.position = hit.point;
                        HandleCollision(hit.collider);
                    }*/

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
            //if (endEffects.Count > 0) PerformEndEffects();
            projectileImpactor.ExecuteEndEffects();
            ReturnToPool();
        }

        public void Stop() 
        {
            if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        }
        
        public void ReturnToPool() => _poolManager.ReturnToPool(gameObject, gameObject.tag);
    }
}