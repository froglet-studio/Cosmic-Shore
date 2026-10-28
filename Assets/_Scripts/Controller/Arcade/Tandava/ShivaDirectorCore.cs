// Tandava's SHIVA DIRECTOR (Assets/_Scripts/Controller/Arcade/TANDAVA.md §0, the 2026-10-28 redesign) - pure C#: no
// UnityEngine, System.Numerics only, so the SAME file compiles and RUNS in Tools/Build/swarm_core_harness (mode `shiva`),
// where its ring layout is held to the lab's (Docs/Studios/ShivaLab/sim.js) by a parity gate.
//
// A squad of three goes into Shiva's cell to break him. He sits in the middle inside three concentric shells of danger
// prisms and cannot be touched. Each of his three FORMS has a circuit of rings that spirals in from the cell to just
// outside the lowest standing shell; the rings are lit in order and any pilot threading the lit one moves the squad on.
// Threading the last ring cracks that shell. He CLAPS: a wave sweeps out from him (the controller wipes the squad's
// prisms and rips the elements off every vessel it passes inside ClapReach), the cell collapses and grows again, and the
// next form begins with a new swarm. The forms: the STILL SHIVA; NATARAJA, whose whole circuit wheels round him while each
// ring bobs and sways; the DEVOURING MOUTH, whose jaw opens on a black hole. A drum (one clock per form) running out is
// the loss; the third shell broken is the win.
//
// The director decides only the arrangement and its clock. Vessels, the swarm, crystals, prisms and the black hole's pull
// are the game's own systems; ShivaController applies what this decides.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>How a Shiva match ends. Explicit values: replicated as an int.</summary>
    public enum ShivaOutcome
    {
        Running = 0,
        /// <summary>The third shell broke: the squad wins.</summary>
        Won = 1,
        /// <summary>A form's drum ran out: his dance ends the cell. The squad loses.</summary>
        Lost = 2,
    }

    /// <summary>What the director is doing. Explicit values: replicated as an int.</summary>
    public enum ShivaState
    {
        /// <summary>The circuit is lit and the drum runs.</summary>
        Play = 0,
        /// <summary>A shell has cracked; his hands come together.</summary>
        Clap = 1,
        /// <summary>The wave sweeps out from him to the membrane.</summary>
        Wave = 2,
        /// <summary>The new cell grows; the next form's swarm comes out when it ends.</summary>
        Regrow = 3,
        Won = 4,
        Lost = 5,
    }

    /// <summary>What happened this step, for the controller (sounds, toasts, effects). Explicit values.</summary>
    public enum ShivaEventKind
    {
        /// <summary>A = the pilot, B = the ring threaded.</summary>
        Threaded = 0,
        /// <summary>A = the form whose shell cracked.</summary>
        ShellCracked = 1,
        /// <summary>A = the form; his hands begin to come together.</summary>
        Clap = 2,
        /// <summary>The hands met: the wave leaves him.</summary>
        Wave = 3,
        /// <summary>A = the new form; the wave reached the membrane and the cell grows again.</summary>
        Regrow = 4,
        /// <summary>A = the form: its circuit is lit, its drum runs and its swarm comes out.</summary>
        Play = 5,
        Won = 6,
        /// <summary>A = the form whose drum ran out.</summary>
        Lost = 7,
    }

    public struct ShivaEvent
    {
        public ShivaEventKind Kind;
        public int A, B;
        public ShivaEvent(ShivaEventKind kind, int a = 0, int b = 0) { Kind = kind; A = a; B = b; }
    }

    /// <summary>One ring of a circuit: where it sits on the spiral (its BASE) and where it is now.</summary>
    public struct ShivaRing
    {
        public float BaseRadius, BaseTheta, BaseY, Omega, BobPhase;
        public Vector3 Centre, Normal, Velocity;
        public bool Done;
    }

    /// <summary>The director's numbers (ShivaSettingsSO carries them; the lab's SHIPPED block is the same table).</summary>
    [Serializable]
    public class ShivaDirectorSettings
    {
        public float CellRadius = 1600f;
        public float Mouth = 72f;
        public int RingsStill = 10, RingsNataraja = 12, RingsMouth = 12;
        public float RingOuter = 1350f, RingStepDegrees = 42f, RingRise = 240f, RingInset = 85f;
        public float DanceSpeed = 0.02f, DanceBob = 110f, DanceSway = 90f;
        public float ClockStill = 110f, ClockNataraja = 180f, ClockMouth = 170f;
        public float ShellOuter = 620f, ShellMiddle = 520f, ShellInner = 430f;
        public float WaveSpeed = 1100f, ClapReach = 950f, RegrowSeconds = 3f;
        public float ClapSeconds = 1.2f, HandsMeetSeconds = 0.6f, WaveStartRadius = 160f;
        public float MouthOpenRate = 0.8f, TurnRate = 0.12f;

        public int RingCount(int form) => Math.Max(1, form == 0 ? RingsStill : form == 1 ? RingsNataraja : RingsMouth);
        public float Shell(int form) => form == 0 ? ShellOuter : form == 1 ? ShellMiddle : ShellInner;
        public float Clock(int form) => form == 0 ? ClockStill : form == 1 ? ClockNataraja : ClockMouth;
    }

    /// <summary>The lab's mulberry32, bit for bit, so a seed lays the same circuit in C# and in the lab.</summary>
    public struct ShivaRandom
    {
        uint _a;
        public ShivaRandom(uint seed) { _a = seed; }
        public float Next()
        {
            unchecked
            {
                _a += 0x6D2B79F5u;
                uint t = _a;
                t = (t ^ (t >> 15)) * (1u | t);
                t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                return (float)((t ^ (t >> 14)) / 4294967296.0);
            }
        }
    }

    /// <summary>The named joints of his figure (y up; the shield's centre is the origin; he faces +z before the yaw).</summary>
    public struct ShivaJoints
    {
        public Vector3 Waist, Chest, Neck, Head, Bun, ShoulderL, ShoulderR;
        public Vector3 ElbowUL, HandUL, ElbowUR, HandUR, ElbowLL, HandLL, ElbowLR, HandLR;
        public Vector3 Mouth, ThirdEye;
        public float MouthOpen, Yaw;
    }

    /// <summary>What a serpent is doing. Explicit values: replicated as an int.</summary>
    public enum ShivaSerpentPose
    {
        /// <summary>It coils round a ring further along the circuit, across its mouth.</summary>
        Guard = 0,
        /// <summary>A pilot came near: every head rears.</summary>
        Rear = 1,
        /// <summary>It charges the pilot, every head struck at it (the danger plates are the weapon).</summary>
        Strike = 2,
    }

    /// <summary>The serpent's dials (ShivaSettingsSO carries them).</summary>
    public struct ShivaSerpentSettings
    {
        public float LungeRange, LungeCooldown, RearSeconds, StrikeSeconds, GuardOrbit, Mouth;
    }

    /// <summary>
    /// One many-headed serpent's mind (sim.js stepSerpent, the decision half - the body is the game's sort core). It
    /// HOLDS a ring further along the circuit, coiling round it across its mouth, and moves on two rings ahead once the
    /// squad has passed it. A pilot inside the lunge range draws a LUNGE: it rears (every head raised), then strikes at
    /// where the pilot will be, its danger plates the weapon, until it is close, out of time or out of reach. Then it
    /// cools down. The server runs it; every peer's body takes the goal and pose it decides.
    /// </summary>
    public sealed class ShivaSerpentBrain
    {
        public ShivaSerpentPose Pose { get; private set; }
        public int GuardRing { get; private set; } = -1;
        public int Target { get; private set; } = -1;
        public Vector3 Goal { get; private set; }
        float _stateTime, _cool, _orbit;

        public ShivaSerpentBrain(float orbitPhase, float firstCooldown) { _orbit = orbitPhase; _cool = firstCooldown; }

        /// <summary>Decide. <paramref name="index"/> / <paramref name="count"/>: this serpent among the live ones (spreads
        /// them along the circuit). <paramref name="playing"/>: false while he claps (it holds, never lunges).</summary>
        public void Step(float dt, in ShivaSerpentSettings s, Vector3 at, ShivaRing[] rings, int litRing, int index, int count,
                         ReadOnlySpan<Vector3> pilots, ReadOnlySpan<Vector3> pilotVelocities, bool playing)
        {
            _stateTime += dt;
            int n = rings?.Length ?? 0;
            if (n > 0)
            {
                if (GuardRing < 0 || GuardRing >= n) GuardRing = Math.Min(n - 1, (int)MathF.Round((index + 1) * n / (float)(count + 1)));
                if (GuardRing < litRing) GuardRing = Math.Min(n - 1, litRing + 2 + (index % 2));
            }
            int near = -1; float best = float.MaxValue;
            for (int i = 0; i < pilots.Length; i++) { float d = (pilots[i] - at).LengthSquared(); if (d < best) { best = d; near = i; } }
            float nearDist = MathF.Sqrt(best);

            if (Pose == ShivaSerpentPose.Guard)
            {
                _cool -= dt;
                _orbit += dt * 0.9f;
                if (n > 0)
                {
                    var g = rings[GuardRing];
                    var e1 = Vector3.Cross(g.Normal, new Vector3(0f, 1f, 0.01f));
                    e1 = e1.LengthSquared() > 1e-8f ? Vector3.Normalize(e1) : Vector3.UnitX;
                    var e2 = Vector3.Cross(g.Normal, e1);
                    float rr = s.Mouth * s.GuardOrbit;
                    Goal = g.Centre + e1 * (MathF.Cos(_orbit) * rr) + e2 * (MathF.Sin(_orbit) * rr);
                }
                if (near >= 0 && nearDist < s.LungeRange * 1.6f) Goal = pilots[near];
                if (playing && near >= 0 && nearDist < s.LungeRange && _cool <= 0f) { Pose = ShivaSerpentPose.Rear; _stateTime = 0f; Target = near; }
            }
            else
            {
                if (Target < 0 || Target >= pilots.Length) { EndLunge(s); return; }
                var lead = pilots[Target] + (Target < pilotVelocities.Length ? pilotVelocities[Target] * 0.35f : Vector3.Zero);
                Goal = lead;
                float d = (pilots[Target] - at).Length();
                if (Pose == ShivaSerpentPose.Rear && _stateTime > s.RearSeconds) { Pose = ShivaSerpentPose.Strike; _stateTime = 0f; }
                else if (Pose == ShivaSerpentPose.Strike && (_stateTime > s.StrikeSeconds || d < 40f || d > s.LungeRange * 1.8f || !playing)) EndLunge(s);
            }
        }

        void EndLunge(in ShivaSerpentSettings s) { Pose = ShivaSerpentPose.Guard; _stateTime = 0f; _cool = s.LungeCooldown; Target = -1; }
    }

    public sealed class ShivaDirectorCore
    {
        public const int Forms = 3;
        public readonly ShivaDirectorSettings S;
        public readonly List<ShivaEvent> Events = new();
        public readonly bool[] ShellUp = { true, true, true };
        public ShivaRing[] Rings { get; private set; }
        public int Form { get; private set; }
        public int RingIndex { get; private set; }
        public ShivaState State { get; private set; }
        public ShivaOutcome Outcome { get; private set; }
        public float Clock { get; private set; }
        public float FormTime { get; private set; }
        public float StateTime { get; private set; }
        public float DanceTime { get; private set; }
        public float ClapK { get; private set; }
        /// <summary>The wave's radius now, and where it was a step ago: everything between the two was swept this step.</summary>
        public float WaveRadius { get; private set; }
        public float WaveRadiusBefore { get; private set; }
        public float MouthOpen { get; private set; }
        public float Yaw { get; private set; }
        public float Time { get; private set; }
        public readonly float[] FormSeconds = new float[Forms], ClockLeft = new float[Forms];
        ShivaRandom _rnd;
        bool _handsMet;

        public ShivaDirectorCore(ShivaDirectorSettings settings, uint seed, Vector3 squadAt)
        {
            S = settings ?? new ShivaDirectorSettings();
            _rnd = new ShivaRandom(seed == 0 ? 1u : seed);
            Form = 0; Clock = S.Clock(0); State = ShivaState.Play;
            Rings = LayRings(S, 0, _rnd.Next() * 6.283f, ref _rnd);
            Yaw = MathF.Atan2(squadAt.X, squadAt.Z);
        }

        public Vector3 LitCentre => RingIndex < Rings.Length ? Rings[RingIndex].Centre : Vector3.Zero;
        /// <summary>The lowest shell still standing, or -1 once all three are broken.</summary>
        public int StandingShell { get { for (int k = 0; k < Forms; k++) if (ShellUp[k]) return k; return -1; } }
        public float StandingShellRadius => StandingShell >= 0 ? S.Shell(StandingShell) : 0f;
        public ShivaJoints Joints => Figure(Form, ClapK, DanceTime, MouthOpen, Yaw);

        /// <summary>Where the squad comes in: behind the first ring of the first circuit, in a line abreast.</summary>
        public Vector3 EntryPoint(int seat, int seats)
        {
            var g = Rings[0];
            var side = Vector3.Normalize(Vector3.Cross(g.Normal, Vector3.UnitY));
            return g.Centre - g.Normal * 420f + side * ((seat - (seats - 1) * 0.5f) * 60f);
        }

        // ── the step ────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>Advance the arrangement. <paramref name="squadCentroid"/> is where the squad is (he turns to face it).</summary>
        public void Step(float dt, Vector3 squadCentroid)
        {
            if (State == ShivaState.Won || State == ShivaState.Lost) { Time += dt; StateTime += dt; return; }
            Time += dt; StateTime += dt; DanceTime += dt;
            if (State == ShivaState.Play) { FormTime += dt; Clock -= dt; }

            float want = MathF.Atan2(squadCentroid.X, squadCentroid.Z), dy = want - Yaw;
            dy = MathF.Atan2(MathF.Sin(dy), MathF.Cos(dy));
            Yaw += Math.Clamp(dy, -S.TurnRate * dt, S.TurnRate * dt);

            if (Form == 1 && State == ShivaState.Play)
            {
                for (int i = 0; i < Rings.Length; i++)
                {
                    var prev = Rings[i].Centre;
                    Rings[i].Centre = DanceAt(S, Rings[i], FormTime);
                    Rings[i].Velocity = dt > 0 ? (Rings[i].Centre - prev) / dt : Vector3.Zero;
                }
                OrientRings(Rings);
            }
            float openTo = Form == 2 ? 1f : 0f;
            MouthOpen += (openTo - MouthOpen) * MathF.Min(1f, dt * S.MouthOpenRate);

            WaveRadiusBefore = WaveRadius;
            if (State == ShivaState.Play && Clock <= 0f)
            {
                Clock = 0f; ClockLeft[Form] = 0f; State = ShivaState.Lost; StateTime = 0f; Outcome = ShivaOutcome.Lost;
                Events.Add(new ShivaEvent(ShivaEventKind.Lost, Form));
                return;
            }
            if (State == ShivaState.Clap)
            {
                ClapK = Math.Clamp(StateTime / S.ClapSeconds, 0f, 1f);
                if (!_handsMet && StateTime >= S.HandsMeetSeconds) { _handsMet = true; WaveRadius = S.WaveStartRadius; Events.Add(new ShivaEvent(ShivaEventKind.Wave, Form)); }
                else if (_handsMet) WaveRadius += S.WaveSpeed * dt;
                if (StateTime >= S.ClapSeconds) { State = ShivaState.Wave; StateTime = 0f; ClapK = 0f; }
            }
            else if (State == ShivaState.Wave)
            {
                WaveRadius += S.WaveSpeed * dt;
                if (WaveRadius >= S.CellRadius + 40f)
                {
                    _handsMet = false; WaveRadius = WaveRadiusBefore = 0f;
                    Form++; FormTime = 0f; Clock = S.Clock(Form);
                    Rings = LayRings(S, Form, _rnd.Next() * 6.283f, ref _rnd); RingIndex = 0;
                    State = ShivaState.Regrow; StateTime = 0f;
                    Events.Add(new ShivaEvent(ShivaEventKind.Regrow, Form));
                }
            }
            else if (State == ShivaState.Regrow && StateTime >= S.RegrowSeconds * 0.5f)
            {
                State = ShivaState.Play; StateTime = 0f;
                Events.Add(new ShivaEvent(ShivaEventKind.Play, Form));
            }
        }

        /// <summary>A pilot moved from <paramref name="from"/> to <paramref name="to"/>: did it thread the lit ring?
        /// <paramref name="mouthScale"/> widens the mouth for that pilot (1 = the shipped mouth).</summary>
        public bool TryThread(int pilot, Vector3 from, Vector3 to, float mouthScale = 1f)
        {
            if (State != ShivaState.Play || RingIndex >= Rings.Length) return false;
            var g = Rings[RingIndex];
            float a = Vector3.Dot(from - g.Centre, g.Normal), b = Vector3.Dot(to - g.Centre, g.Normal);
            if ((a < 0f) == (b < 0f) || a == b) return false;
            var hit = Vector3.Lerp(from, to, a / (a - b));
            if ((hit - g.Centre).Length() > S.Mouth * mouthScale) return false;
            Rings[RingIndex].Done = true; RingIndex++;
            Events.Add(new ShivaEvent(ShivaEventKind.Threaded, pilot, RingIndex - 1));
            if (RingIndex >= Rings.Length) CompleteForm();
            return true;
        }

        void CompleteForm()
        {
            ShellUp[Form] = false; FormSeconds[Form] = FormTime; ClockLeft[Form] = Clock;
            Events.Add(new ShivaEvent(ShivaEventKind.ShellCracked, Form));
            if (Form == Forms - 1) { State = ShivaState.Won; StateTime = 0f; Outcome = ShivaOutcome.Won; Events.Add(new ShivaEvent(ShivaEventKind.Won)); return; }
            State = ShivaState.Clap; StateTime = 0f;
            Events.Add(new ShivaEvent(ShivaEventKind.Clap, Form));
        }

        /// <summary>True while the wave's front passed <paramref name="distance"/> from him this step.</summary>
        public bool WaveSwept(float distance) => WaveRadius > 0f && distance >= WaveRadiusBefore && distance < WaveRadius;

        // ── the circuit (sim.js layRings / orientRings / danceAt - the parity gate holds these to the lab) ─────────────
        /// <summary>A form's circuit as the director laid it from <paramref name="seed"/>: every peer replays the same draws,
        /// so a client builds the server's rings from the replicated seed and form alone.</summary>
        public static ShivaRing[] RingsForForm(ShivaDirectorSettings s, uint seed, int form)
        {
            var rnd = new ShivaRandom(seed == 0 ? 1u : seed);
            ShivaRing[] rings = null;
            for (int k = 0; k <= Math.Clamp(form, 0, Forms - 1); k++) { float theta0 = rnd.Next() * 6.283f; rings = LayRings(s, k, theta0, ref rnd); }
            return rings;
        }

        public static ShivaRing[] LayRings(ShivaDirectorSettings s, int form, float theta0, ref ShivaRandom rnd)
        {
            int n = s.RingCount(form);
            float inner = s.Shell(form) + s.RingInset, phi = rnd.Next() * MathF.PI * 2f;
            var rings = new ShivaRing[n];
            for (int i = 0; i < n; i++)
            {
                float t = n > 1 ? i / (float)(n - 1) : 1f, r = s.RingOuter + (inner - s.RingOuter) * MathF.Pow(t, 0.85f);
                float th = theta0 + i * s.RingStepDegrees * MathF.PI / 180f, y = s.RingRise * MathF.Sin(i * 0.9f + phi) * (r / s.RingOuter);
                float omega = (i % 2 == 1 ? 1f : -1f) * (0.7f + 0.6f * rnd.Next()), bob = rnd.Next() * 6.28f;
                rings[i] = new ShivaRing { BaseRadius = r, BaseTheta = th, BaseY = y, Omega = omega, BobPhase = bob, Centre = new Vector3(r * MathF.Cos(th), y, r * MathF.Sin(th)) };
            }
            OrientRings(rings);
            return rings;
        }

        public static void OrientRings(ShivaRing[] rings)
        {
            int n = rings.Length;
            for (int i = 0; i < n; i++)
            {
                if (i == n - 1) { rings[i].Normal = Vector3.Normalize(-rings[i].Centre); continue; }   // the last ring faces him
                var d = rings[Math.Min(n - 1, i + 1)].Centre - rings[Math.Max(0, i - 1)].Centre;
                rings[i].Normal = d.LengthSquared() > 1e-8f ? Vector3.Normalize(d) : Vector3.UnitX;
            }
        }

        /// <summary>Nataraja: the whole circuit wheels round him; each ring bobs and sways.</summary>
        public static Vector3 DanceAt(ShivaDirectorSettings s, in ShivaRing g, float t)
        {
            float r = g.BaseRadius + s.DanceSway * MathF.Sin(0.23f * t + g.BobPhase * 1.7f) * (g.BaseRadius / s.RingOuter), th = g.BaseTheta + s.DanceSpeed * t;
            return new Vector3(r * MathF.Cos(th), g.BaseY + s.DanceBob * MathF.Sin(0.3f * t + g.BobPhase), r * MathF.Sin(th));
        }

        // ── his figure (sim.js figure) ─────────────────────────────────────────────────────────────────────────────────
        public static ShivaJoints Figure(int form, float clapK, float danceT, float mouthOpen, float yaw)
        {
            float sway = form == 1 ? MathF.Sin(danceT * 1.3f) : 0f, sway2 = form == 1 ? MathF.Sin(danceT * 2.6f + 0.7f) : 0f;
            var j = new ShivaJoints
            {
                Waist = new(0, -320, 0), Chest = new(sway * 26, 40, 0), Neck = new(sway * 34, 168, 6), Head = new(sway * 40, 250, 10),
                Bun = new(sway * 44 + sway2 * 8, 362, -14), ShoulderL = new(-150 + sway * 22, 118, 0), ShoulderR = new(150 + sway * 22, 118, 0),
            };
            if (form == 0)
            {
                j.ElbowUL = new(-270, 232, -50); j.HandUL = new(-305, 340, -60); j.ElbowUR = new(270, 232, -50); j.HandUR = new(305, 340, -60);
                j.ElbowLL = new(-280, 10, 30); j.HandLL = new(-385, -80, 55); j.ElbowLR = new(280, 10, 30); j.HandLR = new(385, -80, 55);
            }
            else if (form == 1)
            {
                float s = sway;
                j.ElbowUR = new(262 + s * 20, 238, -10); j.HandUR = new(300 + s * 30, 352 + sway2 * 20, -10);
                j.ElbowUL = new(-262 + s * 20, 230, -10); j.HandUL = new(-305 + s * 30, 330 - sway2 * 20, -10);
                j.ElbowLR = new(235 + s * 20, 64, 90); j.HandLR = new(210 + s * 22, 92, 190);
                j.ElbowLL = new(-190 + s * 20, 46, 110); j.HandLL = new(-40 + s * 30, -78, 205);
            }
            else
            {
                float r = MathF.Sin(danceT * 0.9f) * 18f;
                j.ElbowUL = new(-290, 220, 60); j.HandUL = new(-395, 300 + r, 150); j.ElbowUR = new(290, 220, 60); j.HandUR = new(395, 300 - r, 150);
                j.ElbowLL = new(-300, 20, 90); j.HandLL = new(-395, -30 - r, 190); j.ElbowLR = new(300, 20, 90); j.HandLR = new(395, -30 + r, 190);
            }
            if (clapK > 0f)
            {
                float k = MathF.Sin(MathF.PI * Math.Clamp(clapK, 0f, 1f));
                j.HandLL = Vector3.Lerp(j.HandLL, new Vector3(-8, 70, 210), k); j.HandLR = Vector3.Lerp(j.HandLR, new Vector3(8, 70, 210), k);
                j.ElbowLL = Vector3.Lerp(j.ElbowLL, new Vector3(-200, 40, 140), k); j.ElbowLR = Vector3.Lerp(j.ElbowLR, new Vector3(200, 40, 140), k);
            }
            j.Mouth = j.Head + new Vector3(0, -34, 78);
            j.ThirdEye = j.Head + new Vector3(0, 38, 80);
            j.MouthOpen = mouthOpen; j.Yaw = yaw;
            if (yaw != 0f)
            {
                float c = MathF.Cos(yaw), sn = MathF.Sin(yaw);
                Vector3 R(Vector3 v) => new(v.X * c + v.Z * sn, v.Y, -v.X * sn + v.Z * c);
                j.Waist = R(j.Waist); j.Chest = R(j.Chest); j.Neck = R(j.Neck); j.Head = R(j.Head); j.Bun = R(j.Bun);
                j.ShoulderL = R(j.ShoulderL); j.ShoulderR = R(j.ShoulderR); j.ElbowUL = R(j.ElbowUL); j.HandUL = R(j.HandUL);
                j.ElbowUR = R(j.ElbowUR); j.HandUR = R(j.HandUR); j.ElbowLL = R(j.ElbowLL); j.HandLL = R(j.HandLL);
                j.ElbowLR = R(j.ElbowLR); j.HandLR = R(j.HandLR); j.Mouth = R(j.Mouth); j.ThirdEye = R(j.ThirdEye);
            }
            return j;
        }
    }
}
