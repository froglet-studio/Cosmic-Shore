using CosmicShore.Environment.FlowField;
using CosmicShore.Game.IO;
using CosmicShore.Game.Projectiles;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core
{
    public class Skimmer : ElementalShipComponent
    {
        [SerializeField] List<TrailBlockImpactEffects> blockImpactEffects;
        [SerializeField] List<SkimmerStayEffects> blockStayEffects;
        [SerializeField] List<ShipImpactEffects> shipImpactEffects;

        [SerializeField] float vaccumAmount = 80f;
        [SerializeField] bool vacuumCrystal = true;

        [SerializeField] float particleDurationAtSpeedOne = 300f;
        [SerializeField] bool affectSelf = true;
        [SerializeField] float chargeAmount;
        [SerializeField] float MultiSkimMultiplier = 0f;
        [SerializeField] bool visible;
        [SerializeField] ElementalFloat Scale = new ElementalFloat(1);
        
        [HideInInspector] public Ship ship;
        [HideInInspector] public Player Player;
        [HideInInspector] public Teams team;

        float appliedScale;
        ResourceSystem resourceSystem;

        Dictionary<string, float> skimStartTimes = new();
        CameraManager cameraManager;

        public int activelySkimmingBlockCount = 0;
        public int ActivelySkimmingBlockCount { get { return activelySkimmingBlockCount; } }

        [Header("Optional Skimmer Components")]
        [SerializeField] GameObject AOEPrefab;
        [SerializeField] float AOEPeriod;
        [SerializeField] private Material lineMaterial;
        [SerializeField] PoolManager markerContainer;

        [SerializeField] int resourceIndex = 0;

        float minMatureBlockSqrDistance = Mathf.Infinity;
        TrailBlock minMatureBlock;
        float fuel = 0;

        float distanceWeight;
        float directionWeight;

        float sweetSpot;
        float sqrSweetSpot;
        float FWHM;
        float sigma;
        float sqrRadius;

        float initialGap;

        void Start()
        {
            cameraManager = CameraManager.Instance;
            if (ship != null)
            {
                BindElementalFloats(ship);
                resourceSystem = ship.GetComponent<ResourceSystem>();
                if (visible)
                    GetComponent<MeshRenderer>().material = new Material(ship.SkimmerMaterial);
            }
            sweetSpot = transform.localScale.x / 4;
            sqrSweetSpot = transform.localScale.x * transform.localScale.x / 16;
            FWHM = sqrSweetSpot; //Full Width at Half Max
            sigma = FWHM / 2.355f;
            sqrRadius = transform.localScale.x * transform.localScale.x / 4;
            initialGap = ship.TrailSpawner.Gap;
            if (appliedScale != Scale.Value)
            {
                appliedScale = Scale.Value;
                transform.localScale = Vector3.one * appliedScale;
            }
            if (markerContainer) markerContainer.transform.parent = ship.Player.transform;
        }

        void Update()
        {
            if (appliedScale != Scale.Value)
            {
                appliedScale = Scale.Value;
                transform.localScale = Vector3.one * appliedScale;
            }
        }

        // TODO: p1- review -- Maja added this to try and enable shark skimmer smashing
        void PerformBlockImpactEffects(TrailBlockProperties trailBlockProperties)
        {
            foreach (TrailBlockImpactEffects effect in blockImpactEffects)
            {
                switch (effect)
                {
                    case TrailBlockImpactEffects.PlayHaptics:
                        if (!ship.ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.BlockCollision);//.PlayBlockCollisionHaptics();
                        break;
                    case TrailBlockImpactEffects.DeactivateTrailBlock:
                        trailBlockProperties.trailBlock.Damage(ship.ShipStatus.Course * ship.ShipStatus.Speed * ship.Inertia, team, Player.PlayerName);
                        break;
                    case TrailBlockImpactEffects.Steal:
                        //Debug.Log($"steal: playername {Player.PlayerName} team: {team}");
                        trailBlockProperties.trailBlock.Steal(Player, team);
                        break;
                    case TrailBlockImpactEffects.GainResourceByVolume:
                        resourceSystem.ChangeResourceAmount(resourceIndex, (chargeAmount * trailBlockProperties.volume) + (activelySkimmingBlockCount * MultiSkimMultiplier));
                        break;
                    case TrailBlockImpactEffects.GainResource:
                        resourceSystem.ChangeResourceAmount(resourceIndex, chargeAmount + (activelySkimmingBlockCount * MultiSkimMultiplier));
                        break;
                    case TrailBlockImpactEffects.FX:
                        StartCoroutine(DisplaySkimParticleEffectCoroutine(trailBlockProperties.trailBlock));
                        break;
                        // This is actually redundant with Skimmer's built in "Fuel Amount" variable
                        //case TrailBlockImpactEffects.ChangeFuel:
                        //FuelSystem.ChangeFuelAmount(ship.blockFuelChange);
                        //break;
                }
            }
        }

        void PerformShipImpactEffects(ShipGeometry shipGeometry)
        {
            if (StatsManager.Instance != null)
                StatsManager.Instance.SkimmerShipCollision(ship, shipGeometry.Ship);
            foreach (ShipImpactEffects effect in shipImpactEffects)
            {
                switch (effect)
                {
                    case ShipImpactEffects.TrailSpawnerCooldown:
                        shipGeometry.Ship.TrailSpawner.PauseTrailSpawner();
                        shipGeometry.Ship.TrailSpawner.RestartTrailSpawnerAfterDelay(10);
                        break;
                    case ShipImpactEffects.PlayHaptics:
                        if (!ship.ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.ShipCollision);//.PlayShipCollisionHaptics();
                        break;
                    case ShipImpactEffects.AreaOfEffectExplosion:
                        if (onCoolDown || shipGeometry.Ship.Team == team) break;

                        var AOEExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
                        AOEExplosion.Ship = ship;
                        AOEExplosion.SetPositionAndRotation(transform.position, transform.rotation);
                        AOEExplosion.MaxScale = ship.ShipStatus.Speed - shipGeometry.Ship.ShipStatus.Speed;
                        StartCoroutine(CooldownCoroutine(AOEPeriod));
                        break;
                }
            }
        }


        bool onCoolDown = false;
        IEnumerator CooldownCoroutine(float Period)
        {
            onCoolDown = true;
            yield return new WaitForSeconds(Period);
            onCoolDown = false;
        }

        void PerformBlockStayEffects(float combinedWeight)
        {
            foreach (SkimmerStayEffects effect in blockStayEffects)
            {
                switch (effect)
                {
                    case SkimmerStayEffects.ChangeResource:
                        resourceSystem.ChangeResourceAmount(resourceIndex, fuel);
                        break;
                    case SkimmerStayEffects.Boost:
                        Boost(combinedWeight);
                        break;
                    case SkimmerStayEffects.ScaleTrailAndCamera:
                        ScaleTrailAndCamera();
                        break;
                    case SkimmerStayEffects.VizualizeDistance:
                        VizualizeDistance(combinedWeight);
                        break;
                    case SkimmerStayEffects.ScaleHapticWithDistance:
                        ScaleHapticWithDistance(combinedWeight);
                        break;
                    case SkimmerStayEffects.ScalePitchAndYaw:
                        ScalePitchAndYaw(combinedWeight);
                        break;
                    case SkimmerStayEffects.Align:
                        if (ship.ShipStatus.AlignmentEnabled) AlignAndNudge(combinedWeight);
                        break;
                    case SkimmerStayEffects.ScaleGap:
                        ScaleGap(combinedWeight);
                        break;
                }
            }
        }

        void StartSkim(TrailBlock trailBlock)
        {
            if (trailBlock == null) return;

            if (skimStartTimes.ContainsKey(trailBlock.ownerID)) return;
            activelySkimmingBlockCount++;
            skimStartTimes.Add(trailBlock.ownerID, Time.time);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<ShipGeometry>(out var shipGeometry))
            {
                PerformShipImpactEffects(shipGeometry);
            }

            if (other.TryGetComponent<TrailBlock>(out var trailBlock) && (affectSelf || trailBlock.Team != team))
            {
                StartSkim(trailBlock);
                PerformBlockImpactEffects(trailBlock.TrailBlockProperties);
            }

            if (ship.ShipStatus.AlignmentEnabled && Player.ActivePlayer && Player.ActivePlayer.Ship == ship) // TODO: ditch line renderer
            {
                VisualizeTubeAroundBlock(trailBlock);
            }
        }

        void VacuumCrystal(Crystal crystal)
        {
            crystal.transform.position = Vector3.MoveTowards(crystal.transform.position, transform.position, vaccumAmount * Time.deltaTime / crystal.transform.lossyScale.x);
        }

        void OnTriggerStay(Collider other)
        {
            float skimDecayDuration = 1;

            if (other.TryGetComponent<Crystal>(out var crystal) && vacuumCrystal) VacuumCrystal(crystal);

            if (!other.TryGetComponent<TrailBlock>(out var trailBlock)) return;
            
            if (trailBlock.Team == team && !affectSelf) return;
            
            // Occasionally, seeing a KeyNotFoundException, so maybe we miss the OnTriggerEnter event (note: always seems to be for AOE blocks)
            if (!skimStartTimes.ContainsKey(trailBlock.ownerID))   
                StartSkim(trailBlock);

            float sqrDistance = (transform.position - other.transform.position).sqrMagnitude;
            if (trailBlock.ownerID != ship.Player.PlayerUUID || Time.time - trailBlock.TrailBlockProperties.TimeCreated > 7)
            {
                minMatureBlockSqrDistance = Mathf.Min(minMatureBlockSqrDistance, sqrDistance);
                if (sqrDistance == minMatureBlockSqrDistance) 
                    minMatureBlock = trailBlock;
            }



            //foreach (Transform child in trailBlock.transform)
            //{
            //    if (child.gameObject.CompareTag("Shard")) // Make sure to tag your marker prefabs
            //    {
            //        AdjustOpacity(child.gameObject, sqrDistance);
            //    }
            //}

            // start with a baseline fuel amount the ranges from 0-1 depending on proximity of the skimmer to the trail block
            fuel = chargeAmount * (1 - (sqrDistance / transform.localScale.x)); // x is arbitrary, just need radius of skimmer

            // apply decay
            fuel *= Mathf.Min(0, (skimDecayDuration - (Time.time - skimStartTimes[trailBlock.ownerID])) / skimDecayDuration);

            // apply multiskim multiplier
            fuel += (activelySkimmingBlockCount * MultiSkimMultiplier);
        }

        private void FixedUpdate()
        {
            if (minMatureBlock)
            {
                distanceWeight = ComputeGaussian(minMatureBlockSqrDistance, sqrSweetSpot, sigma);
                directionWeight = Vector3.Dot(ship.transform.forward, minMatureBlock.transform.forward);
                var combinedWeight = distanceWeight * Mathf.Abs(directionWeight);
                PerformBlockStayEffects(combinedWeight);
            }
            minMatureBlock = null;
            minMatureBlockSqrDistance = Mathf.Infinity;
            fuel = 0;
        }

        void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<TrailBlock>(out var trailBlock) && (affectSelf || trailBlock.Team != team))
            {
                if (skimStartTimes.ContainsKey(trailBlock.ownerID))
                {
                    skimStartTimes.Remove(trailBlock.ownerID);
                    activelySkimmingBlockCount--;
                    if (activelySkimmingBlockCount < 1) 
                        PerformBlockStayEffects(0);
                }
            }
        }

        void ScaleTrailAndCamera()
        {
            var normalizedDistance = Mathf.InverseLerp(15f, sqrRadius, minMatureBlockSqrDistance);

            ship.TrailSpawner.SetNormalizedXScale(normalizedDistance);

            if (cameraManager != null && !ship.ShipStatus.AutoPilotEnabled) 
                cameraManager.SetNormalizedCloseCameraDistance(normalizedDistance);
        }

        void ScaleGap(float combinedWeight)
        {
            ship.TrailSpawner.Gap = Mathf.Lerp(initialGap, ship.TrailSpawner.MinimumGap, combinedWeight);
        }

        void AlignAndNudge(float combinedWeight)  // unused but ready to put in the flip phone effect for squirrel
        {
            if (!minMatureBlock)
                return;
            
            //align
            ship.ShipTransformer.GentleSpinShip(minMatureBlock.transform.forward * directionWeight, ship.transform.up, combinedWeight * Time.deltaTime);

            //nudge
            if (minMatureBlockSqrDistance < sqrSweetSpot)
                ship.ShipTransformer.ModifyVelocity(-(minMatureBlock.transform.position - transform.position).normalized * distanceWeight * Mathf.Abs(directionWeight), Time.deltaTime * 10);
            else 
                ship.ShipTransformer.ModifyVelocity((minMatureBlock.transform.position - transform.position).normalized * distanceWeight * Mathf.Abs(directionWeight), Time.deltaTime * 10);
        }

        void VizualizeDistance(float combinedWeight)
        {
            ship.ResourceSystem.ChangeResourceAmount(resourceIndex, - ship.ResourceSystem.Resources[resourceIndex].CurrentAmount);
            ship.ResourceSystem.ChangeResourceAmount(resourceIndex, combinedWeight);
        }

        void ScalePitchAndYaw(float combinedWeight)
        {
            //ship.ShipTransformer.PitchScaler = ship.ShipTransformer.YawScaler = 150 * (1 + (.5f*combinedWeight));
            ship.ShipTransformer.PitchScaler = ship.ShipTransformer.YawScaler = 150 + (75 * combinedWeight);
        }

        void ScaleHapticWithDistance(float combinedWeight)
        {
            var hapticScale = combinedWeight / 3;
            if (!ship.ShipStatus.AutoPilotEnabled)
                HapticController.PlayConstant(hapticScale, hapticScale, Time.deltaTime);
        }

        void Boost(float combinedWeight)
        {
            ship.ShipStatus.Boosting = true;
            ship.boostMultiplier = 1 + (2.5f * combinedWeight);
        }

        // Function to compute the Gaussian value at a given x
        public static float ComputeGaussian(float x, float b, float c)
        {
            return Mathf.Exp(-Mathf.Pow(x - b, 2) / (2 * c * c));
        }

        IEnumerator DisplaySkimParticleEffectCoroutine(TrailBlock trailBlock)
        {
            if(trailBlock == null) yield break;
            var particle = Instantiate(trailBlock.ParticleEffect);
            particle.transform.parent = trailBlock.transform;

            int timer = 0;
            float scaledTime;
            do
            {
                var distance = trailBlock.transform.position - transform.position;
                scaledTime = particleDurationAtSpeedOne / ship.GetComponent<ShipStatus>().Speed; // TODO: divide by zero possible
                particle.transform.localScale = new Vector3(1, 1, distance.magnitude);
                particle.transform.SetPositionAndRotation(transform.position, Quaternion.LookRotation(distance, trailBlock.transform.up));
                timer++;

                yield return null;
            } 
            while (timer < scaledTime);

            Destroy(particle);
        }

        private void VisualizeTubeAroundBlock(TrailBlock trailBlock)
        {
            if (trailBlock) StartCoroutine(DrawCircle(trailBlock.transform, sweetSpot)); // radius can be adjusted
        }

        IEnumerator DrawCircle(Transform blockTransform, float radius)
        {
            int segments = 21;
            var anglePerSegment = .314f;   // Restore to this if segments becomes dynamic: var anglePerSegment = Mathf.PI * 2f / segments;
            GameObject[] markers = new GameObject[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * anglePerSegment;
                Vector3 localPosition = (Mathf.Cos(angle) * blockTransform.right + Mathf.Sin(angle) * blockTransform.up) * radius;
                Vector3 worldPosition = blockTransform.position + localPosition;
                GameObject marker = markerContainer.SpawnFromPool("Shard", worldPosition,
                    Quaternion.LookRotation(blockTransform.forward, localPosition));
                markers[i] = marker;
            }
            yield return new WaitForSeconds(2f);
            foreach (GameObject marker in markers)
            {
                markerContainer.ReturnToPool(marker, "Shard");
            }
        }

        private void AdjustOpacity(GameObject marker, float sqrDistance)
        {
            float opacity = .1f - .1f * (sqrDistance / sqrRadius); // Closer blocks are less transparent
            
            marker.GetComponent<MeshRenderer>().material.SetFloat("_Opacity", opacity);
        }
    }
}