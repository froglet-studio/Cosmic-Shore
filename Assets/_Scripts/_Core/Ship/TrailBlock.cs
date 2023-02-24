using StarWriter.Core.HangerBuilder;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core
{
    public class TrailBlock : MonoBehaviour
    {
        [SerializeField] GameObject FossilBlock;
        public GameObject ParticleEffect; // TODO: move this so it references the Team to retrieve the effect.
        [SerializeField] Material material;
        [SerializeField] public TrailBlockProperties TrailBlockProperties;

        readonly List<ShipScaleModifier> ScaleModifiers = new List<ShipScaleModifier>();

        [SerializeField] float growthRate = .5f;
        public string ownerId;  // TODO: is the ownerId the player name? I hope it is.
        public float waitTime = .6f;
        public bool destroyed = false;
        public string ID;
        public Vector3 InnerDimensions;
        public int Index;
        //public TrailSpawner TrailSpawner;

        public bool warp = false;
        GameObject shards;
        public Trail Trail;

        public delegate void OnCollisionIncreaseScore(string uuid, float amount);
        public static event OnCollisionIncreaseScore AddToScore;

        private int scoreChange = 1;
        private static GameObject fossilBlockContainer;
        private MeshRenderer meshRenderer;
        private BoxCollider blockCollider;
        Teams team;
        public Teams Team { get => team; set => team = value; }
        string playerName;
        public string PlayerName { get => playerName; set => playerName = value; }

        void Start()
        {
            if (warp) shards = GameObject.FindGameObjectWithTag("field");

            if (fossilBlockContainer == null)
            {
                fossilBlockContainer = new GameObject();
                fossilBlockContainer.name = "FossilBlockContainer";
            }

            meshRenderer = GetComponent<MeshRenderer>();
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


            StartCoroutine(CreateBlockCoroutine());
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
                transform.localScale = DefaultTransformScale * size;
                size += growthRate * Time.deltaTime;
                
                yield return null;
            }

            // Add block to team score when created
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
                    Collide(ship);
                    Explode(impactVector, ship.Team, ship.Player.PlayerName);
                }
            }
            else if (IsExplosion(other.gameObject))
            {
                if (other.GetComponent<AOEExplosion>().Team == Team)
                    return;

                var speed = other.GetComponent<AOEExplosion>().speed * 10;
                var impactVector = (transform.position - other.transform.position).normalized * speed;

                Explode(impactVector, other.GetComponent<AOEExplosion>().Team, other.GetComponent<AOEExplosion>().Ship.Player.PlayerName);
            }
        }

        public void Collide(Ship ship)
        {
            if (ownerId != ship.Player.PlayerUUID)
                AddToScore?.Invoke(ownerId, scoreChange);

            ship.PerformTrailBlockImpactEffects(TrailBlockProperties);
        }

        public void Explode(Vector3 impactVector, Teams team, string playerName)
        {
            // We don't destroy the trail blocks, we keep the objects around so they can be restored
            gameObject.GetComponent<BoxCollider>().enabled = false;
            gameObject.GetComponent<MeshRenderer>().enabled = false;

            // Make exploding block
            var explodingBlock = Instantiate(FossilBlock);
            explodingBlock.transform.position = transform.position;
            explodingBlock.transform.localEulerAngles = transform.localEulerAngles;
            explodingBlock.transform.localScale = transform.localScale;
            explodingBlock.transform.parent = fossilBlockContainer.transform;
            explodingBlock.GetComponent<Renderer>().material = new Material(material);
            explodingBlock.GetComponent<BlockImpact>().HandleImpact(impactVector, team);

            destroyed = true;

            if (StatsManager.Instance != null)
                StatsManager.Instance.BlockDestroyed(team, playerName, TrailBlockProperties);

            if (NodeControlManager.Instance != null)
                NodeControlManager.Instance.RemoveBlock(team, playerName, TrailBlockProperties);
        }

        public void Steal(string playerName, Teams team)
        {
            if (this.team != team)
            {
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
        private bool IsShip(GameObject go)
        {
            return go.layer == LayerMask.NameToLayer("Ships");
        }

        private bool IsExplosion(GameObject go)
        {
            return go.layer == LayerMask.NameToLayer("Explosions");
        }

    }
}