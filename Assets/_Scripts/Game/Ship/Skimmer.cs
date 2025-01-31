using CosmicShore.Environment.FlowField;
using CosmicShore.Game;
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

        public IShip Ship { get; set; }
        public IPlayer Player { get; set; }
        public Teams Team { get; set; }

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
        List<TrailBlock> nextBlocks;
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

            sweetSpot = transform.localScale.x / 4;
            sqrSweetSpot = transform.localScale.x * transform.localScale.x / 16;
            FWHM = sqrSweetSpot; //Full Width at Half Max
            sigma = FWHM / 2.355f;
            sqrRadius = transform.localScale.x * transform.localScale.x / 4;

            if (appliedScale != Scale.Value)
            {
                appliedScale = Scale.Value;
                transform.localScale = Vector3.one * appliedScale;
            }
            
        }

        void Update()
        {
            if (appliedScale != Scale.Value)
            {
                appliedScale = Scale.Value;
                transform.localScale = Vector3.one * appliedScale;
            }
        }

        public void Initialize(IShip ship)
        {
            Ship = ship;
            if (Ship == null)
            {
                Debug.LogError("No ship found!");
                return;
            }

            Player = Ship.Player;
            Team = Ship.Team;
            BindElementalFloats(Ship);
            resourceSystem = Ship.ResourceSystem;
            if (visible)
                GetComponent<MeshRenderer>().material = new Material(Ship.SkimmerMaterial);

            initialGap = Ship.TrailSpawner.Gap;

            if (markerContainer) markerContainer.transform.parent = ship.Player.Transform;
        }

        // TODO: p1- review -- Maja added this to try and enable shark skimmer smashing
        void PerformBlockImpactEffects(TrailBlockProperties trailBlockProperties)
        {
            foreach (TrailBlockImpactEffects effect in blockImpactEffects)
            {
                switch (effect)
                {
                    case TrailBlockImpactEffects.PlayHaptics:
                        if (!Ship.ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.BlockCollision);//.PlayBlockCollisionHaptics();
                        break;
                    case TrailBlockImpactEffects.DeactivateTrailBlock:
                        trailBlockProperties.trailBlock.Damage(Ship.ShipStatus.Course * Ship.ShipStatus.Speed * Ship.GetInertia, Team, Player.PlayerName);
                        break;
                    case TrailBlockImpactEffects.Steal:
                        //Debug.Log($"steal: playername {Player.PlayerName} team: {team}");
                        trailBlockProperties.trailBlock.Steal(Player, Team);
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
                StatsManager.Instance.SkimmerShipCollision(Ship, shipGeometry.Ship);
            foreach (ShipImpactEffects effect in shipImpactEffects)
            {
                switch (effect)
                {
                    case ShipImpactEffects.TrailSpawnerCooldown:
                        shipGeometry.Ship.TrailSpawner.PauseTrailSpawner();
                        shipGeometry.Ship.TrailSpawner.RestartTrailSpawnerAfterDelay(10);
                        break;
                    case ShipImpactEffects.PlayHaptics:
                        if (!Ship.ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.ShipCollision);//.PlayShipCollisionHaptics();
                        break;
                    case ShipImpactEffects.AreaOfEffectExplosion:
                        if (onCoolDown || shipGeometry.Ship.Team == Team) break;

                        var AOEExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
                        AOEExplosion.Ship = Ship;
                        AOEExplosion.SetPositionAndRotation(transform.position, transform.rotation);
                        AOEExplosion.MaxScale = Ship.ShipStatus.Speed - shipGeometry.Ship.ShipStatus.Speed;
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
                        if (Ship.ShipStatus.AlignmentEnabled) VizualizeDistance(combinedWeight);
                        break;
                    case SkimmerStayEffects.ScaleHapticWithDistance:
                        ScaleHapticWithDistance(combinedWeight);
                        break;
                    case SkimmerStayEffects.ScalePitchAndYaw:
                        ScalePitchAndYaw(combinedWeight);
                        break;
                    case SkimmerStayEffects.Align:
                        if (Ship.ShipStatus.AlignmentEnabled) AlignAndNudge(combinedWeight);
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

            if (other.TryGetComponent<TrailBlock>(out var trailBlock) && (affectSelf || trailBlock.Team != Team))
            {
                StartSkim(trailBlock);
                PerformBlockImpactEffects(trailBlock.TrailBlockProperties);
            }
        }

        void VacuumCrystal(Crystal crystal)
        {
            crystal.transform.position = Vector3.MoveTowards(crystal.transform.position, transform.position, vaccumAmount * Time.deltaTime / crystal.transform.lossyScale.x);
        }

        List<TrailBlock> FindNextBlocks(TrailBlock minMatureBlock)
        {
            if (minMatureBlock.Trail == null) return new List<TrailBlock> { minMatureBlock };
            var minIndex = minMatureBlock.TrailBlockProperties.Index;
            List<TrailBlock> nextBlocks;
            if (directionWeight < 0 && minIndex > 0)
                nextBlocks = minMatureBlock.Trail.LookAhead(minIndex, 0, TrailFollowerDirection.Backward, 100f);
            else if (directionWeight > 0 && minIndex < minMatureBlock.Trail.TrailList.Count - 1)
                nextBlocks = minMatureBlock.Trail.LookAhead(minIndex, 0, TrailFollowerDirection.Forward, 100f);
            else
                nextBlocks = minMatureBlock.Trail.LookAhead(minIndex, 0, TrailFollowerDirection.Forward, 100f);
            return nextBlocks;
        }

        void OnTriggerStay(Collider other)
        {
            float skimDecayDuration = 1;

            if (other.TryGetComponent<Crystal>(out var crystal) && vacuumCrystal) VacuumCrystal(crystal);

            if (!other.TryGetComponent<TrailBlock>(out var trailBlock)) return;
            
            if (trailBlock.Team == Team && !affectSelf) return;
            
            // Occasionally, seeing a KeyNotFoundException, so maybe we miss the OnTriggerEnter event (note: always seems to be for AOE blocks)
            if (!skimStartTimes.ContainsKey(trailBlock.ownerID))   
                StartSkim(trailBlock);

            float sqrDistance = (transform.position - other.transform.position).sqrMagnitude;
            if (Time.time - trailBlock.TrailBlockProperties.TimeCreated > 4)
            {
                minMatureBlockSqrDistance = Mathf.Min(minMatureBlockSqrDistance, sqrDistance);
      
                if (sqrDistance == minMatureBlockSqrDistance)
                {
                    minMatureBlock = trailBlock;
                    nextBlocks = FindNextBlocks(minMatureBlock);

                    if (markerContainer) VisualizeTubeAroundBlock(nextBlocks[^1]);
                }     
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
                directionWeight = Vector3.Dot(Ship.Transform.forward, minMatureBlock.transform.forward);
                var combinedWeight = distanceWeight * Mathf.Abs(directionWeight);
                PerformBlockStayEffects(combinedWeight);
            }
            minMatureBlock = null;
            minMatureBlockSqrDistance = Mathf.Infinity;
            fuel = 0;
        }

        void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<TrailBlock>(out var trailBlock) && (affectSelf || trailBlock.Team != Team))
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
            Ship.TrailSpawner.SetNormalizedXScale(normalizedDistance);

            if (cameraManager != null && !Ship.ShipStatus.AutoPilotEnabled) 
                cameraManager.SetNormalizedCloseCameraDistance(normalizedDistance);
        }

        void ScaleGap(float combinedWeight)
        {
            Ship.TrailSpawner.Gap = Mathf.Lerp(initialGap, Ship.TrailSpawner.MinimumGap, combinedWeight);
        }

        void AlignAndNudge(float combinedWeight)  // unused but ready to put in the flip phone effect for squirrel
        {
            if (!minMatureBlock)
                return;

            var nextBlockDistance = (nextBlocks[0].transform.position - transform.position);
            var normNextBlockDistance = nextBlockDistance.normalized;

            if (minMatureBlockSqrDistance < sqrSweetSpot - 3)
            {
                Ship.ShipTransformer.ModifyVelocity(-normNextBlockDistance * 4f, Time.deltaTime * 2f);
            }
            else if (minMatureBlockSqrDistance > sqrSweetSpot + 3)
            {
                Ship.ShipTransformer.ModifyVelocity(normNextBlockDistance * 4f, Time.deltaTime * 2f);
            }
            if (nextBlocks.Count < 5) return;
            if (Vector3.Dot(normNextBlockDistance, transform.up) > 0)
                Ship.ShipTransformer.GentleSpinShip(Ship.ShipStatus.Speed * 200 * combinedWeight * directionWeight * nextBlocks[4].transform.forward, normNextBlockDistance, Time.deltaTime);
            else
                Ship.ShipTransformer.GentleSpinShip(Ship.ShipStatus.Speed * 200 * combinedWeight * directionWeight * nextBlocks[4].transform.forward, -normNextBlockDistance, Time.deltaTime);


        }

        void VizualizeDistance(float combinedWeight)
        {
            Ship.ResourceSystem.ChangeResourceAmount(resourceIndex, - Ship.ResourceSystem.Resources[resourceIndex].CurrentAmount);
            Ship.ResourceSystem.ChangeResourceAmount(resourceIndex, combinedWeight);
        }

        void ScalePitchAndYaw(float combinedWeight)
        {
            //ship.ShipTransformer.PitchScaler = ship.ShipTransformer.YawScaler = 150 * (1 + (.5f*combinedWeight));
            Ship.ShipTransformer.PitchScaler = Ship.ShipTransformer.YawScaler = 150 + (120 * combinedWeight);
        }

        void ScaleHapticWithDistance(float combinedWeight)
        {
            var hapticScale = combinedWeight / 3;
            if (!Ship.ShipStatus.AutoPilotEnabled)
                HapticController.PlayConstant(hapticScale, hapticScale, Time.deltaTime);
        }

        void Boost(float combinedWeight)
        {
            Ship.ShipStatus.Boosting = true;
            Ship.BoostMultiplier = 1 + (2.5f * combinedWeight);
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
                scaledTime = particleDurationAtSpeedOne / Ship.ShipStatus.Speed; // TODO: divide by zero possible
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

        HashSet<Vector3> shardPositions = new HashSet<Vector3>();

        IEnumerator DrawCircle(Transform blockTransform, float radius)
        {
            int segments = 5;
            var anglePerSegment = Mathf.PI * 2f / segments;   // Restore to this if segments becomes dynamic: var anglePerSegment = Mathf.PI * 2f / segments;
            List<GameObject> markers = new();
            for (int i = 0; i < segments; i++)
            {
                float angle = i * anglePerSegment;
                Vector3 localPosition = (Mathf.Cos(angle) * blockTransform.right + Mathf.Sin(angle) * blockTransform.up) * radius;
                Vector3 worldPosition = blockTransform.position + localPosition;
                GameObject marker = markerContainer.SpawnFromPool("Shard", worldPosition,
                    Quaternion.LookRotation(blockTransform.forward, localPosition));
                if (shardPositions.Contains(marker.transform.position))
                {
                    markerContainer.ReturnToPool(marker, "Shard");
                    continue;
                }
                shardPositions.Add(marker.transform.position);
                marker.transform.localScale = blockTransform.localScale/2;
                markers.Add(marker);
            }
            yield return new WaitForSeconds(2f);
            foreach (GameObject marker in markers)
            {
                shardPositions.Remove(marker.transform.position);
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