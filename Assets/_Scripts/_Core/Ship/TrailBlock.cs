using StarWriter.Core.HangerBuilder;
using System.Collections;
using UnityEngine;

namespace StarWriter.Core
{
    public class TrailBlock : MonoBehaviour
    {
        [SerializeField] GameObject FossilBlock;
        [SerializeField] Material explodingMaterial;
        [SerializeField] public TrailBlockProperties TrailBlockProperties;
        [SerializeField] float growthRate = .5f;
        public GameObject ParticleEffect; // TODO: move this so it references the Team to retrieve the effect.
        public string ownerId;  // TODO: is the ownerId the player name? I hope it is.
        public float waitTime = .6f;
        public bool destroyed = false;
        public string ID;
        public Vector3 InnerDimensions;
        public int Index;
        public bool Shielded = false;

        public bool warp = false;
        GameObject shards;
        public Trail Trail;

        static GameObject fossilBlockContainer;
        MeshRenderer meshRenderer;
        BoxCollider blockCollider;
        Teams team;
        public Teams Team { get => team; set => team = value; }
        string playerName;
        public string PlayerName { get => playerName; set => playerName = value; }

        protected virtual void Start()
        {
            if (warp) shards = GameObject.FindGameObjectWithTag("field");

            if (fossilBlockContainer == null)
            {
                fossilBlockContainer = new GameObject();
                fossilBlockContainer.name = "FossilBlockContainer";
            }

            meshRenderer = GetComponent<MeshRenderer>();
            gameObject.GetComponent<MeshRenderer>().material = Hangar.Instance.GetTeamBlockMaterial(team);
            meshRenderer.enabled = false;

            blockCollider = GetComponent<BoxCollider>();
            blockCollider.enabled = false;

            var spread = (Vector3)meshRenderer.material.GetVector("_spread");
            var outerDimensions = InnerDimensions + 2 * spread;

            TrailBlockProperties.volume = outerDimensions.x * outerDimensions.y * outerDimensions.z;
            TrailBlockProperties.position = transform.position;
            TrailBlockProperties.trailBlock = this;
            TrailBlockProperties.Index = Index;
            TrailBlockProperties.Trail = Trail;
            TrailBlockProperties.Shielded = Shielded;

            StartCoroutine(CreateBlockCoroutine());
            if (Shielded) ActivateShield();
        }

        IEnumerator CreateBlockCoroutine()
        {
            yield return new WaitForSeconds(waitTime);

            meshRenderer.enabled = true;
            blockCollider.enabled = true;

            var DefaultTransformScale = InnerDimensions;
            var size = 0.01f;

            if (warp) 
                DefaultTransformScale *= shards.GetComponent<WarpFieldData>().HybridVector(transform).magnitude;
            
            transform.localScale = DefaultTransformScale * size;

            while (size < 1)
            {
                size = Mathf.Clamp(size + growthRate * Time.deltaTime, 0, 1);
                transform.localScale = DefaultTransformScale * size;
                yield return null;
            }

            //Add block to team score when created
            if (StatsManager.Instance != null)
                StatsManager.Instance.BlockCreated(team, playerName, TrailBlockProperties);

            if (NodeControlManager.Instance != null)
                NodeControlManager.Instance.AddBlock(team, playerName, TrailBlockProperties);
        }

        //IEnumerator GrowBlockCoroutine(float amount)
        //{

        //    var DefaultTransformScale = InnerDimensions;
        //    var size = 1;

        //    if (warp)
        //        DefaultTransformScale *= shards.GetComponent<WarpFieldData>().HybridVector(transform).magnitude;

        //    transform.localScale = DefaultTransformScale * size;

        //    while (size < 1 + amount)
        //    {
        //        transform.localScale = DefaultTransformScale * size;
        //        size += growthRate * Time.deltaTime;

        //        yield return null;
        //    }

        //    // Add block to team score when created
        //    if (StatsManager.Instance != null)
        //        StatsManager.Instance.BlockCreated(team, playerName, TrailBlockProperties);

        //    if (NodeControlManager.Instance != null)
        //        NodeControlManager.Instance.AddBlock(team, playerName, TrailBlockProperties);
        //}

        public void Grow(float amount)
        {
            //StartCoroutine(GrowBlockCoroutine(amount));// TODO: start a block scaling coroutine that updates inner dimensions and volume tracking stats
        }



        //void ApplyScaleModifiers()
        //{
        //    float accumulatedSpeedModification = 1;
        //    for (int i = ScaleModifiers.Count - 1; i >= 0; i--)
        //    {
        //        var modifier = ScaleModifiers[i];
        //        modifier.elapsedTime += Time.deltaTime;
        //        ScaleModifiers[i] = modifier;

        //        if (modifier.elapsedTime >= modifier.duration)
        //            ScaleModifiers.RemoveAt(i);
        //        else
        //            accumulatedSpeedModification *= Mathf.Lerp(modifier.initialValue, 1f, modifier.elapsedTime / modifier.duration);
        //    }

        //    accumulatedSpeedModification = Mathf.Min(accumulatedSpeedModification, scaleModifiersModifierMax);
        //    ship.shipData.SpeedMultiplier = accumulatedSpeedModification;
        //}



        // TODO: none of the collision detection should be on the trailblock
        void OnTriggerEnter(Collider other)
        {
            if (IsShip(other.gameObject))
            {
                var ship = other.GetComponent<ShipGeometry>().Ship;
                var impactVector = ship.transform.forward * ship.GetComponent<ShipData>().Speed;

                if (!ship.GetComponent<ShipData>().Attached)
                {
                    ship.PerformTrailBlockImpactEffects(TrailBlockProperties);
                    Explode(impactVector, ship.Team, ship.Player.PlayerName);
                }
            }
        }

        public void Explode(Vector3 impactVector, Teams team, string playerName)
        {
            if (Shielded)
            {
                PopShield();
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
            explodingBlock.GetComponent<Renderer>().material = new Material(explodingMaterial);
            explodingBlock.GetComponent<BlockImpact>().HandleImpact(impactVector, team);

            destroyed = true;

            if (StatsManager.Instance != null)
                StatsManager.Instance.BlockDestroyed(team, playerName, TrailBlockProperties);

            if (NodeControlManager.Instance != null)
                NodeControlManager.Instance.RemoveBlock(team, playerName, TrailBlockProperties);
        }

        public void PopShield()
        {
            Shielded = false;
            TrailBlockProperties.Shielded = false;
            gameObject.GetComponent<MeshRenderer>().material = Hangar.Instance.GetTeamBlockMaterial(this.team);
            // TODO: need stats
        }

        public void ActivateShield()
        {
            Shielded = true;
            TrailBlockProperties.Shielded = true;
            gameObject.GetComponent<MeshRenderer>().material = Hangar.Instance.GetTeamShieldedBlockMaterial(this.team);
            // TODO: need stats
        }

        public void Steal(string playerName, Teams team)
        {
            if (this.team != team)
            {
                if (Shielded)
                {
                    PopShield();
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

        // TODO: utility class needed to hold these
        bool IsShip(GameObject go)
        {
            return go.layer == LayerMask.NameToLayer("Ships");
        }
    }
}