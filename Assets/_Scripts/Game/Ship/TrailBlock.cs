using System;
using System.Collections;
using CosmicShore.Utility.ClassExtensions;
using UnityEngine;

namespace CosmicShore.Core
{
    public class TrailBlock : MonoBehaviour
    {
        [Header("Trail Block Properties")]
        static GameObject fossilBlockContainer;
        [SerializeField] GameObject FossilBlock;
        [SerializeField] public TrailBlockProperties TrailBlockProperties;
        public GameObject ParticleEffect; // TODO: move this so it references the Team to retrieve the effect.
        public Trail Trail;

        [Header("Trail Block Volume")]
        [SerializeField] Vector3 minScale = new Vector3 (.5f, .5f, .5f);
        [SerializeField] public Vector3 MaxScale = new Vector3 (10, 10, 10);
        public Vector3 TargetScale;
        Vector3 outerDimensions; // defines volume
        [SerializeField] public Vector3 GrowthVector = new Vector3(0, 2, 0);
        public float Volume { get => outerDimensions.x * outerDimensions.y * outerDimensions.z; }
        public float growthRate = .01f;

        [Header("Trail Block Status")]
        public float waitTime = .6f;
        public bool destroyed;
        public bool devastated;
        public string ID;
        public int Index;
        public bool warp;
        public bool IsSmallest;
        public bool IsLargest;

        // Shader related properties
        MeshRenderer meshRenderer;
        Vector3 spread;

        // Trail physics components
        BoxCollider blockCollider;
        
        [Header("Team Ownership on the Block")]
        Teams team;
        public string ownerId;  // TODO: is the ownerId the player name? I hope it is.
        public Teams Team 
        { 
            get => team; 
            set 
            {
                //Debug.LogWarning($"Setting Block Team from {team} to {value}");
                team = value;
                ActiveOpaqueMaterial = meshRenderer.material = ThemeManager.Instance.GetTeamBlockMaterial(team);
                ActiveTransparentMaterial = ThemeManager.Instance.GetTeamTransparentBlockMaterial(team);
            }
        }
        public Player Player;
        public string PlayerName => Player ? Player.PlayerName : "";

        const string layerName = "TrailBlocks";

        void Awake()
        {
            gameObject.layer = LayerMask.NameToLayer(layerName);

            meshRenderer = GetComponent<MeshRenderer>();

            InitializeTrailBlockProperties();
        }

        protected virtual void Start()
        {
            if (fossilBlockContainer == null)
                fossilBlockContainer = new GameObject { name = "FossilBlockContainer" };

            // Initialize materials used for animated material transitions
            if (team != Teams.Unassigned)
            {
                ActiveOpaqueMaterial = meshRenderer.material = ThemeManager.Instance.GetTeamBlockMaterial(team);
                ActiveTransparentMaterial = ThemeManager.Instance.GetTeamTransparentBlockMaterial(team);
            }

            blockCollider = GetComponent<BoxCollider>();
            blockCollider.enabled = false;

            meshRenderer.enabled = false;

            spread = meshRenderer.material.GetVector("_Spread");

            TargetScale = TargetScale == Vector3.zero ? transform.localScale : TargetScale;
            UpdateVolume();
            transform.localScale = Vector3.one * Mathf.Epsilon;

            StartCoroutine(CreateBlockCoroutine());

            if (TrailBlockProperties.IsShielded) ActivateShield();
            if (TrailBlockProperties.IsDangerous) MakeDangerous();

            Node targetNode = NodeControlManager.Instance.GetNearestNode(TrailBlockProperties.position);
            Array.ForEach(new[] { Teams.Jade, Teams.Ruby, Teams.Gold }, t =>
            {
                if (t != team) targetNode.countGrids[t].AddBlock(this);  // Add this block to other teams' target tracking.
            });
        }

        void InitializeTrailBlockProperties()
        {
            TrailBlockProperties.position = transform.position;
            TrailBlockProperties.trailBlock = this;
            TrailBlockProperties.Index = Index;
            TrailBlockProperties.Trail = Trail;
            TrailBlockProperties.TimeCreated = Time.time;
        }

        /// <summary>
        /// Updates the blocks calculated volume
        /// </summary>
        /// <returns>The amount the volume changed as a result of the updated calculation</returns>
        float UpdateVolume()
        {
            var oldVolume = TrailBlockProperties.volume;

            outerDimensions = TargetScale + 2 * spread;
            TrailBlockProperties.volume = outerDimensions.x * outerDimensions.y * outerDimensions.z;

            return TrailBlockProperties.volume - oldVolume;
        }

