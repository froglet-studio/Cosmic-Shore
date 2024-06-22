using System.Collections;
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
        float growthRate = .01f;

        [Header("Trail Block Status")]
        public float waitTime = .6f;
        public bool destroyed = false;
        public bool devastated = false;
        public string ID;
        public int Index;
        public bool Shielded = false;
        public bool IsSuperShielded = false;
        public bool warp = false;
        public bool IsSmallest = false;
        public bool IsLargest = false;


        // Shader related properties
        MeshRenderer meshRenderer;
        Vector3 spread;

        // Trail physics components
        BoxCollider blockCollider;
        
        [Header("Team Ownership on the Block")]
        Teams team;
        public string ownerId;  // TODO: is the ownerId the player name? I hope it is.
        public Teams Team { get => team; set => team = value; }
        public Player Player;
        public string PlayerName { get => Player ? Player.PlayerName : ""; }

        protected virtual void Start()
        {
            if (fossilBlockContainer == null)
                fossilBlockContainer = new GameObject { name = "FossilBlockContainer" };

            blockCollider = GetComponent<BoxCollider>();
            blockCollider.enabled = false;

            meshRenderer = GetComponent<MeshRenderer>();
            if (team != Teams.Unassigned)
                meshRenderer.material = Hangar.Instance.GetTeamBlockMaterial(team);
            meshRenderer.enabled = false;

            spread = (Vector3) meshRenderer.material.GetVector("_Spread");

            UpdateVolume();
            transform.localScale = Vector3.one * Mathf.Epsilon;

            InitializeTrailBlockProperties();

            StartCoroutine(CreateBlockCoroutine());
            if (Shielded) ActivateShield();
        }



        private void InitializeTrailBlockProperties()
        {
            TrailBlockProperties.position = transform.position;
            TrailBlockProperties.trailBlock = this;
            TrailBlockProperties.Index = Index;
            TrailBlockProperties.Trail = Trail;
            TrailBlockProperties.Shielded = Shielded;
            TrailBlockProperties.TimeCreated = Time.time;
        }

        void UpdateVolume()
        {
            outerDimensions = TargetScale + 2 * spread;
            TrailBlockProperties.volume = outerDimensions.x * outerDimensions.y * outerDimensions.z;
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

            // TODO: State tracker should go to mini games
            // if (StateTracker.Instance != null)
            //     StateTracker.Instance.AddBlock(TrailBlockProperties);
        }

        bool isSizeChangeActive = false;
        IEnumerator SizeChangeCoroutine()
        {
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
            if (TargetScale.x > MaxScale.x || TargetScale.y > MaxScale.y || TargetScale.z > MaxScale.z)
            {
                ActivateShield();
                IsLargest = true;
            }
            if (TargetScale.x < minScale.x || TargetScale.y < minScale.y || TargetScale.z < minScale.z)
            {
                IsSmallest = true;
            }


            TargetScale.x = Mathf.Clamp(TargetScale.x, minScale.x, MaxScale.x);
            TargetScale.y = Mathf.Clamp(TargetScale.y, minScale.y, MaxScale.y);
            TargetScale.z = Mathf.Clamp(TargetScale.z, minScale.z, MaxScale.z);

            var oldVolume = TrailBlockProperties.volume;
            UpdateVolume();
            var deltaVolume = TrailBlockProperties.volume - oldVolume;

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

        // TODO: none of the collision detection should be on the trailblock
        void OnTriggerEnter(Collider other)
        {
            if (IsShip(other.gameObject))
            {
                var ship = other.GetComponent<ShipGeometry>().Ship;

                if (!ship.GetComponent<ShipStatus>().Attached)
                {
                    ship.PerformTrailBlockImpactEffects(TrailBlockProperties);
                }
            }
        }

        public virtual void Explode(Vector3 impactVector, Teams team, string playerName, bool devastate=false)
        {
            if ((Shielded && !devastate) || IsSuperShielded)
            {
                DeactivateShields();
                return;
            }

            // We don't destroy the trail blocks, we keep the objects around so they can be restored
            gameObject.GetComponent<BoxCollider>().enabled = false;
            gameObject.GetComponent<MeshRenderer>().enabled = false;

            // Make exploding block
            var explodingBlock = Instantiate(FossilBlock);
            explodingBlock.transform.position = transform.position;
            explodingBlock.transform.eulerAngles = transform.eulerAngles;
            explodingBlock.transform.localScale = transform.lossyScale;
            explodingBlock.transform.parent = fossilBlockContainer.transform;
            explodingBlock.GetComponent<Renderer>().material = new Material(Hangar.Instance.GetTeamExplodingBlockMaterial(this.team));
            explodingBlock.GetComponent<BlockImpact>().HandleImpact(impactVector);

            destroyed = true;
            devastated = devastate;

            if (StatsManager.Instance != null)
                StatsManager.Instance.BlockDestroyed(team, playerName, TrailBlockProperties);

            if (NodeControlManager.Instance != null)
                NodeControlManager.Instance.RemoveBlock(team, TrailBlockProperties);

            // TODO: State track should go to mini games
            // if (StateTracker.Instance != null)
            //     StateTracker.Instance.RemoveBlock(TrailBlockProperties);

        }

        public void DeactivateShields()
        {
            if (lerpBlockMaterialPropertiesCoroutine != null) StopCoroutine(lerpBlockMaterialPropertiesCoroutine);
            StartCoroutine(LerpBlockMaterialPropertiesCoroutine(Hangar.Instance.GetTeamBlockMaterial(team)));
            StartCoroutine(DeactivateShieldsCoroutine(1));
            // TODO: need stats
        }

        IEnumerator DeactivateShieldsCoroutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            Shielded = false;
            IsSuperShielded = false;
            TrailBlockProperties.Shielded = false;
            TrailBlockProperties.IsSuperShielded = false;
        }

        public void ActivateShield()
        {
            Shielded = true;
            TrailBlockProperties.Shielded = true;
            if (lerpBlockMaterialPropertiesCoroutine != null) StopCoroutine(lerpBlockMaterialPropertiesCoroutine);
            StartCoroutine(LerpBlockMaterialPropertiesCoroutine(Hangar.Instance.GetTeamShieldedBlockMaterial(team)));
            // TODO: need stats
        }

        public void ActivateSuperShield()
        {
            IsSuperShielded = true;
            TrailBlockProperties.IsSuperShielded = true;
            if (lerpBlockMaterialPropertiesCoroutine != null) StopCoroutine(lerpBlockMaterialPropertiesCoroutine);
            StartCoroutine(LerpBlockMaterialPropertiesCoroutine(Hangar.Instance.GetTeamSuperShieldedBlockMaterial(team)));
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

        Coroutine lerpBlockMaterialPropertiesCoroutine;
        IEnumerator LerpBlockMaterialPropertiesCoroutine(Material targetMaterial, float lerpDuration = .8f)
        {
            Material tempMaterial = new Material(meshRenderer.material);
            meshRenderer.material = tempMaterial;

            Color startColor1 = tempMaterial.GetColor("_BrightColor");
            Color startColor2 = tempMaterial.GetColor("_DarkColor");
            Vector3 startVector = tempMaterial.GetVector("_Spread");

            Color targetColor1 = targetMaterial.GetColor("_BrightColor");
            Color targetColor2 = targetMaterial.GetColor("_DarkColor");
            Vector3 targetVector = targetMaterial.GetVector("_Spread");

            float elapsedTime = 0.0f;

            while (elapsedTime < lerpDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / lerpDuration);

                tempMaterial.SetColor("_BrightColor", Color.Lerp(startColor1, targetColor1, t));
                tempMaterial.SetColor("_DarkColor", Color.Lerp(startColor2, targetColor2, t));
                tempMaterial.SetVector("_Spread", Vector3.Lerp(startVector, targetVector, t));

                yield return null;//new WaitForSeconds(.05f);
            }

            meshRenderer.material = targetMaterial;
        }

        public void Steal(Player player, Teams team)
        {
            if (this.team != team)
            {
                if (Shielded || IsSuperShielded)
                {
                    DeactivateShields();
                    return;
                }
                string name;
                if (player) name = player.PlayerName;
                else name = "no name";
                if (StatsManager.Instance != null)
                    StatsManager.Instance.BlockStolen(team, name, TrailBlockProperties);

                if (NodeControlManager.Instance != null)
                    NodeControlManager.Instance.StealBlock(team, TrailBlockProperties);

                this.team = team;
                Player = player;

                if (lerpBlockMaterialPropertiesCoroutine != null) StopCoroutine(lerpBlockMaterialPropertiesCoroutine);
                StartCoroutine(LerpBlockMaterialPropertiesCoroutine(Hangar.Instance.GetTeamBlockMaterial(team)));
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

        // TODO: utility class needed to hold these
        bool IsShip(GameObject go)
        {
            return go.layer == LayerMask.NameToLayer("Ships");
        }
    }
}