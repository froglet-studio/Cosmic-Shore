using System.Collections;
using UnityEngine;
using CosmicShore.Core;

namespace CosmicShore.Game.Projectiles
{
    public class Projectile : MonoBehaviour
    {
        public Vector3 Velocity { get; set; }
        public Vector3 InitialScale { get; private set; }

        [SerializeField] private ProjectileImpactor projectileImpactor;

        [Header("Projectile Settings")]
        [SerializeField] private bool spike = false;
        [SerializeField] private float growthRate = 1.0f;
        [SerializeField] private bool friendlyFire = false;

        [Header("Data Containers")]
        [SerializeField] private ThemeManagerDataContainerSO _themeManagerData;

        public float Charge { get; private set; }
        public ProjectileType Type { get; private set; }
        public float ProjectileTime { get; private set; }

        public Domains OwnDomain { get; private set; }
        public IVesselStatus VesselStatus { get; private set; }

        private MeshRenderer meshRenderer;
        private Coroutine moveCoroutine;

        // New: Factory reference instead of PoolManager
        private ProjectileFactory _factory;

        private void Awake()
        {
            InitialScale = transform.localScale;
        }

        private void Start()
        {
            if (spike)
            {
                meshRenderer = GetComponent<MeshRenderer>();
                meshRenderer.material = _themeManagerData.GetTeamSpikeMaterial(OwnDomain);
                meshRenderer.material.SetFloat("_Opacity", 0.5f);
            }
        }

        #region Initialization
        public virtual void Initialize(ProjectileFactory factory, Domains ownDomain, IVesselStatus vesselStatus, float charge)
        {
            _factory = factory;
            OwnDomain = ownDomain;
            VesselStatus = vesselStatus;
            Charge = charge;

            if (TryGetComponent(out Gun gun) && VesselStatus != null)
            {
                gun.Initialize(VesselStatus);
            }
        }
        
        public void SetType(ProjectileType type) => Type = type;
        #endregion

        #region Impact Checks
        public bool DisallowImpactOnPrism(Domains trailBlockDomain) => !friendlyFire && trailBlockDomain == OwnDomain;
        public bool DisallowImpactOnVessel(Domains vesselDomain) => vesselDomain == OwnDomain;
        #endregion

        #region Lifecycle
        public void LaunchProjectile(float projectileTime)
        {
            ProjectileTime = projectileTime;

            if (spike)
            {
                transform.localScale = new Vector3(0.4f, 0.4f, 2f);
                GetComponent<MeshRenderer>().material.SetFloat("_Opacity", 0.5f);
            }

            if (moveCoroutine != null)
                StopCoroutine(moveCoroutine);

            moveCoroutine = StartCoroutine(MoveProjectileCoroutine(projectileTime));
        }

        private IEnumerator MoveProjectileCoroutine(float projectileTime)
        {
            float elapsedTime = 0f;
            while (elapsedTime < projectileTime)
            {
                // Calculate movement this frame
                Vector3 moveDistance = Velocity * (Time.deltaTime * Mathf.Cos(elapsedTime * Mathf.PI / (2 * projectileTime)));
                transform.position += moveDistance;

                if (spike)
                {
                    float percentRemaining = elapsedTime / projectileTime;
                    if (percentRemaining > 0.9f && meshRenderer != null)
                        meshRenderer.material.SetFloat("_Opacity", 1 - Mathf.Pow(percentRemaining, 4));
                }

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            projectileImpactor.ExecuteEndEffects();
            ReturnToFactory();
        }

        public void Stop()
        {
            if (moveCoroutine != null)
            {
                StopCoroutine(moveCoroutine);
                moveCoroutine = null;
            }
        }
        #endregion

        #region Return
        public void ReturnToFactory()
        {
            Stop();
            if (_factory != null)
                _factory.ReturnProjectile(this);
            else
                Destroy(gameObject); // fallback in case factory missing
        }
        #endregion
    }
}
