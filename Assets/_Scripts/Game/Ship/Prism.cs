using System;
using UnityEngine;
using CosmicShore.App.Systems.Audio;
using CosmicShore.Utility.ClassExtensions;
using CosmicShore.Game;
using CosmicShore.Utilities;
using UnityEngine.Serialization;

namespace CosmicShore.Core
{
    [RequireComponent(typeof(MaterialPropertyAnimator))]
    [RequireComponent(typeof(PrismScaleAnimator))]
    [RequireComponent(typeof(PrismTeamManager))]
    [RequireComponent(typeof(PrismStateManager))]
    public class Prism : MonoBehaviour
    {
        protected const string DEFAULT_PLAYER_NAME = "DefaultPlayer";

        #region Pool Enforcement

        /// <summary>
        /// Static guard: pool managers increment before Instantiate, decrement after.
        /// Unity is single-threaded so a simple counter is safe.
        /// </summary>
        private static int _poolCreationDepth;
        internal static void BeginPoolCreation() => _poolCreationDepth++;
        internal static void EndPoolCreation() => _poolCreationDepth--;

        /// <summary>True when this instance was created inside a pool's CreateFunc.</summary>
        internal bool CreatedByPool { get; private set; }

        /// <summary>
        /// True when this instance is embedded in a prefab hierarchy (e.g. LifeForm children).
        /// Set by the owning system before Initialize() to exempt from pool-creation checks.
        /// </summary>
        internal bool IsEmbedded { get; set; }

        #endregion

        [Header("Prism Properties")] 
        [SerializeField] public PrismProperties prismProperties;
        public GameObject ParticleEffect;
        public Trail Trail;

        [Header("Prism Growth")] 
        public Vector3 GrowthVector = new Vector3(0, 2, 0);
        public float growthRate = 0.01f;
        public float waitTime = 0.6f;

        [Header("Prism Status")] 
        public bool destroyed;
        public bool devastated;
        public bool IsSmallest;
        public bool IsLargest;
        
        [Header("Team Ownership")] 
        public string ownerID;

        [Header("Event Channels")] 
        [SerializeField] ScriptableEventPrismStats _onTrailBlockCreatedEventChannel;
        [SerializeField] ScriptableEventPrismStats _onTrailBlockDestroyedEventChannel;
        [SerializeField] ScriptableEventPrismStats _onTrailBlockRestoredEventChannel;
        [SerializeField] internal PrismEventChannelWithReturnSO OnBlockImpactedEventChannel;

        public Action<Prism> OnReturnToPool;
        private bool _initialized;
        private Vector3 _lastDestructionScale = Vector3.one;

        /// <summary>
        /// Index into PrismAOERegistry's contiguous NativeArray.
        /// Used for O(1) updates to cache-line-packed AOE data.
        /// -1 means not registered.
        /// </summary>
        internal int AOERegistryIndex = -1;
        
        public Domains Domain
        {
            get => teamManager?.Domain ?? Domains.Unassigned;
            set
            {
                if (teamManager) teamManager.SetInitialTeam(value);
            }
        }

        string _playerName;
        public string PlayerName { get; internal set; }

        // Component references
        private MaterialPropertyAnimator materialAnimator;
        private PrismScaleAnimator scaleAnimator;
        private PrismTeamManager teamManager;
        private PrismStateManager stateManager;
        private MeshRenderer meshRenderer;
        private BoxCollider blockCollider;

        public Vector3 TargetScale
        {
            get => scaleAnimator?.TargetScale ?? transform.localScale;
            set
            {
                scaleAnimator?.SetTargetScale(value);
                scaleAnimator?.BeginGrowthAnimation();
            }
        }

        public float Volume => scaleAnimator?.GetCurrentVolume() ?? .001f;
        public BlockState CurrentState => stateManager?.CurrentState ?? BlockState.Normal;

        public Vector3 MaxScale
        {
            get => scaleAnimator?.MaxScale ?? Vector3.one * 10f;
            set
            {
                if (scaleAnimator is not null) scaleAnimator.MaxScale = value;
            }
        }

        public void ChangeSize()
        {
            if (scaleAnimator is not null)
            {
                scaleAnimator.SetTargetScale(TargetScale);
                scaleAnimator.BeginGrowthAnimation();
            }
        }

        private void Awake()
        {
            CreatedByPool = _poolCreationDepth > 0;

            materialAnimator = GetComponent<MaterialPropertyAnimator>();
            scaleAnimator = GetComponent<PrismScaleAnimator>();
            teamManager = GetComponent<PrismTeamManager>();
            stateManager = GetComponent<PrismStateManager>();
            meshRenderer = GetComponent<MeshRenderer>();
            blockCollider = GetComponent<BoxCollider>();

            scaleAnimator.GrowthRate = growthRate;
            InitializePrismProperties();
        }

