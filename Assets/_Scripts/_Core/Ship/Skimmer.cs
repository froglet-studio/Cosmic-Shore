using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core
{
    public class Skimmer : ElementalShipComponent
    {
        [SerializeField] List<TrailBlockImpactEffects> blockImpactEffects;
        [SerializeField] List<SkimmerStayEffects> blockStayEffects;
        [SerializeField] List<ShipImpactEffects> shipImpactEffects;
        [SerializeField] float particleDurationAtSpeedOne = 300f;
        [SerializeField] bool skimVisualFX = true;
        [SerializeField] bool affectSelf = true;
        [SerializeField] float chargeAmount;
        [SerializeField] float MultiSkimMultiplier = 0f;
        [SerializeField] bool notifyNearbyBlockCount;
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
                        if (!ship.ShipStatus.AutoPilotEnabled) HapticController.PlayBlockCollisionHaptics();
                        break;
                    case TrailBlockImpactEffects.DeactivateTrailBlock:
                        trailBlockProperties.trailBlock.Explode(ship.transform.forward * ship.GetComponent<ShipStatus>().Speed, team, Player.PlayerName);
                        break;
                    case TrailBlockImpactEffects.Steal:
                        //Debug.Log($"steal: playername {Player.PlayerName} team: {team}");
                        trailBlockProperties.trailBlock.Steal(Player.PlayerName, team);
                        break;
                    case TrailBlockImpactEffects.ChangeBoost:
                        resourceSystem.ChangeBoostAmount((chargeAmount * trailBlockProperties.volume) + (activelySkimmingBlockCount * MultiSkimMultiplier));
                        break;
                    case TrailBlockImpactEffects.ChangeAmmo:
                        resourceSystem.ChangeAmmoAmount(chargeAmount + (activelySkimmingBlockCount * MultiSkimMultiplier));
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
            foreach (ShipImpactEffects effect in shipImpactEffects)
            {
                switch (effect)
                {
                    case ShipImpactEffects.TrailSpawnerCooldown:
                        shipGeometry.Ship.TrailSpawner.PauseTrailSpawner();
                        shipGeometry.Ship.TrailSpawner.RestartTrailSpawnerAfterDelay(10);
                        break;
                    case ShipImpactEffects.PlayHaptics:
                        if (!ship.ShipStatus.AutoPilotEnabled) HapticController.PlayShipCollisionHaptics();
                        break;
                }
            }
        }

        void PerformBlockStayEffects(float chargeAmount)
        {
            foreach (SkimmerStayEffects effect in blockStayEffects)
            {
                switch (effect)
                {
                    case SkimmerStayEffects.ChangeBoost:
                        resourceSystem.ChangeBoostAmount(chargeAmount);
                        break;
                    case SkimmerStayEffects.ChangeAmmo:
                        resourceSystem.ChangeAmmoAmount(chargeAmount);
                        break;
                }
            }
        }

        void StartSkim(TrailBlock trailBlock)
        {
            if (skimVisualFX && (affectSelf || trailBlock.Team != team)) 
            {
                StartCoroutine(DisplaySkimParticleEffectCoroutine(trailBlock));
            }

            if (!skimStartTimes.ContainsKey(trailBlock.ID))
            {
                activelySkimmingBlockCount++;
                skimStartTimes.Add(trailBlock.ID, Time.time);
            }
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
        }

        void OnTriggerStay(Collider other)
        {
            float skimDecayDuration = 1;

            if (other.TryGetComponent<TrailBlock>(out var trailBlock) && (affectSelf || trailBlock.Team != team))
            {
                if(!skimStartTimes.ContainsKey(trailBlock.ID))   // Occasionally, seeing a KeyNotFoundException, so maybe we miss the OnTriggerEnter event (note: always seems to be for AOE blocks)
                    StartSkim(trailBlock);

                float distance = Vector3.Distance(transform.position, other.transform.position);

                if (trailBlock.ownerId != ship.Player.PlayerUUID || Time.time - trailBlock.TrailBlockProperties.TimeCreated > 5)
                    minMatureBlockDistance = Mathf.Min(minMatureBlockDistance, distance);

                // start with a baseline fuel amount the ranges from 0-1 depending on proximity of the skimmer to the trail block
                var fuel = chargeAmount * (1 - (distance / transform.localScale.x)); // x is arbitrary, just need radius of skimmer

                // apply decay
                fuel *= Mathf.Min(0, (skimDecayDuration - (Time.time - skimStartTimes[trailBlock.ID])) / skimDecayDuration);

                // apply multiskim multiplier
                fuel += (activelySkimmingBlockCount * MultiSkimMultiplier);

                // grant the fuel
                PerformBlockStayEffects(fuel);
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<TrailBlock>(out var trailBlock) && (affectSelf || trailBlock.Team != team))
            {
                skimStartTimes.Remove(trailBlock.ID);
                activelySkimmingBlockCount--;
            }
        }


        float minMatureBlockDistance = Mathf.Infinity;

        void FixedUpdate()
        {
            DetectTrailDistance();
        }

        void DetectTrailDistance()
        {
            if (!notifyNearbyBlockCount) return;
            
            var normalizedDistance = Mathf.Clamp(Mathf.InverseLerp(15f, transform.localScale.x/2, minMatureBlockDistance), 0,1);

            ship.TrailSpawner.SetNormalizedXScale(normalizedDistance);

            if (cameraManager != null && !ship.ShipStatus.AutoPilotEnabled) 
                cameraManager.SetNormalizedCloseCameraDistance(normalizedDistance);

            minMatureBlockDistance = Mathf.Infinity;
        }
        

        IEnumerator DisplaySkimParticleEffectCoroutine(TrailBlock trailBlock)
        {
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
    }
}