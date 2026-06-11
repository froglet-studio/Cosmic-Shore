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

    /// <summary>Contested-crystal ledger + winner flag shared between pilots.</summary>
    public sealed class SharedRaceState
    {
        readonly Dictionary<int, int> _ownerByCrystal = new();
        public int WinnerPilot = -1;

        public bool IsTaken(int crystal) => _ownerByCrystal.ContainsKey(crystal);

        public bool TryClaim(int crystal, int pilot)
        {
            if (_ownerByCrystal.ContainsKey(crystal)) return false;
            _ownerByCrystal[crystal] = pilot;
            return true;
        }

        public void Reset()
        {
            _ownerByCrystal.Clear();
            WinnerPilot = -1;
        }
    }

    /// <summary>
    /// The race brain — a genuine engine MonoBehaviour shared by the player and the AI
    /// rival (identical flight model = fair race; the AI just synthesizes its input).
    /// Boost runs on the ported ResourceSystem, scoring on the ported RoundStats.
    /// Crystals are contested: first pilot to skim one claims it; first to the win
    /// target takes the race.
    /// </summary>
    public sealed class SkimRaceController : MonoBehaviour
    {
        public PilotInput Input;
        public SkimTrack Track;
        public ResourceSystem Resources;
        public RoundStats Stats;
        public SharedRaceState Shared;
        public int PilotId;
        public bool IsAI;
        public SkimRaceController Opponent;
        public float SkillTurnScale = 1f; // AI flies slightly sloppier lines

        public RaceState State = RaceState.Countdown;
        public float Countdown = 3f;
        public float ElapsedTime;
        public int WinTarget;
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
            if (Track is null || Shared is null) return;
            if (Input is { Restart: true }) { Input.Restart = false; }

            switch (State)
            {
                case RaceState.Countdown:
                    Countdown -= Time.deltaTime;
                    if (Countdown <= 0f) State = RaceState.Racing;
                    return;
                case RaceState.Finished:
                    transform.position += transform.forward * (Speed * Time.deltaTime);
                    Speed = Mathf.Lerp(Speed, BaseSpeed * 0.4f, Time.deltaTime);
                    return;
            }

            if (Shared.WinnerPilot >= 0) { State = RaceState.Finished; return; }

            ElapsedTime += Time.deltaTime;

            float pitchInput, yawInput;
            bool boostInput;
            if (IsAI) SynthesizeAIInput(out pitchInput, out yawInput, out boostInput);
            else (pitchInput, yawInput, boostInput) = (Input.Pitch, Input.Yaw, Input.Boost);

            bool boosting = boostInput && Resources.Resources[0].CurrentAmount > 0.02f;
            if (boosting)
                Resources.ChangeResourceAmount(0, -BoostDrainPerSecond * Time.deltaTime);
            float targetSpeed = boosting ? BoostSpeed : BaseSpeed;
            if (IsAI && Opponent != null)
            {
                // rubber-band speed: claw back when trailing, coast when far ahead
                int crystalLead = Stats.CrystalsCollected - Opponent.Stats.CrystalsCollected;
                if (crystalLead <= -2) targetSpeed *= 1.22f;
                else if (crystalLead >= 3) targetSpeed *= 0.85f;
            }
            Speed = Mathf.Lerp(Speed, targetSpeed, Time.deltaTime * 3f);

            float turn = TurnRate * SkillTurnScale;
            _yawDeg += yawInput * turn * Time.deltaTime;
            // +X Euler is nose-down, so positive (stick-up) pitch input SUBTRACTS
            _pitchDeg = Mathf.Clamp(_pitchDeg - pitchInput * turn * Time.deltaTime, -70f, 70f);
            BankAngle = Mathf.Lerp(BankAngle, -yawInput * 38f, Time.deltaTime * 6f);
            transform.rotation = Quaternion.Euler(_pitchDeg, _yawDeg, 0f);
            transform.position += transform.forward * (Speed * Time.deltaTime);

            // Contested skim collection.
            for (int i = 0; i < Track.Crystals.Count; i++)
            {
                if (Shared.IsTaken(i)) continue;
                if ((Track.Crystals[i] - transform.position).sqrMagnitude > CollectRadius * CollectRadius) continue;
                if (!Shared.TryClaim(i, PilotId)) continue;

                Stats.CrystalsCollected++;
                Stats.OmniCrystalsCollected++;
                Resources.ChangeResourceAmount(0, 0.15f);          // skim refund
                Resources.IncrementLevel(Element.Charge);          // elemental progression
                OnCrystalCollected?.Invoke(i, Track.Crystals[i]);

                if (Stats.CrystalsCollected >= WinTarget)
                {
                    Shared.WinnerPilot = PilotId;
                    Stats.Score = ElapsedTime;                     // HexRace golf scoring
                    State = RaceState.Finished;
                }
            }
        }

        /// <summary>Seek the nearest unclaimed crystal ahead; boost when lined up.</summary>
        void SynthesizeAIInput(out float pitch, out float yaw, out bool boost)
        {
            pitch = 0f; yaw = 0f; boost = false;
            // nearest two unclaimed crystals ahead
            Vector3? first = null, second = null;
            float bestZ = float.MaxValue, secondZ = float.MaxValue;
            for (int i = 0; i < Track.Crystals.Count; i++)
            {
                if (Shared.IsTaken(i)) continue;
                var c = Track.Crystals[i];
                float ahead = c.z - transform.position.z;
                if (ahead < -5f) continue;
                if (ahead < bestZ) { secondZ = bestZ; second = first; bestZ = ahead; first = c; }
                else if (ahead < secondZ) { secondZ = ahead; second = c; }
            }
            // Overtake line: if the opponent will beat me to the nearest crystal, concede
            // it and set up on the following one instead of trailing forever.
            Vector3? target = first;
            if (first.HasValue && second.HasValue && Opponent != null)
            {
                float mine = (first.Value - transform.position).magnitude;
                float theirs = (first.Value - Opponent.transform.position).magnitude;
                if (theirs < mine * 0.8f) target = second;
            }
            if (!target.HasValue) return;

            var local = Quaternion.Inverse(transform.rotation) * (target.Value - transform.position);
            yaw = Mathf.Clamp(local.x * 0.25f, -1f, 1f);
            pitch = Mathf.Clamp(local.y * 0.25f, -1f, 1f);
            bool linedUp = Mathf.Abs(yaw) < 0.35f && Mathf.Abs(pitch) < 0.35f;
            // Rubber-band both directions: hold boost for the opening seconds (give the
            // player the early line), spend freely when trailing, coast when leading —
            // otherwise the leader sweeps every contested crystal on the shared line.
            int lead = Opponent != null ? Stats.CrystalsCollected - Opponent.Stats.CrystalsCollected : 0;
            float reserve = lead >= 3 ? 0.95f : lead <= -2 ? 0.12f : 0.4f;
            boost = linedUp && ElapsedTime > 5f && Resources.Resources[0].CurrentAmount > reserve;
        }

        public void ResetPilot(float lateralOffset)
        {
            ((IRoundStats)Stats).Cleanup();
            Resources.ResetResource(0);
            var start = Track.PointAt(0f);
            transform.position = new Vector3(start.x + lateralOffset, start.y, -30f);
            transform.rotation = Quaternion.identity;
            _pitchDeg = 0f; _yawDeg = 0f; BankAngle = 0f;
            Speed = 0f;
            ElapsedTime = 0f;
            Countdown = 3f;
            State = RaceState.Countdown;
        }
    }

    /// <summary>Builds the engine-side race: shared state, player vessel, AI rival.</summary>
    public static class SkimRaceFactory
    {
        public static (GameLoop loop, SkimRaceController player, SkimRaceController rival) Create(
            int seed, int trackCrystals, PilotInput input)
        {
            var loop = new GameLoop("SkimRace");
            var track = new SkimTrack(seed);
            var shared = new SharedRaceState();
            int winTarget = Math.Min(track.Crystals.Count, Math.Max(1, trackCrystals)) / 2 + 1;

            SkimRaceController Build(string name, Domains domain, int pilotId, bool isAI)
            {
                var vessel = new GameObject(name);
                vessel.SetActive(false); // configure before Awake

                var resources = vessel.AddComponent<ResourceSystem>();
                resources.Resources = new List<Resource> { new() { Name = "boost", resourceGainRate = 0.12f } };
                resources.InitializeElementLevels(new ResourceCollection(0f, 0f, 0f, 0f));

                var pilot = vessel.AddComponent<SkimRaceController>();
                pilot.Track = track;
                pilot.Shared = shared;
                pilot.Resources = resources;
                pilot.Stats = new RoundStats { Name = name, Domain = domain };
                pilot.PilotId = pilotId;
                pilot.IsAI = isAI;
                pilot.WinTarget = winTarget;
                vessel.SetActive(true);
                return pilot;
            }

            var player = Build("Pilot", Domains.Jade, 0, isAI: false);
            player.Input = input;
            var rival = Build("Rival", Domains.Ruby, 1, isAI: true);
            rival.SkillTurnScale = 0.92f; // beatable, but honest lines
            rival.Opponent = player;
            player.Opponent = rival;

            ResetRace(shared, player, rival);
            return (loop, player, rival);
        }

        public static void ResetRace(SharedRaceState shared, SkimRaceController player, SkimRaceController rival)
        {
            shared.Reset();
            player.ResetPilot(-3.5f);
            rival.ResetPilot(3.5f);
        }
    }
}
