using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;

namespace CosmicShore.Client
{
    /// <summary>Frame input handed from the windowing layer to the sim (engine-agnostic).</summary>
    public sealed class PilotInput
    {
        public float Pitch;   // -1..1 (up/down)
        public float Yaw;     // -1..1 (left/right)
        public bool Boost;
        public bool Restart;
    }

    /// <summary>
    /// Deterministic SkimRace course: a seeded sum-of-sines centerline with crystals
    /// strung along it (HexRace-style: same seed → identical track on every machine).
    /// </summary>
    public sealed class SkimTrack
    {
        public readonly List<Vector3> Crystals = new();
        public readonly List<Vector3> Centerline = new();
        readonly float _a1, _a2, _f1, _f2, _p1, _p2;

        public const float Length = 1400f;
        public const float CrystalSpacing = 28f;

        public SkimTrack(int seed)
        {
            var rng = new Random(seed);
            _a1 = 18f + (float)rng.NextDouble() * 14f;
            _a2 = 10f + (float)rng.NextDouble() * 10f;
            _f1 = 0.008f + (float)rng.NextDouble() * 0.006f;
            _f2 = 0.013f + (float)rng.NextDouble() * 0.008f;
            _p1 = (float)rng.NextDouble() * 6.28f;
            _p2 = (float)rng.NextDouble() * 6.28f;

            for (float z = 0f; z <= Length; z += 4f)
                Centerline.Add(PointAt(z));
            for (float z = CrystalSpacing; z <= Length; z += CrystalSpacing)
            {
                // slight seeded lateral scatter so the racing line weaves
                var offset = new Vector3((float)rng.NextDouble() * 6f - 3f, (float)rng.NextDouble() * 6f - 3f, 0f);
                Crystals.Add(PointAt(z) + offset);
            }
        }

        public Vector3 PointAt(float z) => new(
            Mathf.Sin(z * _f1 + _p1) * _a1,
            Mathf.Sin(z * _f2 + _p2) * _a2,
            z);
    }

    public enum RaceState { Countdown = 0, Racing = 1, Finished = 2 }

    /// <summary>
    /// The race brain — a genuine engine MonoBehaviour: flight model in Update,
    /// boost resource through the ported ResourceSystem, scoring through the ported
    /// RoundStats, crystal target + finish per HexRace rules (time is the score).
    /// </summary>
    public sealed class SkimRaceController : MonoBehaviour
    {
        public PilotInput Input;
        public SkimTrack Track;
        public ResourceSystem Resources;
        public RoundStats Stats;

        public RaceState State = RaceState.Countdown;
        public float Countdown = 3f;
        public float ElapsedTime;
        public int CrystalTarget;
        public readonly HashSet<int> Collected = new();
        public float Speed;
        public float BankAngle;
        public event Action<int, Vector3> OnCrystalCollected;

        const float BaseSpeed = 34f;
        const float BoostSpeed = 62f;
        const float TurnRate = 75f;       // deg/sec
        const float CollectRadius = 4.2f; // generous skim radius
        const float BoostDrainPerSecond = 0.45f;

        float _pitchDeg, _yawDeg;

        void Update()
        {
            if (Input is null || Track is null) return;
            if (Input.Restart) { ResetRace(); Input.Restart = false; }

            switch (State)
            {
                case RaceState.Countdown:
                    Countdown -= Time.deltaTime;
                    if (Countdown <= 0f) State = RaceState.Racing;
                    return;
                case RaceState.Finished:
                    // victory drift: ease forward, level out
                    transform.position += transform.forward * (Speed * Time.deltaTime);
                    Speed = Mathf.Lerp(Speed, BaseSpeed * 0.4f, Time.deltaTime);
                    return;
            }

            ElapsedTime += Time.deltaTime;

            // Boost: drains the ported boost resource; regenerates via ResourceSystem's
            // own gain coroutine when released.
            bool boosting = Input.Boost && Resources.Resources[0].CurrentAmount > 0.02f;
            if (boosting)
                Resources.ChangeResourceAmount(0, -BoostDrainPerSecond * Time.deltaTime);
            float targetSpeed = boosting ? BoostSpeed : BaseSpeed;
            Speed = Mathf.Lerp(Speed, targetSpeed, Time.deltaTime * 3f);

            // Flight: rate-limited pitch/yaw, banked visual roll.
            _yawDeg += Input.Yaw * TurnRate * Time.deltaTime;
            // +X Euler is nose-down, so positive (stick-up) pitch input SUBTRACTS
            _pitchDeg = Mathf.Clamp(_pitchDeg - Input.Pitch * TurnRate * Time.deltaTime, -70f, 70f);
            BankAngle = Mathf.Lerp(BankAngle, -Input.Yaw * 38f, Time.deltaTime * 6f);
            transform.rotation = Quaternion.Euler(_pitchDeg, _yawDeg, 0f);
            transform.position += transform.forward * (Speed * Time.deltaTime);

            // Skim collection.
            for (int i = 0; i < Track.Crystals.Count; i++)
            {
                if (Collected.Contains(i)) continue;
                if ((Track.Crystals[i] - transform.position).sqrMagnitude > CollectRadius * CollectRadius) continue;

                Collected.Add(i);
                Stats.CrystalsCollected++;
                Stats.OmniCrystalsCollected++;
                Resources.ChangeResourceAmount(0, 0.15f);          // skim refund
                Resources.IncrementLevel(Element.Charge);          // elemental progression
                OnCrystalCollected?.Invoke(i, Track.Crystals[i]);

                if (Stats.CrystalsCollected >= CrystalTarget)
                {
                    State = RaceState.Finished;
                    Stats.Score = ElapsedTime;                     // HexRace golf scoring
                }
            }
        }

        public void ResetRace()
        {
            ((IRoundStats)Stats).Cleanup();
            Collected.Clear();
            Resources.ResetResource(0);
            transform.position = new Vector3(Track.PointAt(0f).x, Track.PointAt(0f).y, -30f);
            transform.rotation = Quaternion.identity;
            _pitchDeg = 0f; _yawDeg = 0f; BankAngle = 0f;
            Speed = 0f;
            ElapsedTime = 0f;
            Countdown = 3f;
            State = RaceState.Countdown;
        }
    }

    /// <summary>Builds the engine-side scene (loop, vessel object, systems) for a race.</summary>
    public static class SkimRaceFactory
    {
        public static (GameLoop loop, SkimRaceController race) Create(int seed, int crystalTarget, PilotInput input)
        {
            var loop = new GameLoop("SkimRace");
            var track = new SkimTrack(seed);

            var vessel = new GameObject("SkimVessel");
            vessel.SetActive(false); // configure before Awake

            var resources = vessel.AddComponent<ResourceSystem>();
            resources.Resources = new List<Resource>
            {
                new() { Name = "boost", resourceGainRate = 0.12f },
            };
            resources.InitializeElementLevels(new ResourceCollection(0f, 0f, 0f, 0f));

            var race = vessel.AddComponent<SkimRaceController>();
            race.Input = input;
            race.Track = track;
            race.Resources = resources;
            race.Stats = new RoundStats { Name = "Pilot", Domain = Domains.Jade };
            race.CrystalTarget = crystalTarget > 0
                ? Math.Min(crystalTarget, track.Crystals.Count)
                : track.Crystals.Count;

            vessel.SetActive(true);
            race.ResetRace();
            return (loop, race);
        }
    }
}
