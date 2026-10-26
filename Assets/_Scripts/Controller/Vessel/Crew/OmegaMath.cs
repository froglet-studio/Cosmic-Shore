using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The arithmetic of the Omega crews, ported line for line from the Vessel Studio's Omega Crews lab
    /// (<c>Docs/Studios/VesselStudio/omega-studio.js</c>, part B; design <c>Docs/Studios/CONCEPT_FLEET.md</c> §10; this
    /// port <c>OMEGA_CREW.md</c> beside this file). Pure: no scene, no clock, no input. <see cref="OmegaCrew"/> is the glue,
    /// and <c>OmegaMathTests</c> holds every number here to goldens produced by running the page's own functions.
    ///
    /// <para>Three crews, each three members of the shipped fleet. Apart they fly as their MEGA forms (each member its own
    /// hull with one RT move); gathered within <c>gatherRange</c> of the one holding LT they dock over <c>combineSeconds</c>
    /// into ONE OMEGA form with three seats (Helm flies, Arms fires the crew weapon, Core holds the shield and fires the
    /// finisher), paid for by a meter the crew fills apart and spends combined.</para>
    /// </summary>
    public static class OmegaMath
    {
        public const int MemberCount = 3;
        public const int Helm = 0, Arms = 1, Core = 2;

        /// <summary>Serialized on <c>OmegaCrewConfigSO</c>: never renumber.</summary>
        public enum Crew { Warden = 0, Bulwark = 1, Tempest = 2 }

        public enum Form { Mega = 0, Gather = 1, Omega = 2 }

        /// <summary>The page's move kinds, folded to what a hull in this game does with them (OMEGA_CREW.md §3): sling,
        /// drift, gate and tunnel are a boost burst here; whip, ram and blast smash the mass they reach; guns and cannon
        /// are what the page says.</summary>
        public enum MoveKind { Boost = 0, Smash = 1, Guns = 2, Cannon = 3 }

        public enum WeaponKind { Whip = 0, Cannon = 1, Guns = 2 }

        /// <summary>A member's flight (the page's MEMBERS: cruise u/s, turn and roll deg/s) and its RT move's numbers.</summary>
        public readonly struct Member
        {
            public readonly string Name;
            public readonly float Cruise, Turn, Roll;
            public readonly MoveKind Kind;
            public readonly string MoveName;
            /// <summary>Boost: the multiplier and its seconds. Smash: reach (u) and seconds (0 = while held, cool per
            /// smash). Guns: range and rate. Cannon: shot speed, blast radius and charge seconds.</summary>
            public readonly float Boost, Time, Cool, Reach, Range, Rate, Speed, Blast, Charge;

            public Member(string name, float cruise, float turn, float roll, MoveKind kind, string moveName, float boost = 1f,
                float time = 0f, float cool = 0f, float reach = 0f, float range = 0f, float rate = 0f, float speed = 0f,
                float blast = 0f, float charge = 0f)
            {
                Name = name; Cruise = cruise; Turn = turn; Roll = roll; Kind = kind; MoveName = moveName;
                Boost = boost; Time = time; Cool = cool; Reach = reach; Range = range; Rate = rate; Speed = speed; Blast = blast; Charge = charge;
            }
        }

        /// <summary>The page's MEMBERS block, in its order. Indexed by <see cref="CrewDef.Members"/>.</summary>
        public static readonly Member[] Members =
        {
            new("Stoat", 60f, 110f, 130f, MoveKind.Boost, "Dipole sling", boost: 2.0f, time: 1.2f, cool: 4f),
            new("Thresher", 70f, 130f, 150f, MoveKind.Smash, "Wrecking ball", reach: 30f, cool: 0.4f),
            new("Butterfly", 55f, 80f, 90f, MoveKind.Boost, "Fold gate", boost: 1.5f, time: 1.0f, cool: 6f),
            new("Rhino", 58f, 90f, 100f, MoveKind.Smash, "Plough", reach: 16f, boost: 1.4f, time: 1.0f, cool: 3f),
            new("Grizzly", 52f, 85f, 90f, MoveKind.Cannon, "Charged cannon", speed: 240f, blast: 18f, charge: 1.5f),
            new("Scarab", 75f, 120f, 150f, MoveKind.Smash, "Cavitation dash", speed: 260f, time: 0.3f, reach: 14f, cool: 2.5f),
            new("Squirrel", 66f, 120f, 140f, MoveKind.Boost, "Skim boost", boost: 1.5f, time: 1.0f, cool: 2.5f),
            new("Sparrow", 72f, 110f, 160f, MoveKind.Guns, "Pulsefire", range: 220f, rate: 0.1f),
            new("Manta", 68f, 100f, 120f, MoveKind.Boost, "Boost tunnel", boost: 1.6f, time: 1.0f, cool: 3f),
        };

        public const int Stoat = 0, Thresher = 1, Butterfly = 2, Rhino = 3, Grizzly = 4, Scarab = 5, Squirrel = 6, Sparrow = 7, Manta = 8;

        /// <summary>One crew (the page's CREWS): its three members, its omega weapon, its omega flight multipliers.</summary>
        public readonly struct CrewDef
        {
            public readonly Crew Id;
            public readonly string Name, Tag, Finisher, Shield;
            public readonly int[] Members;
            public readonly WeaponKind Weapon;
            public readonly string WeaponName;
            /// <summary>Whip: reach, hits to break a wall, cool. Cannon: speed, blast, charge. Guns: range, rate, wallSeconds.</summary>
            public readonly float WeaponReach, WeaponHits, WeaponCool, WeaponSpeed, WeaponBlast, WeaponCharge, WeaponRange, WeaponRate, WeaponWallSeconds;
            public readonly float OmegaTurn, OmegaSpeed;

            public CrewDef(Crew id, string name, string tag, int[] members, WeaponKind weapon, string weaponName, string shield, string finisher,
                float omegaTurn, float omegaSpeed, float reach = 0f, float hits = 0f, float cool = 0f, float speed = 0f, float blast = 0f,
                float charge = 0f, float range = 0f, float rate = 0f, float wallSeconds = 0f)
            {
                Id = id; Name = name; Tag = tag; Members = members; Weapon = weapon; WeaponName = weaponName; Shield = shield; Finisher = finisher;
                OmegaTurn = omegaTurn; OmegaSpeed = omegaSpeed; WeaponReach = reach; WeaponHits = hits; WeaponCool = cool; WeaponSpeed = speed;
                WeaponBlast = blast; WeaponCharge = charge; WeaponRange = range; WeaponRate = rate; WeaponWallSeconds = wallSeconds;
            }
        }

        public static readonly CrewDef[] Crews =
        {
            new(Crew.Warden, "Warden", "the field crew", new[] { Stoat, Thresher, Butterfly }, WeaponKind.Whip, "Ripple whip", "Fold ward", "Annihilation",
                omegaTurn: 0.6f, omegaSpeed: 1.15f, reach: 60f, hits: 2f, cool: 0.6f),
            new(Crew.Bulwark, "Bulwark", "the heavy crew", new[] { Rhino, Grizzly, Scarab }, WeaponKind.Cannon, "Siege cannon", "Switch wall", "Breaker",
                omegaTurn: 0.5f, omegaSpeed: 1.1f, speed: 220f, blast: 30f, charge: 1.6f),
            new(Crew.Tempest, "Tempest", "the fast crew", new[] { Squirrel, Sparrow, Manta }, WeaponKind.Guns, "Pulsefire battery", "Afterburner veil", "Kabloom",
                omegaTurn: 0.75f, omegaSpeed: 1.3f, range: 260f, rate: 0.08f, wallSeconds: 2.5f),
        };

        public static CrewDef CrewOf(Crew id) => Crews[Mathf.Clamp((int)id, 0, Crews.Length - 1)];

        public static readonly string[] SeatNames = { "Helm", "Arms", "Core" };

        /// <summary>Cruise (u/s), turn and roll (deg/s): what a hull's transformer is set to for a form.</summary>
        public struct Flight
        {
            public float Cruise, Turn, Roll;
            public Flight(float cruise, float turn, float roll) { Cruise = cruise; Turn = turn; Roll = roll; }
        }

        public static Flight MemberFlight(in Member m) => new(m.Cruise, m.Turn, m.Roll);

        /// <summary>The page's <c>omegaFlight</c>: the members' averages, cruise times the crew's speed multiplier, turn
        /// and roll times its turn multiplier.</summary>
        public static Flight OmegaFlight(in CrewDef def)
        {
            float cruise = 0f, turn = 0f, roll = 0f;
            for (int i = 0; i < def.Members.Length; i++)
            {
                var m = Members[def.Members[i]];
                cruise += m.Cruise; turn += m.Turn; roll += m.Roll;
            }
            float n = def.Members.Length;
            return new Flight(cruise / n * def.OmegaSpeed, turn / n * def.OmegaTurn, roll / n * def.OmegaTurn);
        }

        /// <summary>The crew's lab numbers (the page's DEFAULTS; <c>OmegaCrewConfigSO</c> carries them as fields with the
        /// page's labels). <see cref="Studio"/> is the shipped block.</summary>
        public struct Params
        {
            public float CombineSeconds, GatherRange, MeterPerCrystal, MeterPerSkim, Regen, Drain, CombinedCrystal, ShieldDrain,
                CombineAt, FinisherAt, FinisherCone, FinisherCost, SplitSeconds;
            public bool OmegaContactPlough;

            public static Params Studio => new()
            {
                CombineSeconds = 1.6f, GatherRange = 140f, MeterPerCrystal = 0.22f, MeterPerSkim = 0.004f, Regen = 0.02f, Drain = 0.05f,
                CombinedCrystal = 0.5f, ShieldDrain = 0.06f, CombineAt = 0.3f, FinisherAt = 0.9f, FinisherCone = 180f, FinisherCost = 0.7f,
                SplitSeconds = 0.5f, OmegaContactPlough = true,
            };
        }

        /// <summary>The omega's element levels (0-10), the page's <c>lv</c>.</summary>
        public struct Levels
        {
            public float Charge, Mass, Space, Time;
            public static Levels Zero => default;
            public static Levels Full => new() { Charge = 10f, Mass = 10f, Space = 10f, Time = 10f };
        }

        /// <summary>What the levels do (the page's ELEMENTS, all PROPOSALS: one scaled parameter per element): Charge
        /// scales the weapon 1 to 1.6, Mass the shield drain 1 to 0.4, Space the finisher cone 1 to 1.5, Time the combine
        /// time 1 to 0.5. The L5 flags are the page's upgrade gates (unused here; the element map's slots are open).</summary>
        public struct Mults
        {
            public float WeaponPower, ShieldDrain, FinisherCone, CombineSeconds;
            public bool L5Charge, L5Mass, L5Space, L5Time;
        }

        public static Mults ElementMults(in Params p, in Levels lv)
        {
            return new Mults
            {
                WeaponPower = 1f * Mathf.Lerp(1f, 1.6f, Mathf.Clamp01(lv.Charge / 10f)),
                ShieldDrain = p.ShieldDrain * Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(lv.Mass / 10f)),
                FinisherCone = p.FinisherCone * Mathf.Lerp(1f, 1.5f, Mathf.Clamp01(lv.Space / 10f)),
                CombineSeconds = p.CombineSeconds * Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(lv.Time / 10f)),
                L5Charge = lv.Charge >= 5f, L5Mass = lv.Mass >= 5f, L5Space = lv.Space >= 5f, L5Time = lv.Time >= 5f,
            };
        }

        // ------------------------------------------------------------------ the dock and the split

        /// <summary>The page's <c>slotOf</c>: where member <paramref name="i"/> docks round the gather point, in the gather
        /// point's frame as (right, up, forward) u. Member 0 is the point itself; 1 rides 9 up and 4 back; 2 rides 8 down
        /// and 6 back.</summary>
        public static Vector3 SlotOffset(int i) => i switch
        {
            0 => Vector3.zero,
            1 => new Vector3(0f, 9f, -4f),
            _ => new Vector3(0f, -8f, -6f),
        };

        public static Vector3 SlotWorld(Vector3 point, Quaternion rotation, int i) => point + rotation * SlotOffset(i);

        /// <summary>The page's dock ease <c>e = k * k * (3 - 2k)</c> of the gather progress k (0-1).</summary>
        public static float DockEase(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }

        /// <summary>The page's dock lerp rate per second, <c>2 + 10e</c>: a docking member closes on its slot by
        /// <c>min(1, dt * rate)</c> of the gap each frame.</summary>
        public static float DockRate(float ease) => 2f + 10f * ease;

        public static float DockStep(float ease, float dt) => Mathf.Min(1f, dt * DockRate(ease));

        /// <summary>The page's <c>splitNow</c>: where each member appears round the omega's pose when the crew splits, in
        /// the omega's frame as (right, up, forward) u, and the share of the omega's speed every member keeps.</summary>
        public static Vector3 SplitOffset(int i) => i switch
        {
            0 => new Vector3(0f, 0f, 8f),
            1 => new Vector3(16f, 0f, -6f),
            _ => new Vector3(-16f, 0f, -6f),
        };

        public const float SplitSpeedShare = 0.9f;

        // ------------------------------------------------------------------ the meter

        public static float Regen(float meter, float regen, float dt) => Mathf.Min(1f, meter + regen * dt);

        /// <summary>Combined: the drain, plus the shield's while Core holds it.</summary>
        public static float Drain(float meter, float drain, bool shield, float shieldDrain, float dt) =>
            Mathf.Max(0f, meter - (drain + (shield ? shieldDrain : 0f)) * dt);

        /// <summary>A crystal: the full pay apart, <paramref name="combinedCrystal"/> of it in the omega form.</summary>
        public static float OnCrystal(float meter, float perCrystal, bool combined, float combinedCrystal) =>
            Mathf.Min(1f, meter + perCrystal * (combined ? combinedCrystal : 1f));

        public static float OnSkim(float meter, float perSkim) => Mathf.Min(1f, meter + perSkim);

        public static float Finisher(float meter, float cost) => Mathf.Max(0f, meter - cost);

        public static bool CanCombine(float meter, float combineAt) => meter >= combineAt;

        public static bool CanFinish(float meter, float finisherAt) => meter >= finisherAt;

        // ------------------------------------------------------------------ the finisher cone

        /// <summary>The page's <c>omegaCore</c> test for a point <paramref name="local"/> (right, up, forward from the omega):
        /// in the cone when it is ahead within <paramref name="reach"/> and its distance off the axis is at most
        /// <c>0.45 x along + 10</c>.</summary>
        public static bool InFinisherCone(Vector3 local, float reach)
        {
            float along = local.z;
            if (along <= 0f || along > reach) return false;
            float perp = Mathf.Sqrt(local.x * local.x + local.y * local.y);
            return perp <= along * 0.45f + 10f;
        }

        /// <summary>The cone's half-angle (deg) for a spatial query that brackets <see cref="InFinisherCone"/>: the slope
        /// 0.45 plus the 10 u base, which the exact test then trims.</summary>
        public static float FinisherQueryHalfAngle(float reach) => Mathf.Atan2(reach * 0.45f + 10f, reach) * Mathf.Rad2Deg;

        public const float FinisherBaseRadius = 10f;

        // ------------------------------------------------------------------ the Arms turret

        public const float AimRateRadiansPerSecond = 2.2f, AimYawLimit = 0.9f, AimPitchLimit = 0.7f;

        /// <summary>The page's turret: the right stick turns the aim at 2.2 rad/s per unit of stick, bounded to a cone
        /// (yaw within 0.9 rad, pitch within 0.7).</summary>
        public static void AimStep(ref float yaw, ref float pitch, Vector2 stick, float dt)
        {
            yaw = Mathf.Clamp(yaw + stick.x * AimRateRadiansPerSecond * dt, -AimYawLimit, AimYawLimit);
            pitch = Mathf.Clamp(pitch + stick.y * AimRateRadiansPerSecond * dt, -AimPitchLimit, AimPitchLimit);
        }

        /// <summary>The aim direction: the omega's forward turned <paramref name="yaw"/> right about its up, then
        /// <paramref name="pitch"/> up about its right.</summary>
        public static Vector3 AimDirection(Quaternion rotation, float yaw, float pitch)
        {
            var fwd = rotation * Vector3.forward;
            var up = rotation * Vector3.up;
            var right = rotation * Vector3.right;
            var turned = Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, up) * fwd;
            return (Quaternion.AngleAxis(-pitch * Mathf.Rad2Deg, right) * turned).normalized;
        }

        // ------------------------------------------------------------------ a simulated member's flight

        /// <summary>The page's <c>fly</c>: speed eases to its target at <paramref name="rate"/>/s, the course grips the nose
        /// at <paramref name="grip"/>/s, the body moves along the course.</summary>
        public static void Fly(ref Vector3 pos, ref Vector3 course, ref float speed, Vector3 forward, float targetSpeed, float dt,
            float grip = 30f, float rate = 1.5f)
        {
            speed += (targetSpeed - speed) * (1f - Mathf.Exp(-rate * dt));
            course = Vector3.Slerp(course, forward, 1f - Mathf.Exp(-grip * dt)).normalized;
            pos += course * (speed * dt);
        }

        /// <summary>The page's <c>steer</c> for an AI member with its sticks on a target: the nose turns toward
        /// <paramref name="target"/> at the member's turn rate and rolls its up toward <paramref name="up"/> at its roll
        /// rate, never faster.</summary>
        public static Quaternion SteerToward(Quaternion rotation, Vector3 position, Vector3 target, Vector3 up, in Flight f, float dt)
        {
            var to = target - position;
            if (to.sqrMagnitude < 1e-6f) return rotation;
            var fwd = rotation * Vector3.forward;
            var want = Vector3.RotateTowards(fwd, to.normalized, f.Turn * Mathf.Deg2Rad * dt, 0f);
            var currentUp = rotation * Vector3.up;
            var wantUp = Vector3.ProjectOnPlane(up, want);
            if (wantUp.sqrMagnitude < 1e-6f) wantUp = Vector3.ProjectOnPlane(currentUp, want);
            var rolled = Vector3.RotateTowards(Vector3.ProjectOnPlane(currentUp, want).normalized, wantUp.normalized, f.Roll * Mathf.Deg2Rad * dt, 0f);
            return Quaternion.LookRotation(want, rolled);
        }

        /// <summary>Where an AI member flies apart: an echelon slot beside the hull, member 1 to its right and up, member 2
        /// to its left and down, <paramref name="spread"/> u out and a little behind (not the page, which flies the AI
        /// members at the course's crystals; OMEGA_CREW.md §3).</summary>
        public static Vector3 FormationOffset(int member, int hullMember, float spread)
        {
            int k = member == hullMember ? 0 : (member < hullMember ? member + 1 : member);
            return k switch
            {
                0 => Vector3.zero,
                1 => new Vector3(spread, spread * 0.25f, -spread * 0.4f),
                _ => new Vector3(-spread, -spread * 0.25f, -spread * 0.4f),
            };
        }
    }
}
