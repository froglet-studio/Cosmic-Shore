using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using Tk = CosmicShore.Gameplay.PaintingStrokeToolkit;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Deterministic artwork generation for the Fake Artist minigame. Given
    /// (preset, seed, playerCount) it produces exactly playerCount x strokesPerPlayer
    /// medium strokes: the preset's strokes (fixed internal seeds, so identical on every
    /// machine) run through a seeded parametric variation (yaw, mirror, scale jitter,
    /// mild curl-field warp - "players never play the same thing twice"), then get
    /// repartitioned by splitting the longest strokes / dropping the shortest until the
    /// count matches, and finally reordered by OrderForFlightContinuity so a contiguous
    /// deal gives each player a spatially-coherent bundle.
    ///
    /// Pure and allocation-only - no UnityEngine.Object access - so the SERVER generates
    /// gameplay data from it (per-player stroke dots go out via targeted ClientRpcs and
    /// stay secret), and every CLIENT regenerates the identical artwork locally at
    /// reveal time from just (preset, size, seed).
    /// </summary>
    public static class FakeArtistArtworkBuilder
    {
        /// <summary>Presets used for round artwork + as decoy subject options.</summary>
        public static readonly PaintingPreset[] SubjectCatalog =
        {
            PaintingPreset.Star, PaintingPreset.Rainbow, PaintingPreset.Saturn,
            PaintingPreset.TajMahal, PaintingPreset.Nautilus, PaintingPreset.Lotus,
            PaintingPreset.Buckyball, PaintingPreset.TorusKnot, PaintingPreset.DoubleHelix,
            PaintingPreset.SpiralGalaxy, PaintingPreset.LionsHead, PaintingPreset.Phoenix,
            PaintingPreset.Peacock, PaintingPreset.Rose, PaintingPreset.StarryNight,
            PaintingPreset.BobRossVista,
        };

        /// <summary>Player-facing subject names for the vote's multiple choice.</summary>
        public static string SubjectName(PaintingPreset preset) => preset switch
        {
            PaintingPreset.Star => "Star",
            PaintingPreset.Rainbow => "Rainbow",
            PaintingPreset.Saturn => "Saturn",
            PaintingPreset.TajMahal => "Taj Mahal",
            PaintingPreset.Nautilus => "Nautilus",
            PaintingPreset.Lotus => "Lotus",
            PaintingPreset.Buckyball => "Buckyball",
            PaintingPreset.TorusKnot => "Torus Knot",
            PaintingPreset.DoubleHelix => "Double Helix",
            PaintingPreset.SpiralGalaxy => "Spiral Galaxy",
            PaintingPreset.LionsHead => "Lion's Head",
            PaintingPreset.Phoenix => "Phoenix",
            PaintingPreset.Peacock => "Peacock",
            PaintingPreset.Rose => "Rose",
            PaintingPreset.StarryNight => "Starry Night",
            PaintingPreset.BobRossVista => "Mountain Vista",
            _ => preset.ToString(),
        };

        /// <summary>
        /// Builds the round's strokes in artwork-LOCAL space (y=0 base like the presets),
        /// exactly <paramref name="strokeCount"/> of them, flight-continuity ordered.
        /// Deterministic for a given (preset, size, seed, strokeCount).
        /// </summary>
        public static List<PaintingStroke> BuildStrokes(PaintingPreset preset, float size, int seed, int strokeCount)
        {
            if (strokeCount < 1) strokeCount = 1;

            var strokes = PaintingPresetLibrary.Generate(preset, size);
            strokes = Vary(strokes, size, seed);
            strokes = Repartition(strokes, strokeCount, size);
            strokes = Tk.OrderForFlightContinuity(strokes);

            for (int i = 0; i < strokes.Count; i++)
                strokes[i].name = $"Stroke {i + 1}";

            return strokes;
        }

        /// <summary>
        /// Contiguous deal in flight order: player p gets strokes
        /// [p*strokesPerPlayer .. p*strokesPerPlayer + strokesPerPlayer - 1], so each
        /// bundle is spatially coherent (flight-continuity ordering chains neighbors).
        /// </summary>
        public static List<PaintingStroke>[] Deal(List<PaintingStroke> strokes, int playerCount, int strokesPerPlayer)
        {
            var bundles = new List<PaintingStroke>[playerCount];
            for (int p = 0; p < playerCount; p++)
            {
                bundles[p] = new List<PaintingStroke>(strokesPerPlayer);
                for (int s = 0; s < strokesPerPlayer; s++)
                {
                    int idx = p * strokesPerPlayer + s;
                    if (idx < strokes.Count)
                        bundles[p].Add(strokes[idx]);
                }
            }
            return bundles;
        }

        /// <summary>
        /// The sparse "connect the dots" ride dots for one stroke, in the same space the
        /// points are in. These are what players fly (rings for honest artists, first+last
        /// only for the fake artist) - between dots the pilot improvises, which is the game.
        /// </summary>
        public static List<Vector3> BuildDots(IReadOnlyList<Vector3> points, float reach)
        {
            var dots = new List<Vector3>();
            if (points == null || points.Count == 0) return dots;

            float len = ArcLength(points);
            float spacing = Mathf.Max(reach * 3f, len / 5f);
            var indices = Tk.RideCheckpoints(points, spacing, 28f);
            for (int i = 0; i < indices.Count; i++)
                dots.Add(points[indices[i]]);
            return dots;
        }

        /// <summary>
        /// Deterministic artwork anchor for a round: successive rounds place their canvas
        /// around a golden-angle ring so finished artworks accumulate as a gallery (mass is
        /// conserved - nothing is culled between rounds; the scene reload on replay is the
        /// only sink). Rotation faces the canvas back toward the arena center.
        /// </summary>
        public static void AnchorForRound(int roundIndex, float artSize, out Vector3 origin, out Quaternion rotation)
        {
            const float goldenAngle = 137.50776f;
            float angle = roundIndex * goldenAngle;
            float radius = artSize * 1.35f + 220f;
            var dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            origin = dir * radius;
            rotation = Quaternion.LookRotation(-dir, Vector3.up);
        }

        // ── Parametric variation ─────────────────────────────────────────────

        static List<PaintingStroke> Vary(List<PaintingStroke> source, float size, int seed)
        {
            var rng = new Tk.Rng(seed);

            float yaw = rng.Range(0f, 360f);
            bool mirror = rng.Chance(0.5f);
            float scale = rng.Range(0.85f, 1.1f);
            float warpAmp = size * rng.Range(0.015f, 0.045f);
            float warpFreq = 2.2f / Mathf.Max(1f, size);
            int warpSeed = (int)rng.NextUInt();

            var rot = Quaternion.Euler(0f, yaw, 0f);
            var result = new List<PaintingStroke>(source.Count);
            float minY = float.MaxValue;

            foreach (var stroke in source)
            {
                var pts = new List<Vector3>(stroke.points.Count);
                foreach (var raw in stroke.points)
                {
                    var p = raw;
                    if (mirror) p.x = -p.x;
                    p = rot * (p * scale);
                    p += Tk.CurlNoise(p, warpFreq, warpSeed) * warpAmp;
                    pts.Add(p);
                    if (p.y < minY) minY = p.y;
                }
                result.Add(new PaintingStroke { name = stroke.name, domain = stroke.domain, points = pts });
            }

            // Re-base to the ground plane like the presets (warp can dip below y=0).
            if (minY < float.MaxValue && Mathf.Abs(minY) > 0.001f)
            {
                foreach (var stroke in result)
                    for (int i = 0; i < stroke.points.Count; i++)
                        stroke.points[i] -= new Vector3(0f, minY, 0f);
            }

            return result;
        }

        // ── Repartition to an exact stroke count ─────────────────────────────

        static List<PaintingStroke> Repartition(List<PaintingStroke> strokes, int target, float size)
        {
            var work = new List<PaintingStroke>(strokes);

            // Ensure every stroke has enough vertices to split at any arc position.
            float maxSeg = Mathf.Max(4f, size * 0.03f);
            for (int i = 0; i < work.Count; i++)
            {
                if (work[i].points.Count >= 2)
                    work[i].points = Tk.EnforceMaxSegment(work[i].points, maxSeg);
            }

            // Too many strokes: drop the shortest (fine detail) until we fit.
            while (work.Count > target)
            {
                int shortest = 0;
                float shortestLen = float.MaxValue;
                for (int i = 0; i < work.Count; i++)
                {
                    float len = ArcLength(work[i].points);
                    if (len < shortestLen)
                    {
                        shortestLen = len;
                        shortest = i;
                    }
                }
                work.RemoveAt(shortest);
            }

            // Too few strokes: split the longest at its arc midpoint until we fit.
            // Splitting the longest first also equalizes lengths toward "medium".
            int guard = target * 4 + 16;
            while (work.Count < target && guard-- > 0)
            {
                int longest = -1;
                float longestLen = 0f;
                for (int i = 0; i < work.Count; i++)
                {
                    if (work[i].points.Count < 4) continue; // both halves need >= 2 points
                    float len = ArcLength(work[i].points);
                    if (len > longestLen)
                    {
                        longestLen = len;
                        longest = i;
                    }
                }
                if (longest < 0) break; // nothing splittable

                var stroke = work[longest];
                int cut = ArcMidpointIndex(stroke.points);

                var a = new PaintingStroke
                {
                    name = stroke.name,
                    domain = stroke.domain,
                    points = new List<Vector3>(stroke.points.GetRange(0, cut + 1)),
                };
                var b = new PaintingStroke
                {
                    name = stroke.name,
                    domain = stroke.domain,
                    points = new List<Vector3>(stroke.points.GetRange(cut, stroke.points.Count - cut)),
                };

                work[longest] = a;
                work.Insert(longest + 1, b);
            }

            return work;
        }

        /// <summary>Index nearest the arc-length midpoint, clamped so both halves keep >= 2 points.</summary>
        static int ArcMidpointIndex(IReadOnlyList<Vector3> pts)
        {
            float half = ArcLength(pts) * 0.5f;
            float acc = 0f;
            for (int i = 1; i < pts.Count; i++)
            {
                acc += Vector3.Distance(pts[i - 1], pts[i]);
                if (acc >= half)
                    return Mathf.Clamp(i, 1, pts.Count - 2);
            }
            return Mathf.Clamp(pts.Count / 2, 1, pts.Count - 2);
        }

        public static float ArcLength(IReadOnlyList<Vector3> pts)
        {
            if (pts == null || pts.Count < 2) return 0f;
            float len = 0f;
            for (int i = 1; i < pts.Count; i++)
                len += Vector3.Distance(pts[i - 1], pts[i]);
            return len;
        }

        /// <summary>
        /// Picks the round's subject + decoys deterministically from the seed: the correct
        /// subject plus (choiceCount-1) distinct decoys, shuffled so the correct answer's
        /// position carries no information.
        /// </summary>
        public static List<PaintingPreset> BuildSubjectChoices(PaintingPreset correct, int seed, int choiceCount)
        {
            var rng = new Tk.Rng(seed ^ 0x5EED);
            var pool = new List<PaintingPreset>(SubjectCatalog);
            pool.Remove(correct);

            var choices = new List<PaintingPreset> { correct };
            int want = Mathf.Clamp(choiceCount, 2, SubjectCatalog.Length);
            while (choices.Count < want && pool.Count > 0)
            {
                int pick = rng.RangeInt(0, pool.Count);
                choices.Add(pool[pick]);
                pool.RemoveAt(pick);
            }

            // Fisher-Yates so the correct option isn't always first.
            for (int i = choices.Count - 1; i > 0; i--)
            {
                int j = rng.RangeInt(0, i + 1);
                (choices[i], choices[j]) = (choices[j], choices[i]);
            }
            return choices;
        }
    }
}
