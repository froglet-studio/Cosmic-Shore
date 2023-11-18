﻿using CosmicShore.Core.HangerBuilder;
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
        [SerializeField] Vector3 maxScale = new Vector3 (10, 10, 10);
        public Vector3 TargetScale;
        Vector3 outerDimensions; // defines volume
        [SerializeField] Vector3 growthVector = new Vector3(0, 2, 0);
        public float Volume { get => outerDimensions.x * outerDimensions.y * outerDimensions.z; }
        float growthRate = .01f;

        [Header("Trail Block Status")]
        public float waitTime = .6f;
        public bool destroyed = false;
        public bool devastated = false;
        public string ID;
        public int Index;
        public bool Shielded = false;
        public bool warp = false;


        [Header("Game Rewards")]
        GameObject shards;
        
        // Shader related properties
        MeshRenderer meshRenderer;
        Vector3 spread;

        // Trail physics components
        BoxCollider blockCollider;
        
        [Header("Team Ownership on the Block")]
        Teams team;
        public string ownerId;  // TODO: is the ownerId the player name? I hope it is.
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
                StatsManager.Instance.BlockCreated(team, playerName, TrailBlockProperties);

            if (NodeControlManager.Instance != null)
                NodeControlManager.Instance.AddBlock(team, TrailBlockProperties);
        }

        bool isSizeChangeActive = false;
        IEnumerator SizeChangeCoroutine()
        {
            isSizeChangeActive = true;
            float sqrDistance = (TargetScale - transform.localScale).sqrMagnitude;

            while (sqrDistance > .05f)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, TargetScale, Mathf.Clamp(growthRate * Time.deltaTime * sqrDistance,.05f,.2f));
                sqrDistance = (TargetScale - transform.localScale).sqrMagnitude;
                yield return null;
            }
            transform.localScale = TargetScale;
            isSizeChangeActive = false;
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
            if (!isSizeChangeActive)
            {
                StartCoroutine(SizeChangeCoroutine());
            }
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

                if (!ship.GetComponent<ShipStatus>().Attached)
                {
                    ship.PerformTrailBlockImpactEffects(TrailBlockProperties);
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
                NodeControlManager.Instance.RemoveBlock(team, TrailBlockProperties);
        }

        

        public void DeactivateShield()
        {
            if (lerpBlockMaterialPropertiesCoroutine != null) StopCoroutine(lerpBlockMaterialPropertiesCoroutine);
            StartCoroutine(LerpBlockMaterialPropertiesCoroutine(Hangar.Instance.GetTeamBlockMaterial(team)));
            StartCoroutine(DeactivateShieldCoroutine(1));
            // TODO: need stats
        }

        IEnumerator DeactivateShieldCoroutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            Shielded = false;
            TrailBlockProperties.Shielded = false;
        }

        public void ActivateShield()
        {
            Shielded = true;
            TrailBlockProperties.Shielded = true;
            if (lerpBlockMaterialPropertiesCoroutine != null) StopCoroutine(lerpBlockMaterialPropertiesCoroutine);
            StartCoroutine(LerpBlockMaterialPropertiesCoroutine(Hangar.Instance.GetTeamShieldedBlockMaterial(team)));
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
                    NodeControlManager.Instance.StealBlock(team, TrailBlockProperties);

                this.team = team;
                this.playerName = playerName;

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
                    StatsManager.Instance.BlockRestored(team, playerName, TrailBlockProperties);

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