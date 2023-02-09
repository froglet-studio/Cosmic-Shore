using StarWriter.Core.HangerBuilder;
using System.Collections;
using UnityEngine;

namespace StarWriter.Core
{
    public class Trail : MonoBehaviour
    {
        [SerializeField] GameObject FossilBlock;
        public GameObject ParticleEffect; // TODO: move this so it references the Team to retrieve the effect.
        [SerializeField] Material material;
        [SerializeField] TrailBlockProperties trailBlockProperties;

        [SerializeField] float growthRate = .5f;
        public string ownerId;  // TODO: is the ownerId the player name? I hope it is.
        public float waitTime = .6f;
        public bool destroyed = false;
        public string ID;
        public Vector3 InnerDimensions;
        public int Index;
        public TrailSpawner TrailSpawner;

        public bool warp = false;
        GameObject shards;

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

            trailBlockProperties.volume = outerDimensions.x * outerDimensions.y * outerDimensions.z;
            trailBlockProperties.position = transform.position;
            trailBlockProperties.trail = this;
            trailBlockProperties.Index = Index;
            trailBlockProperties.TrailSpawner = TrailSpawner;


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
                StatsManager.Instance.BlockCreated(team, playerName, trailBlockProperties);

            if (NodeControlManager.Instance != null)
                NodeControlManager.Instance.AddBlock(team, playerName, trailBlockProperties);
        }

        // TODO: none of the collision detection should be on the trail
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
            else if (IsSkimmer(other.gameObject))
            {
                other.GetComponent<Skimmer>().PerformSkimmerImpactEffects(trailBlockProperties);
            }
            else if (IsExplosion(other.gameObject))
            {
                if (other.GetComponent<AOEExplosion>().Team == Team)
                    return;

                var speed = other.GetComponent<AOEExplosion>().speed * 10;
                var impactVector = (transform.position - other.transform.position).normalized * speed;

                Explode(impactVector, other.GetComponent<AOEExplosion>().Team, other.GetComponent<AOEExplosion>().Ship.Player.PlayerName);
            }
            else if (IsProjectile(other.gameObject))
            {
                if (other.GetComponent<Projectile>().Team == Team)
                    return;

                var impactVector = other.GetComponent<Projectile>().Velocity;

                Explode(impactVector, other.GetComponent<Projectile>().Team, other.GetComponent<Projectile>().Ship.Player.PlayerName); // TODO: need to attribute the explosion color to the team that made the explosion
            }
        }

        public void Collide(Ship ship)
        {
            if (ownerId == ship.Player.PlayerUUID)
            {
                Debug.Log($"You hit you're teams tail - ownerId: {ownerId}, team: {team}");
            }
            else
            {
                Debug.Log($"Player ({ship.Player.PlayerUUID}) just gave player({ownerId}) a point via tail collision");
                AddToScore?.Invoke(ownerId, scoreChange);
            }

            ship.PerformTrailBlockImpactEffects(trailBlockProperties);
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
                StatsManager.Instance.BlockDestroyed(team, playerName, trailBlockProperties);

            if (NodeControlManager.Instance != null)
                NodeControlManager.Instance.RemoveBlock(team, playerName, trailBlockProperties);
        }

        public void Steal(string playerName, Teams team)
        {
            if (this.team != team)
            {
                if (StatsManager.Instance != null)
                    StatsManager.Instance.BlockStolen(team, playerName, trailBlockProperties);

                if (NodeControlManager.Instance != null)
                    //NodeControlManager.Instance.RemoveBlock(team, playerName, trailBlockProperties);
                    Debug.Log("TODO: Notify NodeControlManager that a block was stolen");

                this.team = team;
                this.playerName = playerName;

                gameObject.GetComponent<MeshRenderer>().material = Hangar.Instance.GetTeamBlockMaterial(team);
            } 
        }

        public void Restore()
        {
            Debug.Log("Restoring trail block");
            if (StatsManager.Instance != null)
                StatsManager.Instance.BlockRestored(team, playerName, trailBlockProperties);

            if (NodeControlManager.Instance != null)
                //NodeControlManager.Instance.RemoveBlock(team, playerName, trailBlockProperties);
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
        private bool IsSkimmer(GameObject go)
        {
            return go.layer == LayerMask.NameToLayer("Skimmers");
        }
        private bool IsExplosion(GameObject go)
        {
            return go.layer == LayerMask.NameToLayer("Explosions");
        }
        private bool IsProjectile(GameObject go)
        {
            return go.layer == LayerMask.NameToLayer("Projectiles");
        }
    }
}