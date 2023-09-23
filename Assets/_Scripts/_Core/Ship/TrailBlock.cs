using StarWriter.Core.HangerBuilder;
using System.Collections;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace StarWriter.Core
{
    public class TrailBlock : MonoBehaviour
    {
        [SerializeField] GameObject FossilBlock;
        [SerializeField] public TrailBlockProperties TrailBlockProperties;

        float growthRate = .01f;
        [SerializeField] Vector3 growthVector = new Vector3(0, 2, 0);
        [SerializeField] Vector3 maxScale = new Vector3 (10, 10, 10);
        [SerializeField] Vector3 minScale = new Vector3 (.5f, .5f, .5f);
        public Vector3 TargetScale;

        public GameObject ParticleEffect; // TODO: move this so it references the Team to retrieve the effect.
        public string ownerId;  // TODO: is the ownerId the player name? I hope it is.
        public float waitTime = .6f;
        public bool destroyed = false;
        public bool devastated = false;
        public string ID;
        public int Index;
        public bool Shielded = false;
        public float Volume { get => outerDimensions.x * outerDimensions.y * outerDimensions.z; }

        public bool warp = false;
        GameObject shards;
        public Trail Trail;

        Vector3 outerDimensions; // defines volume
        static GameObject fossilBlockContainer;

        MeshRenderer meshRenderer;
        Vector3 spread;

        BoxCollider blockCollider;
        Teams team;
        public Teams Team { get => team; set => team = value; }
        string playerName = "Unassigned";
        public string PlayerName { get => playerName; set => playerName = value; }

        protected virtual void Start()
        {
            if (warp)
                shards = GameObject.FindGameObjectWithTag("field");

            if (fossilBlockContainer == null)
                fossilBlockContainer = new GameObject { name = "FossilBlockContainer" };

            blockCollider = GetComponent<BoxCollider>();
            blockCollider.enabled = false;

            meshRenderer = GetComponent<MeshRenderer>();
            if (team != Teams.Unassigned)
                meshRenderer.material = Hangar.Instance.GetTeamBlockMaterial(team);
            meshRenderer.enabled = false;

            spread = (Vector3) meshRenderer.material.GetVector("_spread");

            UpdateVolume();
            transform.localScale = Vector3.one * Mathf.Epsilon;
            
            TrailBlockProperties.position = transform.position;
            TrailBlockProperties.trailBlock = this;
            TrailBlockProperties.Index = Index;
            TrailBlockProperties.Trail = Trail;
            TrailBlockProperties.Shielded = Shielded;
            TrailBlockProperties.TimeCreated = Time.time;

            StartCoroutine(CreateBlockCoroutine());
            if (Shielded) ActivateShield();
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
                StatsManager.Instance.BlockCreated(team, playerName, TrailBlockProperties);

            if (NodeControlManager.Instance != null)
                NodeControlManager.Instance.AddBlock(team, playerName, TrailBlockProperties);
        }

        Coroutine sizeChangeCoroutine;
        IEnumerator SizeChangeCoroutine()
        {
            float sqrDistance = (TargetScale - transform.localScale).sqrMagnitude;

            while (sqrDistance > .001f)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, TargetScale, Mathf.Clamp(growthRate * Time.deltaTime * sqrDistance,0,.2f));
                sqrDistance = (TargetScale - transform.localScale).sqrMagnitude;
                yield return null;
            }
        }

        public void ChangeSize()
        {
            TargetScale.x = Mathf.Clamp(TargetScale.x, minScale.x, maxScale.x);
            TargetScale.y = Mathf.Clamp(TargetScale.y, minScale.y, maxScale.y);
            TargetScale.z = Mathf.Clamp(TargetScale.z, minScale.z, maxScale.z);

            var oldVolume = TrailBlockProperties.volume;
            UpdateVolume();
            var deltaVolume = TrailBlockProperties.volume - oldVolume;

            if (StatsManager.Instance != null) StatsManager.Instance.BlockVolumeModified(deltaVolume, TrailBlockProperties);
            if (sizeChangeCoroutine != null) StartCoroutine(SizeChangeCoroutine());
        }

        public void Grow(float amount = 1)
        {
            Grow(amount * growthVector);
        }

        public void Grow(Vector3 growthVector)
        {
            TargetScale += growthVector;

            if (TargetScale.x > maxScale.x || TargetScale.y > maxScale.y || TargetScale.z > maxScale.z)
                ActivateShield();

            ChangeSize();
        }

        // TODO: none of the collision detection should be on the trailblock
        void OnTriggerEnter(Collider other)
        {
            if (IsShip(other.gameObject))
            {
                var ship = other.GetComponent<ShipGeometry>().Ship;
                var impactVector = ship.transform.forward * ship.GetComponent<ShipStatus>().Speed;

                if (!ship.GetComponent<ShipStatus>().Attached)
                {
                    ship.PerformTrailBlockImpactEffects(TrailBlockProperties);
                }

                // Check again because the ship may have attached as part of it's block impact effects
                if (!ship.GetComponent<ShipStatus>().Attached)
                {
                    Explode(impactVector, ship.Team, ship.Player.PlayerName);
                }
            }
        }

        public void Explode(Vector3 impactVector, Teams team, string playerName, bool devastate=false)
        {
            if (Shielded && !devastate)
            {
                DeactivateShield();
                return;
            }

            // We don't destroy the trail blocks, we keep the objects around so they can be restored
            gameObject.GetComponent<BoxCollider>().enabled = false;
            gameObject.GetComponent<MeshRenderer>().enabled = false;

            // Make exploding block
            var explodingBlock = Instantiate(FossilBlock);
            explodingBlock.transform.position = transform.position;
            explodingBlock.transform.localEulerAngles = transform.localEulerAngles;
            explodingBlock.transform.localScale = transform.localScale;
            explodingBlock.transform.parent = fossilBlockContainer.transform;
            explodingBlock.GetComponent<Renderer>().material = new Material(Hangar.Instance.GetTeamExplodingBlockMaterial(this.team));
            explodingBlock.GetComponent<BlockImpact>().HandleImpact(impactVector);

            destroyed = true;
            devastated = devastate;

            if (StatsManager.Instance != null)
                StatsManager.Instance.BlockDestroyed(team, playerName, TrailBlockProperties);

            if (NodeControlManager.Instance != null)
                NodeControlManager.Instance.RemoveBlock(team, playerName, TrailBlockProperties);
        }

        public void DeactivateShield()
        {
            Shielded = false;
            TrailBlockProperties.Shielded = false;
            gameObject.GetComponent<MeshRenderer>().material = Hangar.Instance.GetTeamBlockMaterial(team);
            // TODO: need stats
        }

        public void ActivateShield()
        {
            Shielded = true;
            TrailBlockProperties.Shielded = true;
            gameObject.GetComponent<MeshRenderer>().material = Hangar.Instance.GetTeamShieldedBlockMaterial(team);
            // TODO: need stats
        }

        public void ActivateShield(float duration)
        {
            StartCoroutine(ActivateShieldCoroutine(duration));
        }

        IEnumerator ActivateShieldCoroutine(float duration)
        {
            ActivateShield();

            yield return new WaitForSeconds(duration);
            
            DeactivateShield();
        }

        public void Steal(string playerName, Teams team)
        {
            if (this.team != team)
            {
                if (Shielded)
                {
                    DeactivateShield();
                    return;
                }
                if (StatsManager.Instance != null)
                    StatsManager.Instance.BlockStolen(team, playerName, TrailBlockProperties);

                if (NodeControlManager.Instance != null)
                    //NodeControlManager.Instance.RemoveBlock(team, playerName, TrailBlockProperties);
                    Debug.Log("TODO: Notify NodeControlManager that a block was stolen");

                this.team = team;
                this.playerName = playerName;

                gameObject.GetComponent<MeshRenderer>().material = Hangar.Instance.GetTeamBlockMaterial(team);
            } 
        }

        public void Restore()
        {
            if (!devastated)
            {
                Debug.Log("Restoring trailBlock block");
                if (StatsManager.Instance != null)
                    StatsManager.Instance.BlockRestored(team, playerName, TrailBlockProperties);

                if (NodeControlManager.Instance != null)
                    //NodeControlManager.Instance.RemoveBlock(team, playerName, TrailBlockProperties);
                    Debug.Log("TODO: Notify NodeControlManager that a block was restored");

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