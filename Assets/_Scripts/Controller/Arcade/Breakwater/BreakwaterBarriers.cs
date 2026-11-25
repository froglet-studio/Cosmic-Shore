using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One prism of a barrier: a box of <see cref="Scale"/> at <see cref="Position"/>, its long side along
    /// <see cref="Forward"/>. <see cref="Leg"/> is the leg of the circuit it stands on, <see cref="Kind"/> the barrier
    /// (<see cref="BreakwaterBarriers.Curtain"/>...), <see cref="Danger"/> says it is a mine or a flak shell, and
    /// <see cref="Core"/> says it is part of a flak battery's core (break the core and the battery stops).</summary>
    public readonly struct BreakwaterBarrierPart
    {
        public readonly Vector3 Position, Forward, Up, Scale;
        public readonly int Leg, Kind;
        public readonly bool Danger, Core;

        public BreakwaterBarrierPart(Vector3 position, Vector3 forward, Vector3 up, Vector3 scale, int leg, int kind,
                                     bool danger, bool core = false)
        {
            Core = core;
            Position = position;
            Forward = forward;
            Up = up;
            Scale = scale;
            Leg = leg;
            Kind = kind;
            Danger = danger;
        }
    }

    /// <summary>The parts <see cref="First"/>..<see cref="First"/>+<see cref="Count"/> of a barrier that MOVE, about
    /// <see cref="Centre"/> on the line: a turnstile wheel turns about <see cref="Axis"/> at <see cref="Rate"/>
    /// degrees a second; a shutter's halves slide along <see cref="Right"/>, <see cref="Rate"/> seconds out of step
    /// with the clock; a flak gun's shells fly along <see cref="Axis"/> from its muzzle <see cref="Centre"/> at
    /// <see cref="Rate"/> u a second.</summary>
    public readonly struct BreakwaterBarrierMover
    {
        public readonly int Kind, First, Count;
        public readonly Vector3 Centre, Axis, Right;
        public readonly float Rate;

        public BreakwaterBarrierMover(int kind, Vector3 centre, Vector3 axis, Vector3 right, float rate, int first, int count)
        {
            Kind = kind;
            Centre = centre;
            Axis = axis;
            Right = right;
            Rate = rate;
            First = first;
            Count = count;
        }

        public BreakwaterBarrierMover From(int first) => new(Kind, Centre, Axis, Right, Rate, first, Count);
    }

    /// <summary>
    /// Breakwater's barriers (BREAKWATER.md "The barriers", Yash 2026-11-25: "pass by obstacles, shoot and make passage
    /// if can't pass"): what stands ACROSS the racing line on the legs the garden ruins leave bare, at I4 only. Three
    /// kinds take turns round the circuit:
    /// a <b>curtain</b> (one thin wall of plates right across the lane: shoot a hole, ram it or fly round),
    /// a <b>minefield</b> (danger prisms round the line with the thread down the middle left open, as a station's eye
    /// is), a <b>lock</b> (three smaller curtains in a row: more rounds to open, a shorter way round),
    /// a <b>turnstile</b> (two bladed wheels turning opposite ways about the line, the thread through their hubs open)
    /// a <b>shutter</b> (a curtain whose two halves slide apart and shut again on a clock) and a <b>flak battery</b>
    /// (a core of blocks plugging the line, and guns round the lane firing danger shells ACROSS it in a FIXED
    /// crossfire - nothing aims - that never enters the thread: shoot the core and the line is open and the guns
    /// stop (<see cref="Silenced"/>), or fly round through the crossfire). The three that move are POSED from a
    /// clock the controller gives
    /// (<see cref="Pose"/>); one that would sweep into a station's reach or another leg's lane is laid as a curtain
    /// instead.
    ///
    /// <para>Each stands at the middle of its leg's flown line, across it. Everything is laid Blue by the controller,
    /// as the stations are, so every pilot's rounds open a curtain and are paid for it. They are prisms with the
    /// game's own rules: nothing here reads or writes a vessel. A broken prism grows back once the field has passed
    /// (<see cref="Regrows"/>), so the second lap meets walls too.</para>
    ///
    /// <para>Pure: a function of the course and the rung, so every peer lays the same barriers from the broadcast
    /// course and the edit-mode tests hold it (<c>BreakwaterBarriersTests</c>).</para>
    /// </summary>
    public static class BreakwaterBarriers
    {
        public const int Curtain = 0, Minefield = 1, Lock = 2, Turnstile = 3, Shutter = 4, Flak = 5;
        public const int KindCount = 6;

        /// <summary>A flak battery: this many guns round the lane, each firing this many shells at a time across it
        /// along a chord that passes <see cref="FlakInner"/> or <see cref="FlakOuter"/> (u) from the line (the guns
        /// take turns), spread over <see cref="FlakDepth"/> (u) along it. A shell flies <see cref="FlakRange"/> (u)
        /// at <see cref="FlakSpeed"/> (u/s), then the gun fires it again.</summary>
        public const int FlakGuns = 8, FlakShellsPerGun = 3;
        public const float FlakInner = 28f, FlakOuter = 40f, FlakRange = 90f, FlakSpeed = 45f, FlakShellSize = 6f;
        public const float FlakDepth = 40f;
        /// <summary>A gun's barrel: a plain block this wide and this long (u) behind its muzzle.</summary>
        public const float FlakBarrelWidth = 8f, FlakBarrelLength = 14f;
        /// <summary>The core: a cube of 2 x 2 x 2 plain blocks of this size at this pitch (u), on the line just
        /// behind the guns, so a pilot on the line has it dead ahead.</summary>
        public const float FlakCoreBlock = 9f, FlakCorePitch = 10f;
        public const int FlakCoreBlocks = 8;

        /// <summary>Is a battery silenced? When half its core or more is broken. Its shells are taken away until
        /// the core has grown back.</summary>
        public static bool Silenced(int coreStanding, int coreTotal) => coreTotal > 0 && coreStanding * 2 <= coreTotal;

        /// <summary>A turnstile: two wheels this far (u) either side of the leg's middle, turning opposite ways at
        /// this rate, each with this many blades of this many segments from <see cref="BladeInner"/> out.</summary>
        public const float WheelOffset = 12f, WheelDegreesPerSecond = 36f, BladeInner = 20.5f;
        public const int WheelBlades = 4, BladeSegments = 4;
        /// <summary>A blade segment: this wide across the blade, this thin along the line, this long outward (u).</summary>
        public const float BladeWidth = 14f, BladeThickness = 3f, BladeLength = 13f;

        /// <summary>A shutter: a disc of plates this far (u) from the line whose halves each slide this far apart.
        /// One cycle: shut, opening, open, closing (s).</summary>
        public const float ShutterRadius = 60f, ShutterTravel = 22f;
        public const float ShutterShut = 2f, ShutterMove = 1f, ShutterOpen = 2f;
        public const float ShutterPeriod = ShutterShut + ShutterMove + ShutterOpen + ShutterMove;

        /// <summary>Phases of a mover's cycle sampled for its clearances.</summary>
        public const int SweepSamples = 16;

        public static bool Moves(int kind) => kind == Turnstile || kind == Shutter || kind == Flak;

        /// <summary>Is this part posed from the clock? Every part of a turnstile or a shutter; of a flak battery,
        /// only the shells.</summary>
        public static bool Moves(in BreakwaterBarrierPart part) => Moves(part.Kind) && (part.Kind != Flak || part.Danger);

        /// <summary>A curtain reaches this far (u) from the line: past the lane (60), so the way round is a real detour.</summary>
        public const float CurtainRadius = 72f;

        /// <summary>A lock's three sheets are smaller and this far apart (u) along the line.</summary>
        public const float LockRadius = 48f, LockSpacing = 18f;
        public const int LockSheets = 3;

        /// <summary>A curtain's plate: this wide and tall (u), this thin along the line, laid at this pitch.</summary>
        public const float PlateSize = 13.4f, PlateThickness = 2f, PlatePitch = 14f;

        /// <summary>A minefield: this many mines of this size (u), between <see cref="ThreadRadius"/> and
        /// <see cref="MineRadius"/> from the line, over this depth (u) along it.</summary>
        public const int Mines = 60;
        public const float MineSize = 6f, MineRadius = 60f, MineDepth = 50f;

        /// <summary>No mine comes nearer the flown line than this (u): the station's eye radius, so the thread a
        /// pilot (and an AI on the line) flies through a station stays open through a minefield too.</summary>
        public const float ThreadRadius = 18f;

        /// <summary>A barrier that lost more than this share of its prisms to the clearances is not laid at all.</summary>
        public const float MinStandingShare = 0.5f;

        /// <summary>A broken barrier prism grows back this long (s) after the field has passed: after the last moment
        /// any hull was within <see cref="RegrowClear"/> (u) of its barrier. So the pilots close behind the one who
        /// opened a wall fly through the hole, and the wall stands again for the next lap.</summary>
        public const float RegrowSeconds = 8f, RegrowClear = 260f;

        /// <summary>Is a hull at <paramref name="distance"/> (u) from a barrier's middle still "at" it? Then nothing
        /// of that barrier grows back, and its clock restarts. Past a rocket's reach (229 u), so a wall never closes
        /// on a round already fired at it.</summary>
        public static bool FieldAt(float distance) => distance < RegrowClear;

        /// <summary>Has a broken prism waited long enough since the field passed?</summary>
        public static bool Regrows(float secondsSinceTheFieldPassed) => secondsSinceTheFieldPassed >= RegrowSeconds;

        /// <summary>Barriers stand on the hardest rung only, as the ruins do.</summary>
        public static bool OnRung(int intensity) => intensity >= 4;

        /// <summary>The kind of the <paramref name="ordinal"/>th barrier leg round the circuit.</summary>
        public static int KindOf(int ordinal) => ((ordinal % KindCount) + KindCount) % KindCount;

        /// <summary>
        /// Every barrier prism of <paramref name="course"/> on this rung into <paramref name="parts"/>.
        /// <paramref name="lap"/> receives the circuit (the course without its first <paramref name="leadIn"/> gates);
        /// a part's <c>Leg</c> indexes it. The lists are cleared first. A barrier stands on each leg the ruins leave
        /// bare (<see cref="BreakwaterRuins.OnBareLeg"/>) that is long enough; a prism that would come into a
        /// station's reach, another leg's lane or the way in from the pads is left out, and a barrier that loses more
        /// than half its prisms that way is left out whole.
        /// </summary>
        public static void Lay(IReadOnlyList<RaceGate> course, int leadIn, int intensity, IReadOnlyList<Vector3> pads,
                               List<RaceGate> lap, List<BreakwaterBarrierPart> parts,
                               List<BreakwaterBarrierMover> movers = null)
        {
            lap.Clear();
            parts.Clear();
            movers?.Clear();
            if (course == null || !OnRung(intensity)) return;
            leadIn = Mathf.Clamp(leadIn, 0, course.Count);
            for (int i = leadIn; i < course.Count; i++) lap.Add(course[i]);
            if (lap.Count < 3) return;

            var settings = BreakwaterRuins.Settings(intensity);
            var offered = new List<BreakwaterBarrierPart>();
            var offeredMovers = new List<BreakwaterBarrierMover>();
            int ordinal = 0;
            for (int leg = 0; leg < lap.Count; leg++)
            {
                if (!BreakwaterRuins.OnBareLeg(leg)) continue;
                var a = lap[leg];
                var b = lap[(leg + 1) % lap.Count];
                if ((b.Position - a.Position).magnitude < settings.MinLegLength) continue;
                int kind = KindOf(ordinal++);

                if (Moves(kind))
                {
                    // A mover is laid whole, clear through its whole cycle, or not at all: then a curtain stands here.
                    offered.Clear();
                    offeredMovers.Clear();
                    Offer(a, b, leg, kind, offered, offeredMovers);
                    if (SweepsClear(offered, offeredMovers, course, leadIn, pads, lap, settings)
                        && RestStands(offered, course, leadIn, pads, lap, settings)
                        && ShellsMissTheThread(offered, offeredMovers, a, b))
                    {
                        if (movers != null)
                            foreach (var m in offeredMovers) movers.Add(m.From(parts.Count + m.First));
                        parts.AddRange(offered);
                        continue;
                    }
                    kind = Curtain;
                }

                offered.Clear();
                Offer(a, b, leg, kind, offered);
                int first = parts.Count;
                for (int i = 0; i < offered.Count; i++)
                    if (Stands(offered[i], course, leadIn, pads, lap, settings)) parts.Add(offered[i]);
                if (parts.Count - first < offered.Count * MinStandingShare)
                    parts.RemoveRange(first, parts.Count - first);
            }
        }

        /// <summary>Every prism of one barrier of <paramref name="kind"/> at the middle of the leg from
        /// <paramref name="a"/> to <paramref name="b"/>, before the clearances.</summary>
        public static void Offer(in RaceGate a, in RaceGate b, int leg, int kind, List<BreakwaterBarrierPart> into,
                                 List<BreakwaterBarrierMover> movers = null)
        {
            Vector3 centre = SpearlineRuins.LinePoint(a, b, 0.5f);
            Vector3 forward = SpearlineRuins.LineDirection(a, b, 0.5f);
            Vector3 right = Vector3.Cross(forward, Mathf.Abs(forward.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 up = Vector3.Cross(right, forward).normalized;
            switch (kind)
            {
                case Curtain:
                    Sheet(centre, forward, right, up, CurtainRadius, leg, kind, into);
                    break;
                case Turnstile:
                    for (int w = 0; w < 2; w++)
                    {
                        float side = w == 0 ? -1f : 1f;
                        Vector3 hub = centre + forward * (side * WheelOffset);
                        int first = into.Count;
                        Wheel(hub, forward, right, up, leg, into);
                        movers?.Add(new BreakwaterBarrierMover(Turnstile, hub, forward, right,
                                                               side * WheelDegreesPerSecond, first, into.Count - first));
                    }
                    break;
                case Shutter:
                {
                    int first = into.Count;
                    Sheet(centre, forward, right, up, ShutterRadius, leg, kind, into, 0.5f * PlatePitch);
                    // Out of step from leg to leg, so two shutters are not open at the same moment of a lap.
                    movers?.Add(new BreakwaterBarrierMover(Shutter, centre, forward, right, leg * 1.3f, first,
                                                           into.Count - first));
                    break;
                }
                case Flak:
                    Battery(centre, forward, right, up, leg, into, movers);
                    break;
                case Lock:
                    for (int n = 0; n < LockSheets; n++)
                        Sheet(centre + forward * ((n - 0.5f * (LockSheets - 1)) * LockSpacing), forward, right, up,
                              LockRadius, leg, kind, into);
                    break;
                default:
                    MineCloud(a, b, centre, forward, right, up, leg, into);
                    break;
            }
        }

        // A disc of plates across the line: every plate of a square grid whose centre is inside the radius.
        // offsetU moves the columns sideways: a shutter's are half a pitch off, so its seam is on the line.
        static void Sheet(Vector3 centre, Vector3 forward, Vector3 right, Vector3 up, float radius, int leg, int kind,
                          List<BreakwaterBarrierPart> into, float offsetU = 0f)
        {
            int half = Mathf.CeilToInt(radius / PlatePitch) + 1;
            var scale = new Vector3(PlateSize, PlateSize, PlateThickness);
            for (int x = -half; x <= half; x++)
                for (int y = -half; y <= half; y++)
                {
                    float u = x * PlatePitch + offsetU, v = y * PlatePitch;
                    if (u * u + v * v > radius * radius) continue;
                    into.Add(new BreakwaterBarrierPart(centre + right * u + up * v, forward, up, scale, leg, kind, false));
                }
        }

        // One wheel of a turnstile: blades at right angles, each a row of segments from the open hub outward.
        static void Wheel(Vector3 hub, Vector3 forward, Vector3 right, Vector3 up, int leg, List<BreakwaterBarrierPart> into)
        {
            var scale = new Vector3(BladeWidth, BladeThickness, BladeLength);
            for (int blade = 0; blade < WheelBlades; blade++)
            {
                float angle = blade * (2f * Mathf.PI / WheelBlades);
                Vector3 radial = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                for (int n = 0; n < BladeSegments; n++)
                {
                    float r = BladeInner + (n + 0.5f) * BladeLength;
                    // Long side outward, thin side along the line.
                    into.Add(new BreakwaterBarrierPart(hub + radial * r, radial, forward, scale, leg, Turnstile, false));
                }
            }
        }

        // A flak battery: the core on the line, then per gun a barrel behind its muzzle and its shells strung along
        // its chord. Gun g's chord turns g/FlakGuns of a circle round the line, so the crossfire covers every way
        // round the core; gun g fires g/FlakGuns of a shell-gap after gun 0.
        static void Battery(Vector3 centre, Vector3 forward, Vector3 right, Vector3 up, int leg,
                            List<BreakwaterBarrierPart> into, List<BreakwaterBarrierMover> movers)
        {
            var block = new Vector3(FlakCoreBlock, FlakCoreBlock, FlakCoreBlock);
            for (int k = 0; k < FlakCoreBlocks; k++)
            {
                float x = (k & 1) - 0.5f, y = ((k >> 1) & 1) - 0.5f, z = ((k >> 2) & 1) - 0.5f;
                into.Add(new BreakwaterBarrierPart(centre + (right * x + up * y + forward * z) * FlakCorePitch,
                                                   forward, up, block, leg, Flak, false, core: true));
            }
            var barrel = new Vector3(FlakBarrelWidth, FlakBarrelWidth, FlakBarrelLength);
            var shell = new Vector3(FlakShellSize, FlakShellSize, FlakShellSize);
            for (int g = 0; g < FlakGuns; g++)
            {
                float angle = g * (2f * Mathf.PI / FlakGuns) + leg;
                Vector3 across = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);   // the way it fires
                Vector3 aside = Vector3.Cross(forward, across);                       // its chord's offset
                float offset = g % 2 == 0 ? FlakInner : FlakOuter;
                float along = ((g * 3 % FlakGuns) / (float)(FlakGuns - 1) - 0.5f) * FlakDepth;
                Vector3 muzzle = centre + forward * along + aside * offset - across * (0.5f * FlakRange);
                Vector3 barrelUp = Mathf.Abs(Vector3.Dot(across, forward)) < 0.9f ? forward : aside;
                into.Add(new BreakwaterBarrierPart(muzzle - across * (0.5f * FlakBarrelLength), across, barrelUp, barrel,
                                                   leg, Flak, false));
                int first = into.Count;
                for (int n = 0; n < FlakShellsPerGun; n++)
                {
                    float flown = (n + g / (float)FlakGuns) / FlakShellsPerGun * FlakRange;
                    into.Add(new BreakwaterBarrierPart(muzzle + across * flown, across, barrelUp, shell, leg, Flak, true));
                }
                movers?.Add(new BreakwaterBarrierMover(Flak, muzzle, across, aside, FlakSpeed, first, FlakShellsPerGun));
            }
        }

        /// <summary>How far open a shutter is at <paramref name="seconds"/> of its cycle: 0 shut, 1 open.</summary>
        public static float ShutterOpening(float seconds)
        {
            float t = seconds % ShutterPeriod;
            if (t < 0f) t += ShutterPeriod;
            if (t < ShutterShut) return 0f;
            t -= ShutterShut;
            if (t < ShutterMove) return Smooth(t / ShutterMove);
            t -= ShutterMove;
            if (t < ShutterOpen) return 1f;
            t -= ShutterOpen;
            return 1f - Smooth(t / ShutterMove);
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);

        /// <summary>Where <paramref name="part"/> of <paramref name="mover"/> is at <paramref name="seconds"/> on the
        /// clock every peer shares.</summary>
        public static void Pose(in BreakwaterBarrierPart part, in BreakwaterBarrierMover mover, float seconds,
                                out Vector3 position, out Vector3 forward, out Vector3 up)
        {
            if (mover.Kind == Shutter)
            {
                float side = Vector3.Dot(part.Position - mover.Centre, mover.Right) >= 0f ? 1f : -1f;
                position = part.Position + mover.Right * (side * ShutterTravel * ShutterOpening(seconds + mover.Rate));
                forward = part.Forward;
                up = part.Up;
                return;
            }
            if (mover.Kind == Flak)
            {
                float rest = Vector3.Dot(part.Position - mover.Centre, mover.Axis);
                float flown = (rest + mover.Rate * seconds) % FlakRange;
                if (flown < 0f) flown += FlakRange;
                position = part.Position + mover.Axis * (flown - rest);
                forward = part.Forward;
                up = part.Up;
                return;
            }
            float angle = mover.Rate * seconds * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            position = mover.Centre + Turn(part.Position - mover.Centre, mover.Axis, cos, sin);
            forward = Turn(part.Forward, mover.Axis, cos, sin);
            up = Turn(part.Up, mover.Axis, cos, sin);
        }

        /// <summary>The seconds one cycle of <paramref name="mover"/> takes.</summary>
        public static float Period(in BreakwaterBarrierMover mover) =>
            mover.Kind == Shutter ? ShutterPeriod
            : mover.Kind == Flak ? FlakRange / Mathf.Abs(mover.Rate)
            : 360f / Mathf.Abs(mover.Rate);

        static Vector3 Turn(Vector3 p, Vector3 axis, float cos, float sin) =>
            p * cos + Vector3.Cross(axis, p) * sin + axis * (Vector3.Dot(axis, p) * (1f - cos));

        // Does every part of every mover stand at every sampled phase of its cycle?
        static bool SweepsClear(List<BreakwaterBarrierPart> offered, List<BreakwaterBarrierMover> movers,
                                IReadOnlyList<RaceGate> course, int leadIn, IReadOnlyList<Vector3> pads,
                                IReadOnlyList<RaceGate> lap, in SpearlineRuinSettings s)
        {
            foreach (var mover in movers)
            {
                float period = Period(mover);
                for (int i = mover.First; i < mover.First + mover.Count; i++)
                    for (int k = 0; k < SweepSamples; k++)
                    {
                        Pose(offered[i], mover, k * period / SweepSamples, out var p, out var f, out var u);
                        var posed = new BreakwaterBarrierPart(p, f, u, offered[i].Scale, offered[i].Leg, offered[i].Kind, false);
                        if (!Stands(posed, course, leadIn, pads, lap, s)) return false;
                    }
            }
            return true;
        }

        // Does every part stand where it was offered? (A battery's core and barrels are in no mover.)
        static bool RestStands(List<BreakwaterBarrierPart> offered, IReadOnlyList<RaceGate> course, int leadIn,
                               IReadOnlyList<Vector3> pads, IReadOnlyList<RaceGate> lap, in SpearlineRuinSettings s)
        {
            for (int i = 0; i < offered.Count; i++)
                if (!Stands(offered[i], course, leadIn, pads, lap, s)) return false;
            return true;
        }

        // Does every flak shell keep out of the thread the whole way down its flight? The line curves and a shell
        // flies straight, so this is checked and not assumed.
        static bool ShellsMissTheThread(List<BreakwaterBarrierPart> offered, List<BreakwaterBarrierMover> movers,
                                        in RaceGate a, in RaceGate b)
        {
            foreach (var mover in movers)
            {
                if (mover.Kind != Flak) continue;
                float period = Period(mover);
                for (int i = mover.First; i < mover.First + mover.Count; i++)
                    for (int k = 0; k < SweepSamples; k++)
                    {
                        Pose(offered[i], mover, k * period / SweepSamples, out var p, out _, out _);
                        if (LegDistance(a, b, p) - 0.5f * offered[i].Scale.magnitude < ThreadRadius) return false;
                    }
            }
            return true;
        }

        // Mines on a golden-angle spiral round the line, spread along it. A mine the curve of the line would bring
        // inside the thread is pushed straight out until it is clear.
        static void MineCloud(in RaceGate a, in RaceGate b, Vector3 centre, Vector3 forward, Vector3 right, Vector3 up,
                              int leg, List<BreakwaterBarrierPart> into)
        {
            const float golden = 2.39996323f;
            float reach = 0.5f * MineSize * 1.7320508f;          // a cube's corner
            float inner = ThreadRadius + reach + 1f, outer = MineRadius - reach;
            var scale = new Vector3(MineSize, MineSize, MineSize);
            for (int k = 0; k < Mines; k++)
            {
                float angle = k * golden + leg;
                float r = Mathf.Lerp(inner, outer, Mathf.Sqrt((k + 0.5f) / Mines));
                float along = ((k * 7 % Mines) / (float)(Mines - 1) - 0.5f) * MineDepth;
                Vector3 radial = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                Vector3 p = centre + forward * along + radial * r;
                for (int push = 0; push < 8 && LegDistance(a, b, p) < inner; push++) p += radial * 2f;
                into.Add(new BreakwaterBarrierPart(p, forward, up, scale, leg, Minefield, true));
            }
        }

        /// <summary>Does <paramref name="part"/> keep off every station's reach, every OTHER leg's lane and the way in
        /// from the pads? Its own leg is the one it is meant to stand across.</summary>
        public static bool Stands(in BreakwaterBarrierPart part, IReadOnlyList<RaceGate> course, int leadIn,
                                  IReadOnlyList<Vector3> pads, IReadOnlyList<RaceGate> lap, in SpearlineRuinSettings s)
        {
            float half = 0.5f * Mathf.Max(part.Scale.x, Mathf.Max(part.Scale.y, part.Scale.z));
            for (int g = 0; g < course.Count; g++)
                if ((part.Position - course[g].Position).magnitude - course[g].Radius < s.RingMargin + half) return false;
            for (int leg = 0; leg < lap.Count; leg++)
                if (leg != part.Leg && SpearlineRuins.LegDistance(lap, leg, part.Position) < s.ClearRadius + half)
                    return false;
            var asRuin = new SpearlineRuinPart(part.Position, part.Forward, part.Up, part.Scale, part.Leg);
            return !BreakwaterRuins.ReachesTheStart(asRuin, course, leadIn, pads, s, 0f);
        }

        /// <summary>How far <paramref name="p"/> is from the flown line from <paramref name="a"/> to <paramref name="b"/>.</summary>
        public static float LegDistance(in RaceGate a, in RaceGate b, Vector3 p)
        {
            float best = float.MaxValue;
            Vector3 prev = a.Position;
            for (int k = 1; k <= SpearlineRuins.LineSamples; k++)
            {
                Vector3 cur = SpearlineRuins.LinePoint(a, b, k / (float)SpearlineRuins.LineSamples);
                Vector3 ab = cur - prev;
                float len = ab.sqrMagnitude;
                float t = len > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - prev, ab) / len) : 0f;
                best = Mathf.Min(best, (p - (prev + ab * t)).magnitude);
                prev = cur;
            }
            return best;
        }
    }
}