        IEnumerator CreateBlockCoroutine()
        {
            yield return new WaitForSeconds(waitTime);

            meshRenderer.enabled = true;
            blockCollider.enabled = true;

            StartCoroutine(SizeChangeCoroutine());

            //Add block to team score when created
            if (StatsManager.Instance != null)
                StatsManager.Instance.BlockCreated(team, PlayerName, TrailBlockProperties);

            if (NodeControlManager.Instance != null)
                NodeControlManager.Instance.AddBlock(team, TrailBlockProperties);
        }

        bool isSizeChangeActive = false;
        IEnumerator SizeChangeCoroutine()
        {
            TargetScale.x = Mathf.Clamp(TargetScale.x, minScale.x, MaxScale.x);
            TargetScale.y = Mathf.Clamp(TargetScale.y, minScale.y, MaxScale.y);
            TargetScale.z = Mathf.Clamp(TargetScale.z, minScale.z, MaxScale.z);

            isSizeChangeActive = true;
            float sqrDistance = (TargetScale - transform.localScale).sqrMagnitude;

            while (sqrDistance > .05f)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, TargetScale, Mathf.Clamp(growthRate * Time.deltaTime * sqrDistance, .05f, .2f));
                sqrDistance = (TargetScale - transform.localScale).sqrMagnitude;
                yield return null;
            }
            transform.localScale = TargetScale;
            isSizeChangeActive = false;
        }

        public void ChangeSize()
        {
            //Debug.Log($"TrailBlock Changing Size from lossy scale {transform.lossyScale} and local scale {transform.lossyScale} to {TargetScale}");
            if (TargetScale.x > MaxScale.x || TargetScale.y > MaxScale.y || TargetScale.z > MaxScale.z)
            {
                ActivateShield();
                IsLargest = true;
            }
            if (TargetScale.x < minScale.x || TargetScale.y < minScale.y || TargetScale.z < minScale.z)
            {
                IsSmallest = true;
            }

            var deltaVolume = UpdateVolume(); ;

            if (StatsManager.Instance != null) StatsManager.Instance.BlockVolumeModified(deltaVolume, TrailBlockProperties);
            if (!isSizeChangeActive)
            {
                StartCoroutine(SizeChangeCoroutine());
            }
        }

        public void Grow(float amount = 1)
        {
            Grow(amount * GrowthVector);
        }

        public void Grow(Vector3 growthVector)
        {
            TargetScale += growthVector;

            ChangeSize();
        }

        // TODO: none of the collision detection should be on the Trailblock
        protected void OnTriggerEnter(Collider other)
        {
            if(other.gameObject.IsLayer("Ships"))
            {
                var ship = other.GetComponent<ShipGeometry>().Ship;

                if (!ship.GetComponent<ShipStatus>().Attached)
                {
                    ship.PerformTrailBlockImpactEffects(TrailBlockProperties);
                }
            }
            
            if (other.gameObject.IsLayer("Crystals"))
            {
                if (!TrailBlockProperties.IsShielded)
                {
                    ActivateShield();
                }
            }
        }

        protected void OnTriggerExit(Collider other)
        {
            if (other.gameObject.IsLayer("Crystals"))
            {
                ActivateShield(2.0f);
            }
        }

        protected virtual void Explode(Vector3 impactVector, Teams team, string playerName, bool devastate = false)
        {
            // We don't destroy the trail blocks, we keep the objects around so they can be restored
            gameObject.GetComponent<BoxCollider>().enabled = false;
            gameObject.GetComponent<MeshRenderer>().enabled = false;

            // Make exploding block
            var explodingBlock = Instantiate(FossilBlock, fossilBlockContainer.transform);
            explodingBlock.transform.position = transform.position;
            explodingBlock.transform.eulerAngles = transform.eulerAngles;
            explodingBlock.transform.localScale = transform.lossyScale;
            explodingBlock.GetComponent<Renderer>().material = new Material(ThemeManager.Instance.GetTeamExplodingBlockMaterial(this.team));
            explodingBlock.GetComponent<BlockImpact>().HandleImpact(impactVector / Volume);

            destroyed = true;
            devastated = devastate;

            if (StatsManager.Instance != null)
                StatsManager.Instance.BlockDestroyed(team, playerName, TrailBlockProperties);

            if (NodeControlManager.Instance != null)
                NodeControlManager.Instance.RemoveBlock(team, TrailBlockProperties);

            // TODO: if devestated destroy game object and material to prevent memory leak
        }

        public void Damage(Vector3 impactVector, Teams team, string playerName, bool devastate=false)
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

        public void MakeDangerous()
        {
            TrailBlockProperties.IsDangerous = true;
            TrailBlockProperties.speedDebuffAmount = .1f;
            TrailBlockProperties.IsShielded = false;
            UpdateMaterial(ThemeManager.Instance.GetTeamTransparentDangerousBlockMaterial(team), ThemeManager.Instance.GetTeamDangerousBlockMaterial(team));
        }

        public void DeactivateShields()
        {
            UpdateMaterial(ThemeManager.Instance.GetTeamTransparentBlockMaterial(team), ThemeManager.Instance.GetTeamBlockMaterial(team));
            StartCoroutine(DeactivateShieldsCoroutine(1));
            // TODO: need stats
        }

        IEnumerator DeactivateShieldsCoroutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            TrailBlockProperties.IsShielded = false;
            TrailBlockProperties.IsSuperShielded = false;
        }

        public void ActivateShield()
        {
            TrailBlockProperties.IsShielded = true;
            TrailBlockProperties.IsDangerous = false;
            UpdateMaterial(ThemeManager.Instance.GetTeamTransparentShieldedBlockMaterial(team), ThemeManager.Instance.GetTeamShieldedBlockMaterial(team));
            // TODO: need stats
        }

        public void ActivateSuperShield()
        {
            TrailBlockProperties.IsSuperShielded = true;
            TrailBlockProperties.IsDangerous = false;
            UpdateMaterial(ThemeManager.Instance.GetTeamTransparentSuperShieldedBlockMaterial(team), ThemeManager.Instance.GetTeamSuperShieldedBlockMaterial(team));
        }

        public void ActivateShield(float duration)
        {
            StartCoroutine(ActivateShieldCoroutine(duration));
        }

        IEnumerator ActivateShieldCoroutine(float duration)
        {
            ActivateShield();

            yield return new WaitForSeconds(duration);
            
            DeactivateShields();
        }

        public void SetTransparency(bool transparent)
        {
            TrailBlockProperties.IsTransparent = transparent;
            
            if (lerpBlockMaterialPropertiesCoroutine == null)
                meshRenderer.material = transparent ? ActiveTransparentMaterial : ActiveOpaqueMaterial;
        }

        Coroutine lerpBlockMaterialPropertiesCoroutine;
        IEnumerator LerpBlockMaterialPropertiesCoroutine(Material transparentMaterial, Material opaqueMaterial, float lerpDuration = .8f)
        {
            Material tempMaterial = new Material(ActiveTransparentMaterial);
            meshRenderer.material = tempMaterial;

            Color startColor1 = tempMaterial.GetColor("_BrightColor");
            Color startColor2 = tempMaterial.GetColor("_DarkColor");
            Vector3 startVector = tempMaterial.GetVector("_Spread");

            // We always go transparent while lerping
            Color targetColor1 = transparentMaterial.GetColor("_BrightColor");
            Color targetColor2 = transparentMaterial.GetColor("_DarkColor");
            Vector3 targetVector = transparentMaterial.GetVector("_Spread");

            float elapsedTime = 0.0f;

            while (elapsedTime < lerpDuration)
            {
                // Reinitialize and restart the animation if interrupted
                if (interruptMaterialLerp)
                {
                    interruptMaterialLerp = false;
                    elapsedTime = 0.0f;
                    transparentMaterial = interruptedLerpTransparentMaterial;
                    opaqueMaterial = interruptedLerpOpaqueMaterial;

                    startColor1 = tempMaterial.GetColor("_BrightColor");
                    startColor2 = tempMaterial.GetColor("_DarkColor");
                    startVector = tempMaterial.GetVector("_Spread");

                    targetColor1 = interruptedLerpTransparentMaterial.GetColor("_BrightColor");
                    targetColor2 = interruptedLerpTransparentMaterial.GetColor("_DarkColor");
                    targetVector = interruptedLerpTransparentMaterial.GetVector("_Spread");
                }

                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / lerpDuration);

                meshRenderer.material.SetColor("_BrightColor", Color.Lerp(startColor1, targetColor1, t));
                meshRenderer.material.SetColor("_DarkColor", Color.Lerp(startColor2, targetColor2, t));
                meshRenderer.material.SetVector("_Spread", Vector3.Lerp(startVector, targetVector, t));

                yield return null;//new WaitForSeconds(.05f);
            }

            ActiveTransparentMaterial = transparentMaterial;
            ActiveOpaqueMaterial = opaqueMaterial;

            meshRenderer.material = TrailBlockProperties.IsTransparent ? transparentMaterial : opaqueMaterial;

            Destroy(tempMaterial);
            
            lerpBlockMaterialPropertiesCoroutine = null;
        }

        //
        // Properties used for animated material transitions
        //
        Material ActiveTransparentMaterial;
        Material ActiveOpaqueMaterial;
        Material interruptedLerpTransparentMaterial;
        Material interruptedLerpOpaqueMaterial;
        bool interruptMaterialLerp = false;
        
        void UpdateMaterial(Material transparentMaterial, Material opaqueMaterial, float lerpDuration = .8f)
        {
            if (ActiveTransparentMaterial == null || ActiveOpaqueMaterial == null)
            {
                ActiveOpaqueMaterial = meshRenderer.material = ThemeManager.Instance.GetTeamBlockMaterial(team);
                ActiveTransparentMaterial = ThemeManager.Instance.GetTeamTransparentBlockMaterial(team);
            }

            if (lerpBlockMaterialPropertiesCoroutine == null)
            {
                lerpBlockMaterialPropertiesCoroutine = StartCoroutine(LerpBlockMaterialPropertiesCoroutine(transparentMaterial, opaqueMaterial, lerpDuration));
            }
            else
            {
                interruptMaterialLerp = true;
                interruptedLerpTransparentMaterial = transparentMaterial;
                interruptedLerpOpaqueMaterial = opaqueMaterial;
            }
        }

        public void Steal(Player player, Teams team)
        {
            if (Team != team)
            {
                if (TrailBlockProperties.IsShielded || TrailBlockProperties.IsSuperShielded)
                {
                    DeactivateShields();
                    return;
                }
                var playerName = player ? player.PlayerName : "No name";
                
                if (StatsManager.Instance != null)
                    StatsManager.Instance.BlockStolen(team, playerName, TrailBlockProperties);

                if (NodeControlManager.Instance != null)
                    NodeControlManager.Instance.StealBlock(team, TrailBlockProperties);

                ChangeTeam(team);
                Player = player;
            } 
        }

        // This changes a block team, and material
        public void ChangeTeam(Teams team)
        {
            if (Team != team)
            {
                // If the team has never been assigned, assign it to the property so the material properties get updated.
                // Else, assign directly to the field so the animated material transitions work correctly
                if (this.team == Teams.Unassigned)
                    Team = team;
                else
                    this.team = team;

                if (TrailBlockProperties.IsDangerous)
                    UpdateMaterial(ThemeManager.Instance.GetTeamTransparentDangerousBlockMaterial(team), ThemeManager.Instance.GetTeamDangerousBlockMaterial(team));
                else if (TrailBlockProperties.IsShielded)
                    UpdateMaterial(ThemeManager.Instance.GetTeamTransparentShieldedBlockMaterial(team), ThemeManager.Instance.GetTeamShieldedBlockMaterial(team));
                else if (TrailBlockProperties.IsSuperShielded) 
                    UpdateMaterial(ThemeManager.Instance.GetTeamTransparentSuperShieldedBlockMaterial(team), ThemeManager.Instance.GetTeamSuperShieldedBlockMaterial(team));
                else 
                    UpdateMaterial(ThemeManager.Instance.GetTeamTransparentBlockMaterial(team), ThemeManager.Instance.GetTeamBlockMaterial(team));
            }
        }

        public void Restore()
        {
            if (!devastated)
            {
                Debug.Log("Restoring trailBlock block");
                if (StatsManager.Instance != null)
                    StatsManager.Instance.BlockRestored(team, PlayerName, TrailBlockProperties);

                if (NodeControlManager.Instance != null)
                    NodeControlManager.Instance.RestoreBlock(team, TrailBlockProperties);

                gameObject.GetComponent<BoxCollider>().enabled = true;
                gameObject.GetComponent<MeshRenderer>().enabled = true;

                destroyed = false;
            }
        }

        void OnDestroy()
        {
            // Cleanup material instance created for the block
            Destroy(GetComponent<MeshRenderer>().material);
        }
    }
}