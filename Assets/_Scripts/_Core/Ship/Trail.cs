﻿using StarWriter.Core.HangerBuilder;
using System.Collections;
using UnityEngine;

namespace StarWriter.Core
{
    public class Trail : MonoBehaviour
    {
        [SerializeField] GameObject FossilBlock;
        [SerializeField] GameObject ParticleEffect;
        [SerializeField] Material material;
        [SerializeField] TrailBlockProperties trailBlockProperties;

        public string ownerId;  // TODO: is the ownerId the player name? I hope it is.
        public float waitTime = .6f;
        public bool embiggen;
        public bool destroyed = false;
        public float MaxSize = 1f;
        public string ID;
        public Vector3 Dimensions;

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

            trailBlockProperties.volume = MaxSize * Dimensions.x * Dimensions.y * Dimensions.z;
            trailBlockProperties.position = transform.position;
            trailBlockProperties.trail = this;

            StartCoroutine(ToggleBlockCoroutine(MaxSize));
        }

        IEnumerator ToggleBlockCoroutine(float MaxSize)
        {
            var DefaultTransformScale = transform.localScale;

            if (warp) DefaultTransformScale *= shards.GetComponent<WarpFieldData>().HybridVector(transform).magnitude;

            var size = 0.01f;

            yield return new WaitForSeconds(waitTime);

            transform.localScale = DefaultTransformScale * size;

            meshRenderer.enabled = true;
            blockCollider.enabled = true;

            while (size < MaxSize)
            {
                size += .5f * Time.deltaTime;
                transform.localScale = DefaultTransformScale * size;
                yield return null;
            }

            // Add block to team score when created
            if (StatsManager.Instance != null)
            {
                StatsManager.Instance.UpdateTeamScore(team, trailBlockProperties.volume);
                StatsManager.Instance.UpdateScore(playerName, trailBlockProperties.volume);

                StatsManager.Instance.BlockCreated(team, playerName, trailBlockProperties);

                //Debug.LogWarning($"Created block. Volume: {trailBlockProperties.volume}, Dimensions: {Dimensions}, MaxSize: {MaxSize}");
            }

            if (NodeControlManager.Instance != null)
            {
                // Node control tracking
                NodeControlManager.Instance.AddBlock(team, playerName, trailBlockProperties);
            }
        }

        public void InstantiateParticle(Transform skimmer)
        {
            var particle = Instantiate(ParticleEffect);
            particle.transform.parent = transform;
            StartCoroutine(UpdateParticleCoroutine(particle, skimmer));
        }

        IEnumerator UpdateParticleCoroutine(GameObject particle, Transform skimmer)
        {
            var time = 50;
            var timer = 0;
            while (timer < time)
            {
                var distance = transform.position - skimmer.position;
                particle.transform.localScale = new Vector3(1, 1, distance.magnitude);
                particle.transform.rotation = Quaternion.LookRotation(distance, transform.up);
                particle.transform.position = skimmer.position;
                timer++;

                yield return null;
            }
            Destroy(particle);
        }

        void OnTriggerEnter(Collider other)
        {
            if (IsShip(other.gameObject))
            {
                var ship = other.GetComponent<ShipGeometry>().Ship;
                var impactVector = ship.transform.forward * ship.GetComponent<ShipData>().speed;

                Collide(ship);
                Explode(impactVector, ship.Team);
                if (StatsManager.Instance != null)
                {
                    StatsManager.Instance.BlockDestroyed(team, ship.Player.PlayerName, trailBlockProperties);
                }

                if (NodeControlManager.Instance != null)
                {
                    NodeControlManager.Instance.RemoveBlock(team, ship.Player.PlayerName, trailBlockProperties);
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


                Explode(impactVector, other.GetComponent<AOEExplosion>().Team); // TODO: need to attribute the explosion color to the team that made the explosion

                if (StatsManager.Instance != null)
                {
                    StatsManager.Instance.BlockDestroyed(other.GetComponent<AOEExplosion>().Team, other.GetComponent<AOEExplosion>().Ship.Player.PlayerName, trailBlockProperties);
                }

                if (NodeControlManager.Instance != null)
                {
                    NodeControlManager.Instance.RemoveBlock(other.GetComponent<AOEExplosion>().Team, other.GetComponent<AOEExplosion>().Ship.Player.PlayerName, trailBlockProperties);
                }
            }
            else if (IsProjectile(other.gameObject))
            {
                if (other.GetComponent<Projectile>().Team == Team)
                    return;

                var speed = other.GetComponent<Projectile>().Velocity;
                var impactVector = speed;

                Explode(impactVector, other.GetComponent<Projectile>().Team); // TODO: need to attribute the explosion color to the team that made the explosion

                StatsManager.Instance.BlockDestroyed(other.GetComponent<Projectile>().Team, other.GetComponent<Projectile>().Ship.Player.PlayerName, trailBlockProperties);

                if (NodeControlManager.Instance != null)
                {
                    NodeControlManager.Instance.RemoveBlock(other.GetComponent<Projectile>().Team, other.GetComponent<Projectile>().Ship.Player.PlayerName, trailBlockProperties);
                }
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

        public void Explode(Vector3 impactVector, Teams team)
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

            // Remove block from team score when destroyed
            if (StatsManager.Instance != null)
            {
                StatsManager.Instance.UpdateTeamScore(team, trailBlockProperties.volume * -1);
                StatsManager.Instance.UpdateScore(playerName, trailBlockProperties.volume * -1);
            }
        }

        public void ConvertToTeam(string PlayerName, Teams team)
        {
            StatsManager.Instance.UpdateTeamScore(this.team, trailBlockProperties.volume * -1);
            StatsManager.Instance.UpdateScore(playerName, trailBlockProperties.volume * -1);

            this.team = team;
            this.playerName = PlayerName;

            StatsManager.Instance.UpdateTeamScore(this.team, trailBlockProperties.volume);
            StatsManager.Instance.UpdateScore(playerName, trailBlockProperties.volume);

            gameObject.GetComponent<MeshRenderer>().material = Hangar.Instance.GetTeamBlockMaterial(team);
        }

        public void restore()
        {
            gameObject.GetComponent<BoxCollider>().enabled = true;
            gameObject.GetComponent<MeshRenderer>().enabled = true;

            destroyed = false;

            // Add block back to team score when created
            StatsManager.Instance.UpdateTeamScore(team, trailBlockProperties.volume);
            StatsManager.Instance.UpdateScore(playerName, trailBlockProperties.volume);
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