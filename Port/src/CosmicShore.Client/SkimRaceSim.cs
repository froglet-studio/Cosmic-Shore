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
        public SkimInputStatus Status;
        public bool BoostHeld;
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

        // VesselTransformer parity (the original's serialized defaults):
        // free rotation about the vessel's own axes, throttle from stick spread.
        const float PitchScaler = 130f;
        const float YawScaler = 130f;
        const float RollScaler = 130f;
        const float ThrottleScaler = 50f;
        const float MinimumSpeed = 10f;
        const float LERP_AMOUNT = 1.5f;
        const float BoostMultiplier = 2.1f;
        const float CollectRadius = 4.2f; // generous skim radius
        const float BoostDrainPerSecond = 0.45f;

        void Update()
        {
            if (Track is null || Shared is null || Status is null) return;

            switch (State)
            {
                case RaceState.Countdown:
                    Countdown -= Time.deltaTime;
                    if (Countdown <= 0f) State = RaceState.Racing;
                    return;
                case RaceState.Finished:
                    transform.position += transform.forward * (Speed * Time.deltaTime);
                    Speed = Mathf.Lerp(Speed, MinimumSpeed, Time.deltaTime);
                    return;
            }

            if (Shared.WinnerPilot >= 0) { State = RaceState.Finished; return; }

            ElapsedTime += Time.deltaTime;

            if (IsAI) SynthesizeAIInput();

            // Boost gates on the ported boost resource, then multiplies the throttle
            // term exactly like VesselTransformer's boostAmount.
            bool boosting = BoostHeld && Resources.Resources[0].CurrentAmount > 0.02f;
            if (boosting)
                Resources.ChangeResourceAmount(0, -BoostDrainPerSecond * Time.deltaTime);
            float boostAmount = boosting ? BoostMultiplier : 1f;

            // ── Cosmic Shore flight model (VesselTransformer parity) ──
            float turnScale = SkillTurnScale * Time.deltaTime;
            var rotation = transform.rotation;
            rotation = Quaternion.AngleAxis(Status.YSum * PitchScaler * turnScale, transform.right) * rotation;
            rotation = Quaternion.AngleAxis(Status.XSum * YawScaler * turnScale, transform.up) * rotation;
            rotation = Quaternion.AngleAxis(Status.YDiff * RollScaler * turnScale, transform.forward) * rotation;
            transform.rotation = rotation.normalized;

            Speed = Mathf.Lerp(
                Speed,
                Status.XDiff * ThrottleScaler * boostAmount + MinimumSpeed,
                LERP_AMOUNT * Time.deltaTime);
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
        void SynthesizeAIInput()
        {
            Status.XSum = 0f; Status.YSum = 0f; Status.YDiff = 0f;
            Status.XDiff = 0.55f;
            BoostHeld = false;
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
            ApplySeek(target.Value);
        }

        void ApplySeek(Vector3 target)
        {

            var local = Quaternion.Inverse(transform.rotation) * (target - transform.position);
            // Sign parity with the strategies: stick right ⇒ XSum positive ⇒ yaw right;
            // stick up ⇒ YSum negative ⇒ nose up (+X axis rotation is nose-down).
            Status.XSum = Mathf.Clamp(local.x * 0.25f, -1f, 1f);
            Status.YSum = Mathf.Clamp(-local.y * 0.25f, -1f, 1f);
            bool linedUp = Mathf.Abs(Status.XSum) < 0.35f && Mathf.Abs(Status.YSum) < 0.35f;

            // Rubber-band both directions: hold boost for the opening seconds (give the
            // player the early line), spend freely when trailing, coast when leading —
            // otherwise the leader sweeps every contested crystal on the shared line.
            int lead = Opponent != null ? Stats.CrystalsCollected - Opponent.Stats.CrystalsCollected : 0;
            Status.XDiff = lead >= 3 ? 0.4f : lead <= -2 ? 1f : 0.62f;
            float reserve = lead >= 3 ? 0.95f : lead <= -2 ? 0.12f : 0.4f;
            BoostHeld = linedUp && ElapsedTime > 5f && Resources.Resources[0].CurrentAmount > reserve;
        }

        public void ResetPilot(float lateralOffset)
        {
            ((IRoundStats)Stats).Cleanup();
            Resources.ResetResource(0);
            Status?.ResetForReplay();
            BoostHeld = false;
            var start = Track.PointAt(0f);
            transform.position = new Vector3(start.x + lateralOffset, start.y, -30f);
            transform.rotation = Quaternion.identity;
            BankAngle = 0f;
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
            int seed, int trackCrystals, SkimInputStatus playerStatus)
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
            player.Status = playerStatus;
            // Boost rides the authentic event channel: Button1 (gamepad A / keyboard).
            playerStatus.OnButtonPressed.OnRaised += e => { if (e == InputEvents.Button1Action) player.BoostHeld = true; };
            playerStatus.OnButtonReleased.OnRaised += e => { if (e == InputEvents.Button1Action) player.BoostHeld = false; };
            var rival = Build("Rival", Domains.Ruby, 1, isAI: true);
            rival.Status = new SkimInputStatus();
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
