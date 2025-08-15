﻿﻿﻿﻿using UnityEngine;
using System.Collections;
using CosmicShore.Utility.ClassExtensions;
using CosmicShore.Game;
using CosmicShore.Utilities;

namespace CosmicShore.Core
{
    [RequireComponent(typeof(MaterialPropertyAnimator))]
    [RequireComponent(typeof(BlockScaleAnimator))]
    [RequireComponent(typeof(BlockTeamManager))]
    [RequireComponent(typeof(BlockStateManager))]
    public class TrailBlock : MonoBehaviour
    {
        const string DEFAULT_PLAYER_NAME = "DefaultPlayer"; // -> This should be removed later,

        [Header("Trail Block Properties")]
        [SerializeField] private GameObject FossilBlock;
        [SerializeField] public TrailBlockProperties TrailBlockProperties;
        public GameObject ParticleEffect;
        public Trail Trail;

        [Header("Trail Block Growth")]
        public Vector3 GrowthVector = new Vector3(0, 2, 0);
        public float growthRate = 0.01f;
        public float waitTime = 0.6f;

        [Header("Trail Block Status")]
        public bool destroyed;
        public bool devastated;
        public bool IsSmallest;
        public bool IsLargest;

        [Header("Team Ownership")]
        public string ownerID;

        [Header("Event Channels")]
        
        // [SerializeField] TrailBlockEventChannelSO _onTrailBlockCreatedEventChannel;
        [SerializeField] ScriptableEventTrailBlockEventData _onTrailBlockCreatedEventChannel;
        // [SerializeField] TrailBlockEventChannelSO _onTrailBlockDestroyedEventChannel;
        [SerializeField] ScriptableEventTrailBlockEventData _onTrailBlockDestroyedEventChannel;
        // [SerializeField] TrailBlockEventChannelSO _onTrailBlockRestoredEventChannel;
        [SerializeField] ScriptableEventTrailBlockEventData _onTrailBlockRestoredEventChannel;
        
        [SerializeField] TrailBlockEventChannelWithReturnSO _onFlockSpawnedEventChannel;

        public Teams Team
        {
            get => teamManager?.Team ?? Teams.Unassigned;
            set
            {
                if (teamManager != null)
                    teamManager.SetInitialTeam(value);
            }
        }
        // public IPlayer Player;
        string _playerName;
        public string PlayerName 
        { 
            get
            {
                if (_playerName == null)
                {
                    _playerName = DEFAULT_PLAYER_NAME; // Default player name if not set -> this is a temp fix. 
                    // _playerName should never be null in here
                }
                return _playerName;
            }
            set => _playerName = value;
        }

        // Component references
        private MaterialPropertyAnimator materialAnimator;
        private BlockScaleAnimator scaleAnimator;
        private BlockTeamManager teamManager;
        private BlockStateManager stateManager;
        private MeshRenderer meshRenderer;
        private BoxCollider blockCollider;

        // Public accessors for backward compatibility
        public Vector3 TargetScale
        {
            get => scaleAnimator?.TargetScale ?? transform.localScale;
            set
            {
                scaleAnimator?.SetTargetScale(value);
            }
        }

        public float Volume => scaleAnimator?.GetCurrentVolume() ?? .001f;

        public Vector3 MaxScale
        {
            get => scaleAnimator?.MaxScale ?? Vector3.one * 10f;  // Default max scale as fallback
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
            }
        }

        // Static references
        // private TeamColorPoolManager FossilBlockPool => TeamColorPoolManager.Instance as TeamColorPoolManager;
        private const string layerName = "TrailBlocks";

        private void Awake()
        {
            gameObject.layer = LayerMask.NameToLayer(layerName);

            // Cache component references
            materialAnimator = GetComponent<MaterialPropertyAnimator>();
            scaleAnimator = GetComponent<BlockScaleAnimator>();
            teamManager = GetComponent<BlockTeamManager>();
            stateManager = GetComponent<BlockStateManager>();
            meshRenderer = GetComponent<MeshRenderer>();
            blockCollider = GetComponent<BoxCollider>();

            scaleAnimator.GrowthRate = growthRate;

            InitializeTrailBlockProperties();
        }

        protected virtual void Start()
        {
            blockCollider.enabled = false;
            meshRenderer.enabled = false;

            StartCoroutine(CreateBlockCoroutine());

            // Apply initial states if needed
            if (TrailBlockProperties.IsShielded) ActivateShield();
            if (TrailBlockProperties.IsDangerous) MakeDangerous();
        }

        private void InitializeTrailBlockProperties()
        {
            if (TrailBlockProperties == null) return;

            TrailBlockProperties.position = transform.position;
            TrailBlockProperties.trailBlock = this;
            TrailBlockProperties.Trail = Trail;
            TrailBlockProperties.TimeCreated = Time.time;
            
            // Initialize volume immediately to prevent zero-volume explosions
            TrailBlockProperties.volume = 1f; // Set a default non-zero volume
        }