        /// <summary>
        /// Called when spawning from pool. Resets state and starts growth.
        /// </summary>
        public virtual void Initialize(string playerName = DEFAULT_PLAYER_NAME)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!CreatedByPool && !IsEmbedded)
            {
                Debug.LogError(
                    $"[Prism] '{gameObject.name}' was Instantiate()'d outside of a pool. " +
                    "All prisms MUST be spawned from a pool manager (InteractivePrismPoolManager, " +
                    "HealthPrismPoolManager, or BlockProjectilePoolManager). " +
                    "Add the appropriate pool manager to your scene and assign it to the spawning component's " +
                    "pool field. See GenericPoolManager<T> for setup.", this);
            }
#endif
            // [Fix] Always clean up previous state when coming from pool
            ResetState();

            _initialized = true;
            PlayerName = playerName;
            blockCollider.enabled = false;
            meshRenderer.enabled = false;

            var authoredTargetScale = scaleAnimator ? scaleAnimator.TargetScale : transform.localScale;
            if (authoredTargetScale == Vector3.zero)
                authoredTargetScale = transform.localScale;

            scaleAnimator.Initialize();
            scaleAnimator.SetTargetScale(authoredTargetScale);

            // Queue activation through centralized manager instead of per-prism coroutine.
            // This prevents the thundering-herd problem where thousands of WaitForSeconds
            // timers expire on the same frame.
            PrismActivationQueue.EnsureInstance().Enqueue(this, authoredTargetScale, waitTime);

