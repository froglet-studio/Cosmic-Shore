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
        [SerializeField] int markerDistance = 70;

        [SerializeField] int resourceIndex = 0;

        float minMatureBlockSqrDistance = Mathf.Infinity;
        TrailBlock minMatureBlock;
        List<TrailBlock> nextBlocks = new();
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

            Player = Ship.ShipStatus.Player;
            Team = Ship.ShipStatus.Team;
            BindElementalFloats(Ship);
            resourceSystem = Ship.ShipStatus.ResourceSystem;
            if (visible)
                GetComponent<MeshRenderer>().material = new Material(Ship.ShipStatus.SkimmerMaterial);

            initialGap = Ship.ShipStatus.TrailSpawner.Gap;

            if (markerContainer) markerContainer.transform.parent = ship.ShipStatus.Player.Transform;
        }

        void PerformBlockImpactEffects(TrailBlockProperties trailBlockProperties)
        {
            foreach (TrailBlockImpactEffects effect in blockImpactEffects)
            {
                switch (effect)
                {
                    case TrailBlockImpactEffects.PlayHaptics:
                        if (!Ship.ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.BlockCollision);
                        break;
                    case TrailBlockImpactEffects.DeactivateTrailBlock:
                        trailBlockProperties.trailBlock.Damage(Ship.ShipStatus.Course * Ship.ShipStatus.Speed * Ship.ShipStatus.GetInertia, Team, Player.PlayerName);
                        break;
                    case TrailBlockImpactEffects.Steal:
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
                }
            }
        }

        void PerformShipImpactEffects(ShipGeometry shipGeometry)
        {
            if (Ship == null)
                return;

            if (StatsManager.Instance != null)
                StatsManager.Instance.SkimmerShipCollision(Ship, shipGeometry.Ship);
            foreach (ShipImpactEffects effect in shipImpactEffects)
            {
                switch (effect)
                {
                    case ShipImpactEffects.TrailSpawnerCooldown:
                        shipGeometry.Ship.ShipStatus.TrailSpawner.PauseTrailSpawner();
                        shipGeometry.Ship.ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(10);
                        break;
                    case ShipImpactEffects.PlayHaptics:
                        if (!Ship.ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.ShipCollision);//.PlayShipCollisionHaptics();
                        break;
                    case ShipImpactEffects.AreaOfEffectExplosion:
                        if (onCoolDown || shipGeometry.Ship.ShipStatus.Team == Team) break;

                        var AOEExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
                        AOEExplosion.Detonate(Ship);
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
                MakeBoosters(trailBlock);
            }
        }

        void VacuumCrystal(Crystal crystal)
        {
            crystal.transform.position = Vector3.MoveTowards(crystal.transform.position, transform.position, vaccumAmount * Time.deltaTime / crystal.transform.lossyScale.x);
        }

        List<TrailBlock> FindNextBlocks(TrailBlock minMatureBlock, float distance = 100f)
        {
            if (minMatureBlock.Trail == null) return new List<TrailBlock> { minMatureBlock };
            var minIndex = minMatureBlock.TrailBlockProperties.Index;
            List<TrailBlock> nextBlocks;
            if (directionWeight < 0 && minIndex > 0)
                nextBlocks = minMatureBlock.Trail.LookAhead(minIndex, 0, TrailFollowerDirection.Backward, distance);
            else if (directionWeight > 0 && minIndex < minMatureBlock.Trail.TrailList.Count - 1)
                nextBlocks = minMatureBlock.Trail.LookAhead(minIndex, 0, TrailFollowerDirection.Forward, distance);
            else
                nextBlocks = minMatureBlock.Trail.LookAhead(minIndex, 0, TrailFollowerDirection.Forward, distance);
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
                    bool shouldUpdateMinMatureBlock = true;

                    // Check reference equality directly
                    //if (trailBlock != null && nextBlocks.Count > 0)
                    //{
                    //    foreach (var block in tempNextBlocks)
                    //    {
                    //        if (block.TrailBlockProperties.Index == trailBlock.TrailBlockProperties.Index)
                    //        {
                    //            shouldUpdateMinMatureBlock = false;
                    //            break;
                    //        }
                    //    }
                    //}

                    minMatureBlock = trailBlock;
                    nextBlocks = FindNextBlocks(minMatureBlock, 20);

                    //if (shouldUpdateMinMatureBlock)
                    //{
                    //    tempNextBlocks = nextBlocks;
                    //    if (markerContainer) VisualizeTubeAroundBlock(nextBlocks[^1]);
                    //}
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

        float boosterTimer = 0;

        void MakeBoosters(TrailBlock trailBlock)
        {
            var markerCount = 5;
            var cooldown = 4f;
            if (Time.time - boosterTimer < cooldown) return;
            boosterTimer = Time.time;
            var nextBlocks = FindNextBlocks(trailBlock, markerCount*markerDistance);
            if (markerContainer)
            {
                // Handle the case where there are no blocks or only 1 marker needed
                if (nextBlocks.Count == 0 || markerCount <= 0)
                    return;

                // Always visualize the last element
                VisualizeTubeAroundBlock(nextBlocks[nextBlocks.Count - 1]);

                // If we only need one marker, we're done
                if (markerCount == 1)
                    return;

                // Calculate the step size for even spacing between markers
                float stepSize = (float)(nextBlocks.Count - 1) / (markerCount - 1);

                // Visualize the remaining markers with even spacing
                for (int i = 1; i < markerCount - 1; i++)
                {
                    // Calculate the index, rounding to nearest integer
                    int index = nextBlocks.Count - 1 - (int)Mathf.Round(i * stepSize);

                    // Ensure index is within valid range
                    if (index >= 0 && index < nextBlocks.Count)
                    {
                        VisualizeTubeAroundBlock(nextBlocks[index]);
                    }
                }
            }
        }

        void ScaleTrailAndCamera()
        {
            var normalizedDistance = Mathf.InverseLerp(15f, sqrRadius, minMatureBlockSqrDistance);
            Ship.ShipStatus.TrailSpawner.SetNormalizedXScale(normalizedDistance);

            if (cameraManager != null && !Ship.ShipStatus.AutoPilotEnabled) 
                cameraManager.SetNormalizedCloseCameraDistance(normalizedDistance);
        }

        void ScaleGap(float combinedWeight)
        {
            Ship.ShipStatus.TrailSpawner.Gap = Mathf.Lerp(initialGap, Ship.ShipStatus.TrailSpawner.MinimumGap, combinedWeight);
        }

        /*
Change: Instead of computing the misalignment between the ship’s up and the radial from the tube center (which produced a repulsive feel), 
we now compute the desired radial by projecting the ship’s up vector onto the plane perpendicular to the tube’s forward. 
This lets the player's roll input (the ship's up vector) determine the lateral target. 
Then we compute the tangent direction along the tube’s cross–section and slerp the rotation toward that, 
while keeping the existing subtle velocity nudging.
*/
        /*
Change: Instead of using a misalignment-based repulsive approach, this version computes a desired tube position based on the ship’s current up vector. 
That is, the desired tube position is defined as the tube center plus (ship.Transform.up * sweetSpot). 
Then, the target forward is the direction from the ship’s current position to that desired position.
This approach, combined with the existing subtle velocity nudging, attracts the ship toward the tube’s surface without imposing lateral displacement.
*/
        // Change: Instead of using the ship’s up versus radial misalignment (which repelled the ship), this version computes an error vector (U - radial) and nudges the forward direction accordingly—so the ship’s forward is adjusted toward a path that will bring it to the tube’s surface while preserving the player's roll.
        void AlignAndNudge(float combinedWeight)
        {
            if (!minMatureBlock || nextBlocks.Count < 5)
                return;

            // Calculate distances and directions
            var nextBlockDistance = (nextBlocks[0].transform.position - transform.position);
            var normNextBlockDistance = nextBlockDistance.normalized;

            // Apply velocity nudging to maintain sweet spot distance
            if (minMatureBlockSqrDistance < sqrSweetSpot - 3)
            {
                Ship.ShipStatus.ShipTransformer.ModifyVelocity(-normNextBlockDistance * 4f, Time.deltaTime * 2f);
            }
            else if (minMatureBlockSqrDistance > sqrSweetSpot + 3)
            {
                Ship.ShipStatus.ShipTransformer.ModifyVelocity(normNextBlockDistance * 4f, Time.deltaTime * 2f);
            }

            // Get the tube's forward direction from a block further ahead
            Vector3 tubeForward = nextBlocks[4].transform.forward;

            // Calculate the radial direction, properly considering the tube's axis
            Vector3 fromTube = transform.position - nextBlocks[0].transform.position;
            Vector3 radial = Vector3.ProjectOnPlane(fromTube, tubeForward).normalized;

            // Determine if we're inside or outside the tube based on current up vector
            bool isInside = Vector3.Dot(normNextBlockDistance, transform.up) > 0;
            Vector3 targetUp = isInside ? normNextBlockDistance : -normNextBlockDistance;

            // Calculate target forward direction
            Vector3 targetForward = Vector3.Lerp(
                transform.forward,
                directionWeight * tubeForward,
                combinedWeight
            );

            // Apply the gentle spin with speed-based interpolation
            float alignSpeed = Ship.ShipStatus.Speed * Time.deltaTime / 15f;
            Ship.ShipStatus.ShipTransformer.GentleSpinShip(targetForward, targetUp, alignSpeed);
        }

        void VizualizeDistance(float combinedWeight)
        {
            Ship.ShipStatus.ResourceSystem.ChangeResourceAmount(resourceIndex, - Ship.ShipStatus.ResourceSystem.Resources[resourceIndex].CurrentAmount);
            Ship.ShipStatus.ResourceSystem.ChangeResourceAmount(resourceIndex, combinedWeight);
        }

        void ScalePitchAndYaw(float combinedWeight)
        {
            //ship.ShipTransformer.PitchScaler = ship.ShipTransformer.YawScaler = 150 * (1 + (.5f*combinedWeight));
            Ship.ShipStatus.ShipTransformer.PitchScaler = Ship.ShipStatus.ShipTransformer.YawScaler = 150 + (120 * combinedWeight);
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
            Ship.ShipStatus.BoostMultiplier = 1 + (2.5f * combinedWeight);
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
            int segments = Mathf.Min((int)(Mathf.PI * 2f * radius / blockTransform.localScale.x),360);// 8;
            var anglePerSegment = blockTransform.localScale.x / (radius); //Mathf.PI * 2f / segments; //blockTransform.localScale.x / (2 * radius)  // Restore to this if segments becomes dynamic: var anglePerSegment = Mathf.PI * 2f / segments;
            List<GameObject> markers = new();
            for (int i = -segments/2; i < segments/2; i++)
            {
                float angle = i * anglePerSegment;
                Vector3 localPosition = (Mathf.Cos(angle + (Mathf.PI / 2)) * blockTransform.right + Mathf.Sin(angle + (Mathf.PI / 2)) * blockTransform.up) * radius;
                Vector3 worldPosition = blockTransform.position + localPosition;
                GameObject marker = markerContainer.SpawnFromPool("Shard", worldPosition,
                    Quaternion.LookRotation(directionWeight * blockTransform.forward, localPosition));
                if (shardPositions.Contains(marker.transform.position))
                {
                    markerContainer.ReturnToPool(marker, "Shard");
                    continue;
                }
                shardPositions.Add(marker.transform.position);
                marker.transform.localScale = blockTransform.localScale/2;
                marker.GetComponentInChildren<NudgeShard>().Prisms = FindNextBlocks(blockTransform.GetComponent<TrailBlock>(), markerDistance * Ship.ShipStatus.ResourceSystem.Resources[0].CurrentAmount);
                markers.Add(marker);
            }
            yield return new WaitForSeconds(8f);

            foreach (GameObject marker in markers)
            {
                if (marker == null) continue;

                shardPositions.Remove(marker.transform.position);
                markerContainer.ReturnToPool(marker, "Shard");
            }
        }
    }
}