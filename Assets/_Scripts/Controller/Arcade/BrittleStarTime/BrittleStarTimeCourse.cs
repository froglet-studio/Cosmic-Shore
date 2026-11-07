using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Brittle Star's circuit, stated against its flight model (BrittleStar.prefab and
    /// BrittleStarConfig.asset; BrittleStarTimeCourseTests reads them back). Four intensities, four MOVEMENT questions
    /// (Shombith, 2026-11-06: "I1 Gearbox, I2 Feather slalom, I3 Coast-in hairpins, I4 Corkscrew"): each rung asks one
    /// thing of Speed Mode and the turn, and nothing else - no guns, cords, ring or Creation (BRITTLESTARTIME.md).
    ///
    /// <list type="bullet">
    /// <item><b>I1 Gearbox</b> - Headlong's shared solver: corners just tighter than Speed Mode's circle. Hold Speed, or
    /// shift down to Cruise to turn in?</item>
    /// <item><b>I2 Feather slalom</b> - <see cref="Slalom"/>: a weave whose bends sit between the cruise circle and Speed
    /// Mode's. Neither mode flies it cleanly; tapping A in and out, so the hull eases to a speed between, does.</item>
    /// <item><b>I3 Coast-in hairpins</b> - <see cref="Hairpins"/>: long straights into hairpins Speed Mode cannot turn.
    /// There is no brake: let go of Speed Mode early enough that the coast-down lands you slow at the apex, then light it
    /// again on the way out.</item>
    /// <item><b>I4 Corkscrew</b> - <see cref="Corkscrew"/>: a tight coil the hull starts at Time 5 for (the card's
    /// starting elements), where Speed Roll is live. Its turns are tighter than Speed Mode's circle and wider than Speed
    /// Mode rolling and pulling: only a pilot who rolls holds Speed through it.</item>
    /// </list>
    ///
    /// <para><b>Speed is a mode.</b> Cruise is <c>(10 + 35)</c> = 45 u/s; Speed Mode multiplies the throttle target by
    /// <see cref="SpeedModeMultiplier"/> (6), 270 u/s, and by 7 at Time 5. Turn rate is <c>0.1 x speed + 80</c> deg/s,
    /// so Speed Mode turns a ~145 u circle (~162 u at Time 5); Speed Roll's pull adds
    /// <see cref="SpeedRollPullDegreesPerSecond"/> to it (~90 u). Speed eases toward its target at
    /// <see cref="SpeedEaseRate"/> per second, which is what feathering and coasting are made of.</para>
    ///
    /// <para><b>Placement.</b> Gate 0 sits on the spawn pole (as Headlong's does), every shape lies in the plane square to
    /// the pole at <see cref="PlaceRadius"/> from the cell's centre, turned about the pole by the seed (and mirrored by
    /// it at I3 and I4). A shape that would not fit the shell, or a race whose lap is not
    /// <see cref="GatesPerLap"/> rings, gets the Gearbox instead (<see cref="BuildLap"/> returns false).</para>
    /// </summary>
    public static class BrittleStarTimeCourse
    {
        // ── The vessel this course is cut for (BrittleStarTimeCourseTests reads each back off its asset) ──

        /// <summary>`DefaultMinimumSpeed` on BrittleStar.prefab.</summary>
        public const float MinimumSpeed = 10f;

        /// <summary>`DefaultThrottleScaler` on BrittleStar.prefab.</summary>
        public const float ThrottleScaler = 35f;

        /// <summary>`RotationThrottleScaler` on BrittleStar.prefab.</summary>
        public const float RotationThrottleScaler = 0.1f;

        /// <summary>The smaller of `PitchScaler` and `YawScaler` on BrittleStar.prefab.</summary>
        public const float TurnScaler = 80f;

        /// <summary>`speedModeMultiplier.Value` on BrittleStarConfig.asset (x6 at rest).</summary>
        public const float SpeedModeMultiplier = 6f;

        /// <summary>`speedModeMultiplier.Max` on BrittleStarConfig.asset (x8 at Time 10).</summary>
        public const float SpeedModeMultiplierAtTime10 = 8f;

        /// <summary>`speedRollPullDegreesPerSecond` on BrittleStarConfig.asset: Speed Roll's pull (Time 5).</summary>
        public const float SpeedRollPullDegreesPerSecond = 90f;

        /// <summary>The fleet's speed ease (`VesselTransformer.LERP_AMOUNT`): speed closes on its target at this share
        /// per second, an exponential with a ~0.67 s time constant.</summary>
        public const float SpeedEaseRate = 1.5f;

        /// <summary>Cruise at full throttle: 45 u/s.</summary>
        public const float CruiseSpeed = MinimumSpeed + ThrottleScaler;

        /// <summary>Speed Mode at rest: 270 u/s.</summary>
        public const float SpeedModeSpeed = CruiseSpeed * SpeedModeMultiplier;

        /// <summary>Speed Mode at Time 5, where I4 starts the hull: x7, 315 u/s.</summary>
        public static float SpeedModeSpeedAtTime5 =>
            CruiseSpeed * Mathf.Lerp(SpeedModeMultiplier, SpeedModeMultiplierAtTime10, 0.5f);

        public static float TurnRateDegPerSec(float speed) => speed * RotationThrottleScaler + TurnScaler;

        /// <summary>The circle the hull turns at <paramref name="speed"/>, with <paramref name="extraDegPerSec"/> of
        /// turn on top (Speed Roll's pull).</summary>
        public static float CornerRadiusAt(float speed, float extraDegPerSec = 0f) =>
            speed / ((TurnRateDegPerSec(speed) + extraDegPerSec) * Mathf.Deg2Rad);

        /// <summary>The speed whose circle is <paramref name="radius"/>: the inverse of <see cref="CornerRadiusAt"/>.</summary>
        public static float SpeedForRadius(float radius)
        {
            float k = radius * Mathf.Deg2Rad;
            return k * TurnScaler / Mathf.Max(1e-4f, 1f - k * RotationThrottleScaler);
        }

        /// <summary>Distance the hull covers coasting from <paramref name="from"/> down to <paramref name="to"/> once Speed
        /// Mode is let go: speed eases toward cruise at <see cref="SpeedEaseRate"/>.</summary>
        public static float CoastDistance(float from, float to)
        {
            float above0 = from - CruiseSpeed, above1 = Mathf.Max(1e-3f, to - CruiseSpeed);
            if (above0 <= above1) return 0f;
            float t = Mathf.Log(above0 / above1) / SpeedEaseRate;
            return CruiseSpeed * t + above0 / SpeedEaseRate * (1f - Mathf.Exp(-SpeedEaseRate * t));
        }

        /// <summary>Speed Mode's circle (~145 u).</summary>
        public static float SpeedModeRadius => CornerRadiusAt(SpeedModeSpeed);

        /// <summary>The cruise circle (~30 u).</summary>
        public static float CruiseRadius => CornerRadiusAt(CruiseSpeed);

        /// <summary>Speed Mode's circle at Time 5 (~162 u).</summary>
        public static float SpeedModeRadiusAtTime5 => CornerRadiusAt(SpeedModeSpeedAtTime5);

        /// <summary>Speed Mode at Time 5 rolling and pulling: the left stick's turn plus Speed Roll's pull (~90 u).</summary>
        public static float SpeedRollRadiusAtTime5 => CornerRadiusAt(SpeedModeSpeedAtTime5, SpeedRollPullDegreesPerSecond);

        // ── The ladder ──

        public enum Rung { Gearbox = 1, FeatherSlalom = 2, CoastInHairpins = 3, Corkscrew = 4 }

        public static Rung RungFor(int intensity) => (Rung)Mathf.Clamp(intensity, 1, 4);

        /// <summary>Rings per lap. The race target is <c>laps x GatesPerLap</c>.</summary>
        public const int GatesPerLap = 8;

        /// <summary>The mouth radius per intensity, I1..I4.</summary>
        public static float MouthRadius(int intensity) => new[] { 80f, 60f, 44f, 42f }[Mathf.Clamp(intensity, 1, 4) - 1];

        /// <summary>How far from the cell's centre gate 0 (and the plane every shape lies in) sits.</summary>
        public const float PlaceRadius = 700f;

        /// <summary>The Gearbox circuit's base circle - Headlong's octagon at 800, mid-shell.</summary>
        public const float BaseRadius = 800f;

        /// <summary>
        /// I1 GEARBOX: Headlong's solver with the corner profile that gives a median of two corners a lap tighter than
        /// Speed Mode's ~145 u circle (measured over 400 seeds) and wide mouths. Also every rung's fallback.
        /// </summary>
        public static HeadlongCircuitSettings Gearbox() => new HeadlongCircuitSettings
        {
            GateCount = GatesPerLap,
            BaseRadius = BaseRadius,
            CornerProfile = new[] { 160f, 150f, 60f, 6f, 4f, 3f, 2f, 1f },
            // An ABSOLUTE floor in this hull's units: no corner is ever tighter, whatever the solver does.
            // CornerRadiusFactor stays 0 (it would be read as a fraction of the RHINO's flat-out radius).
            CornerRadiusFactor = 0f,
            CornerFloorRadius = 0.45f * SpeedModeRadius,
            RadialSwing = 0.42f,
            AngularSpread = 3.0f,
            LateralPerturbation = 215f,
            RingRadius = MouthRadius(1),
            AxisJitterDegrees = 36f,
            MaxPresentDegrees = 72f,
        };

        /// <summary>
        /// One lap of rings for <paramref name="intensity"/>, laid inside the shell [<paramref name="inner"/>,
        /// <paramref name="outer"/>]. False when the rung's shape cannot be laid (the lap is not
        /// <see cref="GatesPerLap"/> rings, or the shape does not fit the shell): the caller flies the Gearbox.
        /// I1 is always false here - the Gearbox is Headlong's solver, built by the course source.
        /// </summary>
        public static bool BuildLap(int intensity, int seed, float inner, float outer, int gatesPerLap,
                                    out List<RaceGate> lap)
        {
            lap = null;
            var rung = RungFor(intensity);
            if (rung == Rung.Gearbox || gatesPerLap != GatesPerLap) return false;

            var rng = new RaceCourseGeometry.Rng(seed * 7919 + intensity);
            float yaw = rng.Range(0f, 360f);
            float hand = rng.Unit() < 0.5f ? 1f : -1f;
            var local = rung switch
            {
                Rung.FeatherSlalom => Slalom(),
                Rung.CoastInHairpins => Hairpins(hand),
                _ => Corkscrew(hand),
            };
            lap = Place(local, yaw, MouthRadius(intensity));
            return Fits(lap, inner, outer);
        }

        /// <summary>A gate in the shape's own frame: x forward at gate 0, y across, z along the pole.</summary>
        public readonly struct LocalGate
        {
            public readonly Vector3 Position;
            public readonly Vector3 Axis;
            public LocalGate(Vector3 position, Vector3 axis)
            {
                Position = position;
                Axis = axis.normalized;
            }
        }

        // ── I2: the feather slalom ──

        /// <summary>The loop the weave rides on.</summary>
        public const float SlalomLoopRadius = 300f;

        /// <summary>How far each ring sits off the loop, alternately out and in.</summary>
        public const float SlalomAmplitude = 90f;

        /// <summary>
        /// I2: <see cref="GatesPerLap"/> rings round a <see cref="SlalomLoopRadius"/> loop, alternately
        /// <see cref="SlalomAmplitude"/> outside and inside it - four full weaves a lap. Each ring faces along the loop.
        /// </summary>
        public static List<LocalGate> Slalom()
        {
            var gates = new List<LocalGate>(GatesPerLap);
            for (int i = 0; i < GatesPerLap; i++)
            {
                float t = 2f * Mathf.PI * i / GatesPerLap;
                float side = i % 2 == 0 ? 1f : -1f;
                var outward = new Vector3(Mathf.Sin(t), -Mathf.Cos(t), 0f);
                var onLoop = new Vector3(SlalomLoopRadius * Mathf.Sin(t), SlalomLoopRadius * (1f - Mathf.Cos(t)), 0f);
                gates.Add(new LocalGate(onLoop + outward * (SlalomAmplitude * side),
                                        new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f)));
            }
            return gates;
        }

        /// <summary>
        /// The tightest bend of the weave a pilot flies who passes every ring <paramref name="slack"/> in from its
        /// centre towards the loop (0 = through the centres, the mouth radius = brushing the rim). Measured on the smooth
        /// weave <c>loop(t) + (A - slack) cos(4t) outward(t)</c> the rings sample.
        /// </summary>
        public static float SlalomBendRadius(float slack) =>
            MinRadius(t =>
            {
                var onLoop = new Vector3(SlalomLoopRadius * Mathf.Sin(t), SlalomLoopRadius * (1f - Mathf.Cos(t)), 0f);
                var outward = new Vector3(Mathf.Sin(t), -Mathf.Cos(t), 0f);
                return onLoop + outward * ((SlalomAmplitude - slack) * Mathf.Cos(GatesPerLap / 2f * t));
            });

        // ── I3: the coast-in hairpins ──

        /// <summary>Each straight's length.</summary>
        public const float HairpinStraight = 600f;

        /// <summary>The two straights' separation; the hairpin's own radius is half of it.</summary>
        public const float HairpinWidth = 90f;

        /// <summary>
        /// I3: a paperclip - three rings down one straight, one at the apex of the far hairpin (facing across), three
        /// rings back up the other straight, one at the apex of the near hairpin. <paramref name="hand"/> mirrors it.
        /// </summary>
        public static List<LocalGate> Hairpins(float hand)
        {
            float L = HairpinStraight, w = HairpinWidth * hand;
            return new List<LocalGate>(GatesPerLap)
            {
                new(new Vector3(0.12f * L, 0f, 0f), Vector3.right),
                new(new Vector3(0.45f * L, 0f, 0f), Vector3.right),
                new(new Vector3(0.80f * L, 0f, 0f), Vector3.right),
                new(new Vector3(L + 0.5f * HairpinWidth, 0.5f * w, 0f), new Vector3(0f, hand, 0f)),
                new(new Vector3(0.85f * L, w, 0f), Vector3.left),
                new(new Vector3(0.50f * L, w, 0f), Vector3.left),
                new(new Vector3(0.15f * L, w, 0f), Vector3.left),
                new(new Vector3(-0.5f * HairpinWidth, 0.5f * w, 0f), new Vector3(0f, -hand, 0f)),
            };
        }

        /// <summary>The widest a pilot can swing round a hairpin: its own radius plus the mouth of its apex ring.</summary>
        public static float HairpinFlyableRadius(float mouth) => 0.5f * HairpinWidth + mouth;

        // ── I4: the corkscrew ──

        /// <summary>The coil's radius about its axis.</summary>
        public const float CoilRadius = 100f;

        /// <summary>The coil's advance along its axis per radian of turn (a 361 u pitch a turn).</summary>
        public const float CoilAdvancePerRadian = 57.5f;

        /// <summary>Rings on the coil, a quarter turn apart.</summary>
        public const int CoilGates = 6;

        /// <summary>Degrees of coil between two rings. A quarter turn: a coarser step aliases into a lazier spiral the
        /// pilot could fly instead (with 8 rings round a whole lap, five turns read as three the other way).</summary>
        public const float CoilStepDegrees = 90f;

        /// <summary>The return leg's turn radius - wider than Speed Mode's circle at Time 5, so it is flown flat out.</summary>
        public const float ReturnRadius = 200f;

        /// <summary>
        /// I4: <see cref="CoilGates"/> rings a quarter turn apart on a coil of <see cref="CoilRadius"/> about the x axis,
        /// each facing along the coil; then a fast return - one ring at the apex of the far turn, one at the near - back to
        /// the coil's mouth. <paramref name="hand"/> is the coil's chirality.
        /// </summary>
        public static List<LocalGate> Corkscrew(float hand)
        {
            var gates = new List<LocalGate>(GatesPerLap);
            float step = CoilStepDegrees * Mathf.Deg2Rad;
            for (int k = 0; k < CoilGates; k++)
            {
                float psi = k * step;                   // turn so far
                float phi = hand * psi;                 // where round the coil
                var pos = new Vector3(CoilAdvancePerRadian * psi, CoilRadius * Mathf.Cos(phi), CoilRadius * Mathf.Sin(phi));
                var tangent = new Vector3(CoilAdvancePerRadian, -CoilRadius * hand * Mathf.Sin(phi),
                                          CoilRadius * hand * Mathf.Cos(phi));
                gates.Add(new LocalGate(pos, tangent));
            }
            // The return runs on the side the coil leaves towards: five quarter turns from phi = 0 always exit heading -y,
            // whichever way it winds.
            float end = CoilAdvancePerRadian * (CoilGates - 1) * step;
            gates.Add(new LocalGate(new Vector3(end + ReturnRadius, -ReturnRadius, 0f), Vector3.down));
            gates.Add(new LocalGate(new Vector3(-ReturnRadius, -ReturnRadius, 0f), Vector3.up));
            return gates;
        }

        /// <summary>
        /// The tightest bend of the coil a pilot flies who passes every coil ring <paramref name="slack"/> in from its
        /// centre towards the coil's axis: a helix of radius <c>CoilRadius - slack</c>, whose bend is
        /// <c>r + p^2 / r</c>.
        /// </summary>
        public static float CoilBendRadius(float slack)
        {
            float r = Mathf.Max(1e-3f, CoilRadius - slack);
            return r + CoilAdvancePerRadian * CoilAdvancePerRadian / r;
        }

        // ── Shared ──

        static List<RaceGate> Place(List<LocalGate> local, float yawDegrees, float mouth)
        {
            // The plane square to the pole: x and y in it (turned by the seed), z along the pole.
            var rot = Quaternion.AngleAxis(yawDegrees, Vector3.up);
            Vector3 X = rot * Vector3.right, Y = rot * Vector3.forward, Z = Vector3.up;
            Vector3 origin = local[0].Position;
            var gates = new List<RaceGate>(local.Count);
            foreach (var g in local)
            {
                Vector3 d = g.Position - origin;
                Vector3 p = Vector3.up * PlaceRadius + X * d.x + Y * d.y + Z * d.z;
                Vector3 a = X * g.Axis.x + Y * g.Axis.y + Z * g.Axis.z;
                gates.Add(new RaceGate(p, a.normalized, mouth));
            }
            return gates;
        }

        /// <summary>Every ring inside the shell and no two mouths overlapping.</summary>
        public static bool Fits(List<RaceGate> lap, float inner, float outer)
        {
            for (int i = 0; i < lap.Count; i++)
            {
                float r = lap[i].Position.magnitude;
                if (r < inner || r > outer) return false;
                for (int j = i + 1; j < lap.Count; j++)
                    if ((lap[i].Position - lap[j].Position).magnitude < lap[i].Radius + lap[j].Radius) return false;
            }
            return true;
        }

        /// <summary>The smallest radius of curvature of a closed curve over t in [0, 2 pi), sampled finely.</summary>
        static float MinRadius(System.Func<float, Vector3> curve, int samples = 2048)
        {
            float h = 2f * Mathf.PI / samples, best = float.MaxValue;
            for (int i = 0; i < samples; i++)
            {
                float t = i * h;
                Vector3 a = curve(t - h), b = curve(t), c = curve(t + h);
                Vector3 d1 = (c - a) / (2f * h), d2 = (c - 2f * b + a) / (h * h);
                float speed = d1.magnitude;
                float k = Vector3.Cross(d1, d2).magnitude / (speed * speed * speed);
                if (k > 1e-9f) best = Mathf.Min(best, 1f / k);
            }
            return best;
        }
    }
}
