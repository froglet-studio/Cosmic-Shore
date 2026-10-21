using System;
using System.Collections.Generic;
using V3 = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>The numbers <see cref="OrcaCrewCore"/> and its parts read, from <see cref="OrcaCrewConfigSO"/>. A plain struct
    /// so the core and its tests need no ScriptableObject. Every field names the Crew of Four Lab key it came from.</summary>
    public struct OrcaCrewSettings
    {
        /// <summary>lab hullRadius: the hull's contact radius (u); the mounts are placed in multiples of it.</summary>
        public float HullRadius;
        /// <summary>lab mountSmooth: seconds for the outrigger frame to follow the hull's turns.</summary>
        public float MountSmooth;
        /// <summary>lab horizonSmooth: seconds for a turret view's held horizon to drift back to the hull's up.</summary>
        public float HorizonSmooth;
        /// <summary>lab turretTurn: the AI's turret slew (deg/s).</summary>
        public float TurretTurnDeg;
        /// <summary>lab missileSpeed (u/s) and missileTurn (deg/s), at Charge 0.</summary>
        public float MissileSpeed, MissileTurnDeg;
        /// <summary>lab missileLife: a missile bursts this long after launch (s).</summary>
        public float MissileLife;
        /// <summary>lab launchCd: a turret's reload after its missile ends (s).</summary>
        public float LaunchCooldown;
        /// <summary>lab maxMissiles / omniMissiles: each gunner seat's own magazine (round 5).</summary>
        public int MaxMissiles, OmniMissiles;
        /// <summary>lab shieldBlocks, shieldOpenDeg, shieldMinR, shieldMaxR.</summary>
        public int ShieldBlocks;
        public float ShieldOpenDeg, ShieldMinRadius, ShieldMaxRadius;
    }

    /// <summary>One AI crew skill (the lab's SKILL rows). Inputs only: an AI seat writes the same input a person would.</summary>
    public struct OrcaCrewSkill
    {
        /// <summary>Turret aim noise (deg), lead fraction (0 none, 1 full), stick noise, seconds before firing once
        /// aligned, how late the helm raises the shield (0 in time, 1 late), and the share of the blast radius a missile
        /// closes to before it bursts on a hull.</summary>
        public float AimNoiseDeg, Lead, SteerNoise, ReactSeconds, ShieldLate, Detonate;

        public static readonly OrcaCrewSkill Easy = new() { AimNoiseDeg = 9f, Lead = 0f, SteerNoise = 0.3f, ReactSeconds = 1.2f, ShieldLate = 0.6f, Detonate = 0.3f };
        public static readonly OrcaCrewSkill Medium = new() { AimNoiseDeg = 5f, Lead = 0.5f, SteerNoise = 0.15f, ReactSeconds = 0.6f, ShieldLate = 0.3f, Detonate = 0.5f };
        public static readonly OrcaCrewSkill Hard = new() { AimNoiseDeg = 2.5f, Lead = 1f, SteerNoise = 0.05f, ReactSeconds = 0.25f, ShieldLate = 0f, Detonate = 0.65f };

        public static OrcaCrewSkill For(int level) => level <= 0 ? Easy : level == 1 ? Medium : Hard;
    }

    /// <summary>A body's orientation as forward and up (world). Right is <c>Cross(U, F)</c>: Unity's left-handed frame, so
    /// local x is starboard, y up, z forward, as the lab's <c>local</c> / <c>toWorld</c>.</summary>
    public struct OrcaFrame
    {
        public V3 F, U;
        public OrcaFrame(V3 f, V3 u) { F = f; U = u; }

        public V3 Right => V3.Normalize(V3.Cross(U, F));
        public V3 ToWorld(V3 l) { var r = Right; return r * l.X + U * l.Y + F * l.Z; }
        public V3 Local(V3 w) { var r = Right; return new V3(V3.Dot(w, r), V3.Dot(w, U), V3.Dot(w, F)); }

        /// <summary>The lab's <c>turnBody</c>: yaw turns F toward Right, pitch turns F toward U, roll turns U toward Right
        /// (radians), then the frame is re-orthonormalized.</summary>
        public void Turn(float yaw, float pitch, float roll)
        {
            V3 f = F, u = U, r = V3.Normalize(V3.Cross(u, f));
            if (yaw != 0f) { float c = MathF.Cos(yaw), s = MathF.Sin(yaw); f = V3.Normalize(f * c + r * s); }
            if (pitch != 0f) { float c = MathF.Cos(pitch), s = MathF.Sin(pitch); var nf = V3.Normalize(f * c + u * s); u = V3.Normalize(u * c - f * s); f = nf; }
            if (roll != 0f) { r = V3.Normalize(V3.Cross(u, f)); float c = MathF.Cos(roll), s = MathF.Sin(roll); u = V3.Normalize(u * c + r * s); }
            r = V3.Normalize(V3.Cross(u, f)); u = V3.Normalize(V3.Cross(f, r));
            F = f; U = u;
        }

        /// <summary>The lab's <c>followFrame</c>: ease toward <paramref name="target"/> with time constant
        /// <paramref name="tau"/> (s), then re-orthonormalize. The outrigger mounts ride this lagged copy of the hull.</summary>
        public void Follow(OrcaFrame target, float dt, float tau)
        {
            float k = 1f - MathF.Exp(-dt / MathF.Max(0.005f, tau));
            var f = V3.Normalize(F + (target.F - F) * k);
            var u = U + (target.U - U) * k;
            u = V3.Normalize(u - f * V3.Dot(u, f));
            F = f; U = u;
        }
    }

    /// <summary>
    /// The 4-seat vessel's crew rules that need no engine (Crew of Four Lab round 4, <c>Docs/Studios/CREW_OF_FOUR_LAB.md</c>
    /// §4): the stabilized turret, the flown missile, the shared magazine, the shield's shape and the AI seats' choices.
    /// Ported line for line from the lab's pure functions (float, where the lab is float64) and held to the page by
    /// <c>Tools/Build/orca_crew_lab/parity.cjs</c>. It knows nothing about prisms, hulls or cameras: <see cref="OrcaCrew"/>
    /// is the glue to the scene.
    /// </summary>
    public static class OrcaCrewCore
    {
        public const float Deg2Rad = MathF.PI / 180f;
        /// <summary>The lab's outrigger mounts, in hull radii (x starboard, y up, z forward): gunner 1 on a dorsal mast,
        /// gunners 2 and 3 on the port and starboard booms. Index 0 is the helm and has no turret.</summary>
        public static readonly V3[] Mounts = { V3.Zero, new(0f, 1.8f, -0.2f), new(-2.4f, 0.35f, -0.5f), new(2.4f, 0.35f, -0.5f) };
        /// <summary>A person's Release pressed during the reload is held this long (s); an AI's for one step.</summary>
        public const float ReleaseBuffer = 0.5f;
        /// <summary>A second Release bursts a flown missile only once it has flown this long (s).</summary>
        public const float MinBurstAge = 0.25f;
        /// <summary>Missiles launch this far ahead of the mount, along the aim (u).</summary>
        public const float LaunchStandoff = 3f;

        /// <summary>The lab's <c>mul</c>: an element's effect at <paramref name="level"/> (-5..15) is 1 + k × level / 15.</summary>
        public static float ElementMul(float level, float k) => 1f + k * level / 15f;

        /// <summary>The lab's <c>mountPos</c>: where turret <paramref name="seat"/> (1..3) sits in the world.</summary>
        public static V3 MountPosition(V3 hullPos, OrcaFrame pod, int seat, float hullRadius) => hullPos + pod.ToWorld(Mounts[seat] * hullRadius);

        /// <summary>The lab's <c>rotateToward</c>: turn unit vector <paramref name="cur"/> toward <paramref name="want"/> by at
        /// most <paramref name="maxAngle"/> radians.</summary>
        public static V3 RotateToward(V3 cur, V3 want, float maxAngle)
        {
            float cc = Math.Clamp(V3.Dot(cur, want), -1f, 1f), a = MathF.Acos(cc);
            if (a <= maxAngle || a < 1e-6f) return want;
            var perp = want - cur * cc;
            if (perp.Length() < 1e-6f) perp = V3.Cross(cur, V3.UnitY);
            perp = V3.Normalize(perp);
            return V3.Normalize(cur * MathF.Cos(maxAngle) + perp * MathF.Sin(maxAngle));
        }

        /// <summary>The lab's <c>steerToward</c>: the stick (yaw, pitch in -1..1, and a wings-levelling roll) an AI pushes to
        /// point <paramref name="body"/> along <paramref name="dir"/>. <paramref name="noise"/> is a pre-drawn Gaussian × the
        /// skill's stick noise (0 for none), so the core stays deterministic.</summary>
        public static (float yaw, float pitch, float roll) SteerToward(OrcaFrame body, V3 dir, float noise)
        {
            var r = body.Right;
            float dr = V3.Dot(dir, r), du = V3.Dot(dir, body.U), df = V3.Dot(dir, body.F);
            float yaw = MathF.Atan2(dr, df) / (35f * Deg2Rad), pitch = MathF.Atan2(du, MathF.Max(df, 0.05f)) / (35f * Deg2Rad);
            if (df < 0f && MathF.Abs(dr) < 0.15f) yaw = 1f;
            float roll = MathF.Abs(pitch) < 0.6f ? Math.Clamp(2f * V3.Dot(r, V3.UnitY), -1f, 1f) : 0f;
            return (Math.Clamp(yaw + noise, -1f, 1f), Math.Clamp(pitch + noise * 0.6f, -1f, 1f), roll);
        }

        /// <summary>The lab's <c>raiseShield</c> shape: <paramref name="count"/> directions (hull-local, unit) on a Fibonacci
        /// sphere with the forward cone of half-angle <paramref name="openDeg"/> left open. The helm's danger prisms stand on
        /// these, each at a radius between the shield's min and max.</summary>
        public static void ShieldDirections(int count, float openDeg, List<V3> into)
        {
            into.Clear();
            float open = MathF.Cos(openDeg * Deg2Rad);
            for (int k = 0; k < count * 2 && into.Count < count; k++)
            {
                float y = 1f - 2f * (k + 0.5f) / (count * 2), rr = MathF.Sqrt(1f - y * y), th = k * 2.39996f;
                var d = new V3(MathF.Cos(th) * rr, y, MathF.Sin(th) * rr);
                if (d.Z > open) continue;
                into.Add(d);
            }
        }

        /// <summary>The lab's helm shield trigger: how many rival missiles are closing on the hull inside the warning range a
        /// helm of this <paramref name="shieldLate"/> notices.</summary>
        public static bool IsIncoming(V3 hullPos, V3 missilePos, V3 missileForward, float shieldLate)
        {
            var rel = hullPos - missilePos;
            float d = rel.Length();
            return d < 160f + 120f * (1f - shieldLate) && V3.Dot(rel, missileForward) > d * 0.6f;
        }

        /// <summary>The lab's gunner aim: the unit direction from <paramref name="from"/> to a target led by
        /// <paramref name="lead"/> × its flight time at <paramref name="missileSpeed"/>, plus a small aim error.</summary>
        public static V3 LeadAim(V3 from, V3 leadFrom, V3 targetPos, V3 targetVel, float missileSpeed, float lead, V3 noise)
        {
            float tt = V3.Distance(leadFrom, targetPos) / MathF.Max(1f, missileSpeed) * lead;
            return V3.Normalize(V3.Normalize(targetPos + targetVel * tt - from) + noise);
        }
    }

    /// <summary>One gyro-stabilized turret (lab round 4). Its aim lives in the world, so the hull's turns never swing it; its
    /// view holds its own horizon and eases it back to the hull's up over the horizon time.</summary>
    public sealed class OrcaTurret
    {
        public V3 Aim = V3.UnitZ;
        public V3 Up = V3.UnitY;
        /// <summary>Reload left (s), a buffered Release (s), seconds the AI has held aim, the AI's current aim error.</summary>
        public float Cooldown, Queued, React;
        public V3 Noise;
        /// <summary>Seconds this turret has waited with nothing to fire (empty magazine or no target).</summary>
        public float Wait;

        public void Reset(OrcaFrame hull, V3 startAim)
        {
            Aim = V3.Normalize(startAim);
            Up = hull.U;
            Cooldown = Queued = React = Wait = 0f;
            Noise = V3.Zero;
        }

        /// <summary>A person turns the aim about the view's own (held) horizon: <paramref name="az"/> right,
        /// <paramref name="el"/> up (radians).</summary>
        public void TurnBy(float az, float el)
        {
            if (az == 0f && el == 0f) return;
            var f = new OrcaFrame(Aim, Up);
            f.Turn(az, el, 0f);
            Aim = f.F; Up = f.U;
        }

        /// <summary>The AI slews the aim toward a world direction at the turret rate.</summary>
        public void SlewToward(V3 want, float dt, float turnDeg) => Aim = OrcaCrewCore.RotateToward(Aim, want, turnDeg * OrcaCrewCore.Deg2Rad * dt);

        /// <summary>Keep the view's up square to the aim, hold it still, and ease it toward <paramref name="hullUp"/>.
        /// Returns how far the view's horizon moved this step (deg): the sway nobody steered.</summary>
        public float HoldHorizon(V3 hullUp, float dt, float horizonSmooth)
        {
            var up = Up - Aim * V3.Dot(Up, Aim);
            if (up.Length() < 1e-3f) up = hullUp - Aim * V3.Dot(hullUp, Aim);
            var hu = hullUp - Aim * V3.Dot(hullUp, Aim);
            float k = 1f - MathF.Exp(-dt / MathF.Max(0.05f, horizonSmooth));
            var held = V3.Normalize(up);
            if (hu.Length() > 1e-3f) up = held + (V3.Normalize(hu) - held) * k;
            Up = V3.Normalize(up);
            // atan2 of |cross| and dot, not acos: in float a step's tiny turn rounds acos(dot) to zero (the lab is float64)
            return MathF.Atan2(V3.Cross(held, Up).Length(), V3.Dot(held, Up)) / OrcaCrewCore.Deg2Rad;
        }

        /// <summary>Queue a Release: a person's is held <see cref="OrcaCrewCore.ReleaseBuffer"/> across the reload, an AI's
        /// for one step.</summary>
        public void Release(bool human, float dt) => Queued = human ? OrcaCrewCore.ReleaseBuffer : dt;

        /// <summary>Advance the reload and the buffered Release; true when a missile should launch now (the caller takes it
        /// from the magazine). <paramref name="canTake"/>: the magazine holds more than this seat's reserve.</summary>
        public bool Step(float dt, bool canTake)
        {
            if (Cooldown > 0f) Cooldown -= dt;
            if (Queued <= 0f) return false;
            Queued -= dt;
            if (Cooldown > 0f || !canTake) return false;
            Queued = 0f;
            return true;
        }
    }

    /// <summary>A flown missile (the lab's <c>stepMissiles</c> body): it flies straight ahead at its speed, the stick turns
    /// it, and it bursts on contact, at its fuse, or on a second Release.</summary>
    public sealed class OrcaMissile
    {
        public V3 Position;
        public OrcaFrame Frame;
        public float Age;
        public float StickYaw, StickPitch, StickRoll;

        public void Launch(V3 mount, V3 aim, V3 podUp, V3 hullRight)
        {
            Position = mount + aim * OrcaCrewCore.LaunchStandoff;
            var u = podUp - aim * V3.Dot(podUp, aim);
            Frame = new OrcaFrame(aim, u.Length() > 0.1f ? V3.Normalize(u) : hullRight);
            Age = 0f;
            StickYaw = StickPitch = StickRoll = 0f;
        }

        /// <summary>One step at <paramref name="speed"/> (u/s) and <paramref name="turnDeg"/> (deg/s). Returns where it was,
        /// so the caller can sweep the segment for contacts.</summary>
        public V3 Step(float dt, float speed, float turnDeg)
        {
            float rate = turnDeg * OrcaCrewCore.Deg2Rad * dt;
            Frame.Turn(Math.Clamp(StickYaw, -1f, 1f) * rate, Math.Clamp(StickPitch, -1f, 1f) * rate, Math.Clamp(StickRoll, -1f, 1f) * rate);
            var prev = Position;
            Position += Frame.F * (speed * dt);
            Age += dt;
            return prev;
        }

        public bool FuseOut(float life) => Age >= life;
        public bool CanBurstByHand => Age > OrcaCrewCore.MinBurstAge;
    }

    /// <summary>One gunner seat's magazine (round 5: "not a shared pool"); an omni crystal gives every seat one more.</summary>
    public sealed class OrcaMagazine
    {
        public int Count;
        public int Max;

        public OrcaMagazine(int start, int max) { Max = Math.Max(1, max); Count = Math.Clamp(start, 0, Max); }

        public bool CanTake => Count > 0;
        public void Take() { if (Count > 0) Count--; }

        /// <summary>An omni crystal: add <paramref name="missiles"/> up to the magazine's size; returns how many fitted.</summary>
        public int AddOmni(int missiles) { int was = Count; Count = Math.Min(Max, Count + Math.Max(0, missiles)); return Count - was; }
    }

    /// <summary>The fast gun's rate and heat (the lab's <c>rapidStep</c>, round 5): rounds owed while held, heat rising
    /// while it fires, and once hot a lockout until it has cooled all the way.</summary>
    public sealed class OrcaFastGun
    {
        public float Heat, Owed;
        public bool Hot;

        /// <summary>How many rounds to fire this step at <paramref name="rate"/> rounds/s, overheating after
        /// <paramref name="heatSeconds"/> of fire and cooling over <paramref name="coolSeconds"/>. A press after a release
        /// fires at once.</summary>
        public int Step(bool hold, float dt, float rate, float heatSeconds, float coolSeconds)
        {
            int n = 0;
            float interval = 1f / MathF.Max(0.01f, rate);
            if (hold && !Hot)
            {
                Owed += dt;
                while (Owed >= interval) { Owed -= interval; n++; }
                Heat += dt / MathF.Max(0.05f, heatSeconds);
                if (Heat >= 1f) { Heat = 1f; Hot = true; }
            }
            else
            {
                Owed = interval;
                Heat = MathF.Max(0f, Heat - dt / MathF.Max(0.05f, coolSeconds));
                if (Hot && Heat <= 0f) Hot = false;
            }
            return n;
        }

        public void Reset() { Heat = 0f; Hot = false; Owed = 0f; }
    }
}
