using StarWriter.Core.Input;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core
{
    public class Skimmer : MonoBehaviour
    {
        [SerializeField] List<TrailBlockImpactEffects> trailBlockImpactEffects;
        [SerializeField] List<SkimmerStayEffects> skimmerStayEffects;
        [SerializeField] float time = 300f;
        [SerializeField] bool skimVisualFX = true;
        [SerializeField] public Ship ship;
        [SerializeField] public Player Player;
        [SerializeField] float chargeAmount;
        [SerializeField] float MultiSkimMultiplier = 0f;
        [SerializeField] bool notifyNearbyBlockCount;
        [HideInInspector] public Teams team;
        ResourceSystem resourceSystem;

        Dictionary<string, float> skimStartTimes = new Dictionary<string, float>();
        CameraManager cameraManager;

        int activelySkimmingBlockCount = 0;

        public int ActivelySkimmingBlockCount { get { return activelySkimmingBlockCount; } }

        // TODO: move this away from using an event
        public delegate void Skim(string uuid, float amount);
        public static event Skim OnSkim;

        public void Start()
        {
            //PerformSkimmerStartEffects();
            cameraManager = CameraManager.Instance;
            resourceSystem = ship.GetComponent<ResourceSystem>();
        }


        //public void PerformSkimmerStartEffects()
        //{
        //    foreach (TrailBlockImpactEffects effect in trailBlockImpactEffects)
        //    {
        //        switch (effect)
        //        {
        //            case TrailBlockImpactEffects.PlayHaptics:
        //                break;
        //            case TrailBlockImpactEffects.DeactivateTrailBlock:
        //                break;
        //            case TrailBlockImpactEffects.Steal:
        //                break;
        //        }
        //    }
        //}


        //Maja added this to try and enable shark skimmer smashing
        void PerformSkimmerImpactEffects(TrailBlockProperties trailBlockProperties)
        {
            foreach (TrailBlockImpactEffects effect in trailBlockImpactEffects)
            {
                switch (effect)
                {
                    case TrailBlockImpactEffects.PlayHaptics:
                        HapticController.PlayBlockCollisionHaptics();
                        break;
                    case TrailBlockImpactEffects.DeactivateTrailBlock:
                        trailBlockProperties.trailBlock.Explode(ship.transform.forward * ship.GetComponent<ShipData>().Speed, team, Player.PlayerName);
                        break;
                    case TrailBlockImpactEffects.Steal:
                        trailBlockProperties.trailBlock.Steal(Player.PlayerName, team);
                        break;
                    case TrailBlockImpactEffects.ChangeBoost:
                        resourceSystem.ChangeBoostAmount(Player.PlayerUUID, chargeAmount + (activelySkimmingBlockCount * MultiSkimMultiplier));
                        break;
                    case TrailBlockImpactEffects.ChangeAmmo:
                        resourceSystem.ChangeAmmoAmount(Player.PlayerUUID, chargeAmount + (activelySkimmingBlockCount * MultiSkimMultiplier));
                        break;
                        // This is actually redundant with Skimmer's built in "Fuel Amount" variable
                        //case TrailBlockImpactEffects.ChangeFuel:
                        //FuelSystem.ChangeFuelAmount(Player.PlayerUUID, ship.blockFuelChange);
                        //break;
                }
            }
        }

        void PerformSkimmerStayEffects(TrailBlockProperties trailBlockProperties, float chargeAmount)
        {
            foreach (SkimmerStayEffects effect in skimmerStayEffects)
            {
                switch (effect)
                {
                    case SkimmerStayEffects.ChangeBoost:
                        resourceSystem.ChangeBoostAmount(Player.PlayerUUID, chargeAmount);
                        break;
                    case SkimmerStayEffects.ChangeAmmo:
                        resourceSystem.ChangeAmmoAmount(Player.PlayerUUID, chargeAmount);
                        break;
                }
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<ShipGeometry>(out var shipGeometry))
            {
                //Debug.Log($"skimmer ship geometry: {shipGeometry}");
            }
            if (other.TryGetComponent<TrailBlock>(out var trailBlock))
            {
                StartSkim(trailBlock);
                PerformSkimmerImpactEffects(trailBlock.TrailBlockProperties);
            }      
        }

        void StartSkim(TrailBlock trailBlock)
        {
            activelySkimmingBlockCount++;
            if (skimVisualFX) StartCoroutine(DisplaySkimParticleEffectCoroutine(trailBlock));

            if (!skimStartTimes.ContainsKey(trailBlock.ID))
                skimStartTimes.Add(trailBlock.ID, Time.time);

            //OnSkim?.Invoke(ship.Player.PlayerUUID, fuelAmount + (activelySkimmingBlockCount * MultiSkimMultiplier));

            if (notifyNearbyBlockCount)
                NotifyNearbyBlockCount();
        }


        void OnTriggerStay(Collider other)
        {
            float skimDecayDuration = 1;

            if (other.TryGetComponent<TrailBlock>(out var trailBlock))
            {
                if(!skimStartTimes.ContainsKey(trailBlock.ID))   // Occasionally, seeing a KeyNotFoundException, so maybe we miss the OnTriggerEnter event (note: always seems to be for AOE blocks)
                    StartSkim(trailBlock);

                // start with a baseline fuel amount the ranges from 0-1 depending on proximity of the skimmer to the trail block
                var fuel = chargeAmount * (1 - (Vector3.Magnitude(transform.position - other.transform.position) / transform.localScale.x));

                // apply decay
                fuel *= Mathf.Min(0, (skimDecayDuration - (Time.time - skimStartTimes[trailBlock.ID])) / skimDecayDuration);

                // apply multiskim multiplier
                fuel += (activelySkimmingBlockCount * MultiSkimMultiplier);

                // grant the fuel
                PerformSkimmerStayEffects(trailBlock.TrailBlockProperties, fuel);
                //OnSkim?.Invoke(ship.Player.PlayerUUID, fuel);
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<TrailBlock>(out var trailBlock))
            {
                skimStartTimes.Remove(trailBlock.ID);
                activelySkimmingBlockCount--;

                if (notifyNearbyBlockCount)
                    NotifyNearbyBlockCount();
            }
        }

        void NotifyNearbyBlockCount()
        {
            ship.TrailSpawner.SetNearbyBlockCount(ActivelySkimmingBlockCount);
            cameraManager.SetCloseCameraDistance(Mathf.Min((cameraManager.FarCamDistance) * (1 - (float)activelySkimmingBlockCount / ship.TrailSpawner.MaxNearbyBlockCount), cameraManager.CloseCamDistance));
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
                scaledTime = time / ship.GetComponent<ShipData>().Speed;
                particle.transform.localScale = new Vector3(1, 1, distance.magnitude);
                particle.transform.rotation = Quaternion.LookRotation(distance, trailBlock.transform.up);
                particle.transform.position = transform.position;
                timer++;

                yield return null;
            } while (timer < scaledTime);

            Destroy(particle);
        }

    }
}