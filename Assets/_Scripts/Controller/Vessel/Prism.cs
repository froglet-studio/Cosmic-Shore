using System;
using UnityEngine;
using System.Collections;
using CosmicShore.Core;
using CosmicShore.Utility;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine.Serialization;
using CosmicShore.Data;
using System.Linq;
namespace CosmicShore.Gameplay
{
    [RequireComponent(typeof(MaterialPropertyAnimator))]
    [RequireComponent(typeof(PrismScaleAnimator))]
    [RequireComponent(typeof(PrismTeamManager))]
    [RequireComponent(typeof(PrismStateManager))]
    public class Prism : MonoBehaviour
    {
        protected const string DEFAULT_PLAYER_NAME = "DefaultPlayer";

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
        private Vector3 _lastDestructionScale = Vector3.one;

        /// <summary>
        /// Index into PrismAOERegistry's contiguous NativeArray.
        /// Used for O(1) updates to cache-line-packed AOE data.
        /// -1 means not registered.
        /// </summary>
        internal int AOERegistryIndex = -1;

        /// <summary>
        /// The cell whose per-domain density grids this prism is registered in, or null.
        /// Trail prisms feed the cell's density partition (Cell.AddBlock/RemoveBlock) so
        /// fauna anti-domain targeting sees trail mass — not just flora. Replaces the
        /// deprecated CellControlManager registration path.
        /// </summary>
        Cell _registeredCell;
        
        public Domains Domain
        {
            get => teamManager?.Domain ?? Domains.Blue;
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
            materialAnimator = GetComponent<MaterialPropertyAnimator>();
            scaleAnimator = GetComponent<PrismScaleAnimator>();
            teamManager = GetComponent<PrismTeamManager>();
            stateManager = GetComponent<PrismStateManager>();
            meshRenderer = GetComponent<MeshRenderer>();
            blockCollider = GetComponent<BoxCollider>();

            scaleAnimator.GrowthRate = growthRate;
            InitializePrismProperties();

            // Keep the cell's per-domain grids consistent when this prism changes
            // hands (steal / ChangeTeam): the cell re-files it under the new domain.
            if (teamManager)
                teamManager.OnTeamChanged += HandleTeamChangedForCell;
        }

        // --- Cell density-grid registration -----------------------------------

        void HandleTeamChangedForCell(Domains oldDomain, Domains newDomain)
        {
            _registeredCell?.NotifyBlockDomainChanged(this);
        }

        /// <summary>
        /// Registers this prism with the cell that spatially contains it, feeding the
        /// cell's per-domain density grids (anti-domain fauna targeting) and
        /// LiveBlockCount (phase transitions). Idempotent.
        /// </summary>
        void RegisterWithCell()
        {
            if (_registeredCell) return;
            // Fauna bodies (LightFauna / Boid HealthPrisms) are creatures, not environment
            // mass: they must NOT inflate the cell's phase count or pollute the density grid.
            // Otherwise a forager swarm reads as its own "mass concentration" and seeks
            // itself instead of the trail/flora buildup. Only HealthPrisms can be fauna
            // bodies, so the GetComponentInParent walk is gated to that subtype to keep
            // ordinary trail-prism spawns cheap.
            if (this is HealthPrism && GetComponentInParent<Fauna>() != null) return;
            _registeredCell = Cell.FindCellContaining(transform.position);
            _registeredCell?.AddBlock(this);
        }

        /// <summary>Removes this prism from its registered cell's grids. Idempotent.</summary>
        void UnregisterFromCell()
        {
            if (!_registeredCell) return;
            _registeredCell.RemoveBlock(this);
            _registeredCell = null;
        }

        /// <summary>
        /// Called when spawning from pool. Resets state and starts growth.
        /// </summary>
        public virtual void Initialize(string playerName = DEFAULT_PLAYER_NAME)
        {
            // [Fix] Always clean up previous state when coming from pool
            ResetState();

            PlayerName = playerName;
            blockCollider.enabled = false;
            meshRenderer.enabled = false;

            var authoredTargetScale = scaleAnimator ? scaleAnimator.TargetScale : transform.localScale;
            if (authoredTargetScale == Vector3.zero)
                authoredTargetScale = transform.localScale;

            scaleAnimator.Initialize();
            scaleAnimator.SetTargetScale(authoredTargetScale);
            StartCoroutine(CreateBlockCoroutine(authoredTargetScale));

            if (prismProperties.IsShielded) ActivateShield();
            if (prismProperties.IsDangerous) MakeDangerous();
        }