            if (prismProperties.IsShielded) ActivateShield();
            if (prismProperties.IsDangerous) MakeDangerous();
        }

        private void ResetState()
        {
            // Cancel any pending deferred activation from PrismActivationQueue
            if (PrismActivationQueue.Instance != null)
                PrismActivationQueue.Instance.Cancel(this);

            // Unregister from AOE batch processing
            if (AOERegistryIndex >= 0)
            {
                PrismAOERegistry.Instance?.Unregister(AOERegistryIndex);
                AOERegistryIndex = -1;
            }

            destroyed = false;
            devastated = false;
            IsSmallest = false;
            IsLargest = false;

            // Clear trail renderer to prevent visual artifacts across the map
            if (Trail != null && Trail.TrailRenderer != null)
            {
                Trail.TrailRenderer.Clear();
            }

            // Ensure physics/rendering are off until activation queue enables them
            if (blockCollider) blockCollider.enabled = false;
            if (meshRenderer) meshRenderer.enabled = false;
        }

        /// <summary>
        /// Public method to immediately return this instance to the pool.
        /// </summary>
        public void ReturnToPool()
        {
            OnReturnToPool?.Invoke(this);
        }

        private void InitializePrismProperties()
        {
            if (prismProperties == null) return;
 
            prismProperties.position = transform.position;
            prismProperties.prism = this;
            prismProperties.Trail = Trail;
            prismProperties.TimeCreated = Time.time;
            gameObject.layer = LayerMask.NameToLayer(prismProperties.DefaultLayerName);

            prismProperties.volume = 1f;
        }

        /// <summary>
        /// Called by <see cref="PrismActivationQueue"/> when this prism's delay has elapsed.
        /// Contains the same logic that was previously in CreateBlockCoroutine after the yield.
        /// </summary>
        internal void ExecuteDeferredActivation(Vector3 authoredTargetScale)
        {
            meshRenderer.enabled = true;
            blockCollider.enabled = true;

            if (scaleAnimator.TargetScale == Vector3.zero)
                scaleAnimator.SetTargetScale(authoredTargetScale);

            prismProperties.volume = scaleAnimator.GetCurrentVolume();

            scaleAnimator.BeginGrowthAnimation();

            _onTrailBlockCreatedEventChannel.Raise(new PrismStats
            {
                OwnName = PlayerName,
                Volume = prismProperties.volume,
            });

            // Register with AOE registry for cache-friendly batch explosion processing
            var registry = PrismAOERegistry.EnsureInstance();
            if (registry != null && registry.IsAvailable)
                AOERegistryIndex = registry.Register(this);
        }

        // Growth Methods
        public void Grow(float amount = 1) => scaleAnimator.Grow(amount);

        // Collision Handling
        protected void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out CellItem cellItem))
            {
                if (!prismProperties.IsShielded)
                    ActivateShield();
            }
        }

        protected void OnTriggerExit(Collider other)
        {
            if (other.gameObject.IsLayer("Crystals"))
                ActivateShield(2.0f);
        }

        protected virtual GameObject SetupDestruction(Domains domain, string attackerPlayerName, bool devastate = false)
        {
            var destructionScale = transform.lossyScale; // Use lossyScale to get the actual world scale, which accounts for parent scaling

            if (scaleAnimator)
            {
                scaleAnimator.IsScaling = false;      
                scaleAnimator.enabled = false;       
            }

            blockCollider.enabled = false;
            meshRenderer.enabled = false;

            prismProperties.volume = Mathf.Max(scaleAnimator ? scaleAnimator.GetCurrentVolume() : 1f, 1f);

            destroyed = true;
            devastated = devastate;

            // Mark destroyed in AOE registry so Burst job skips this prism
            if (AOERegistryIndex >= 0)
                PrismAOERegistry.Instance?.MarkDestroyed(AOERegistryIndex);

            _onTrailBlockDestroyedEventChannel.Raise(new PrismStats
            {
                OwnName = PlayerName,
                Volume = prismProperties.volume,
                AttackerName = attackerPlayerName,
            });

            /*if (CellControlManager.Instance)
                CellControlManager.Instance.RemoveBlock(domain, prismProperties);*/

            _lastDestructionScale = destructionScale;
            return null;
        }

        // Explosion Methods
        protected virtual void Explode(Vector3 impactVector, Domains domain, string playerName, bool devastate = false)
        {
            SetupDestruction(domain, playerName, devastate);
            AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.BlockDestroy);

            var returnData = OnBlockImpactedEventChannel.RaiseEvent(new PrismEventData
            {
                ownDomain = Domain,
                SpawnPosition = transform.position,
                Rotation = transform.rotation,
                Scale = _lastDestructionScale,
                Velocity = impactVector / prismProperties.volume,
                PrismType = PrismType.Explosion
            });
        }

        // Implosion Methods
        protected virtual void Implode(Transform targetTransform, Domains domain, string playerName, bool devastate = false)
        {
            SetupDestruction(domain, playerName, devastate);
            AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.BlockDestroy);

            var returnData = OnBlockImpactedEventChannel.RaiseEvent(new PrismEventData
            {
                ownDomain = Domain,
                SpawnPosition = transform.position,
                Rotation = transform.rotation,
                Scale = _lastDestructionScale,
                TargetTransform = targetTransform,
                Volume = prismProperties.volume,
                PrismType = PrismType.Implosion
            });
        }

        public void Damage(Vector3 impactVector, Domains domain, string playerName, bool devastate = false)
        {
            if ((prismProperties.IsShielded && !devastate) || prismProperties.IsSuperShielded)
                DeactivateShields();
            else
                Explode(impactVector, domain, playerName, devastate);
        }

        public void Consume(Transform target, Domains domain, string playerName, bool devastate = false)
        {
            if ((prismProperties.IsShielded && !devastate) || prismProperties.IsSuperShielded)
                DeactivateShields();
            else
                Implode(target, domain, playerName, devastate);
        }

        // State Management Methods
        public void MakeDangerous() => stateManager?.MakeDangerous();
        public void DeactivateShields() => stateManager?.DeactivateShields();
        public void ActivateShield() => stateManager?.ActivateShield();
        public void ActivateShield(float duration) => stateManager?.ActivateShield(duration);
        public void ActivateSuperShield() => stateManager?.ActivateSuperShield();
        public void SetTransparency(bool transparent) => materialAnimator?.SetTransparency(transparent);

        // Team Management Methods
        public void Steal(string playerName, Domains domain, bool superSteal = false) =>
            teamManager.Steal(playerName, domain, superSteal);
        public void ChangeTeam(Domains domain) => teamManager?.ChangeTeam(domain);
        
        public void RegisterProjectileCreated(string playerName)
        {
            if (string.IsNullOrEmpty(playerName))
                playerName = DEFAULT_PLAYER_NAME;

            PlayerName = playerName;
            ownerID    = playerName;

            prismProperties.position = transform.position;
            prismProperties.prism    = this;
            prismProperties.Trail    = Trail;
            prismProperties.TimeCreated = Time.time;
            prismProperties.volume   = Mathf.Max(scaleAnimator ? scaleAnimator.GetCurrentVolume() : 1f, 1f);

            gameObject.layer = LayerMask.NameToLayer(prismProperties.DefaultLayerName);
            _onTrailBlockCreatedEventChannel.Raise(new PrismStats
            {
                OwnName = PlayerName,
                Volume     = prismProperties.volume,
            });
        }

        // Restoration
        public void Restore()
        {
            if (!devastated)
            {
                _onTrailBlockRestoredEventChannel.Raise(new PrismStats
                {
                    OwnName = PlayerName,
                    Volume = prismProperties.volume,
                    AttackerName = prismProperties.prism.PlayerName,
                });

                /*if (CellControlManager.Instance != null)
                    CellControlManager.Instance.RestoreBlock(Domain, prismProperties);*/

                blockCollider.enabled = true;
                meshRenderer.enabled = true;
                destroyed = false;
            }
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Pool-managed prisms that are still active were Destroy()'d instead of returned.
            // Released prisms are inactive (SetActive(false)) so this won't false-positive on pool cleanup.
            // During scene unload, scene.isLoaded is false — skip check to avoid noise.
            if (CreatedByPool && gameObject.activeSelf && gameObject.scene.isLoaded)
            {
                Debug.LogError(
                    $"[Prism] '{gameObject.name}' was Destroy()'d while still active. " +
                    "Pool-managed prisms must be returned via ReturnToPool() — never Destroy()'d directly. " +
                    "The pool manages the full object lifecycle.", this);
            }
#endif
        }
    }
}