        private IEnumerator CreateBlockCoroutine()
        {
            yield return new WaitForSeconds(waitTime);
            // yield return new WaitUntil(() => Player != null);

            meshRenderer.enabled = true;
            blockCollider.enabled = true;

            // Set initial target scale before beginning growth animation
            if (scaleAnimator.TargetScale == Vector3.zero)
            {
                scaleAnimator.SetTargetScale(Vector3.one);
            }
            
            // Update volume before growth animation starts
            TrailBlockProperties.volume = scaleAnimator.GetCurrentVolume();
            
            scaleAnimator.BeginGrowthAnimation();

            // TODO - Raise events about block creation.
            /*if (StatsManager.Instance != null)
                StatsManager.Instance.BlockCreated(Team, PlayerName, TrailBlockProperties);*/

            _onTrailBlockCreatedEventChannel.Raise(new TrailBlockEventData
            {
                OwnTeam = Team,
                PlayerName = PlayerName,
                TrailBlockProperties = TrailBlockProperties
            });

            if (CellControlManager.Instance is not null)
            {
                CellControlManager.Instance.AddBlock(Team, TrailBlockProperties);
                
                // Setup team node tracking after block is fully initialized
                Cell targetCell = CellControlManager.Instance.GetNearestCell(TrailBlockProperties.position);
                System.Array.ForEach(new[] { Teams.Jade, Teams.Ruby, Teams.Gold }, t =>
                {
                    if (t != Team) targetCell.countGrids[t].AddBlock(this);
                });
            }
        }

        // Growth Methods
        public void Grow(float amount = 1) => scaleAnimator.Grow(amount);
        public void Grow(Vector3 growthVector) => scaleAnimator.Grow(growthVector);

        // Collision Handling
        protected void OnTriggerEnter(Collider other)
        {
            // Deprecated - New Impact Effect System has been implemented. Remove it once all tested.
            /*if (other.TryGetComponent(out IVesselCollider vesselCollider))
            {
                var ship = vesselCollider.Ship;
                if (!ship.ShipStatus.Attached)
                    ship.PerformTrailBlockImpactEffects(TrailBlockProperties);
            }*/
            
            if (other.TryGetComponent(out CellItem cellItem))
            {
                if (!TrailBlockProperties.IsShielded)
                    ActivateShield();
            }
        }

        protected void OnTriggerExit(Collider other)
        {
            if (other.gameObject.IsLayer("Crystals"))
                ActivateShield(2.0f);
        }

        // Destruction Methods
        protected virtual void Explode(Vector3 impactVector, Teams team, string playerName, bool devastate = false)
        {
            blockCollider.enabled = false;
            meshRenderer.enabled = false;

            // Ensure volume is up to date before explosion
            TrailBlockProperties.volume = Mathf.Max(scaleAnimator.GetCurrentVolume(), 1f);

            // var explodingBlock = FossilBlockPool.SpawnFromTeamPool(Team, transform.position, transform.rotation);
            var returnData = _onFlockSpawnedEventChannel.RaiseEvent(new TrailBlockEventData
            {
                OwnTeam = Team,
                PlayerTeam = team,
                PlayerName = playerName,
                Position = transform.position,
                Rotation = transform.rotation,
            });
            GameObject explodingBlock = returnData.SpawnedObject;

            if (explodingBlock == null)
            {
                Debug.LogError("Failed to spawn exploding block. Check if the pool is initialized and has available objects.");
                return;
            }

            explodingBlock.transform.localScale = transform.lossyScale;

            var impact = explodingBlock.GetComponent<BlockImpact>();
            if (impact != null) impact.HandleImpact(impactVector / TrailBlockProperties.volume);

            destroyed = true;
            devastated = devastate;

            /*if (StatsManager.Instance != null)
                StatsManager.Instance.BlockDestroyed(team, playerName, TrailBlockProperties);*/

            _onTrailBlockDestroyedEventChannel.Raise(new TrailBlockEventData
            {
                OwnTeam = team,
                PlayerName = playerName,
                TrailBlockProperties = TrailBlockProperties,
            });

            if (CellControlManager.Instance != null)
                CellControlManager.Instance.RemoveBlock(team, TrailBlockProperties);
        }

        public void Damage(Vector3 impactVector, Teams team, string playerName, bool devastate = false)
        {
            if ((TrailBlockProperties.IsShielded && !devastate) || TrailBlockProperties.IsSuperShielded)
            {
                DeactivateShields();
            }
            else
            {
                Explode(impactVector, team, playerName, devastate);
            }
        }

        // State Management Methods
        public void MakeDangerous() => stateManager?.MakeDangerous();
        public void DeactivateShields() => stateManager?.DeactivateShields();
        public void ActivateShield() => stateManager?.ActivateShield();
        public void ActivateSuperShield() => stateManager?.ActivateSuperShield();
        public void ActivateShield(float duration) => stateManager?.ActivateShield(duration);
        public void SetTransparency(bool transparent) => materialAnimator?.SetTransparency(transparent);

        // Team Management Methods
        public void Steal(string playerName, Teams team, bool superSteal = false) => teamManager.Steal(playerName, team, superSteal);
        // public void Steal(string playerName, Teams team) => Steal(playerName, team);
        public void ChangeTeam(Teams team) => teamManager?.ChangeTeam(team);

        // Restoration
        public void Restore()
        {
            if (!devastated)
            {
                /*if (StatsManager.Instance != null)
                    StatsManager.Instance.BlockRestored(Team, PlayerName, TrailBlockProperties);*/

                _onTrailBlockRestoredEventChannel.Raise(new TrailBlockEventData
                {
                    OwnTeam = Team,
                    PlayerName = PlayerName,
                    TrailBlockProperties = TrailBlockProperties
                });

                if (CellControlManager.Instance != null)
                    CellControlManager.Instance.RestoreBlock(Team, TrailBlockProperties);

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