        private void ResetState()
        {
            // Unregister from AOE batch processing
            if (AOERegistryIndex >= 0)
            {
                PrismAOERegistry.Instance?.Unregister(AOERegistryIndex);
                AOERegistryIndex = -1;
            }

            // Pool-reuse safety: a prism returned to the pool without going through
            // SetupDestruction (e.g. trail clear) must not leave a stale registration
            // in the previous cell's grids.
            UnregisterFromCell();

            destroyed = false;
            devastated = false;
            IsSmallest = false;
            IsLargest = false;
            
            // Clear trail renderer to prevent visual artifacts across the map
            if (Trail != null && Trail.TrailRenderer != null)
            {
                Trail.TrailRenderer.Clear();
            }
            
            // Ensure physics/rendering are off until Coroutine enables them
            if (blockCollider) blockCollider.enabled = false;
            if (meshRenderer) meshRenderer.enabled = false;
            
            StopAllCoroutines();
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

        private IEnumerator CreateBlockCoroutine(Vector3 authoredTargetScale)
        {
            yield return new WaitForSeconds(waitTime);

            // Destroyed before creation completed (e.g. AOE within waitTime of spawn) —
            // don't resurrect the renderer/collider or register a dead prism with the
            // AOE registry and cell grids.
            if (destroyed) yield break;

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

            // Register with the containing cell's density grids — this is what makes
            // trail mass visible to fauna anti-domain targeting and to the cell's
            // phase system. (Replaces the deprecated CellControlManager path that was
            // commented out here; without it the grids only ever contained flora, so
            // fauna appeared to have "explicit knowledge of flora locations" and
            // ignored even massive player trails.)
            RegisterWithCell();
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

            // Remove from the containing cell's density grids — destroyed mass must
            // stop attracting fauna, and the cell's LiveBlockCount must fall so the
            // phase system can descend (the consumption half of the oscillation).
            UnregisterFromCell();

            _lastDestructionScale = destructionScale;
            return null;
        }

        // Explosion Methods
        protected virtual void Explode(Vector3 impactVector, Domains domain, string playerName, bool devastate = false)
        {
            SetupDestruction(domain, playerName, devastate);
            AudioSystem.Instance?.PlayGameplaySFX(GameplaySFXCategory.BlockDestroy, transform.position);

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
            AudioSystem.Instance?.PlayGameplaySFX(GameplaySFXCategory.BlockDestroy, transform.position);

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

        // Idempotency guard for Damage/Consume. Multiple destruction sources can resolve
        // against the same prism in a single frame: two fauna iterating cached
        // OverlapSphere snapshots, AOE damage, and trail collisions all hit the same
        // prism before the disabled collider drops out of the next physics tick. Without
        // this gate, the second hit re-runs SetupDestruction (re-raising the destroyed
        // event, bumping AOE registry state) and Implode/Explode spawns a duplicate VFX
        // that has to play out its full duration in PrismEffectsManager — that's the
        // accumulating "garbage" the user observes (fauna swarm-eat trail blocks → 64-128
        // concurrent implosions). Once destroyed, hits become no-ops until Restore /
        // ResetState clears the flag.
        public void Damage(Vector3 impactVector, Domains domain, string playerName, bool devastate = false)
        {
            if (destroyed) return;
            // Super-shielded prisms are fully invulnerable. No damage source
            // currently breaks them; ways to break them will be added later.
            // The impactor's other effect SOs (sparks, sound) still fire on
            // OnTriggerEnter, so the hit reads visually without state change.
            if (prismProperties.IsSuperShielded) return;
            if (prismProperties.IsShielded && !devastate)
                DeactivateShields();
            else
                Explode(impactVector, domain, playerName, devastate);
        }

        public void Consume(Transform target, Domains domain, string playerName, bool devastate = false)
        {
            if (destroyed) return;
            if (prismProperties.IsSuperShielded) return;
            if (prismProperties.IsShielded && !devastate)
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

                // Restored mass re-enters the cell's density grids.
                RegisterWithCell();

                blockCollider.enabled = true;
                meshRenderer.enabled = true;
                destroyed = false;
            }
        }

        private void OnDisable()
        {
            // Pool return / deactivation: a disabled prism must not keep attracting
            // fauna or holding up the cell's LiveBlockCount until its next reuse.
            UnregisterFromCell();
        }

        private void OnDestroy()
        {
            // No material cleanup needed — we use sharedMaterial exclusively,
            // so no per-instance material clones are created.

            if (teamManager)
                teamManager.OnTeamChanged -= HandleTeamChangedForCell;

            // Scene teardown / explicit Destroy: don't leave a stale entry in the
            // cell's grids and LiveBlockCount.
            UnregisterFromCell();
        }
    }
}