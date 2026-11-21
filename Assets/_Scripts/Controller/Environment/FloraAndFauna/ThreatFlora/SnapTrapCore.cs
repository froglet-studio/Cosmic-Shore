// THREAT FLORA 1 - the SNAP TRAP (a Venus flytrap clump that turns to face your routes), ported from the
// research model Tools/Ecology/flora/snaptrap.py (branch cece/eco-flora, DISCOVERIES.md "Threat flora",
// "Top 2 to port" #1) with its searched best (results/search_snaptrap_best.json). Plain C# over a
// struct-of-arrays: no UnityEngine, so this file compiles and RUNS headless in
// Tools/Build/threat_flora_harness (asserted), and the glue (SnapTrapFlora) converts at the boundary.
// Docs/THREAT_FLORA.md §2 is the design record.
//
// One trap = 27 prism SLOTS (a 3-prism stalk, two 8-prism lobes, 8 danger teeth on the lips) and ONE heart
// crystal that sits INSIDE the jaws. Its life is a five-state machine, a per-plant int:
//   OPEN     lobes at the resting gape; senses any vessel inside Sense
//   PRIMING  a vessel is near: the lobes GLOW and gape wider over TPrime (the telegraph)
//   ARMED    fully open and glowing; a vessel whose PATH crosses the mouth cone fires it
//   CLOSING  the lobes sweep shut over TClose; a vessel still between them when they meet is SNAPPED
//   SHUT     digesting (TDigest after a catch, TReset otherwise), then eases back open over ReopenSeconds
// A primed trap whose vessel leaves eases back to OPEN (an un-fired telegraph is not a lie), and a trap with
// fewer than half its lobe+tooth slots cannot fire (breaking a lobe is counterplay).
//
// Nothing here moves a prism per frame. The core emits KEYFRAMES - one per state change (plus one per
// HelioReposeDegrees of heliotropic turn) - and the glue writes each prism's final transform ONCE and stamps
// the GPU flight/colour clock for the course (Docs/PRISM_ANIMATION.md: one stamp, the GPU runs the course).
//
// Mass is conserved per COLONY (the research's single `reserve`, the RHIZOME): a seeded trap brings its own
// body into it, every trap's roots and jaws feed it, lost slots are re-laid ONLY from it, and a daughter buds
// only when it holds a whole body. Every volume in and out is in the ledger (Audit()).
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>The five snap-trap states (static values - they are per-plant ints in the core).</summary>
    public enum SnapTrapState
    {
        Open = 0,
        Priming = 1,
        Armed = 2,
        Closing = 3,
        Shut = 4,
    }

    /// <summary>What the core asks the glue to do (or tells it happened). Static values.</summary>
    public enum SnapTrapEventKind
    {
        /// <summary>Lay slot Slot of Trap (the reserve is already debited; call CancelLay if it fails).</summary>
        Lay = 0,
        /// <summary>Write the trap's pose for PoseMask slots once, and stamp a flight of Duration seconds.</summary>
        Pose = 1,
        /// <summary>The trap fired (ARMED -> CLOSING).</summary>
        Fired = 2,
        /// <summary>A vessel (index Vessel) was snapped: swept by a lobe, or inside the jaws when they met.</summary>
        Snap = 3,
        /// <summary>Root feeding: look for one edible prism within Root of the heart, and Deposit it.</summary>
        AbsorbRequest = 4,
        /// <summary>The rhizome can pay a daughter: plant one at Position facing Axis (AddTrap(..., budded: true)).</summary>
        BudRequest = 5,
        /// <summary>Glow on (Glow = 1) or off (Glow = 0) over Duration seconds - the lobes' colour clock.</summary>
        Glow = 6,
        /// <summary>The jaws shut: eat edible mass caught inside the mouth (Position = mouth centre), Deposit it.</summary>
        MouthAbsorbRequest = 7,
    }

    public struct SnapTrapEvent
    {
        public SnapTrapEventKind Kind;
        public int Trap;
        public int Slot;
        public int Vessel;
        public int Glow;
        public float Duration;
        public int PoseMask;
        public Vector3 Position;
        public Vector3 Axis;
    }

    /// <summary>
    /// Every number the trap runs on. Defaults are the research's searched best
    /// (results/search_snaptrap_best.json) over its DEFAULTS (snaptrap.py) - quoted per field.
    /// </summary>
    public sealed class SnapTrapParams
    {
        public float Sense = 145.9975f;        // best: sense (u) - a vessel inside it primes the trap
        public float TPrime = 0.7308f;         // best: t_prime (s) - the telegraph: glow + gape-wide ramp
        public float TClose = 0.5245f;         // best: t_close (s) - the closing sweep
        public float TReset = 6.6876f;         // best: t_reset (s) - shut after an empty snap
        public float TDigest = 9f;             // DEFAULTS t_digest (s) - shut after a catch
        public float Gape = 35f;               // DEFAULTS gape (deg) - resting half-angle of each lobe
        public float DGape = 9.8316f;          // best: dgape (deg) - extra gape while primed
        public float ShutGape = 3f;            // snaptrap.py: shut = radians(3.0)
        public float MouthLength = 36.3478f;   // best: mouth_len (u)
        public float MouthWidth = 15.7119f;    // best: mouth_w (u) - half-width of the mouth slab
        public float MouthOffset = 22f;        // snaptrap.py: m = h + a * 22 (the hinge, from the heart)
        public float HeartInJaws = 30f;        // snaptrap.py heart_pos: h + a * 30 (the bait sits in the jaws)
        public float TurnDegrees = 6.9547f;    // best: turn_deg (deg/s) - heliotropism toward vessels
        public float BudBias = 0.3049f;        // best: bud_bias - daughters bud toward the traffic EMA
        public float Root = 90f;               // DEFAULTS root (u) - root feeding radius
        public float AbsorbEvery = 4f;         // DEFAULTS absorb_every (s)
        public float GrowSeconds = 6f;         // snaptrap.py: grow += dt / 6.0
        public float ReopenSeconds = 1.5f;     // snaptrap.py: th eases open at (gape - shut) / 1.5 per s
        public float ShutGlowFade = 0.5f;      // snaptrap.py: it[sh] decays at dt / 0.5
        public float EmaPerStep = 0.03f;       // snaptrap.py: ema = 0.97 ema + 0.03 u, at dt = 0.1
        public float EmaStepSeconds = 0.1f;    // the research step the EMA rate is quoted at
        public float BudMin = 45f, BudMax = 80f; // snaptrap.py: _sprout(h[par] + d * uniform(45, 80))
        public float BudEvery = 0.1f;          // snaptrap.py asks every step (dt 0.1) while the reserve holds a body
        public float GeometryScale = 1f;       // scales the slot layout and every mouth length together
        public float HelioReposeDegrees = 4f;  // GAME: one pose keyframe per this much heliotropic turn
        public float BiteFlightSeconds = 0.12f;// GAME: the teeth's snap at SHUT (see Docs/THREAT_FLORA.md §2.3)
        public float PathSampleSpacing = 4f;   // GAME: the trigger samples a vessel's path, not just its position
        public float HelioConeDegrees = 180f;  // GAME: heliotropism may turn a trap at most this far from the heading
                                               // it was planted with (180 = unlimited, the research). A rim grove
                                               // keeps its jaws inside the cell with it (Docs/THREAT_FLORA.md §4.2)
        // prism volumes (GAME units: the glue reads them off the actual leaf sizes; the research ran 12 / 8)
        public float StalkVolume = 12f;
        public float LobeVolume = 12f;
        public float ToothVolume = 8f;

        public SnapTrapParams Clone() => (SnapTrapParams)MemberwiseClone();

        /// <summary>
        /// The research's element variants (Tools/Ecology/flora/elements.py, snaptrap rows): TIME = the trap's own
        /// tempo x1.25; MASS = volume x1.5, mouth width x1.2, slower x1.15; SPACE = reach x1.35 at the same volume;
        /// CHARGE = armour (the platform's Charge shield, nothing in the core). 1 Charge, 2 Mass, 3 Space, 4 Time
        /// (CosmicShore.Data.Element).
        /// GAME: Space scales the WHOLE layout (lobes, teeth, mouth) by 1.35, where the research scaled mouth_len
        /// alone - there the glow was an abstract sphere, here the glowing lobes ARE the telegraph, and a mouth
        /// longer than its lobes would bite past what it shows (harness S5).
        /// </summary>
        public SnapTrapParams ForElement(int element)
        {
            var p = Clone();
            const float T = 1.25f, M = 1.5f, S = 1.35f;
            switch (element)
            {
                case 4:
                    p.TPrime /= T; p.TClose /= T; p.TReset /= T; p.AbsorbEvery /= T; p.TurnDegrees *= T;
                    break;
                case 2:
                    p.StalkVolume *= M; p.LobeVolume *= M; p.ToothVolume *= M; p.MouthWidth *= 1.2f;
                    p.TClose *= 1.15f; p.TPrime *= 1.15f;
                    break;
                case 3:
                    p.GeometryScale *= S; p.Sense *= S;
                    break;
            }
            return p;
        }
    }

    public sealed class SnapTrapCore
    {
        // ── the 27 slots (snaptrap.py _layout): kind 0 = stalk, +1 / -1 = lobe side; r along the lobe, s across ──
        public const int SlotCount = 27;
        public const int StalkSlots = 3;
        public const int PoseStalk = 1, PoseLobes = 2, PoseTeeth = 4, PoseAll = 7;

        public static readonly int[] SlotKind = new int[SlotCount];
        public static readonly float[] SlotR = new float[SlotCount];
        public static readonly float[] SlotS = new float[SlotCount];
        public static readonly bool[] SlotTooth = new bool[SlotCount];

        static SnapTrapCore()
        {
            int k = 0;
            foreach (float r in new[] { 5f, 12f, 19f }) { SlotKind[k] = 0; SlotR[k] = r; k++; }
            foreach (int side in new[] { 1, -1 })
            {
                foreach (float r in new[] { 10f, 22f, 34f, 46f })
                foreach (float s in new[] { -12f, 12f })
                { SlotKind[k] = side; SlotR[k] = r; SlotS[k] = s; k++; }
                foreach (float s in new[] { -18f, -6f, 6f, 18f })
                { SlotKind[k] = side; SlotR[k] = 53f; SlotS[k] = s; SlotTooth[k] = true; k++; }
            }
        }

        public readonly SnapTrapParams P;
        ThreatRng _rng;

        // ── per-trap SoA ───────────────────────────────────────────────────────────────────────────────────
        int _cap;
        public int Count { get; private set; }      // slots handed out (a dead trap's slot is reused)
        public Vector3[] Heart = Array.Empty<Vector3>();
        public Vector3[] Axis = Array.Empty<Vector3>();
        public Vector3[] Home = Array.Empty<Vector3>();       // the planted heading (HelioConeDegrees is about it)
        public int[] State = Array.Empty<int>();
        public float[] Timer = Array.Empty<float>();
        public float[] Theta = Array.Empty<float>();      // current lobe half-angle (radians)
        public float[] Intent = Array.Empty<float>();     // 0..1 glow
        public float[] Grow = Array.Empty<float>();       // 0..1 growth (new traps bloom over GrowSeconds)
        public bool[] Alive = Array.Empty<bool>();
        public bool[] Used = Array.Empty<bool>();
        public bool[] Caught = Array.Empty<bool>();
        public Vector3[] Ema = Array.Empty<Vector3>();
        public float[] AbsorbT = Array.Empty<float>();
        public int[] Fired = Array.Empty<int>();
        public bool[] Occupied = Array.Empty<bool>();     // [trap * SlotCount + slot]
        // keyframe state (what the glue last wrote)
        public Vector3[] PoseAxis = Array.Empty<Vector3>();
        public float[] PoseLobeTheta = Array.Empty<float>();
        public float[] PoseToothTheta = Array.Empty<float>();
        public float[] PoseToothR = Array.Empty<float>();   // teeth slide to the prey's depth on a catch
        public float[] PoseTime = Array.Empty<float>();
        public bool[] GlowOn = Array.Empty<bool>();
        float[] _caughtZ = Array.Empty<float>();

        // ── the ledger (volumes) ──────────────────────────────────────────────────────────────────────────
        public double Planted, Absorbed, Lost, SkeletonOut;

        /// <summary>
        /// The RHIZOME: one reserve for the whole clump colony (the research's colony `reserve`). Every trap's
        /// roots and jaws feed it, every slot is laid from it, and a daughter buds only when it holds a whole
        /// body. Each trap is still its own lifeform with its own crystal; the rhizome is the mass they share.
        /// </summary>
        public double Reserve;
        float _budT;
        public int Snaps, FiredTotal;

        public float Time { get; private set; }
        public readonly List<SnapTrapEvent> Events = new List<SnapTrapEvent>(256);

        public SnapTrapCore(SnapTrapParams p, ulong seed, int capacity = 64)
        {
            P = p ?? new SnapTrapParams();
            _rng = new ThreatRng(seed);
            Ensure(Math.Max(4, capacity));
        }

        // ── sizes ─────────────────────────────────────────────────────────────────────────────────────────
        public float SlotVolume(int slot) => slot < StalkSlots ? P.StalkVolume : SlotTooth[slot] ? P.ToothVolume : P.LobeVolume;

        /// <summary>A whole trap's body: the cost of a trap, and what a daughter is handed.</summary>
        public float BodyVolume => StalkSlots * P.StalkVolume + 16f * P.LobeVolume + 8f * P.ToothVolume;

        float Gape => ThreatFloraMath.Deg2Rad(P.Gape);
        float Wide => ThreatFloraMath.Deg2Rad(P.Gape + P.DGape);
        float ShutAngle => ThreatFloraMath.Deg2Rad(P.ShutGape);
        float L => P.MouthLength * P.GeometryScale;
        float W => P.MouthWidth * P.GeometryScale;

        /// <summary>
        /// The research's telegraph rule (snaptrap.py hazards): the glow must COVER the strike volume -
        /// max(0.7 L, L tan(gape + dgape)). A mouth that looks smaller than it bites is the bug the reader found.
        /// </summary>
        public float GlowRadius => MathF.Max(0.7f * L, L * MathF.Tan(Wide));

        void Ensure(int n)
        {
            if (n <= _cap) return;
            int c = Math.Max(n, _cap * 2);
            Array.Resize(ref Heart, c); Array.Resize(ref Axis, c); Array.Resize(ref Home, c); Array.Resize(ref State, c);
            Array.Resize(ref Timer, c); Array.Resize(ref Theta, c); Array.Resize(ref Intent, c);
            Array.Resize(ref Grow, c); Array.Resize(ref Alive, c); Array.Resize(ref Used, c);
            Array.Resize(ref Caught, c); Array.Resize(ref Ema, c); Array.Resize(ref AbsorbT, c);
            Array.Resize(ref Fired, c); Array.Resize(ref Occupied, c * SlotCount);
            Array.Resize(ref PoseAxis, c); Array.Resize(ref PoseLobeTheta, c); Array.Resize(ref PoseToothTheta, c);
            Array.Resize(ref PoseToothR, c); Array.Resize(ref PoseTime, c); Array.Resize(ref GlowOn, c);
            Array.Resize(ref _caughtZ, c);
            _cap = c;
        }

        // ── life ──────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Plant a trap. A SEEDED trap (budded = false) brings exactly its own body into the rhizome as planted mass,
        /// as the research plants its colony (`reserve = n_traps * cost`). A BUDDED daughter (budded = true) brings
        /// nothing: the rhizome already holds her body (the BudRequest was raised because it did). Either way she
        /// BLOOMS: slots are laid from the rhizome in order as growth passes their threshold (stalk, then each
        /// lobe hinge-out, its teeth last).
        /// </summary>
        public int AddTrap(Vector3 heart, Vector3 axis, bool budded)
        {
            int i = -1;
            for (int k = 0; k < Count; k++) if (!Used[k]) { i = k; break; }
            if (i < 0) { Ensure(Count + 1); i = Count++; }
            Used[i] = true; Alive[i] = true;
            Heart[i] = heart;
            float al = axis.Length();
            Axis[i] = al < 1e-6f ? _rng.OnUnitSphere() : axis / al;
            Home[i] = Axis[i];
            State[i] = (int)SnapTrapState.Open; Timer[i] = 0f; Theta[i] = Gape; Intent[i] = 0f;
            Grow[i] = 0f; Caught[i] = false; Ema[i] = Vector3.Zero; Fired[i] = 0;
            AbsorbT[i] = _rng.Range(0f, P.AbsorbEvery);
            for (int s = 0; s < SlotCount; s++) Occupied[i * SlotCount + s] = false;
            PoseAxis[i] = Axis[i]; PoseLobeTheta[i] = Gape; PoseToothTheta[i] = Gape; PoseToothR[i] = 53f;
            PoseTime[i] = Time; GlowOn[i] = false; _caughtZ[i] = 0f;
            if (!budded)
            {
                Planted += BodyVolume;
                Reserve += BodyVolume;
            }
            LayUpTo(i, 0f);
            return i;
        }

        /// <summary>
        /// Lay the whole body at once from the reserve - the research's initial colony (`_sprout(..., grown=True)`).
        /// The game never calls it (everything blooms - the continuity law); the harness does, to start where the
        /// research started.
        /// </summary>
        public void CompleteGrowth(int i)
        {
            if (i < 0 || i >= Count || !Alive[i]) return;
            Grow[i] = 1f;
            LayUpTo(i, 1f);
        }

        /// <summary>
        /// The heart was taken (a joust): the trap dies. Its laid body stays as an inert skeleton (cell mass: it
        /// leaves this ledger as SkeletonOut); the rhizome lives on in the other traps.
        /// </summary>
        public void Kill(int i)
        {
            if (i < 0 || i >= Count || !Alive[i]) return;
            Alive[i] = false;
            for (int s = 0; s < SlotCount; s++)
            {
                int o = i * SlotCount + s;
                if (!Occupied[o]) continue;
                Occupied[o] = false;
                SkeletonOut += SlotVolume(s);
            }
            Intent[i] = 0f;
        }

        /// <summary>Forget a dead trap's slot entirely (the glue destroyed the GameObject).</summary>
        public void Release(int i)
        {
            if (i < 0 || i >= Count) return;
            if (Alive[i]) Kill(i);
            Used[i] = false;
        }

        /// <summary>A laid slot's prism left by an active force (rammed, shot, eaten, consumed): its volume leaves
        /// the trap (the force's ledger owns it now). The slot is re-laid only from the reserve.</summary>
        public void SlotLost(int i, int slot)
        {
            if (i < 0 || i >= Count || slot < 0 || slot >= SlotCount) return;
            int o = i * SlotCount + slot;
            if (!Occupied[o]) return;
            Occupied[o] = false;
            Lost += SlotVolume(slot);
        }

        /// <summary>The glue could not lay a slot it was asked to (pool empty): refund it.</summary>
        public void CancelLay(int i, int slot)
        {
            int o = i * SlotCount + slot;
            if (i < 0 || i >= Count || !Occupied[o]) return;
            Occupied[o] = false;
            Reserve += SlotVolume(slot);
        }

        /// <summary>Food eaten by this trap (roots or jaws). The volume is the eaten prism's own.</summary>
        public void Deposit(int i, float volume)
        {
            if (i < 0 || i >= Count || !Alive[i] || volume <= 0f) return;
            Reserve += volume;
            Absorbed += volume;
            // snaptrap.py: absorb, then repair rammed / cut slots from the reserve (Lay events append to Events,
            // so a caller walking Events by index sees them this tick)
            if (Grow[i] >= 1f) LayUpTo(i, 1f);
        }

        public int LiveCount
        {
            get { int n = 0; for (int i = 0; i < Count; i++) if (Alive[i]) n++; return n; }
        }

        public int LaidSlots(int i)
        {
            int n = 0;
            for (int s = 0; s < SlotCount; s++) if (Occupied[i * SlotCount + s]) n++;
            return n;
        }

        public double LaidVolume()
        {
            double v = 0;
            for (int i = 0; i < Count; i++)
                for (int s = 0; s < SlotCount; s++)
                    if (Occupied[i * SlotCount + s]) v += SlotVolume(s);
            return v;
        }

        /// <summary>
        /// The mass audit: planted + absorbed - lost - skeleton == rhizome + laid. Returns the
        /// residual (0 to rounding); the research's audit closed to 1e-16 relative on every run.
        /// </summary>
        public double Audit() => (Planted + Absorbed - Lost - SkeletonOut) - (Reserve + LaidVolume());

        /// <summary>A trap with fewer than half its lobe + tooth slots cannot fire (snaptrap.py `intact`).</summary>
        public bool Intact(int i)
        {
            int n = 0;
            for (int s = StalkSlots; s < SlotCount; s++) if (Occupied[i * SlotCount + s]) n++;
            return n * 2 >= SlotCount - StalkSlots;
        }

        public Vector3 MouthCentre(int i) => Heart[i] + Axis[i] * (P.MouthOffset * P.GeometryScale);

        public Vector3 HeartCrystal(int i) => Heart[i] + Axis[i] * (P.HeartInJaws * P.GeometryScale);

        /// <summary>The heart crystal at the LAST KEYFRAME's axis - where the glue last put it.</summary>
        public Vector3 HeartCrystalPosed(int i) => Heart[i] + PoseAxis[i] * (P.HeartInJaws * P.GeometryScale);

        // ── poses ─────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Slot pose at the LAST KEYFRAME (what the glue wrote): position, the prism's forward and up.
        /// Stalk prisms run up the axis; lobe plates lie in their lobe's plane (forward along the lobe);
        /// teeth point across the mouth, toward the other lip.
        /// </summary>
        public void PosedSlot(int i, int slot, out Vector3 pos, out Vector3 forward, out Vector3 up)
        {
            bool tooth = SlotTooth[slot];
            float th = tooth ? PoseToothTheta[i] : PoseLobeTheta[i];
            float r = tooth ? PoseToothR[i] : SlotR[slot];
            SlotPose(Heart[i], PoseAxis[i], th, slot, r, out pos, out forward, out up);
        }

        /// <summary>Slot pose for an arbitrary axis / half-angle (snaptrap.py _pose).</summary>
        public void SlotPose(Vector3 heart, Vector3 a, float theta, int slot, float r, out Vector3 pos,
            out Vector3 forward, out Vector3 up)
        {
            ThreatFloraMath.Frame(a, out Vector3 n, out Vector3 b);
            float g = P.GeometryScale;
            int kind = SlotKind[slot];
            if (kind == 0)
            {
                pos = heart + a * (SlotR[slot] * g);
                forward = a;
                up = n;
                return;
            }
            Vector3 m = heart + a * (P.MouthOffset * g);
            float c = MathF.Cos(theta), s = MathF.Sin(theta);
            Vector3 u = c * a + kind * s * n;
            pos = m + u * (r * g) + b * (SlotS[slot] * g);
            if (SlotTooth[slot])
            {
                forward = s * a - kind * c * n;    // in the lobe plane, perpendicular to it, toward the far lip
                up = b;
            }
            else
            {
                forward = u;
                up = Vector3.Normalize(Vector3.Cross(u, b)); // the plate's normal
            }
        }

        void Keyframe(int i, int mask, float duration)
        {
            if (mask == 0) return;
            PoseAxis[i] = Axis[i];
            PoseTime[i] = Time;
            Events.Add(new SnapTrapEvent { Kind = SnapTrapEventKind.Pose, Trap = i, PoseMask = mask, Duration = duration });
        }

        void SetGlow(int i, bool on, float duration)
        {
            if (GlowOn[i] == on) return;
            GlowOn[i] = on;
            Events.Add(new SnapTrapEvent { Kind = SnapTrapEventKind.Glow, Trap = i, Glow = on ? 1 : 0, Duration = duration });
        }

        // ── growth ────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>snaptrap.py _lay_upto: slots are laid in order as growth g passes k / K, each debiting the reserve.</summary>
        void LayUpTo(int i, float g)
        {
            for (int s = 0; s < SlotCount; s++)
            {
                int o = i * SlotCount + s;
                if (Occupied[o] || (float)s / SlotCount > g) continue;
                float v = SlotVolume(s);
                if (Reserve < v) return;
                Reserve -= v;
                Occupied[o] = true;
                Events.Add(new SnapTrapEvent { Kind = SnapTrapEventKind.Lay, Trap = i, Slot = s });
            }
        }

        // ── the step ──────────────────────────────────────────────────────────────────────────────────────

        /// <summary>One tick. Vessels are the sensed pilots this tick (position + previous position).</summary>
        public void Step(float dt, ThreatVessel[] vessels, int vesselCount)
        {
            Events.Clear();
            Time += dt;
            float gape = Gape, wide = Wide, shut = ShutAngle, Lm = L, Wm = W;
            float emaK = 1f - MathF.Pow(1f - P.EmaPerStep, dt / P.EmaStepSeconds);
            float body = BodyVolume;

            // roots first, for every trap (snaptrap.py: the absorb loop runs before the bud check)
            for (int i = 0; i < Count; i++)
            {
                if (!Alive[i]) continue;
                if (Grow[i] < 1f)
                {
                    Grow[i] = Math.Min(1f, Grow[i] + dt / P.GrowSeconds);
                    LayUpTo(i, Grow[i]);
                    continue;
                }
                AbsorbT[i] -= dt;
                if (AbsorbT[i] <= 0f)
                {
                    AbsorbT[i] = P.AbsorbEvery * _rng.Range(0.8f, 1.2f);
                    Events.Add(new SnapTrapEvent { Kind = SnapTrapEventKind.AbsorbRequest, Trap = i, Position = Heart[i] });
                    LayUpTo(i, 1f);   // repair rammed / cut slots from the rhizome (never from nothing)
                }
            }
            // budding: while the rhizome holds a whole body, a uniformly drawn living parent buds a daughter toward
            // its traffic EMA (snaptrap.py: d = normal + bud_bias * 3 * ema[par]; at uniform(45, 80))
            _budT -= dt;
            if (_budT <= 0f && Reserve >= body)
            {
                _budT = P.BudEvery;
                int live = LiveCount;
                if (live > 0)
                {
                    int pick = _rng.Range(0, live), par = -1;
                    for (int i = 0, n = 0; i < Count; i++) if (Alive[i] && n++ == pick) { par = i; break; }
                    Vector3 d = _rng.OnUnitSphere() + P.BudBias * 3f * Ema[par];
                    float dl = d.Length();
                    d = dl < 1e-6f ? _rng.OnUnitSphere() : d / dl;
                    Events.Add(new SnapTrapEvent
                    {
                        Kind = SnapTrapEventKind.BudRequest, Trap = par,
                        Position = Heart[par] + d * (_rng.Range(P.BudMin, P.BudMax) * P.GeometryScale),
                        Axis = _rng.OnUnitSphere(),
                    });
                }
            }

            for (int i = 0; i < Count; i++)
            {
                if (!Alive[i]) continue;

                if (Grow[i] < 1f) continue;

                // ── sensing ──
                Vector3 a = Axis[i];
                ThreatFloraMath.Frame(a, out Vector3 n, out Vector3 b);
                Vector3 m = Heart[i] + a * (P.MouthOffset * P.GeometryScale);
                bool near = false, inMouth = false;
                float th0 = Theta[i];
                for (int v = 0; v < vesselCount; v++)
                {
                    Vector3 d = vessels[v].Position - m;
                    float dist = d.Length();
                    if (dist < P.Sense)
                    {
                        near = true;
                        Vector3 u = d / Math.Max(dist, 1e-6f);
                        Ema[i] = (1f - emaK) * Ema[i] + emaK * u;
                    }
                    // the trigger: the vessel's PATH this tick crosses the mouth cone (sampled every PathSampleSpacing)
                    if (!inMouth && dist < P.Sense + 200f)
                        inMouth = PathCrossesMouth(vessels[v].Previous, vessels[v].Position, m, a, n, b, th0, Lm, Wm);
                }

                // ── the state machine (snaptrap.py step, per trap) ──
                int st = State[i];
                float tm = Timer[i] + dt;
                float th = th0, it = Intent[i];
                int stBefore = st;
                if (st == (int)SnapTrapState.Open && near) { st = (int)SnapTrapState.Priming; tm = 0f; }
                if (st == (int)SnapTrapState.Priming)
                {
                    it = Math.Min(1f, it + dt / P.TPrime);
                    th = gape + (wide - gape) * it;
                    if (!near)
                    {
                        it = Math.Max(0f, it - 2f * dt / P.TPrime);
                        if (it <= 0f) st = (int)SnapTrapState.Open;
                    }
                    else if (it >= 1f) st = (int)SnapTrapState.Armed;
                }
                // sequential, as the research's vectorised masks are: a trap armed this tick can fire this tick
                if (st == (int)SnapTrapState.Armed)
                {
                    if (!near) st = (int)SnapTrapState.Priming;
                    else if (inMouth && Intact(i))
                    {
                        st = (int)SnapTrapState.Closing; tm = 0f;
                        Fired[i]++; FiredTotal++;
                        Events.Add(new SnapTrapEvent { Kind = SnapTrapEventKind.Fired, Trap = i, Position = m });
                    }
                }
                if (st == (int)SnapTrapState.Closing)
                {
                    float thPrev = th;
                    th = Math.Max(shut, wide - (wide - shut) * tm / P.TClose);
                    it = 1f;
                    bool done = tm >= P.TClose;
                    // the sweep: a lobe passing a vessel this tick, or a vessel still inside when the jaws meet
                    for (int v = 0; v < vesselCount && !Caught[i]; v++)
                    {
                        Vector3 d = vessels[v].Position - m;
                        float z = Vector3.Dot(d, a), y = MathF.Abs(Vector3.Dot(d, n)), w = MathF.Abs(Vector3.Dot(d, b));
                        bool inSlab = z > 0f && z < Lm + 6f && w < Wm + 6f;
                        bool swept = inSlab && y < z * MathF.Tan(thPrev) + 6f && y > z * MathF.Tan(th) - 6f;
                        bool caught = done && inSlab && y < z * MathF.Tan(th) + 8f;
                        if (!(swept || caught)) continue;
                        Caught[i] = true;
                        _caughtZ[i] = Math.Clamp(z, 6f, Lm);
                        Snaps++;
                        Events.Add(new SnapTrapEvent { Kind = SnapTrapEventKind.Snap, Trap = i, Vessel = v, Position = vessels[v].Position });
                    }
                    if (done) { st = (int)SnapTrapState.Shut; tm = 0f; }
                }
                if (st == (int)SnapTrapState.Shut)
                {
                    float reset = Caught[i] ? P.TDigest : P.TReset;
                    it = Math.Max(0f, it - dt / P.ShutGlowFade);
                    if (tm > reset)
                    {
                        th = Math.Min(gape, th + dt * (gape - shut) / P.ReopenSeconds);
                        if (th >= gape - 1e-6f) { st = (int)SnapTrapState.Open; Caught[i] = false; }
                    }
                    else th = shut;
                }

                // ── keyframes: one per state change (the GPU runs each course) ──
                if (st != stBefore) OnTransition(i, stBefore, st, m);
                else if (st == (int)SnapTrapState.Shut && tm > (Caught[i] ? P.TDigest : P.TReset)
                         && PoseLobeTheta[i] < gape - 1e-4f)
                {
                    // the reopen starts (still SHUT while the lobes ease open)
                    PoseLobeTheta[i] = gape; PoseToothTheta[i] = gape; PoseToothR[i] = 53f;
                    Keyframe(i, PoseAll, P.ReopenSeconds);
                }
                State[i] = st; Timer[i] = tm; Theta[i] = th; Intent[i] = it;

                // ── heliotropism toward traffic (snaptrap.py), keyframed every HelioReposeDegrees ──
                float ne = Ema[i].Length();
                if (ne >= 0.05f && st != (int)SnapTrapState.Closing)
                {
                    Axis[i] = ThreatFloraMath.TurnToward(Axis[i], Ema[i] / ne, ThreatFloraMath.Deg2Rad(P.TurnDegrees) * dt);
                    if (P.HelioConeDegrees < 180f)
                    {
                        float cone = ThreatFloraMath.Deg2Rad(P.HelioConeDegrees);
                        if (Vector3.Dot(Axis[i], Home[i]) < MathF.Cos(cone))
                            Axis[i] = ThreatFloraMath.TurnToward(Home[i], Axis[i], cone);
                    }
                }
                if (st != (int)SnapTrapState.Closing && st != (int)SnapTrapState.Shut)
                {
                    float turned = MathF.Acos(Math.Clamp(Vector3.Dot(Axis[i], PoseAxis[i]), -1f, 1f));
                    if (turned >= ThreatFloraMath.Deg2Rad(P.HelioReposeDegrees))
                    {
                        PoseAxis[i] = Axis[i];
                        Keyframe(i, PoseAll, Math.Clamp(Time - PoseTime[i], 0.2f, 2f));
                    }
                }
            }
        }

        void OnTransition(int i, int from, int to, Vector3 mouth)
        {
            float gape = Gape, wide = Wide, shut = ShutAngle;
            PoseAxis[i] = Axis[i];
            switch ((SnapTrapState)to)
            {
                case SnapTrapState.Priming when from == (int)SnapTrapState.Open:
                    // the telegraph: lobes and teeth gape wide while the lobes glow, over TPrime
                    PoseLobeTheta[i] = wide; PoseToothTheta[i] = wide; PoseToothR[i] = 53f;
                    Keyframe(i, PoseAll, P.TPrime);
                    SetGlow(i, true, P.TPrime);
                    break;
                case SnapTrapState.Open when from == (int)SnapTrapState.Priming:
                    // the vessel left: ease back (the research relaxes at twice the priming rate)
                    PoseLobeTheta[i] = gape; PoseToothTheta[i] = gape;
                    Keyframe(i, PoseAll, P.TPrime * 0.5f);
                    SetGlow(i, false, P.TPrime * 0.5f);
                    break;
                case SnapTrapState.Closing:
                    // the LOBES sweep shut; the toothed lips hold until the jaws meet (Docs/THREAT_FLORA.md §2.3)
                    PoseLobeTheta[i] = shut;
                    Keyframe(i, PoseAll, P.TClose);   // teeth keep their (wide) angle: only the lobes move
                    break;
                case SnapTrapState.Shut:
                    // the bite: the teeth snap shut - onto the prey's depth when something was caught - and the
                    // jaws digest what is inside the mouth
                    PoseToothTheta[i] = shut;
                    PoseToothR[i] = Caught[i] ? Math.Clamp(_caughtZ[i] / MathF.Cos(shut), 10f, 53f) : 53f;
                    Keyframe(i, PoseAll, P.BiteFlightSeconds); // lobes already shut: only the teeth move
                    SetGlow(i, false, P.ShutGlowFade);
                    Events.Add(new SnapTrapEvent { Kind = SnapTrapEventKind.MouthAbsorbRequest, Trap = i, Position = mouth });
                    break;
                case SnapTrapState.Open when from == (int)SnapTrapState.Shut:
                    // reopen finished: the lobes were already keyframed open at the start of the ease
                    break;
            }
        }

        /// <summary>True when the segment a->b passes through the mouth cone (snaptrap.py `in_mouth`, sampled).</summary>
        bool PathCrossesMouth(Vector3 from, Vector3 to, Vector3 m, Vector3 a, Vector3 n, Vector3 b, float th,
            float len, float width)
        {
            float seg = (to - from).Length();
            int k = Math.Max(1, (int)(seg / Math.Max(P.PathSampleSpacing, 0.5f)) + 1);
            float tan = MathF.Tan(th);
            for (int s = 0; s <= k; s++)
            {
                Vector3 p = Vector3.Lerp(from, to, (float)s / k) - m;
                float z = Vector3.Dot(p, a);
                if (z <= 0f || z >= len) continue;
                if (MathF.Abs(Vector3.Dot(p, b)) >= width) continue;
                if (MathF.Abs(Vector3.Dot(p, n)) < z * tan + 6f) return true;
            }
            return false;
        }

        /// <summary>
        /// The strike volume's farthest point from the mouth centre at full gape: the corners of the mouth slab
        /// (z = L, |w| = W, |y| = L tan(wide) + 6). The harness asserts the glowing lobes cover it.
        /// </summary>
        public float StrikeExtent
        {
            get
            {
                float y = L * MathF.Tan(Wide) + 6f;
                return MathF.Sqrt(L * L + W * W + y * y);
            }
        }
    }
}
