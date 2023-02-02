using StarWriter.Core.Input;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core
{
    public class Skimmer : MonoBehaviour
    {
        [SerializeField] List<TrailBlockImpactEffects> trailBlockImpactEffects;
        [SerializeField] float time = 300f;
        [SerializeField] bool skimVisualFX = true;
        [SerializeField] public Ship ship;
        [SerializeField] public Player Player;
        [SerializeField] float fuelAmount;
        [SerializeField] float MultiSkimMultiplier = 0f;
        [SerializeField] bool NotifyNearbyBlockCount;
        [HideInInspector] public Teams team;
 
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
        public void PerformSkimmerImpactEffects(TrailBlockProperties trailBlockProperties)
        {
            foreach (TrailBlockImpactEffects effect in trailBlockImpactEffects)
            {
                switch (effect)
                {
                    case TrailBlockImpactEffects.PlayHaptics:
                        HapticController.PlayBlockCollisionHaptics();
                        break;
                    case TrailBlockImpactEffects.DeactivateTrailBlock:
                        trailBlockProperties.trail.Explode(ship.transform.forward * ship.GetComponent<ShipData>().Speed, team, Player.PlayerName);
                        break;
                    case TrailBlockImpactEffects.Steal:
                        trailBlockProperties.trail.Steal(Player.PlayerName, team);
                        break;
                    // This is actually redundant with Skimmer's built in "Fuel Amount" variable
                    //case TrailBlockImpactEffects.ChangeFuel:
                        //FuelSystem.ChangeFuelAmount(Player.PlayerUUID, ship.blockFuelChange);
                        //break;
                }
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<Trail>(out var trail))
            {
                activelySkimmingBlockCount++;
                if (skimVisualFX) StartCoroutine(DisplaySkimParticleEffectCoroutine(trail));

                if (!skimStartTimes.ContainsKey(trail.ID))
                    skimStartTimes.Add(trail.ID, Time.time);

                OnSkim?.Invoke(ship.Player.PlayerUUID, fuelAmount + (activelySkimmingBlockCount * MultiSkimMultiplier));

                if (NotifyNearbyBlockCount)
                    ship.TrailSpawner.SetNearbyBlockCount(ActivelySkimmingBlockCount);
                    //cameraManager.SetCloseCameraDistance(Mathf.Min(cameraManager.closeCamDistance * (10 - activelySkimmingBlockCount), cameraManager.closeCamDistance));
            }
        }

        void OnTriggerStay(Collider other)
        {
            float skimDecayDuration = 1;

            if (other.TryGetComponent<Trail>(out var trail))
            {
                // start with a baseline fuel amount the ranges from 0-1 depending on proximity of the skimmer to the trail block
                var fuel = fuelAmount * (1 - (Vector3.Magnitude(transform.position - other.transform.position) / transform.localScale.x));

                // apply decay
                fuel *= Mathf.Min(0, (skimDecayDuration - (Time.time - skimStartTimes[trail.ID])) / skimDecayDuration);

                // apply multiskim multiplier
                fuel += (activelySkimmingBlockCount * MultiSkimMultiplier);

                // grant the fuel
                OnSkim?.Invoke(ship.Player.PlayerUUID, fuel);
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<Trail>(out var trail))
            {
                skimStartTimes.Remove(trail.ID);
                activelySkimmingBlockCount--;

                if (NotifyNearbyBlockCount)
                    ship.TrailSpawner.SetNearbyBlockCount(ActivelySkimmingBlockCount);
                    //cameraManager.SetCloseCameraDistance(Mathf.Min(cameraManager.closeCamDistance*(10-activelySkimmingBlockCount), cameraManager.closeCamDistance));
            }
        }


        IEnumerator DisplaySkimParticleEffectCoroutine(Trail trail)
        {
            var particle = Instantiate(trail.ParticleEffect);
            particle.transform.parent = trail.transform;

            int timer = 0;
            float scaledTime;
            do
            {
                var distance = trail.transform.position - transform.position;
                scaledTime = time / ship.GetComponent<ShipData>().Speed;
                particle.transform.localScale = new Vector3(1, 1, distance.magnitude);
                particle.transform.rotation = Quaternion.LookRotation(distance, trail.transform.up);
                particle.transform.position = transform.position;
                timer++;

                yield return null;
            } while (timer < scaledTime);

            Destroy(particle);
        }
    }
}