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

        [SerializeField] private ProjectileImpactor projectileImpactor;

        public float Charge { get; private set; }

        #region Deprecated
        [SerializeField] protected List<TrailBlockImpactEffects> trailBlockImpactEffects;
        [SerializeField, RequireInterface(typeof(IImpactEffect))] protected List<ScriptableObject> _trailBlockImpactEffects;
        [SerializeField] List<ShipImpactEffects> shipImpactEffects;
        [SerializeField, RequireInterface(typeof(IImpactEffect))] protected List<ScriptableObject> _shipImpactEffects;
        [SerializeField] List<CrystalImpactEffects> crystalImpactEffects;
        [SerializeField, RequireInterface(typeof(IImpactEffect))] protected List<ScriptableObject> _crystalImpactEffects;
        [SerializeField] protected List<TrailBlockImpactEffects> endEffects;
        [SerializeField, RequireInterface(typeof(IImpactEffect))] protected List<ScriptableObject> _endEffects;
        #endregion

        public float ProjectileTime;

        [SerializeField] private bool spike = false;
        [SerializeField] private float growthRate = 1.0f;
        [SerializeField] private bool friendlyFire = false;

        [Header("Data Containers")]
        [SerializeField] private ThemeManagerDataContainerSO _themeManagerData;

        protected PoolManager _poolManager;            // kept for compat; we’ll prefer PooledObject.Manager
        public Teams OwnTeam { get; private set; }
        public IShipStatus ShipStatus { get; private set; }

        // NEW: cached renderer/MPB + pooled metadata
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _mpb;
        private PooledObject _pooled;

        private Coroutine moveCoroutine;

        private void Awake()
        {
            InitialScale = transform.localScale;
            _renderer = GetComponent<MeshRenderer>();
            _mpb = new MaterialPropertyBlock();
            _pooled = GetComponent<PooledObject>(); // added by PoolManagerBase on instantiation
        }

        private void Start()
        {
            if (spike && _renderer != null)
            {
                // Use shared material to avoid per-instance instantiation
                var teamMat = _themeManagerData != null ? _themeManagerData.GetTeamSpikeMaterial(OwnTeam) : null;
                if (teamMat != null) _renderer.sharedMaterial = teamMat;

                // Drive opacity via MPB (no material copies)
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat("_Opacity", 0.5f);
                _renderer.SetPropertyBlock(_mpb);
            }
        }

        public virtual void Initialize(PoolManager poolManager, Teams ownTeam, IShipStatus shipStatus, float charge)
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

        public void LaunchProjectile(float projectileTime)
        {
            if (spike && _renderer != null)
            {
                transform.localScale = new Vector3(.4f, .4f, 2f);

                // keep opacity at 0.5 initially via MPB
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat("_Opacity", 0.5f);
                _renderer.SetPropertyBlock(_mpb);
            }

            if (moveCoroutine != null) StopCoroutine(moveCoroutine);
            moveCoroutine = StartCoroutine(MoveProjectileCoroutine(projectileTime));
        }

        public IEnumerator MoveProjectileCoroutine(float projectileTime)
        {
            float elapsedTime = 0f;

            while (elapsedTime < projectileTime)
            {
                // movement
                Vector3 moveDistance = Velocity * Time.deltaTime * Mathf.Cos(elapsedTime * Mathf.PI / (2f * projectileTime));
                transform.position += moveDistance;

                if (spike && _renderer != null)
                {
                    // fade near the end
                    float percentRemaining = elapsedTime / projectileTime;
                    if (percentRemaining > 0.9f)
                    {
                        _renderer.GetPropertyBlock(_mpb);
                        _mpb.SetFloat("_Opacity", 1f - Mathf.Pow(percentRemaining, 4f));
                        _renderer.SetPropertyBlock(_mpb);
                    }

                    // optional stretch:
                    // var stretchedPoint = new Vector3(.5f, .5f, elapsedTime * growthRate);
                    // transform.localScale = stretchedPoint;
                }

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            projectileImpactor.ExecuteEndEffects();
            ReturnToPool();
        }

        public void Stop()
        {
            if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        }

        // NEW: pool return without tag, preferring metadata
        public void ReturnToPool()
        {
            if (_pooled != null && _pooled.Manager != null)
            {
                _pooled.Manager.ReturnToPool(gameObject);
            }
            else if (_poolManager != null)
            {
                _poolManager.ReturnToPool(gameObject);
            }
            else
            {
                // Last resort to avoid leaks in misconfigured scenes
                var mgr = GetComponentInParent<PoolManagerBase>();
                if (mgr != null) mgr.ReturnToPool(gameObject);
                else Destroy(gameObject);
            }
        }
    }
}
