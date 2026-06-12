using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Random = System.Random; // disambiguate from CosmicShore.Engine.Random

namespace CosmicShore.Client
{
    /// <summary>
    /// Deterministic CLOSED circuit: a seeded ring whose radius and altitude undulate
    /// on integer harmonics, so the loop closes seamlessly and laps never end.
    /// Crystals sit at evenly spaced stations around the loop, each carrying an
    /// ELEMENT (the buff/debuff fundamental): claiming one raises that elemental level
    /// permanently for the race.
    /// </summary>
    public sealed class SkimTrack
    {
        public readonly List<Vector3> Crystals = new();
        public readonly List<float> CrystalAngles = new();
        public readonly List<Element> StationElements = new();
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
                StationElements.Add(RollElement(rng, i));
            }
        }

        /// <summary>Every 7th station is Omni; the rest weight toward Charge (energy game).</summary>
        static Element RollElement(Random rng, int station)
        {
            if (station % 7 == 6) return Element.Omni;
            double roll = rng.NextDouble();
            if (roll < 0.40) return Element.Charge;
            if (roll < 0.65) return Element.Mass;
            if (roll < 0.85) return Element.Space;
            return Element.Time;
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

    /// <summary>No-op HUD controller for the prefab fixture's serialized slot (the GL window is the HUD).</summary>
    sealed class SkimVesselHud : MonoBehaviour, IVesselHUDController
    {
        public void Initialize(IVesselStatus vesselStatus) { }
        public void SubscribeToEvents() { }
        public void UnsubscribeFromEvents() { }
        public void ShowHUD() { }
        public void HideHUD() { }
        public void SetBlockPrefab(GameObject prefab) { }
    }

    /// <summary>Concrete VesselAnimation (base is abstract) — no-op puppetry.</summary>
    sealed class SkimVesselAnimation : VesselAnimation
    {
        protected override void AssignTransforms() { }
        protected override void PerformShipPuppetry(float Pitch, float Yaw, float Roll, float Throttle) { }
    }

    /// <summary>
    /// One pilot of the field — a REAL ported rig (Player + InputStatus + VesselController +
    /// VesselStatus + VesselTransformer + AIPilot + the RequireComponent family) plus the
    /// race-side bookkeeping the rig doesn't own: loop progress and the skim/drift HUD
    /// readouts. The trail itself is the rig's: real Prisms spawned by the real
    /// VesselPrismController into its <see cref="Trail"/> (see <see cref="TrailPrisms"/>).
    /// </summary>
    public sealed class SkimRacePilot
    {
        public IPlayer Player;
        public IVesselStatus Status;
        public Transform Transform;        // the vessel's transform — flown by VesselTransformer
        public ResourceSystem Resources;   // the rig's real ResourceSystem
        /// <summary>This pilot's own course registry — its AIPilot retargets on this
        /// registry's OnCellItemsUpdated (see SkimRaceDirector.SyncPilotCourses).</summary>
        public CellRuntimeDataSO Course;
        /// <summary>Live skim-contact state on the near-field skimmer (trigger-pass fed).</summary>
        public SkimContactTracker SkimTracker;
        public float GridOffset;

        public IRoundStats Stats => Player.RoundStats;
        public Domains Domain => Player.RoundStats.Domain;
        public IInputStatus Input => Player.InputStatus;
        public bool IsAI => Player.IsInitializedAsAI;

        /// <summary>The REAL prism trail — conserved mass; only an active force (race reset)
        /// removes it. Spawned by the rig's VesselPrismController async loop.</summary>
        public List<Prism> TrailPrisms => Status.VesselPrismController.Trail.TrailList;

        /// <summary>Boost intent (Button1Action edge state); the director gates it on energy.</summary>
        public bool BoostHeld;

        /// <summary>Total loop radians travelled — laps and position tie-breaks.</summary>
        public float TotalProgress;
        public int Lap => 1 + (int)(TotalProgress / MathF.Tau);

        /// <summary>True while gaining trail-skim energy this frame (HUD glow + audio).</summary>
        public bool IsSkimming;
        /// <summary>Skim contact strength 0..1 this frame (audio shimmer gain).</summary>
        public float SkimStrength;
        /// <summary>Analog drift amount this frame — VesselTransformer's clamped trigger sum.</summary>
        public float DriftAmount;

        internal float _lastLoopAngle;
    }

    /// <summary>
    /// The race brain. Flight is OWNED by the real ported systems — VesselTransformer flies
    /// every vessel from its real InputStatus (human: the ported GamepadInputStrategy fed by
    /// the window; rivals: the real AIPilot seeking crystals registered in CellRuntimeDataSO).
    /// Crystal claims flow through the REAL CrystalImpactor family (rung 3): Omni stations
    /// through OmniCrystalImpactor (vessel contact, any domain), Team stations through
    /// TeamCrystalImpactor (vessel contact, domain-locked), elemental stations through
    /// ElementalCrystalImpactor (skimmer claim — the crystal flies to the claimer and is
    /// consumed). Element levels and claim energy are granted by the effect SOs wired into
    /// those impactors (the real SkimmerAdjustElementLevelByCrystalEffectSO /
    /// VesselIncrementLevelByCrystalEffectSO + the SkimRace* race-rule effects) — never by
    /// this director. Crystal lifetime (spawn / dark window / relight / consumed-respawn)
    /// is owned by SkimRaceCrystalManager via the real Crystal.Respawn() → CrystalManager
    /// chain. Trails are REAL Prisms spawned by each rig's VesselPrismController (factory:
    /// SkimRacePrismFactory), and trail-skim energy flows through the REAL skimmer pipeline
    /// (trigger SphereCollider on the near-field Skimmer → SkimmerImpactor →
    /// SkimRaceTrailSkimEnergyEffectSO). This director only applies the StatsManager-shaped
    /// bookkeeping and the SkimRace rules:
    ///   countdown/win/replay · energy→top-speed
    ///   (the real VesselTransformer.ThrottleScalerMultiplier ElementalFloat hook) ·
    ///   boost gating/drain (BoostActionSO shape: VesselStatus.IsBoosting × BoostMultiplier) ·
    ///   two-tier analog drift (DriftActionSO shape: VesselTransformer.BeginDrift/EndDrift).
    /// </summary>
    public sealed class SkimRaceDirector : MonoBehaviour
    {
        // ── race design (the SkimRace rules — carried over from the sprint sim) ──
        public const float CrystalRespawnSeconds = 12f;
        // Claim at this center distance. The sprint sim used 4.2u against its slower
        // hand-rolled flight; the real VesselTransformer turns ~31u-radius at race speed
        // (the proven HexRaceRound contact rig claims at 25u), so a sub-5u sphere strands
        // the field in orbits. 10u keeps claims skill-flavored on the 46u-spaced stations
        // while the forward-ordered AI courses keep the whole field scoring (measured:
        // ~80 claims/min field-wide at 15u, ~½ that at 10u — a 30-target race lands in
        // the 1-3 minute arcade window).
        public const float CollectRadius = 10f;
        public const float VesselContactRadius = 2f;    // vessel contact-bubble SphereCollider
        const float TopSpeedEnergyGain = 0.6f;          // full energy bar = +60% top speed
        const float BoostDrainPerSecond = 0.45f;
        const float MassWidthPerLevel = 0.03f;          // VesselPrismController.XScaler per Mass level
        const float TimePerLevel = 0.06f;               // boost drain reduction per Time level (floor 40%)

        // ── real drift parameters — DriftActionSO CLASS defaults (the tuned per-vessel
        // assets live in Assets/_SO_Assets, which is off-limits): Mult=1.5, damping=0.
        // Both tiers use the class defaults; the two-tier analog interpolation
        // (trigger sum ≤1 single, >1 sharp) is the real VesselTransformer's.
        const float DriftRotationMultiplier = 1.5f;     // DriftActionSO.Mult default
        const float DriftDampingTarget = 0f;            // DriftActionSO.driftDamping default

        public SkimTrack Track { get; private set; }
        public GameDataSO GameData { get; private set; }
        public CellRuntimeDataSO CourseData { get; private set; }
        /// <summary>The race's CrystalManager-family manager — owns all station crystal lifetime.</summary>
        public SkimRaceCrystalManager Crystals { get; private set; }
        public readonly List<SkimRacePilot> Pilots = new();
        public SkimRacePilot HumanPilot => Pilots[0];

        public int WinTarget { get; private set; }
        public RaceState State { get; private set; } = RaceState.Countdown;
        public float Countdown { get; private set; } = 3f;
        public float ElapsedTime { get; private set; }
        /// <summary>Index into <see cref="Pilots"/>; -1 while the race runs. Human is 0.</summary>
        public int WinnerPilot { get; private set; } = -1;
        /// <summary>Scored crystal claims this race (diagnostics + tests).</summary>
        public int TotalClaims { get; private set; }

        /// <summary>(station, position, byHuman) — raised from inside the real impact pipeline.</summary>
        public event Action<int, Vector3, bool> OnCrystalClaimed;

        bool _humanAutopilot;     // victory lap / screenshot mode hand the human to the real AIPilot
        SkimRacePrismFactory _prismFactory; // answers every rig's prism spawn channel
        readonly List<Crystal> _scratchCrystals = new(); // SyncPilotCourses sort buffer

        internal void InitializeRace(SkimTrack track, GameDataSO gameData, CellRuntimeDataSO courseData,
            SkimRacePrismFactory prismFactory, SkimRaceCrystalManager crystalManager,
            List<SkimRacePilot> pilots, int winTarget,
            ScriptableEventInputEvents onButtonPressed, ScriptableEventInputEvents onButtonReleased)
        {
            Track = track;
            GameData = gameData;
            CourseData = courseData;
            _prismFactory = prismFactory;
            Crystals = crystalManager;
            Pilots.AddRange(pilots);
            WinTarget = winTarget;

            // Claims arrive from INSIDE the real impactor dispatch (per-crystal SOAP
            // channels for vessel claims; the elemental claim effect for skimmer claims),
            // re-raised by the manager with the station identity attached.
            Crystals.OnStationClaimed += HandleStationClaimed;

            // Course registry changes (spawn, dark window, relight, consumed) all raise
            // the primary registry's OnCellItemsUpdated — keep every pilot's personal
            // course sorted off that one channel instead of director-staged ticks.
            CourseData.OnCellItemsUpdated.OnRaised += SyncPilotCourses;

            // The human's ported GamepadInputStrategy raises these SOAP channels (shared
            // across the rig clones exactly like the real prefab assets; AI never raises).
            onButtonPressed.OnRaised += HandleButtonPressed;
            onButtonReleased.OnRaised += HandleButtonReleased;

            RestartRace();
        }

        // ── frame ────────────────────────────────────────────────────

        void Update()
        {
            switch (State)
            {
                case RaceState.Countdown:
                    Countdown -= Time.deltaTime;
                    if (Countdown <= 0f) BeginRacing();
                    return;

                case RaceState.Racing:
                    ElapsedTime += Time.deltaTime;
                    for (int i = 0; i < Pilots.Count; i++)
                        TickPilot(Pilots[i]);
                    return;

                case RaceState.Finished:
                    // crystals keep cycling under the victory lap — the manager owns that
                    return;
            }
        }

        void BeginRacing()
        {
            State = RaceState.Racing;
            foreach (var pilot in Pilots)
                pilot.Status.IsStationary = false; // release the grid — the real rigs fly
            GameData.StartTurn();
        }

        void TickPilot(SkimRacePilot pilot)
        {
            var status = pilot.Status;
            var resources = pilot.Resources;

            // Analog drift readout — mirrors VesselTransformer's clamped trigger sum
            // (HUD gauge + the drift skim bonus below; the transformer itself reads the
            // same triggers for the real two-tier rotation/course blend).
            var input = pilot.Input;
            pilot.DriftAmount = Mathf.Clamp01(input.LeftTriggerAnalog + input.RightTriggerAnalog);

            // Trail skim → energy now flows entirely through the REAL pipeline: the
            // near-field Skimmer's trigger sphere sweeps prisms, the engine TriggerPass
            // dispatches into SkimmerImpactor.AcceptImpactee, and the race's
            // SkimRaceTrailSkimEnergyEffectSO charges the bar per prism (drift + Charge
            // bonuses inside the effect). The director only mirrors the live contact
            // state into the HUD readouts.
            pilot.IsSkimming = pilot.SkimTracker.IsSkimming;
            pilot.SkimStrength = pilot.SkimTracker.Strength;

            // Mass lays heavier ribbon — the REAL controller knob: XScaler widens every
            // block the VesselPrismController spawns from here on (skim reach itself
            // scales through the skimmer's Mass-bound Scale ElementalFloat).
            status.VesselPrismController.XScaler = 1f + MassWidthPerLevel * resources.GetLevel(Element.Mass);

            // Boost — BoostActionSO shape: IsBoosting gates VesselTransformer.MoveShip's
            // boostAmount = VesselStatus.BoostMultiplier (real serialized default, 4x).
            // The race rule drains the energy bar; Time levels burn cooler.
            bool boosting = pilot.BoostHeld && resources.Resources[0].CurrentAmount > 0.02f;
            status.IsBoosting = boosting;
            if (boosting)
                resources.ChangeResourceAmount(0,
                    -BoostDrainPerSecond
                    * MathF.Max(0.4f, 1f - TimePerLevel * resources.GetLevel(Element.Time)) * Time.deltaTime);

            // Energy raises top speed through the REAL hook: ThrottleScalerMultiplier is
            // the ElementalFloat the transformer multiplies into its throttle term.
            float energy = resources.Resources[0].CurrentAmount;
            status.VesselTransformer.ThrottleScalerMultiplier.Value = 1f + TopSpeedEnergyGain * energy;

            // Lap / position progress (guard against the spawn-in jump).
            float loopAngle = SkimTrack.AngleOf(pilot.Transform.position);
            float delta = SkimTrack.ForwardDelta(pilot._lastLoopAngle, loopAngle);
            if (delta < MathF.PI * 0.5f) pilot.TotalProgress += delta;
            pilot._lastLoopAngle = loopAngle;
        }

        // ── crystals (real contact pipeline; lifetime owned by the manager) ──

        public bool IsStationActive(int station) => Crystals.IsStationAvailable(station);

        /// <summary>Renderer hook: live station crystals + elemental crystals mid-flight to their claimer.</summary>
        public bool TryGetDrawableCrystal(int station, out Vector3 position)
            => Crystals.TryGetDrawableCrystalPosition(station, out position);

        /// <summary>
        /// Refreshes every pilot's personal course registry with the live crystals that
        /// pilot may actually claim and raises its OnCellItemsUpdated so that pilot's
        /// AIPilot retargets. Claimability is the real Crystal.CanBeCollected semantic —
        /// uncommitted (Blue) crystals for everyone, team crystals only for the matching
        /// domain — so no pilot ever orbits a station it cannot take. Ordering is the
        /// race-design lever: the verbatim AIPilot selection walks the list and (for
        /// any distance &gt; 1u) its `sqDistance &lt; MinDistance²` comparison accepts each
        /// subsequent entry, so the LAST list item is the chosen target. The director
        /// sorts each registry by forward loop distance DESCENDING — the last item is the
        /// next station AHEAD of that pilot, so the field races the circuit forward
        /// (tangentially aligned approaches, no greedy-nearest U-turn orbits). One shared
        /// registry would pack the whole field onto a single station; per-pilot
        /// registries are the race-design layer's station assignment.
        /// </summary>
        void SyncPilotCourses()
        {
            for (int p = 0; p < Pilots.Count; p++)
            {
                var pilot = Pilots[p];
                var registry = pilot.Course;
                float pilotAngle = SkimTrack.AngleOf(pilot.Transform.position);

                _scratchCrystals.Clear();
                for (int s = 0; s < Track.Crystals.Count; s++)
                {
                    if (!Crystals.IsStationAvailable(s)) continue;
                    var crystal = Crystals.GetStationCrystal(s);
                    if (!crystal || !crystal.CanBeCollected(pilot.Domain)) continue;
                    _scratchCrystals.Add(crystal);
                }
                _scratchCrystals.Sort((a, b) =>
                    SkimTrack.ForwardDelta(pilotAngle, SkimTrack.AngleOf(b.transform.position))
                        .CompareTo(SkimTrack.ForwardDelta(pilotAngle, SkimTrack.AngleOf(a.transform.position))));

                registry.CellItems.Clear();
                registry.Crystals.Clear();
                for (int i = 0; i < _scratchCrystals.Count; i++)
                {
                    registry.CellItems.Add(_scratchCrystals[i]);
                    registry.Crystals.Add(_scratchCrystals[i]);
                }
                registry.OnCellItemsUpdated.Raise(); // → that pilot's AIPilot retargets
            }
        }

        /// <summary>
        /// Fires from INSIDE the real impactor dispatch — the manager re-raises the
        /// per-crystal claim signals (OnCrystalCollected SOAP channel for vessel claims,
        /// the elemental claim effect for skimmer claims) with the station attached.
        /// Applies ONLY the StatsManager-shaped bookkeeping and checks the win target:
        /// elemental levels and claim energy were already granted by the effect SOs
        /// inside the impactor chain, and the respawn window is staged by the manager
        /// through the real Crystal.Respawn() path.
        /// </summary>
        void HandleStationClaimed(int station, Element element, string playerName)
        {
            if (State != RaceState.Racing) return; // victory-lap claims keep the loop alive, not the score

            var pilot = FindPilot(playerName);
            if (pilot == null) return;

            TotalClaims++;
            var stats = pilot.Stats;
            stats.CrystalsCollected++;
            if (element == Element.Omni)
                stats.OmniCrystalsCollected++;
            else
                stats.ElementalCrystalsCollected++;
            OnCrystalClaimed?.Invoke(station, Track.Crystals[station], !pilot.IsAI);

            if (stats.CrystalsCollected >= WinTarget)
                FinishRace(pilot);
        }

        SkimRacePilot FindPilot(string playerName)
        {
            for (int i = 0; i < Pilots.Count; i++)
                if (Pilots[i].Player.Name == playerName)
                    return Pilots[i];
            return null;
        }

        void FinishRace(SkimRacePilot winner)
        {
            WinnerPilot = Pilots.IndexOf(winner);
            winner.Stats.Score = ElapsedTime;   // HexRace golf scoring: time, lower is better
            State = RaceState.Finished;

            foreach (var pilot in Pilots)
            {
                pilot.BoostHeld = false;
                pilot.Status.IsBoosting = false;
            }

            // Victory lap: hand the human's rig to the REAL AIPilot (rivals already fly it),
            // so the scoreboard sits over a field still cruising the glowing prismscape.
            EndHumanDrift(isSharp: false);
            EndHumanDrift(isSharp: true);
            EngageHumanAutopilot();
        }

        // ── human input wiring (the real SOAP button channels) ───────

        void HandleButtonPressed(InputEvents inputEvent)
        {
            switch (inputEvent)
            {
                case InputEvents.Button1Action:
                    HumanPilot.BoostHeld = true; // BoostActionSO path — gated on energy in TickPilot
                    break;
                case InputEvents.OnlyLeftStickAction:
                case InputEvents.OnlyRightStickAction:
                    BeginHumanDrift(isSharp: false); // single trigger → tier 1
                    break;
                case InputEvents.BothSticksAction:
                    BeginHumanDrift(isSharp: true);  // both triggers → sharp tier
                    break;
            }
        }

        void HandleButtonReleased(InputEvents inputEvent)
        {
            switch (inputEvent)
            {
                case InputEvents.Button1Action:
                    HumanPilot.BoostHeld = false;
                    break;
                case InputEvents.OnlyLeftStickAction:
                case InputEvents.OnlyRightStickAction:
                    EndHumanDrift(isSharp: false);
                    break;
                case InputEvents.BothSticksAction:
                    EndHumanDrift(isSharp: true);
                    break;
            }
        }

        /// <summary>DriftActionSO.StartAction shape: register the tier with the real
        /// transformer and flag the status — the transformer's analog interpolation
        /// (trigger sum ≤1 single, >1 sharp) does the rest each frame.</summary>
        void BeginHumanDrift(bool isSharp)
        {
            var transformer = HumanPilot.Status.VesselTransformer;
            transformer.BeginDrift(DriftRotationMultiplier, DriftDampingTarget, isSharp);
            HumanPilot.Status.IsDrifting = true;
        }

        /// <summary>DriftActionSO.StopAction shape.</summary>
        void EndHumanDrift(bool isSharp)
        {
            var transformer = HumanPilot.Status.VesselTransformer;
            transformer.EndDrift(isSharp);
            HumanPilot.Status.IsDrifting = transformer.IsDriftActive;
        }

        // ── standings ────────────────────────────────────────────────

        /// <summary>1-based race position: crystals first, distance travelled breaks ties.</summary>
        public int PositionOf(SkimRacePilot pilot)
        {
            int position = 1;
            for (int i = 0; i < Pilots.Count; i++)
            {
                var other = Pilots[i];
                if (ReferenceEquals(other, pilot)) continue;
                if (other.Stats.CrystalsCollected > pilot.Stats.CrystalsCollected
                    || (other.Stats.CrystalsCollected == pilot.Stats.CrystalsCollected
                        && other.TotalProgress > pilot.TotalProgress))
                    position++;
            }
            return position;
        }

        /// <summary>Best crystal count among everyone except this pilot (HUD).</summary>
        public int BestOtherCrystals(SkimRacePilot pilot)
        {
            int best = 0;
            for (int i = 0; i < Pilots.Count; i++)
                if (!ReferenceEquals(Pilots[i], pilot) && Pilots[i].Stats.CrystalsCollected > best)
                    best = Pilots[i].Stats.CrystalsCollected;
            return best;
        }

        // ── lifecycle ────────────────────────────────────────────────

        /// <summary>Headless screenshot runs skip the grid hold.</summary>
        public void SkipCountdown()
        {
            if (State == RaceState.Countdown) Countdown = 0f;
        }

        /// <summary>Hands the human's rig to the real AIPilot (screenshot autopilot / victory lap).</summary>
        public void EngageHumanAutopilot()
        {
            if (_humanAutopilot) return;
            HumanPilot.Player.Vessel.ToggleAIPilot(true);
            _humanAutopilot = true;
        }

        public void RestartRace()
        {
            TotalClaims = 0;
            WinnerPilot = -1;
            ElapsedTime = 0f;
            Countdown = 3f;
            State = RaceState.Countdown;

            if (_humanAutopilot)
            {
                HumanPilot.Player.Vessel.ToggleAIPilot(false);
                _humanAutopilot = false;
            }

            // Active force: a fresh race wipes the course and the prismscape. The factory
            // destroys every real Prism GameObject (the ONLY mass sink); the trail lists
            // themselves are cleared by the real reset path below (Player.ResetForPlay →
            // VesselStatus.ResetForPlay → VesselPrismController.StopSpawn + ClearTrails).
            CourseData.ResetRuntimeData();
            _prismFactory.DespawnAll();
            foreach (var pilot in Pilots)
            {
                pilot.Course.CellItems.Clear();
                pilot.Course.Crystals.Clear();
            }

            for (int i = 0; i < Pilots.Count; i++)
                ResetPilot(Pilots[i]);

            // Fresh course through the manager (its generation bump drops stale in-flight
            // claim handlers); each spawn raises OnCellItemsUpdated → SyncPilotCourses.
            Crystals.ResetStations();
            SyncPilotCourses();
        }

        void ResetPilot(SkimRacePilot pilot)
        {
            var player = pilot.Player;
            player.ResetForPlay();   // the REAL reset: transformer, resources, prism controller, AI off
            pilot.Stats.Cleanup();   // zero the round stats (Name/Domain persist)

            pilot.TotalProgress = 0f;
            pilot.BoostHeld = false;
            pilot.DriftAmount = 0f;
            pilot.IsSkimming = false;
            pilot.SkimStrength = 0f;
            pilot.SkimTracker.ResetContacts(); // the old prismscape is gone — drop stale overlaps
            pilot.Resources.InitializeElementLevels(new ResourceCollection(0f, 0f, 0f, 0f));
            pilot.Status.VesselTransformer.ThrottleScalerMultiplier.Value = 1f;
            // ResetTransformer zeroes its internal speed but not the replicated mirror;
            // the spawn loop gates on VesselStatus.Speed > 3, so a stale top-speed value
            // from the previous race would pile prisms on the grid during the countdown.
            pilot.Status.Speed = 0f;

            player.StartPlayer();    // the REAL start: vessel un-stations, prism spawn loop ON,
                                     // AI pilots re-engage. Trails are REAL prisms from here.
            if (!pilot.IsAI)
            {
                // The window drives the ported GamepadInputStrategy itself (with the
                // prompter's post-strategy yaw/roll inversions); pausing the rig's
                // InputController keeps it from double-processing the same strategy.
                player.InputController.SetPause(true);
            }

            // Grid up a little way before the first station, facing along the loop.
            float startAngle = -30f / SkimTrack.BaseRadius;
            var tangent = Track.TangentAt(startAngle);
            var pose = new Pose
            {
                position = Track.PointAt(startAngle) + Track.SideAt(startAngle) * pilot.GridOffset,
                rotation = Quaternion.LookRotation(tangent, Vector3.up),
            };
            player.Vessel.SetPose(pose);
            pilot.Status.Course = tangent;
            pilot.Status.IsStationary = true; // countdown holds the grid; BeginRacing releases it
            pilot._lastLoopAngle = SkimTrack.AngleOf(pose.position);
        }

        /// <summary>
        /// Deterministic wind-down: stops every rig's async prism spawn loop
        /// (VesselPrismController.StartSpawn runs async-void — left running it outlives
        /// the race and hangs test hosts) and the AI/drift loops via the real reset path.
        /// Idempotent; the window calls it on close and tests call it in teardown.
        /// </summary>
        public void Shutdown()
        {
            foreach (var pilot in Pilots)
            {
                pilot.Status.VesselPrismController.StopSpawn();
                pilot.Player.ResetForPlay(); // AI path toggles the AIPilot OFF
            }
        }
    }

    /// <summary>
    /// Builds the race on the REAL ported pipeline: a prefab-shaped Squirrel vessel template
    /// (the C6/HexRaceRound fixture layout + the CT1 contact rig), a Player template carrying
    /// the real InputStatus, the verbatim PlayerSpawner/VesselSpawner clone+DI path, and the
    /// SkimRaceDirector that runs the race rules on top.
    /// </summary>
    public static class SkimRaceFactory
    {
        // grid slots fan outward from the racing line
        static readonly float[] GridOffsets = { -3.5f, 3.5f, -7.5f, 7.5f, -11.5f, 11.5f, 0f, -15f };

        // ── trail/skim rig authoring (prefab-level tuning, applied to the template) ──
        internal const float SkimmerReachBase = 7f; // near-field skimmer scale at Mass level 0
        const float SkimmerReachAtMass10 = 9.5f; // …and at Mass level 10 (ElementalFloat Min→Max)
        static readonly Vector3 TrailBaseScale = new(5f, 1f, 6f); // block ≈ 2.5×1×6 neon slab
        const float TrailWavelength = 6f;        // one block every ~6u of travel

        /// <summary>Reflection stand-in for inspector wiring of serialized fields (HexRaceRound pattern).</summary>
        internal static void SetPrivateField(object target, string field, object value)
        {
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                if (f == null) continue;
                f.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().Name, field);
        }

        public static (GameLoop loop, SkimRaceDirector director) Create(int seed, int trackCrystals, int rivalCount)
        {
            var loop = new GameLoop("SkimRace");
            NetworkManager.Singleton = null;

            // Project layers (ProjectSettings/TagManager.asset) the prism/crystal/skimmer
            // rigs reference at runtime (Prism.InitializePrismProperties assigns
            // "TrailBlocks"; Prism.OnTriggerExit checks "Crystals"). Idempotent.
            LayerMask.SetLayerName(7, "Skimmers");
            LayerMask.SetLayerName(8, "Ships");
            LayerMask.SetLayerName(9, "Crystals");
            LayerMask.SetLayerName(10, "Explosions");
            LayerMask.SetLayerName(11, "TrailBlocks");

            int stations = Math.Clamp(trackCrystals, 8, 60);
            var track = new SkimTrack(seed, stations);
            // a full station count ≈ 2+ laps of the circuit (crystals respawn), so the
            // closed-track loop — leave trails, lap back, skim them — actually engages
            int winTarget = stations;

            // ── shared data: theme + GameDataSO (vessel paint is headless-stubbed; the
            // GL layer colors by domain itself) ───────────────────────
            var theme = ScriptableObject.CreateInstance<ThemeManagerDataContainerSO>();
            theme.TeamMaterialSets = new Dictionary<Domains, SO_MaterialSet>();
            foreach (var domain in new[] { Domains.Jade, Domains.Ruby, Domains.Blue, Domains.Gold })
            {
                var set = ScriptableObject.CreateInstance<SO_MaterialSet>();
                Material Mat(string role) => new((Shader)null) { name = $"{domain}-{role}" };
                set.ShipMaterial = Mat("ship");
                // Prism material states (engine materials are data-only property stores;
                // the GL layer colors by domain itself). Real instances keep the prism
                // team/state managers' verbatim material pipeline on its happy path.
                set.BlockMaterial = Mat("block");
                set.TransparentBlockMaterial = Mat("block-transparent");
                set.ShieldedBlockMaterial = Mat("block-shielded");
                set.TransparentShieldedBlockMaterial = Mat("block-shielded-transparent");
                set.SuperShieldedBlockMaterial = Mat("block-supershielded");
                set.TransparentSuperShieldedBlockMaterial = Mat("block-supershielded-transparent");
                set.DangerousBlockMaterial = Mat("block-dangerous");
                set.TransparentDangerousBlockMaterial = Mat("block-dangerous-transparent");
                set.ExplodingBlockMaterial = Mat("block-exploding");
                set.BlockSilhouettePrefab = new GameObject($"{domain}-silhouette");
                theme.TeamMaterialSets[domain] = set;
            }

            var gameData = ScriptableObject.CreateInstance<GameDataSO>();
            gameData.ThemeManagerData = theme;
            gameData.OnInitializeGame = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            gameData.OnPlayerNetworkSpawnedUlong = ScriptableObject.CreateInstance<ScriptableEventUlong>();
            gameData.OnVesselNetworkSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            gameData.selectedVesselClass = ScriptableObject.CreateInstance<VesselClassTypeVariable>();
            gameData.selectedVesselClass.Value = VesselClassType.Squirrel;

            // ── the course registries (V11): one primary registry owns the crystals
            // (Crystal.DestroyCrystal removes itself there), and each pilot gets a
            // personal registry the director keeps sorted far→near (SyncPilotCourses) ──
            var courseData = CreateCourseRegistry(gameData);

            // ── the crystal layer (rung 3): the REAL element-granting effects, the
            // race-rule effects (per-game effect-asset pattern), and the race's
            // CrystalManager-family manager that owns station crystal lifetime ──
            // Element grants: the real effects, wired exactly where the original wires
            // them (SkimmerAdjustElementLevelByCrystalEffect rides each elemental
            // crystal's elementalCrystalShipEffects; VesselIncrementLevelByCrystalEffect
            // rides the vessel-claim effect list).
            var skimmerLevelEffect = ScriptableObject.CreateInstance<SkimmerAdjustElementLevelByCrystalEffectSO>();
            var vesselLevelEffect = ScriptableObject.CreateInstance<VesselIncrementLevelByCrystalEffectSO>();
            var omniSurgeEffect = ScriptableObject.CreateInstance<SkimRaceOmniCrystalSurgeEffectSO>();
            var teamEnergyEffect = ScriptableObject.CreateInstance<SkimRaceCrystalEnergyEffectSO>();
            var elementalClaimEffect = ScriptableObject.CreateInstance<SkimRaceElementalClaimEffectSO>();

            // Team stations (every 7th, slot 3) are domain-locked to a domain actually
            // racing — never Blue: TeamCrystalImpactor gates on strict equality while
            // CanBeCollected treats Blue as "anyone", so a Blue lock would put a crystal
            // in every course that only a Blue pilot could take.
            rivalCount = Math.Clamp(rivalCount, 1, 7);
            var teamLockDomains = new List<Domains> { Domains.Jade };
            if (rivalCount >= 1) teamLockDomains.Add(Domains.Ruby);
            if (rivalCount >= 2) teamLockDomains.Add(Domains.Gold);
            var stationDomains = new Domains[stations];
            for (int i = 0; i < stations; i++)
                stationDomains[i] = SkimRaceCrystalManager.IsTeamSlot(i)
                    ? teamLockDomains[(i / 7) % teamLockDomains.Count]
                    : Domains.Blue;

            var crystalManagerGo = new GameObject("SkimRaceCrystalManager");
            crystalManagerGo.SetActive(false); // configure-before-activation
            var crystalManager = crystalManagerGo.AddComponent<SkimRaceCrystalManager>();
            SetPrivateField(crystalManager, "cellData", courseData);
            crystalManagerGo.SetActive(true);
            crystalManager.Configure(track, stationDomains,
                omniEffects: new VesselCrystalEffectSO[] { omniSurgeEffect },
                teamEffects: new VesselCrystalEffectSO[] { vesselLevelEffect, teamEnergyEffect },
                elementalEffects: new SkimmerCrystalEffectSO[] { skimmerLevelEffect, elementalClaimEffect },
                elementalClaimEffect: elementalClaimEffect);

            // ── shared input button channels: the rig's InputStatus raises through these
            // (one pair of SOAP assets shared across clones, exactly like the real prefabs;
            // only the human's strategy ever raises) ──────────────────
            var onButtonPressed = ScriptableObject.CreateInstance<ScriptableEventInputEvents>();
            var onButtonReleased = ScriptableObject.CreateInstance<ScriptableEventInputEvents>();

            // ── the prism pipeline: one factory answers every rig's spawn channel, and
            // one shared skimmer-prism effect container carries the race's skim-energy
            // rule (the per-vessel effect-asset pattern) ───────────────
            var prismFactory = new SkimRacePrismFactory(theme);
            var skimEnergyEffect = ScriptableObject.CreateInstance<SkimRaceTrailSkimEnergyEffectSO>();
            var skimmerImpactorContainer = ScriptableObject.CreateInstance<SkimmerImpactorDataContainerSO>();
            SetPrivateField(skimmerImpactorContainer, "skimmerPrismEffectsSO",
                new SkimmerPrismEffectSO[] { skimEnergyEffect });

            // ── prefabs + spawners (verbatim C6 pipeline) ─────────────
            var vesselTemplate = BuildVesselPrefab("SquirrelPrefab", VesselClassType.Squirrel, gameData,
                courseData, prismFactory, skimmerImpactorContainer, onButtonPressed, onButtonReleased);
            var prefabContainer = ScriptableObject.CreateInstance<VesselPrefabContainer>();
            SetPrivateField(prefabContainer, "_shipPrefabs", new[] { vesselTemplate.transform });

            var playerTemplateGo = new GameObject("PlayerPrefab");
            var playerTemplate = playerTemplateGo.AddComponent<Player>();
            SetPrivateField(playerTemplate, "gameData", gameData);
            // The REAL InputStatus (V7) rides the player prefab; InputController.Awake's
            // GetOrAdd finds it. The strategy raises button events through its channels.
            var inputStatusTemplate = playerTemplateGo.AddComponent<InputStatus>();
            SetPrivateField(inputStatusTemplate, "_onButtonPressed", onButtonPressed);
            SetPrivateField(inputStatusTemplate, "_onButtonReleased", onButtonReleased);

            var container = new Container();
            container.RegisterValue(gameData);
            container.RegisterValue(new GameObject("PlayerDataService").AddComponent<PlayerDataService>());
            // VesselImpactor's [Inject] AudioSystem (shell) must resolve at clone injection.
            container.RegisterValue(new GameObject("AudioSystem").AddComponent<AudioSystem>());

            var spawnerGo = new GameObject("Spawners");
            var vesselSpawner = spawnerGo.AddComponent<VesselSpawner>();
            SetPrivateField(vesselSpawner, "vesselPrefabContainer", prefabContainer);
            var playerSpawner = spawnerGo.AddComponent<PlayerSpawner>();
            SetPrivateField(playerSpawner, "_playerPrefab", playerTemplate);
            SetPrivateField(playerSpawner, "vesselSpawner", vesselSpawner);
            container.InjectGameObject(spawnerGo);

            // Spawn poses for GameDataSO.AddPlayer (the director re-grids afterwards).
            var spawnTransforms = new Transform[rivalCount + 1];
            for (int i = 0; i < spawnTransforms.Length; i++)
            {
                var point = new GameObject($"spawnPoint{i}");
                point.transform.position = new Vector3((i - rivalCount * 0.5f) * 8f, 0f, -SkimTrack.BaseRadius);
                spawnTransforms[i] = point.transform;
            }
            gameData.SetSpawnPositions(spawnTransforms);

            // ── the field: the human plus seeded-skill AI rivals, all the same real rig ──
            var pilots = new List<SkimRacePilot>
            {
                SpawnPilot(playerSpawner, gameData, vesselTemplate, "Pilot", Domains.Jade,
                    isAI: false, skill: 1f, GridOffsets[0]),
            };

            // Ruby/Gold/Blue rivals with seeded skill — same flight model, different lines.
            // Blue is the neutral domain; it joins last.
            var rivalDomains = new[] { Domains.Ruby, Domains.Gold, Domains.Blue };
            var personalityRng = new Random(seed * 31 + 7);
            for (int i = 0; i < rivalCount; i++)
            {
                // skill skews high so back-of-field rivals stay in the race
                float skill = 0.3f + (float)personalityRng.NextDouble() * 0.7f; // AIPilot.skillLevel
                pilots.Add(SpawnPilot(playerSpawner, gameData, vesselTemplate, $"Rival {i + 1}",
                    rivalDomains[i % rivalDomains.Length], isAI: true, skill,
                    GridOffsets[(i + 1) % GridOffsets.Length]));
            }

            var directorGo = new GameObject("SkimRaceDirector");
            var director = directorGo.AddComponent<SkimRaceDirector>();
            director.InitializeRace(track, gameData, courseData, prismFactory, crystalManager,
                pilots, winTarget, onButtonPressed, onButtonReleased);
            return (loop, director);
        }

        static CellRuntimeDataSO CreateCourseRegistry(GameDataSO gameData)
        {
            var registry = ScriptableObject.CreateInstance<CellRuntimeDataSO>();
            SetPrivateField(registry, "gameData", gameData);
            registry.OnResetForReplay = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            registry.OnCrystalSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            registry.OnCellItemsUpdated = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            registry.OnPhaseChanged = ScriptableObject.CreateInstance<ScriptableEventCellPhase>();
            return registry;
        }

        static SkimRacePilot SpawnPilot(PlayerSpawner playerSpawner, GameDataSO gameData,
            GameObject vesselTemplate, string name, Domains domain, bool isAI, float skill, float gridOffset)
        {
            // Prefab-variant skill: bake the seeded skill level into the template before the
            // clone — the real pipeline also configures AIPilot skill before initialization
            // (ServerPlayerVesselInitializerWithAI.ConfigureAIPilot).
            SetPrivateField(vesselTemplate.GetComponent<AIPilot>(), "skillLevel", skill);

            var player = playerSpawner.SpawnPlayerAndShip(new IPlayer.InitializeData
            {
                vesselClass = VesselClassType.Squirrel,
                PlayerName = name,
                AvatarId = 0,
                AllowSpawning = true,
                IsAI = isAI,
            }) ?? throw new InvalidOperationException($"PlayerSpawner failed to spawn '{name}'.");

            // Same domain assignment shape as HexRaceRound (single-player init defaults Jade).
            ((Player)player).SetDomain(domain);
            player.RoundStats.Domain = domain;

            gameData.AddPlayer(player); // registers RoundStats + assigns a spawn pose

            var vesselGo = ((VesselController)player.Vessel).gameObject;

            // Personal course registry — wired into the CLONE's AIPilot before activation
            // (OnEnable subscribes to the registry's OnCellItemsUpdated channel), so the
            // director can hand each pilot its own station ordering (SyncPilotCourses).
            var course = CreateCourseRegistry(gameData);
            var aiPilot = vesselGo.GetComponent<AIPilot>();
            SetPrivateField(aiPilot, "cellData", course);
            SetPrivateField(aiPilot, "OnCellItemsUpdated", course.OnCellItemsUpdated);

            vesselGo.SetActive(true);   // clones mirror the inactive prefab root — activate into the scene

            var status = vesselGo.GetComponent<VesselStatus>();
            return new SkimRacePilot
            {
                Player = player,
                Status = status,
                Transform = player.Vessel.Transform,
                Resources = status.ResourceSystem,
                Course = course,
                SkimTracker = vesselGo.GetComponentInChildren<SkimContactTracker>(includeInactive: true),
                GridOffset = gridOffset,
            };
        }

        /// <summary>
        /// The prefab-shaped Squirrel rig (HexRaceRound/C6 fixture layout): the REAL
        /// VesselController + VesselStatus + the RequireComponent family + child skimmers +
        /// the CT1 contact rig (contact-bubble SphereCollider + VesselImpactor/
        /// NetworkVesselImpactor + ImpactCollider). The VesselPrismController is wired to
        /// the race's prism factory channel (real Prism trails) and the near-field skimmer
        /// carries the skim contact rig (trigger sphere + SkimmerImpactor + skim-energy
        /// effect container + SkimContactTracker). Every AIPilot shares the race's course
        /// registry and its OnCellItemsUpdated channel; every R_VesselActionHandler and the
        /// player rig's InputStatus share the race's button channels. The E16 clone remap
        /// makes Instantiate prefab-faithful.
        /// </summary>
        static GameObject BuildVesselPrefab(string name, VesselClassType vesselType, GameDataSO gameData,
            CellRuntimeDataSO courseData, SkimRacePrismFactory prismFactory,
            SkimmerImpactorDataContainerSO skimmerImpactorContainer,
            ScriptableEventInputEvents onButtonPressed, ScriptableEventInputEvents onButtonReleased)
        {
            var go = new GameObject(name);
            go.SetActive(false); // prefab: never live in the scene

            var prismController = go.AddComponent<VesselPrismController>();
            SetPrivateField(prismController, "_onPrismSpawnedEventChannel", prismFactory.SpawnChannel);
            SetPrivateField(prismController, "BaseScale", TrailBaseScale);
            SetPrivateField(prismController, "initialWavelength", TrailWavelength);

            var resources = go.AddComponent<ResourceSystem>();
            // skimming trails is the energy source — passive regen is a trickle
            resources.Resources = new List<Resource> { new() { Name = "boost", resourceGainRate = 0.04f } };

            go.AddComponent<VesselTransformer>();

            var pilot = go.AddComponent<AIPilot>();
            // template default — SpawnPilot overrides each clone with its personal registry
            SetPrivateField(pilot, "cellData", courseData);
            SetPrivateField(pilot, "OnCellItemsUpdated", courseData.OnCellItemsUpdated);
            SetPrivateField(pilot, "abilities", new List<AIAbility>());
            // Prefab-authored skill spread so AIPilot.skillLevel is a real lever (the class
            // defaults are flat .6/.6, which would make seeded skill inert).
            SetPrivateField(pilot, "defaultThrottleLow", 0.55f);
            SetPrivateField(pilot, "defaultThrottleHigh", 0.8f);

            go.AddComponent<SilhouetteController>();

            var cameraCustomizer = go.AddComponent<VesselCameraCustomizer>();
            SetPrivateField(cameraCustomizer, "OnInitializePlayerCamera",
                ScriptableObject.CreateInstance<ScriptableEventTransform>());

            go.AddComponent<SkimVesselAnimation>();

            var actionHandler = go.AddComponent<R_VesselActionHandler>();
            SetPrivateField(actionHandler, "_onButtonPressed", onButtonPressed);
            SetPrivateField(actionHandler, "_onButtonReleased", onButtonReleased);
            SetPrivateField(actionHandler, "_resourceEventClassActions", new List<ResourceEventShipActionMapping>());

            var geometry = new GameObject("geometry");
            geometry.transform.SetParent(go.transform);
            var customization = go.AddComponent<VesselCustomization>();
            SetPrivateField(customization, "_shipGeometries", new List<GameObject> { geometry });

            go.AddComponent<R_ShipElementStatsHandler>();

            var hud = go.AddComponent<SkimVesselHud>();
            var controller = go.AddComponent<VesselController>();
            SetPrivateField(controller, "gameData", gameData);
            var status = go.AddComponent<VesselStatus>();

            Skimmer NewChildSkimmer(string skimmerName)
            {
                var child = new GameObject(skimmerName);
                child.transform.SetParent(go.transform);
                var skimmer = child.AddComponent<Skimmer>();
                SetPrivateField(skimmer, "onSkimmerShipImpact", ScriptableObject.CreateInstance<ScriptableEventString>());
                return skimmer;
            }

            var orientationHandle = new GameObject("orientationHandle");
            orientationHandle.transform.SetParent(go.transform);

            // Near-field skimmer = the trail-skim surface. Its Scale ElementalFloat drives
            // the child's localScale (Skimmer.ApplyScaleIfChanged) and is BOUND TO MASS —
            // the real elemental hook: Mass crystal claims grow your skim reach. The
            // trigger sphere (radius 1 × localScale) sweeps prisms; the engine TriggerPass
            // dispatches into SkimmerImpactor → the race's skim-energy effect, and the
            // SkimContactTracker mirrors live overlap state for HUD/audio.
            var nearFieldSkimmer = NewChildSkimmer("nearFieldSkimmer");
            var skimmerScale = new ElementalFloat(SkimmerReachBase) { Enabled = true };
            SetPrivateField(skimmerScale, "Min", SkimmerReachBase);
            SetPrivateField(skimmerScale, "Max", SkimmerReachAtMass10);
            SetPrivateField(skimmerScale, "element", Element.Mass);
            SetPrivateField(nearFieldSkimmer, "Scale", skimmerScale);

            var skimmerGo = nearFieldSkimmer.gameObject;
            var skimmerTrigger = skimmerGo.AddComponent<SphereCollider>();
            skimmerTrigger.isTrigger = true;
            skimmerTrigger.radius = 1f; // world reach = radius × localScale (= Scale.Value)

            var skimmerImpactor = skimmerGo.AddComponent<SkimmerImpactor>();
            SetPrivateField(skimmerImpactor, "skimmer", nearFieldSkimmer);
            SetPrivateField(skimmerImpactor, "skimmerImpactorDataContainer", skimmerImpactorContainer);

            var skimmerImpactCollider = skimmerGo.AddComponent<ImpactCollider>();
            SetPrivateField(skimmerImpactCollider, "impactorObject", skimmerImpactor);

            skimmerGo.AddComponent<SkimContactTracker>();

            // The prism controller's waitTillOutsideSkimmer math reads this reference:
            // a fresh block's collider stays off until the spawning skimmer has cleared it
            // (the real own-fresh-trail protection — lap back to skim your own ribbon).
            SetPrivateField(prismController, "skimmer", nearFieldSkimmer);

            SetPrivateField(status, "_shipInstance", controller);
            SetPrivateField(status, "vesselHUDController", hud);
            SetPrivateField(status, "orientationHandle", orientationHandle);
            SetPrivateField(status, "_name", name);
            SetPrivateField(status, "vesselType", vesselType);
            SetPrivateField(status, "_nearFieldSkimmer", nearFieldSkimmer);
            SetPrivateField(status, "_farFieldSkimmer", NewChildSkimmer("farFieldSkimmer"));

            // Contact rig (CT1): non-trigger contact bubble + impactor routing. Crystal
            // triggers pair with this collider in the engine trigger pass; the crystal's
            // OmniCrystalImpactor resolves the vessel through this ImpactCollider.
            var contactBubble = go.AddComponent<SphereCollider>();
            contactBubble.radius = SkimRaceDirector.VesselContactRadius;

            var networkVesselImpactor = go.AddComponent<NetworkVesselImpactor>();
            var vesselImpactor = go.AddComponent<VesselImpactor>();
            SetPrivateField(vesselImpactor, "vesselImpactorDataContainerSO",
                ScriptableObject.CreateInstance<VesselImpactorDataContainerSO>());
            SetPrivateField(vesselImpactor, "networkVesselImpactor", networkVesselImpactor);
            SetPrivateField(networkVesselImpactor, "vesselImpactor", vesselImpactor);

            var impactCollider = go.AddComponent<ImpactCollider>();
            SetPrivateField(impactCollider, "impactorObject", vesselImpactor);

            return go;
        }
    }
}
