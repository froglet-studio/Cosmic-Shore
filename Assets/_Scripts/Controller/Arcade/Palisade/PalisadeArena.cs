using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One rung of Palisade's arena (PALISADE.md section 9): how much helps the Swordfish and how much costs it.</summary>
    public struct PalisadeArenaSettings
    {
        public int Lanes;             // picket lanes, each ending in a frame
        public int PostsPerLane;      // posts in a lane's row: the straight fuel a pilot slices
        public int ShieldEvery;       // every Nth post is shielded (two strokes, or a ram); 0 = every post is plain
        public float FrameClosure;    // the share of a frame's hoop that stands (1 = a closed hoop)
        public bool DangerTips;       // the two ends of a broken frame are danger spikes
        public int Rubble;            // loose chunks between the lanes
        public int RubbleDangerEvery; // every Nth chunk is danger, the rest shielded; 0 = none is danger
    }

    /// <summary>One arena prism, relative to the cell's centre: where it stands, how it is turned (forward and up, as
    /// <c>Quaternion.LookRotation</c> takes them), its size (long along <see cref="Forward"/>), its kind and what it is
    /// part of.</summary>
    public readonly struct PalisadeArenaPart
    {
        public readonly Vector3 Position, Forward, Up, Scale;
        public readonly PrismKind Kind;

        /// <summary><see cref="PalisadeArena.Post"/>, <see cref="PalisadeArena.Frame"/> or <see cref="PalisadeArena.Rubble"/>.</summary>
        public readonly int Piece;

        /// <summary>The lane this post or frame stone belongs to; -1 for rubble.</summary>
        public readonly int Lane;

        public PalisadeArenaPart(Vector3 position, Vector3 forward, Vector3 up, Vector3 scale, PrismKind kind, int piece, int lane)
        {
            Position = position;
            Forward = forward;
            Up = up;
            Scale = scale;
            Kind = kind;
            Piece = piece;
            Lane = lane;
        }
    }

    /// <summary>One picket lane, relative to the cell's centre. A pilot flies from <see cref="RunStart"/> to
    /// <see cref="FrameCentre"/>: beside the row of posts, in blade reach of every one, and out through the frame.</summary>
    public readonly struct PalisadeLane
    {
        /// <summary>The first and last post of the row.</summary>
        public readonly Vector3 FirstPost, LastPost;

        /// <summary>Where the run beside the row begins, and the centre of the frame it ends in.</summary>
        public readonly Vector3 RunStart, FrameCentre;

        /// <summary>The lane's direction (unit), and the side of the row the run is on (unit, away from the cell's centre).</summary>
        public readonly Vector3 Axis, Side;

        public PalisadeLane(Vector3 firstPost, Vector3 lastPost, Vector3 runStart, Vector3 frameCentre, Vector3 axis, Vector3 side)
        {
            FirstPost = firstPost;
            LastPost = lastPost;
            RunStart = runStart;
            FrameCentre = frameCentre;
            Axis = axis;
            Side = side;
        }

        /// <summary>The far end of the run beside the row: level with the last post, still to its side.</summary>
        public Vector3 RunEnd => LastPost + Side * PalisadeArena.RunOffset;

        /// <summary>The straight a pilot gets to wind the ramp on: from the run's start to the frame.</summary>
        public float StraightLength => Vector3.Distance(RunStart, FrameCentre);
    }

    /// <summary>
    /// Palisade's arena (Yash, 2026-11-25: "environmental objects and shapes are placed so they give an advantage or a
    /// disadvantage and also look good"). Three pieces, each there for what the Swordfish's kit does with it:
    ///
    /// <list type="bullet">
    /// <item>A <b>picket lane</b>: a straight row of thin posts. The blade slices them in passing, which fuels the ramp
    /// and lights the sword, so the afterimage window opens as the row ends. Plain posts help; a shielded post takes
    /// two strokes, and a missed one is a ram.</item>
    /// <item>A <b>frame</b> where each lane ends: a hoop of shielded stones round an opening a few blade lengths wide.
    /// An afterimage wall laid in it is flanked by shielded mass, so a raid has to come through the mouth. A broken
    /// frame leaves a side open toward the cell's centre, and from I3 its two ends are danger spikes.</item>
    /// <item><b>Rubble</b>: loose shielded and danger chunks between the lanes, which break a ramp line and cost a
    /// stroke or a debuff.</item>
    /// </list>
    ///
    /// <para>The rungs trade help for cost: I1 is long plain lanes into closed frames and nothing else; I4 is short,
    /// half-shielded lanes into slivers of frame, in a field of rubble. Everything is laid in <see cref="Domains.Blue"/>
    /// by the controller - the platform's no-team colour, hostile to every blade and counted in no team's standing
    /// volume (<c>StatsManager.PrismCreated</c> credits a roster name, and these have none).</para>
    ///
    /// <para>Pure: a function of the rung alone, with every draw a hash of an integer, so every peer lays the same
    /// arena with no message and the edit-mode tests hold each placement rule (<c>PalisadeArenaTests</c>). Nothing here
    /// touches the vessel.</para>
    /// </summary>
    public static class PalisadeArena
    {
        public const int Post = 0, Frame = 1, Rubble = 2;

        static readonly PalisadeArenaSettings[] ByRung =
        {
            new() { Lanes = 3, PostsPerLane = 14, ShieldEvery = 0, FrameClosure = 1f,    DangerTips = false, Rubble = 0,  RubbleDangerEvery = 0 },
            new() { Lanes = 3, PostsPerLane = 10, ShieldEvery = 5, FrameClosure = 0.75f, DangerTips = false, Rubble = 0,  RubbleDangerEvery = 0 },
            new() { Lanes = 2, PostsPerLane = 8,  ShieldEvery = 3, FrameClosure = 0.5f,  DangerTips = true,  Rubble = 10, RubbleDangerEvery = 0 },
            new() { Lanes = 2, PostsPerLane = 6,  ShieldEvery = 2, FrameClosure = 1f / 3f, DangerTips = true, Rubble = 18, RubbleDangerEvery = 3 },
        };

        /// <summary>Nothing stands nearer the cell's centre than this (u): the spawn ring and the nucleus are inside it.</summary>
        public const float InnerRadius = 600f;

        /// <summary>Nothing stands farther from the cell's centre than this (u): the membrane is outside it.</summary>
        public const float OuterRadius = 950f;

        /// <summary>A lane's middle is this far (u) from the cell's centre; the row runs across the radius there.</summary>
        public const float LaneRadius = 640f;

        /// <summary>Posts stand this far apart (u) down the row.</summary>
        public const float PostSpacing = 30f;

        /// <summary>The run is this far (u) to the side of the row: inside the blade's reach at rest (24 u).</summary>
        public const float RunOffset = 14f;

        /// <summary>The blade's reach at rest (u), from SWORDFISH.md (Blade Reach 24 -> 40). Read, never set.</summary>
        public const float BladeReach = 24f;

        /// <summary>The run starts this far (u) before the first post, and the frame stands this far past the last.</summary>
        public const float RunIn = 60f, FrameGap = 90f;

        /// <summary>The frame's opening (u, radius) and how many stones a closed hoop has.</summary>
        public const float FrameRadius = 30f;
        public const int FrameStones = 12;

        /// <summary>Rubble keeps this far (u) from every lane's run and frame, and from other rubble.</summary>
        public const float RubbleLaneClearance = 90f, RubbleSpacing = 60f;

        /// <summary>No arena prism is thinner than this (u) on any axis.</summary>
        public const float MinThickness = 8f;

        static readonly Vector3 PostScale = new(8f, 8f, 34f);
        static readonly Vector3 StoneScale = new(12f, 12f, 16f);
        static readonly Vector3 TipScale = new(9f, 9f, 28f);

        public static PalisadeArenaSettings ForIntensity(int intensity) => ByRung[Mathf.Clamp(intensity, 1, 4) - 1];

        /// <summary>Lay the rung's arena into <paramref name="parts"/> and its lanes into <paramref name="lanes"/>
        /// (both cleared first), relative to the cell's centre.</summary>
        public static void Lay(PalisadeArenaSettings s, List<PalisadeArenaPart> parts, List<PalisadeLane> lanes)
        {
            parts.Clear();
            lanes.Clear();
            for (int k = 0; k < s.Lanes; k++)
            {
                var lane = LaneOf(k, s);
                lanes.Add(lane);
                LayPosts(k, lane, s, parts);
                LayFrame(k, lane, s, parts);
            }
            LayRubble(s, lanes, parts);
        }

        /// <summary>
        /// Lane <paramref name="k"/> of the rung: lanes are spread evenly round the cell and tipped alternately above
        /// and below its equator, and each row runs across its radius with a roll of its own, so no two lanes are
        /// parallel and the arena reads as a place from any side.
        /// </summary>
        public static PalisadeLane LaneOf(int k, PalisadeArenaSettings s)
        {
            float around = (360f / Mathf.Max(1, s.Lanes)) * k + 20f;
            float tip = (k % 2 == 0) ? 24f : -24f;
            Vector3 flat = Quaternion.AngleAxis(around, Vector3.up) * Vector3.forward;
            Vector3 tipAxis = Vector3.Cross(Vector3.up, flat).normalized;
            Vector3 radial = (Quaternion.AngleAxis(-tip, tipAxis) * flat).normalized;

            Vector3 across = Vector3.Cross(Vector3.up, radial).normalized;
            Vector3 axis = (Quaternion.AngleAxis(18f + 47f * k, radial) * across).normalized;

            Vector3 middle = radial * LaneRadius;
            float half = (s.PostsPerLane - 1) * PostSpacing * 0.5f;
            Vector3 first = middle - axis * half;
            Vector3 last = middle + axis * half;
            return new PalisadeLane(first, last, first - axis * RunIn + radial * RunOffset, last + axis * FrameGap, axis, radial);
        }

        static void LayPosts(int k, PalisadeLane lane, PalisadeArenaSettings s, List<PalisadeArenaPart> parts)
        {
            Vector3 across = Vector3.Cross(lane.Axis, lane.Side).normalized;
            for (int i = 0; i < s.PostsPerLane; i++)
            {
                bool shielded = s.ShieldEvery > 0 && i % s.ShieldEvery == s.ShieldEvery - 1;
                // A post stands ACROSS both the lane and the run's side, so the row is a picket seen from the run
                // beside it and only its thin side faces the hull.
                parts.Add(new PalisadeArenaPart(lane.FirstPost + lane.Axis * (i * PostSpacing), across, lane.Axis,
                    PostScale, shielded ? PrismKind.Shielded : PrismKind.Plain, Post, k));
            }
        }

        /// <summary>How many of a hoop's <see cref="FrameStones"/> stand at <paramref name="closure"/>.</summary>
        public static int StonesStanding(float closure) =>
            Mathf.Clamp(Mathf.RoundToInt(FrameStones * closure), 2, FrameStones);

        static void LayFrame(int k, PalisadeLane lane, PalisadeArenaSettings s, List<PalisadeArenaPart> parts)
        {
            int standing = StonesStanding(s.FrameClosure);
            Vector3 other = Vector3.Cross(lane.Axis, lane.Side).normalized;
            float step = 360f / FrameStones;
            // The standing arc is centred on the side AWAY from the cell's centre, so what is missing faces the centre:
            // a broken frame covers a wall from outside and leaves it open to the traffic inside.
            float firstAngle = -(standing - 1) * step * 0.5f;
            for (int j = 0; j < standing; j++)
            {
                float angle = (firstAngle + j * step) * Mathf.Deg2Rad;
                Vector3 radial = lane.Side * Mathf.Cos(angle) + other * Mathf.Sin(angle);
                Vector3 position = lane.FrameCentre + radial * (FrameRadius + StoneScale.x * 0.5f);
                bool tip = s.DangerTips && standing < FrameStones && (j == 0 || j == standing - 1);
                // A stone is long down the lane, so the hoop is a collar with depth; a tip is a longer danger spike.
                parts.Add(new PalisadeArenaPart(position, lane.Axis, radial, tip ? TipScale : StoneScale,
                    tip ? PrismKind.Danger : PrismKind.Shielded, Frame, k));
            }
        }

        static void LayRubble(PalisadeArenaSettings s, List<PalisadeLane> lanes, List<PalisadeArenaPart> parts)
        {
            int firstRubble = parts.Count;
            for (int j = 0; j < s.Rubble; j++)
            {
                for (int attempt = 0; attempt < 64; attempt++)
                {
                    int key = j * 64 + attempt;
                    float z = Hash01(key * 4 + 1) * 2f - 1f;
                    float around = Hash01(key * 4 + 2) * Mathf.PI * 2f;
                    float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                    var direction = new Vector3(ring * Mathf.Cos(around), z, ring * Mathf.Sin(around));
                    float size = 14f + 8f * Hash01(key * 4 + 3);
                    float margin = size;   // a chunk's corner reaches under its size from its centre
                    float radius = Mathf.Lerp(InnerRadius + margin, OuterRadius - margin, Hash01(key * 4 + 4));
                    Vector3 position = direction * radius;

                    if (!RubbleFits(position, lanes, parts, firstRubble)) continue;

                    bool danger = s.RubbleDangerEvery > 0 && j % s.RubbleDangerEvery == s.RubbleDangerEvery - 1;
                    // Turned off the radius by a hashed tilt, so the chunks do not all face the centre alike.
                    Vector3 tilt = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                    Vector3 forward = (Quaternion.AngleAxis(Hash01(key * 4 + 5) * 360f, direction) * tilt).normalized;
                    parts.Add(new PalisadeArenaPart(position, forward, direction,
                        new Vector3(size, size * 0.75f, size * 1.25f), danger ? PrismKind.Danger : PrismKind.Shielded, Rubble, -1));
                    break;
                }
            }
        }

        static bool RubbleFits(Vector3 position, List<PalisadeLane> lanes, List<PalisadeArenaPart> parts, int firstRubble)
        {
            for (int k = 0; k < lanes.Count; k++)
                if (DistanceToRun(position, lanes[k]) < RubbleLaneClearance) return false;
            for (int i = firstRubble; i < parts.Count; i++)
                if (Vector3.Distance(position, parts[i].Position) < RubbleSpacing) return false;
            return true;
        }

        /// <summary>How far <paramref name="point"/> is from a lane: from its run, its row and its frame taken as one
        /// segment, from the run's start to a frame's radius past the frame.</summary>
        public static float DistanceToRun(Vector3 point, PalisadeLane lane)
        {
            Vector3 a = lane.FirstPost - lane.Axis * RunIn;
            Vector3 b = lane.FrameCentre + lane.Axis * FrameRadius;
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector3.Distance(point, a + ab * t);
        }

        /// <summary>A draw in [0, 1) from an integer key: the same on every peer and every run.</summary>
        public static float Hash01(int key)
        {
            unchecked
            {
                uint h = (uint)key * 2654435761u + 0x9E3779B9u;
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return (h >> 8) / 16777216f;
            }
        }
    }
}
