using System;
using UnityEngine;
using System.Collections;
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
        protected const string DEFAULT_PLAYER_NAME = "DefaultPlayer"; // -> This should be removed later,

        [Header("Prism Properties")] 
        
        [SerializeField]
        public PrismProperties prismProperties;
        public GameObject ParticleEffect;
        public Trail Trail;

        [Header("Prism Growth")] 
        public Vector3 GrowthVector = new Vector3(0, 2, 0);
        public float growthRate = 0.01f;
        public float waitTime = 0.6f;

        [Header("Prism Status")] public bool destroyed;
        public bool devastated;
        public bool IsSmallest;
        public bool IsLargest;
        
        [Header("Team Ownership")] 
        public string ownerID;

        [Header("Event Channels")] 
        
        [SerializeField]
        ScriptableEventPrismStats _onTrailBlockCreatedEventChannel;

        [SerializeField] 
        ScriptableEventPrismStats _onTrailBlockDestroyedEventChannel;

        [SerializeField] 
        ScriptableEventPrismStats _onTrailBlockRestoredEventChannel;

        [SerializeField]
        PrismEventChannelWithReturnSO OnBlockImpactedEventChannel;

        public Action<Prism> OnReturnToPool;

        public Domains Domain
        {
            get => teamManager?.Domain ?? Domains.Unassigned;
            set
            {
                if (teamManager)
                    teamManager.SetInitialTeam(value);
            }
        }

        // public IPlayer Player;
        string _playerName;
        public string PlayerName { get; private set; }

        // Component references
        private MaterialPropertyAnimator materialAnimator;
        private PrismScaleAnimator scaleAnimator;
        private PrismTeamManager teamManager;
        private PrismStateManager stateManager;
        private MeshRenderer meshRenderer;
        private BoxCollider blockCollider;

        // Public accessors for backward compatibility
        public Vector3 TargetScale
        {
            get => scaleAnimator?.TargetScale ?? transform.localScale;
            set
            {
                scaleAnimator?.SetTargetScale(value);
                scaleAnimator
                    ?.BeginGrowthAnimation(); // TODO ->, Make separate method for SetTargetScale, and Begin Growth.
            }
        }

        public float Volume => scaleAnimator?.GetCurrentVolume() ?? .001f;
        public BlockState CurrentState => stateManager?.CurrentState ?? BlockState.Normal;

        public Vector3 MaxScale
        {
            get => scaleAnimator?.MaxScale ?? Vector3.one * 10f; // Default max scale as fallback
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
            // Cache component references
            materialAnimator = GetComponent<MaterialPropertyAnimator>();
            scaleAnimator = GetComponent<PrismScaleAnimator>();
            teamManager = GetComponent<PrismTeamManager>();
            stateManager = GetComponent<PrismStateManager>();
            meshRenderer = GetComponent<MeshRenderer>();
            blockCollider = GetComponent<BoxCollider>();

            scaleAnimator.GrowthRate = growthRate;

            InitializePrismProperties();
        }

        public virtual void Initialize(string playerName = DEFAULT_PLAYER_NAME)
        {
            PlayerName = playerName;
            blockCollider.enabled = false;
            meshRenderer.enabled = false;

            scaleAnimator.Initialize();
            // CreateBlock();
            StartCoroutine(CreateBlockCoroutine());

            // Apply initial states if needed
            if (prismProperties.IsShielded) ActivateShield();
            if (prismProperties.IsDangerous) MakeDangerous();
        }

        /// <summary>
        /// Public method to immediately return this instance to the pool.
        /// Also reparents under the PoolManager's transform for hierarchy cleanliness.
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

            // Initialize volume immediately to prevent zero-volume explosions
            prismProperties.volume = 1f; // Set a default non-zero volume
        }

        private IEnumerator CreateBlockCoroutine()
        {
            yield return new WaitForSeconds(waitTime);
            meshRenderer.enabled = true;
            blockCollider.enabled = true;

            // Set initial target scale before beginning growth animation
            if (scaleAnimator.TargetScale == Vector3.zero)
            {
                scaleAnimator.SetTargetScale(Vector3.one);
            }

            // Update volume before growth animation starts
            prismProperties.volume = scaleAnimator.GetCurrentVolume();

            scaleAnimator.BeginGrowthAnimation();
            
            _onTrailBlockCreatedEventChannel.Raise(new PrismStats
            {
                OwnName = PlayerName,
                Volume = prismProperties.volume,
            });

            // TODO - Use Event Channel
            if (CellControlManager.Instance)
            {
                CellControlManager.Instance.AddBlock(Domain, prismProperties);

                // Setup team node tracking after block is fully initialized
                Cell targetCell = CellControlManager.Instance.GetNearestCell(prismProperties.position);
                System.Array.ForEach(new[] { Domains.Jade, Domains.Ruby, Domains.Gold }, t =>
                {
                    if (t != Domain && targetCell != null)
                        targetCell.countGrids[t].AddBlock(this);
                });
            }
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
            blockCollider.enabled = false;
            meshRenderer.enabled = false;

            // Ensure volume is up to date before destruction
            prismProperties.volume = Mathf.Max(scaleAnimator.GetCurrentVolume(), 1f);

            destroyed = true;
            devastated = devastate;

            // Stats tracking
            _onTrailBlockDestroyedEventChannel.Raise(new PrismStats
            {
                OwnName = PlayerName,
                Volume = prismProperties.volume,
                AttackerName = attackerPlayerName,
            });

            // Cell control management
            if (CellControlManager.Instance != null)
                CellControlManager.Instance.RemoveBlock(domain, prismProperties);

            return null; // Will be set by specific destruction method
        }

        // Explosion Methods
        protected virtual void Explode(Vector3 impactVector, Domains domain, string playerName, bool devastate = false)
        {
            SetupDestruction(domain, playerName, devastate);

            // Spawn explosion object
            var returnData = OnBlockImpactedEventChannel.RaiseEvent(new PrismEventData
            {
                ownDomain = Domain,
                SpawnPosition = transform.position,
                Rotation = transform.rotation,
                Scale = transform.localScale,
                Velocity = impactVector / prismProperties.volume,
                PrismType = PrismType.Explosion
            });
        }

        // Implosion Methods
        protected virtual void Implode(Transform targetTransform, Domains domain, string playerName,
            bool devastate = false)
        {
            SetupDestruction(domain, playerName, devastate);

            // Spawn implosion object
            var returnData = OnBlockImpactedEventChannel.RaiseEvent(new PrismEventData
            {
                ownDomain = Domain,
                SpawnPosition = transform.position,
                Rotation = transform.rotation,
                Scale = transform.lossyScale,
                TargetTransform = targetTransform,
                Volume = prismProperties.volume,
                PrismType = PrismType.Implosion
            });
        }

        public void Damage(Vector3 impactVector, Domains domain, string playerName, bool devastate = false)
        {
            if ((prismProperties.IsShielded && !devastate) || prismProperties.IsSuperShielded)
            {
                DeactivateShields();
            }
            else
            {
                Explode(impactVector, domain, playerName, devastate);
            }
        }

        public void Consume(Transform target, Domains domain, string playerName, bool devastate = false)
        {
            if ((prismProperties.IsShielded && !devastate) || prismProperties.IsSuperShielded)
            {
                DeactivateShields();
            }
            else
            {
                Implode(target, domain, playerName, devastate);
            }
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

        // public void Steal(string playerName, Teams team) => Steal(playerName, team);
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
            prismProperties.volume   = Mathf.Max(
                scaleAnimator ? scaleAnimator.GetCurrentVolume() : 1f,
                1f);

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

                if (CellControlManager.Instance != null)
                    CellControlManager.Instance.RestoreBlock(Domain, prismProperties);

                blockCollider.enabled = true;
                meshRenderer.enabled = true;
                destroyed = false;
            }
        }

        private void OnDestroy()
        {
            if (meshRenderer != null && meshRenderer.material != null)
            {
                Destroy(meshRenderer.material);
            }
        }
    }
}