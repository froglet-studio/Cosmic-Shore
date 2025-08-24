using CosmicShore.Game.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    public class Skimmer : ElementalShipComponent
    {
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        List<ScriptableObject> _blockStayEffects;
        [SerializeField] float vaccumAmount = 80f;
        [SerializeField] bool vacuumCrystal = true;

        [SerializeField] float particleDurationAtSpeedOne = 300f;
        [SerializeField] bool affectSelf = true;
        [SerializeField] float chargeAmount;
        [SerializeField] float MultiSkimMultiplier = 0f;
        [SerializeField] bool visible;
        [SerializeField] ElementalFloat Scale = new ElementalFloat(1);

        public IPlayer Player => ShipStatus.Player;

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

        TrailBlock _minMatureBlock;
        List<TrailBlock> _nextBlocks = new();

        public IShipStatus ShipStatus { get; private set; }

        float _minMatureBlockSqrDistance = Mathf.Infinity;
        float _appliedScale;
        float _distanceWeight;
        float _directionWeight;
        float _sweetSpot;
        float _sqrSweetSpot;
        float _FWHM;
        float _sigma;
        float _sqrRadius;
        float _initialGap;
        float _boosterTimer = 0;

        bool _onCoolDown = false;

        void Start()
        {
            cameraManager = CameraManager.Instance;

            _sweetSpot = transform.localScale.x / 4;
            _sqrSweetSpot = transform.localScale.x * transform.localScale.x / 16;
            _FWHM = _sqrSweetSpot; //Full Width at Half Max
            _sigma = _FWHM / 2.355f;
            _sqrRadius = transform.localScale.x * transform.localScale.x / 4;

            if (_appliedScale != Scale.Value)
            {
                _appliedScale = Scale.Value;
                transform.localScale = Vector3.one * _appliedScale;
            }
            
        }

        void Update()
        {
            if (_appliedScale != Scale.Value)
            {
                _appliedScale = Scale.Value;
                transform.localScale = Vector3.one * _appliedScale;
            }
        }

        public void Initialize(IShipStatus shipStatus)
        {
            ShipStatus = shipStatus;
            BindElementalFloats(ShipStatus.Ship);
            
            if (visible)
                GetComponent<MeshRenderer>().material = new Material(ShipStatus.SkimmerMaterial);

            _initialGap = ShipStatus.TrailSpawner.Gap;

            if (markerContainer) markerContainer.transform.parent = ShipStatus.Player.Transform;
        }
        
        IEnumerator CooldownCoroutine(float Period)
        {
            _onCoolDown = true;
            yield return new WaitForSeconds(Period);
            _onCoolDown = false;
        }

        // Deprecated - New Impact Effect System has been implemented. Remove it once all tested.
        void PerformBlockStayEffects(float combinedWeight)
        {
            /*var castedEffects = _blockStayEffects.Cast<IImpactEffect>();
            var impactEffectData = new ImpactEffectData(_shipStatus, null, Vector3.zero);  
            
            ShipHelper.ExecuteImpactEffect(castedEffects, impactEffectData);*/
        }

        void StartSkim(TrailBlock trailBlock)
        {
            if (trailBlock == null) return;

            if (skimStartTimes.ContainsKey(trailBlock.ownerID)) return;
            activelySkimmingBlockCount++;
            skimStartTimes.Add(trailBlock.ownerID, Time.time);
        }

        public void ExecuteImpactOnShip(IShip ship)
        {
            if (StatsManager.Instance != null)
                StatsManager.Instance.ExecuteSkimmerShipCollision(ShipStatus.Ship, ship);
        }

        public void ExecuteImpactOnPrism(TrailBlock trailBlock)
        {
            if (ShipStatus is null || (!affectSelf && trailBlock.Team == ShipStatus.Team)) return;
                
            StartSkim(trailBlock);
            MakeBoosters(trailBlock);
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
            if (_directionWeight < 0 && minIndex > 0)
                nextBlocks = minMatureBlock.Trail.LookAhead(minIndex, 0, TrailFollowerDirection.Backward, distance);
            else if (_directionWeight > 0 && minIndex < minMatureBlock.Trail.TrailList.Count - 1)
                nextBlocks = minMatureBlock.Trail.LookAhead(minIndex, 0, TrailFollowerDirection.Forward, distance);
            else
                nextBlocks = minMatureBlock.Trail.LookAhead(minIndex, 0, TrailFollowerDirection.Forward, distance);
            return nextBlocks;
        }

        void OnTriggerStay(Collider other)
        {
            // TODO : Temp fix. Need better way
            if (ShipStatus is null)
                return;
            
            float skimDecayDuration = 1;

            if (other.TryGetComponent<Crystal>(out var crystal) && vacuumCrystal) VacuumCrystal(crystal);

            if (!other.TryGetComponent<TrailBlock>(out var trailBlock)) return;
            
            if (trailBlock.Team == ShipStatus.Team && !affectSelf) return;
            
            // Occasionally, seeing a KeyNotFoundException, so maybe we miss the OnTriggerEnter event (note: always seems to be for AOE blocks)
            if (!skimStartTimes.ContainsKey(trailBlock.ownerID))   
                StartSkim(trailBlock);

            float sqrDistance = (transform.position - other.transform.position).sqrMagnitude;
            if (Time.time - trailBlock.TrailBlockProperties.TimeCreated > 4)
            {
                _minMatureBlockSqrDistance = Mathf.Min(_minMatureBlockSqrDistance, sqrDistance);

                if (Mathf.Approximately(sqrDistance, _minMatureBlockSqrDistance))
                {
                    _minMatureBlock = trailBlock;
                    _nextBlocks = FindNextBlocks(_minMatureBlock, 20);
                }
            }
        }

        private void FixedUpdate()
        {
            // TODO : Temp fix. Need better way
            if (ShipStatus == null)
                return;

            if (_minMatureBlock)
            {
                _distanceWeight = ComputeGaussian(_minMatureBlockSqrDistance, _sqrSweetSpot, _sigma);
                _directionWeight = Vector3.Dot(ShipStatus.Transform.forward, _minMatureBlock.transform.forward);
                var combinedWeight = _distanceWeight * Mathf.Abs(_directionWeight);
                PerformBlockStayEffects(combinedWeight);
            }
            _minMatureBlock = null;
            _minMatureBlockSqrDistance = Mathf.Infinity;
        }

        void OnTriggerExit(Collider other)
        {
            // TODO : Temp fix. Need better way
            if (ShipStatus is null)
                return;
            
            if (!other.TryGetComponent<TrailBlock>(out var trailBlock) ||
                (!affectSelf && trailBlock.Team == ShipStatus.Team)) 
                return;
            
            if (!skimStartTimes.ContainsKey(trailBlock.ownerID)) 
                return;
            
            skimStartTimes.Remove(trailBlock.ownerID);
            activelySkimmingBlockCount--;
            
            if (activelySkimmingBlockCount < 1) 
                PerformBlockStayEffects(0);
        }


        void MakeBoosters(TrailBlock trailBlock)
        {
            var markerCount = 5;
            var cooldown = 4f;
            if (Time.time - _boosterTimer < cooldown) return;
            _boosterTimer = Time.time;
            var nextBlocks = FindNextBlocks(trailBlock, markerCount*markerDistance);
            if (markerContainer)
            {
                // Handle the case where there are no blocks or only 1 marker needed
                if (nextBlocks.Count == 0)
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
            var normalizedDistance = Mathf.InverseLerp(15f, _sqrRadius, _minMatureBlockSqrDistance);
            ShipStatus.TrailSpawner.SetNormalizedXScale(normalizedDistance);

            // if (cameraManager != null && !_shipStatus.AutoPilotEnabled) 
            //     cameraManager.SetNormalizedCloseCameraDistance(normalizedDistance);
        }

        void ScaleGap(float combinedWeight)
        {
            var trailSpawner = ShipStatus.TrailSpawner;
            trailSpawner.Gap = Mathf.Lerp(_initialGap, trailSpawner.MinimumGap, combinedWeight);
        }

        /*
Change: Instead of computing the misalignment between the ship�s up and the radial from the tube center (which produced a repulsive feel), 
we now compute the desired radial by projecting the ship�s up vector onto the plane perpendicular to the tube�s forward. 
This lets the player's roll input (the ship's up vector) determine the lateral target. 
Then we compute the tangent direction along the tube�s cross�section and slerp the rotation toward that, 
while keeping the existing subtle velocity nudging.
*/
        /*
Change: Instead of using a misalignment-based repulsive approach, this version computes a desired tube position based on the ship�s current up vector. 
That is, the desired tube position is defined as the tube center plus (ship.Transform.up * sweetSpot). 
Then, the target forward is the direction from the ship�s current position to that desired position.
This approach, combined with the existing subtle velocity nudging, attracts the ship toward the tube�s surface without imposing lateral displacement.
*/
        // Change: Instead of using the ship�s up versus radial misalignment (which repelled the ship), this version computes an error vector (U - radial) and nudges the forward direction accordingly�so the ship�s forward is adjusted toward a path that will bring it to the tube�s surface while preserving the player's roll.
        void AlignAndNudge(float combinedWeight)
        {
            if (!_minMatureBlock || _nextBlocks.Count < 5)
                return;

            // Calculate distances and directions
            var nextBlockDistance = (_nextBlocks[0].transform.position - transform.position);
            var normNextBlockDistance = nextBlockDistance.normalized;

            // Apply velocity nudging to maintain sweet spot distance
            if (_minMatureBlockSqrDistance < _sqrSweetSpot - 3)
            {
                ShipStatus.ShipTransformer.ModifyVelocity(-normNextBlockDistance * 4f, Time.deltaTime * 2f);
            }
            else if (_minMatureBlockSqrDistance > _sqrSweetSpot + 3)
            {
                ShipStatus.ShipTransformer.ModifyVelocity(normNextBlockDistance * 4f, Time.deltaTime * 2f);
            }

            // Get the tube's forward direction from a block further ahead
            Vector3 tubeForward = _nextBlocks[4].transform.forward;

            // Calculate the radial direction, properly considering the tube's axis
            Vector3 fromTube = transform.position - _nextBlocks[0].transform.position;
            Vector3 radial = Vector3.ProjectOnPlane(fromTube, tubeForward).normalized;

            // Determine if we're inside or outside the tube based on current up vector
            bool isInside = Vector3.Dot(normNextBlockDistance, transform.up) > 0;
            Vector3 targetUp = isInside ? normNextBlockDistance : -normNextBlockDistance;

            // Calculate target forward direction
            Vector3 targetForward = Vector3.Lerp(
                transform.forward,
                _directionWeight * tubeForward,
                combinedWeight
            );

            // Apply the gentle spin with speed-based interpolation
            float alignSpeed = ShipStatus.Speed * Time.deltaTime / 15f;
            ShipStatus.ShipTransformer.GentleSpinShip(targetForward, targetUp, alignSpeed);
        }

        void VizualizeDistance(float combinedWeight)
        {
            ShipStatus.ResourceSystem.ChangeResourceAmount(resourceIndex, - ShipStatus.ResourceSystem.Resources[resourceIndex].CurrentAmount);
            ShipStatus.ResourceSystem.ChangeResourceAmount(resourceIndex, combinedWeight);
        }

        void ScalePitchAndYaw(float combinedWeight)
        {
            //ship.ShipTransformer.PitchScaler = ship.ShipTransformer.YawScaler = 150 * (1 + (.5f*combinedWeight));
            ShipStatus.ShipTransformer.PitchScaler = ShipStatus.ShipTransformer.YawScaler = 150 + (120 * combinedWeight);
        }

        void ScaleHapticWithDistance(float combinedWeight)
        {
            var hapticScale = combinedWeight / 3;
            if (!ShipStatus.AutoPilotEnabled)
                HapticController.PlayConstant(hapticScale, hapticScale, Time.deltaTime);
        }

        void Boost(float combinedWeight)
        {
            ShipStatus.Boosting = true;
            ShipStatus.BoostMultiplier = 1 + (2.5f * combinedWeight);
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
                scaledTime = particleDurationAtSpeedOne / ShipStatus.Speed; // TODO: divide by zero possible
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
            if (trailBlock) StartCoroutine(DrawCircle(trailBlock.transform, _sweetSpot)); // radius can be adjusted
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
                    Quaternion.LookRotation(_directionWeight * blockTransform.forward, localPosition));
                if (shardPositions.Contains(marker.transform.position))
                {
                    markerContainer.ReturnToPool(marker, "Shard");
                    continue;
                }
                shardPositions.Add(marker.transform.position);
                marker.transform.localScale = blockTransform.localScale/2;
                marker.GetComponentInChildren<NudgeShard>().Prisms = FindNextBlocks(blockTransform.GetComponent<TrailBlock>(), markerDistance * ShipStatus.ResourceSystem.Resources[0].CurrentAmount);
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