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

    /// <summary>One emitted trail segment — position, ribbon-span direction, emit time.</summary>
    public struct TrailPoint
    {
        public Vector3 Pos;
        public Vector3 Right;
        public float Time;
    }

    /// <summary>
    /// Deterministic CLOSED circuit (prompter directive, 2026-06-11): a seeded ring whose
    /// radius and altitude undulate on integer harmonics, so the loop closes seamlessly
    /// and laps never end. Crystals sit at evenly spaced stations around the loop
    /// (HexRace-style: same seed → identical track on every machine).
    /// </summary>
    public sealed class SkimTrack
    {
        public readonly List<Vector3> Crystals = new();
        public readonly List<float> CrystalAngles = new();
        public readonly List<Vector3> Centerline = new(); // closed: last point == first
        public const float BaseRadius = 220f;

        readonly float _a1, _a2, _b1, _b2, _p1, _p2, _q1, _q2;
        readonly int _k1, _k2, _m1, _m2;

        public SkimTrack(int seed, int stations)
        {
            var rng = new Random(seed);
            // integer harmonics → r(0)=r(2π), y(0)=y(2π): the loop closes exactly
            _k1 = 2 + rng.Next(2);                       // 2-3 broad radial lobes
            _k2 = 5 + rng.Next(3);                       // 5-7 radial ripple
            _m1 = 2 + rng.Next(2);                       // 2-3 vertical waves
            _m2 = 4 + rng.Next(3);                       // 4-6 vertical ripple
            _a1 = 24f + (float)rng.NextDouble() * 16f;
            _a2 = 8f + (float)rng.NextDouble() * 8f;
            _b1 = 14f + (float)rng.NextDouble() * 10f;
            _b2 = 5f + (float)rng.NextDouble() * 5f;
            _p1 = (float)rng.NextDouble() * MathF.Tau;
            _p2 = (float)rng.NextDouble() * MathF.Tau;
            _q1 = (float)rng.NextDouble() * MathF.Tau;
            _q2 = (float)rng.NextDouble() * MathF.Tau;

            const int samples = 640;
            for (int i = 0; i <= samples; i++)            // <= closes the polyline
                Centerline.Add(PointAt(i / (float)samples * MathF.Tau));

            for (int i = 0; i < stations; i++)
            {
                float angle = i / (float)stations * MathF.Tau;
                // slight seeded scatter off the racing line so the line weaves
                var side = SideAt(angle);
                var lift = Vector3.Cross(TangentAt(angle), side).normalized;
                Crystals.Add(PointAt(angle)
                             + side * ((float)rng.NextDouble() * 6f - 3f)
                             + lift * ((float)rng.NextDouble() * 6f - 3f));
                CrystalAngles.Add(angle);
            }
        }

        public Vector3 PointAt(float t)
        {
            float r = BaseRadius + _a1 * MathF.Sin(_k1 * t + _p1) + _a2 * MathF.Sin(_k2 * t + _p2);
            float y = _b1 * MathF.Sin(_m1 * t + _q1) + _b2 * MathF.Sin(_m2 * t + _q2);
            return new Vector3(MathF.Cos(t) * r, y, MathF.Sin(t) * r);
        }

        public Vector3 TangentAt(float t) => (PointAt(t + 0.002f) - PointAt(t - 0.002f)).normalized;

        /// <summary>Horizontal outward-perpendicular of the loop at t (ribbon/rail offset frame).</summary>
        public Vector3 SideAt(float t) => Vector3.Cross(Vector3.up, TangentAt(t)).normalized;

        /// <summary>Loop angle of a world position (travel direction = increasing angle).</summary>
        public static float AngleOf(Vector3 p)
        {
            float a = MathF.Atan2(p.z, p.x);
            return a < 0f ? a + MathF.Tau : a;
        }

        /// <summary>Forward angular distance from one loop angle to another (0..2π).</summary>
        public static float ForwardDelta(float from, float to)
        {
            float d = to - from;
            while (d < 0f) d += MathF.Tau;
            while (d >= MathF.Tau) d -= MathF.Tau;
            return d;
        }
    }

    public enum RaceState { Countdown = 0, Racing = 1, Finished = 2 }

    /// <summary>
    /// Contested-crystal ledger + winner flag shared between pilots. On the closed
    /// circuit crystals RESPAWN a few seconds after a claim so later laps stay alive;
    /// the claim itself is permanent score.
    /// </summary>
    public sealed class SharedRaceState
    {
        public const float CrystalRespawnSeconds = 12f;

        readonly Dictionary<int, (int owner, float time)> _claims = new();
        public int WinnerPilot = -1;

        public bool IsTaken(int crystal)
            => _claims.TryGetValue(crystal, out var claim)
               && Time.time - claim.time < CrystalRespawnSeconds;

        public bool TryClaim(int crystal, int pilot)
        {
            if (IsTaken(crystal)) return false;
            _claims[crystal] = (pilot, Time.time);
            return true;
        }

        public void Reset()
        {
            _claims.Clear();
            WinnerPilot = -1;
        }
    }

    /// <summary>
    /// The race brain — a genuine engine MonoBehaviour shared by the player and the AI
    /// rival (identical flight model = fair race; the AI just synthesizes its input).
    /// Boost runs on the ported ResourceSystem, scoring on the ported RoundStats.
    /// The Squirrel loop (prompter directive, 2026-06-11): pilots leave PERSISTENT
    /// trails around the closed circuit; skimming any trail (the rival's, or your own
    /// once it has aged) charges energy; energy raises TOP SPEED; the analog triggers
    /// drift the vessel — the nose steers while the velocity holds its line.
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

        /// <summary>Persistent trail mass — only an active force (race reset) removes it.</summary>
        public readonly List<TrailPoint> Trail = new();
        /// <summary>Velocity direction; equals the nose except while drifting.</summary>
        public Vector3 Course = Vector3.forward;
        /// <summary>Analog drift amount this frame (max of the two triggers).</summary>
        public float DriftAmount;
        /// <summary>True while gaining trail-skim energy this frame (HUD glow).</summary>
        public bool IsSkimming;

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

        // The Squirrel loop tunables:
        const float TopSpeedEnergyGain = 0.6f;   // full energy bar = +60% top speed
        const float SkimRadius = 7f;             // how close to a trail counts as skimming
        const float SkimGainPerSecond = 0.4f;    // energy/s at zero distance, before drift bonus
        const float DriftSkimBonus = 1f;         // full-drift skimming charges up to 2x
        const float OwnTrailMinAge = 3f;         // own trail counts once you lap back onto it
        const float TrailSpacingSqr = 0.8f;      // ~0.9u between emitted trail points
        const float DriftCourseAlignFast = 7f;   // course→nose align rate, triggers released
        const float DriftCourseAlignSlow = 0.55f;// align rate at full drift — the slide

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
                    transform.position += Course * (Speed * Time.deltaTime);
                    Speed = Mathf.Lerp(Speed, MinimumSpeed, Time.deltaTime);
                    return;
            }

            if (Shared.WinnerPilot >= 0) { State = RaceState.Finished; return; }

            ElapsedTime += Time.deltaTime;

            if (IsAI) SynthesizeAIInput();

            // ── analog drift (prompter directive): triggers decouple velocity from
            // the nose — steering keeps turning the vessel, the course holds its line
            // and only slowly re-aligns while a trigger is held.
            DriftAmount = Mathf.Clamp01(Mathf.Max(Status.LeftTriggerAnalog, Status.RightTriggerAnalog));

            // ── trail skim → energy (prompter directive): riding near any persistent
            // trail charges the bar. The rival's trail always counts; your own only
            // after it ages (otherwise the ribbon you just emitted feeds you forever).
            // Drifting along a trail charges faster — the Squirrel fantasy.
            float skimProximity = SkimProximity();
            IsSkimming = skimProximity > 0f;
            if (IsSkimming)
                Resources.ChangeResourceAmount(0,
                    SkimGainPerSecond * skimProximity * (1f + DriftSkimBonus * DriftAmount) * Time.deltaTime);

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

            // course chases the nose: instantly when gripped, barely when drifting
            float alignRate = Mathf.Lerp(DriftCourseAlignFast, DriftCourseAlignSlow, DriftAmount);
            Course = Vector3.Slerp(Course, transform.forward, 1f - MathF.Exp(-alignRate * Time.deltaTime)).normalized;

            // energy raises top speed (prompter directive): the throttle term scales
            // with the bar, so a charged pilot is simply faster.
            float energy = Resources.Resources[0].CurrentAmount;
            Speed = Mathf.Lerp(
                Speed,
                Status.XDiff * ThrottleScaler * (1f + TopSpeedEnergyGain * energy) * boostAmount + MinimumSpeed,
                LERP_AMOUNT * Time.deltaTime);
            transform.position += Course * (Speed * Time.deltaTime);

            EmitTrail();

            // Contested skim collection (crystals respawn — see SharedRaceState).
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

        void EmitTrail()
        {
            if (Speed <= 4f) return;
            // the ribbon traces the actual path (course), so a drift leaves the
            // swept line you actually travelled, not where the nose pointed
            var emit = transform.position - Course * 1.6f - transform.up * 0.6f;
            if (Trail.Count > 0 && (emit - Trail[^1].Pos).sqrMagnitude <= TrailSpacingSqr) return;
            var span = Vector3.Cross(transform.up, Course);
            span = span.sqrMagnitude > 0.001f ? span.normalized : transform.right;
            Trail.Add(new TrailPoint { Pos = emit, Right = span, Time = Time.time });
        }

        /// <summary>
        /// 0 when no trail is within SkimRadius; otherwise 1 at zero distance falling
        /// linearly to 0 at the radius. Scans the rival's whole trail and own aged trail.
        /// </summary>
        float SkimProximity()
        {
            float bestSqr = SkimRadius * SkimRadius;
            bool found = false;
            var p = transform.position;

            if (Opponent != null)
            {
                var theirs = Opponent.Trail;
                for (int i = 0; i < theirs.Count; i++)
                {
                    float d = (theirs[i].Pos - p).sqrMagnitude;
                    if (d < bestSqr) { bestSqr = d; found = true; }
                }
            }

            float ownCutoff = Time.time - OwnTrailMinAge;
            for (int i = 0; i < Trail.Count; i++)
            {
                if (Trail[i].Time > ownCutoff) break; // list is time-ordered
                float d = (Trail[i].Pos - p).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; found = true; }
            }

            return found ? 1f - MathF.Sqrt(bestSqr) / SkimRadius : 0f;
        }

        /// <summary>Seek the next unclaimed crystal around the loop; boost when lined up.</summary>
        void SynthesizeAIInput()
        {
            Status.XSum = 0f; Status.YSum = 0f; Status.YDiff = 0f;
            Status.XDiff = 0.55f;
            BoostHeld = false;

            // next two unclaimed stations by forward loop distance (the circuit has
            // no "+z ahead" — direction of travel is increasing loop angle)
            float myAngle = SkimTrack.AngleOf(transform.position);
            Vector3? first = null, second = null;
            float bestDelta = float.MaxValue, secondDelta = float.MaxValue;
            for (int i = 0; i < Track.Crystals.Count; i++)
            {
                if (Shared.IsTaken(i)) continue;
                float delta = SkimTrack.ForwardDelta(myAngle, Track.CrystalAngles[i]);
                if (delta < 0.02f) continue; // station effectively underfoot — line up the next one
                if (delta < bestDelta) { secondDelta = bestDelta; second = first; bestDelta = delta; first = Track.Crystals[i]; }
                else if (delta < secondDelta) { secondDelta = delta; second = Track.Crystals[i]; }
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
            Trail.Clear();          // active force: a fresh race wipes the prismscape
            DriftAmount = 0f;
            IsSkimming = false;
            // grid up a little way before the first station, facing along the loop
            float startAngle = -30f / SkimTrack.BaseRadius;
            var tangent = Track.TangentAt(startAngle);
            transform.position = Track.PointAt(startAngle) + Track.SideAt(startAngle) * lateralOffset;
            transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            Course = tangent;
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
            int stations = Math.Clamp(trackCrystals, 8, 60);
            var track = new SkimTrack(seed, stations);
            var shared = new SharedRaceState();
            // a full station count ≈ 2+ laps of the circuit (crystals respawn), so the
            // closed-track loop — leave trails, lap back, skim them — actually engages
            int winTarget = stations;

            SkimRaceController Build(string name, Domains domain, int pilotId, bool isAI)
            {
                var vessel = new GameObject(name);
                vessel.SetActive(false); // configure before Awake

                var resources = vessel.AddComponent<ResourceSystem>();
                // skimming trails is the energy source — passive regen is a trickle
                resources.Resources = new List<Resource> { new() { Name = "boost", resourceGainRate = 0.04f } };
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
