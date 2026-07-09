using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using Tk = CosmicShore.Gameplay.PaintingStrokeToolkit;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Procedural stroke generators for the built-in <see cref="PaintingPreset"/>s, plus the
    /// <see cref="ShapeDefinition"/> converter. Pure geometry — no scene access — so it is unit-testable.
    ///
    /// Conventions: paintings are authored in local space with their base at y=0 and their front
    /// facing +Z (the toy ring / the approaching player). Strokes are ordered bottom-up in build
    /// order, and batched by domain where it reads well so the player switches colour at meaningful
    /// architectural boundaries rather than at random.
    ///
    /// AUTHORING RULE — order strokes by DECREASING radius of curvature: long straight / broad
    /// strokes first (pools, plinths), tight detail last (balcony rings, crescents). The painting
    /// then doubles as its own difficulty ramp, and the runner's adaptive reach (tighter on short
    /// segments) ramps with it. See Docs/ToySystem/ARCHITECTURE.md § "Authoring rule".
    /// </summary>
    public static class PaintingPresetLibrary
    {
        public static List<PaintingStroke> Generate(PaintingPreset preset, float size)
        {
            float s = Mathf.Max(1f, size);
            var strokes = preset switch
            {
                PaintingPreset.Star => Star(s),
                PaintingPreset.Rainbow => Rainbow(s),
                PaintingPreset.Saturn => Saturn(s),
                PaintingPreset.TajMahal => TajMahal(s),
                PaintingPreset.Nautilus => Nautilus(s),
                PaintingPreset.Lotus => Lotus(s),
                PaintingPreset.Buckyball => Buckyball(s),
                PaintingPreset.TorusKnot => TorusKnotPreset(s),
                PaintingPreset.DoubleHelix => DoubleHelix(s),
                PaintingPreset.SpiralGalaxy => SpiralGalaxy(s),
                PaintingPreset.LionsHead => LionsHead(s),
                PaintingPreset.Phoenix => Phoenix(s),
                PaintingPreset.Peacock => Peacock(s),
                PaintingPreset.Rose => Rose(s),
                PaintingPreset.StarryNight => StarryNight(s),
                PaintingPreset.BobRossVista => BobRossVista(s),
                _ => new List<PaintingStroke>(),
            };
            // Safety net for every grandiose generator: the runner assumes the base plane sits at
            // y=0, and the impressionist fills can drift a touch below their authored floor. Lift so
            // the lowest point of the whole painting sits exactly on the ground.
            if (preset >= PaintingPreset.Nautilus)
                PaintingStrokeToolkit.RebaseToGround(strokes);
            return strokes;
        }

        /// <summary>
        /// Convert a legacy <see cref="ShapeDefinition"/> into painting strokes: every pen-up entry in
        /// <c>trailEnabledPerSegment</c> ends the current stroke, so smiley eyes / lightning forks become
        /// separate strokes with proper pen-up flight between them. All strokes share one domain.
        /// </summary>
        public static List<PaintingStroke> FromShape(ShapeDefinition shape, Domains domain, float scale)
        {
            var result = new List<PaintingStroke>();
            if (!shape) return result;

            shape.EnsureWaypoints();
            var pts = shape.waypoints;
            if (pts == null || pts.Count < 2) return result;

            // Shapes are authored around their own centroid in XY; re-base so the lowest point sits at y=0.
            float minY = float.MaxValue;
            foreach (var p in pts) minY = Mathf.Min(minY, p.y * scale);

            var current = new List<Vector3>();
            int strokeIndex = 1;
            for (int i = 0; i < pts.Count; i++)
            {
                current.Add(new Vector3(pts[i].x * scale, pts[i].y * scale - minY, pts[i].z * scale));

                // trailEnabledPerSegment[i] == false means "pen up while flying TOWARD waypoint i+1…"
                bool penUpAfter = i < pts.Count - 1 && !shape.IsTrailEnabledForSegment(i + 1);
                if ((penUpAfter || i == pts.Count - 1) && current.Count >= 2)
                {
                    result.Add(new PaintingStroke
                    {
                        name = $"{shape.shapeName} {ToRoman(strokeIndex++)}",
                        domain = domain,
                        points = current,
                    });
                    current = new List<Vector3>();
                }
                else if (penUpAfter)
                {
                    current = new List<Vector3>();
                }
            }

            return result;
        }

        /// <summary>Axis-aligned local bounds over every stroke point. Zero-size when empty.</summary>
        public static Bounds ComputeBounds(IReadOnlyList<PaintingStroke> strokes)
        {
            bool any = false;
            var b = new Bounds(Vector3.zero, Vector3.zero);
            if (strokes == null) return b;

            foreach (var stroke in strokes)
            {
                if (stroke?.points == null) continue;
                foreach (var p in stroke.points)
                {
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>Total flight length of every stroke, world units (for tests / tuning).</summary>
        public static float TotalPathLength(IReadOnlyList<PaintingStroke> strokes)
        {
            float len = 0f;
            if (strokes == null) return len;
            foreach (var stroke in strokes)
            {
                if (stroke?.points == null) continue;
                for (int i = 1; i < stroke.points.Count; i++)
                    len += Vector3.Distance(stroke.points[i - 1], stroke.points[i]);
            }
            return len;
        }

        // ── Low end: Star — one big stroke, one colour ───────────────────────────

        static List<PaintingStroke> Star(float size)
        {
            float r = size * 0.5f;
            float cy = r * 1.1f; // base clears y=0
            var pts = new List<Vector3>();
            const int points = 6;
            for (int i = 0; i <= points * 2; i++)
            {
                float angle = (i / (float)(points * 2)) * Mathf.PI * 2f - Mathf.PI / 2f;
                float rad = (i % 2 == 0) ? r : r * 0.42f;
                pts.Add(new Vector3(Mathf.Cos(angle) * rad, cy + Mathf.Sin(angle) * rad, 0f));
            }
            return new List<PaintingStroke>
            {
                new() { name = "Star", domain = Domains.Gold, points = pts },
            };
        }

        // ── Low-mid: Rainbow — three arcs, one per domain (teaches the gates) ────

        static List<PaintingStroke> Rainbow(float size)
        {
            var strokes = new List<PaintingStroke>
            {
                new() { name = "Ruby Band", domain = Domains.Ruby, points = Semicircle(size * 0.50f, false) },
                new() { name = "Gold Band", domain = Domains.Gold, points = Semicircle(size * 0.41f, true) },
                new() { name = "Jade Band", domain = Domains.Jade, points = Semicircle(size * 0.32f, false) },
            };
            return strokes;

            static List<Vector3> Semicircle(float r, bool rightToLeft)
            {
                var pts = new List<Vector3>();
                const int segs = 14;
                for (int i = 0; i <= segs; i++)
                {
                    float t = i / (float)segs;
                    float a = Mathf.PI * (rightToLeft ? t : 1f - t);
                    pts.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
                }
                return pts;
            }
        }

        // ── Mid: Saturn — planet + two tilted rings (first genuinely 3D flight) ──

        static List<PaintingStroke> Saturn(float size)
        {
            float cy = size * 0.62f;
            var center = new Vector3(0f, cy, 0f);

            // Planet: a vertical great circle facing the viewer.
            var planet = Circle(center, Vector3.right, Vector3.up, size * 0.28f, 20);

            // Rings: circles in a plane tilted out of the picture — real 3D flying.
            Quaternion tilt = Quaternion.AngleAxis(24f, Vector3.right);
            Vector3 u = tilt * Vector3.right;
            Vector3 v = tilt * Vector3.forward;
            var outer = Circle(center, u, v, size * 0.50f, 24);
            var inner = Circle(center, u, v, size * 0.40f, 20);

            return new List<PaintingStroke>
            {
                new() { name = "Planet", domain = Domains.Gold, points = planet },
                new() { name = "Outer Ring", domain = Domains.Ruby, points = outer },
                new() { name = "Inner Ring", domain = Domains.Jade, points = inner },
            };
        }

        // ── High end: the Taj Mahal ──────────────────────────────────────────────
        //
        // size = full plinth width W. Height comes out ≈ 0.69·W (dome finial), with four minarets,
        // a chamfered main body, the grand iwan + niches, an onion dome drawn as a rib cage, four
        // chhatris, and a jade reflecting pool + charbagh gardens running toward the viewer (+Z).
        // ~55 strokes across all three domains, ordered ground-up in build order.

        static List<PaintingStroke> TajMahal(float size)
        {
            float W = size;
            var strokes = new List<PaintingStroke>();

            // Master proportions (fractions of W).
            float plinthHalf = 0.50f * W;
            float plinthH = 0.055f * W;
            float bodyHalf = 0.21f * W;      // a
            float chamfer = 0.075f * W;      // c
            float y0 = plinthH;              // body base
            float y1 = plinthH + 0.30f * W;  // body cornice / roofline
            float drumTop = y1 + 0.045f * W; // y2
            float domeApex = drumTop + 0.21f * W;
            float finialTop = domeApex + 0.055f * W;

            // 1-4 · Waterworks (Jade) — the approach: pool + gardens reach toward the viewer.
            strokes.Add(new PaintingStroke
            {
                name = "Reflecting Pool",
                domain = Domains.Jade,
                points = RectXZ(0f, 0.004f * W, 1.00f * W, 0.045f * W, 0.45f * W),
            });
            strokes.Add(new PaintingStroke
            {
                name = "Pool Waterline",
                domain = Domains.Jade,
                points = new List<Vector3> { new(0f, 0.004f * W, 0.58f * W), new(0f, 0.004f * W, 1.40f * W) },
            });
            strokes.Add(new PaintingStroke
            {
                name = "West Charbagh",
                domain = Domains.Jade,
                points = RectXZ(-0.185f * W, 0.004f * W, 0.99f * W, 0.07f * W, 0.39f * W),
            });
            strokes.Add(new PaintingStroke
            {
                name = "East Charbagh",
                domain = Domains.Jade,
                points = RectXZ(0.185f * W, 0.004f * W, 0.99f * W, 0.07f * W, 0.39f * W),
            });

            // 5-6 · Plinth (Gold).
            strokes.Add(new PaintingStroke
            {
                name = "Plinth Base",
                domain = Domains.Gold,
                points = RectXZ(0f, 0f, 0f, plinthHalf, plinthHalf),
            });
            strokes.Add(new PaintingStroke
            {
                name = "Plinth Crown",
                domain = Domains.Gold,
                points = RectXZ(0f, plinthH, 0f, plinthHalf, plinthHalf),
            });

            // 7-8 · Main body plan: chamfered square at base and cornice (Gold).
            strokes.Add(new PaintingStroke
            {
                name = "Body Base",
                domain = Domains.Gold,
                points = ChamferedSquare(y0, bodyHalf, chamfer),
            });
            strokes.Add(new PaintingStroke
            {
                name = "Body Cornice",
                domain = Domains.Gold,
                points = ChamferedSquare(y1, bodyHalf, chamfer),
            });

            // 9-12 · Corner towers: an ∩ up-across-down over each chamfer face (Gold).
            foreach (var (sx, sz, label) in Corners())
            {
                strokes.Add(new PaintingStroke
                {
                    name = $"{label} Corner Tower",
                    domain = Domains.Gold,
                    points = new List<Vector3>
                    {
                        new(sx * (bodyHalf - chamfer), y0, sz * bodyHalf),
                        new(sx * (bodyHalf - chamfer), y1, sz * bodyHalf),
                        new(sx * bodyHalf, y1, sz * (bodyHalf - chamfer)),
                        new(sx * bodyHalf, y0, sz * (bodyHalf - chamfer)),
                    },
                });
            }

            // 13-14 · The grand iwan on the front face z = +bodyHalf (Ruby).
            float fz = bodyHalf;
            float fw = 0.085f * W, fh = 0.26f * W;
            strokes.Add(new PaintingStroke
            {
                name = "Grand Iwan",
                domain = Domains.Ruby,
                points = new List<Vector3>
                {
                    new(-fw, y0, fz), new(-fw, y0 + fh, fz), new(fw, y0 + fh, fz), new(fw, y0, fz),
                },
            });
            strokes.Add(new PaintingStroke
            {
                name = "Iwan Arch",
                domain = Domains.Ruby,
                points = PointedArch(0f, y0, fz, 0.058f * W, 0.13f * W, 0.225f * W),
            });

            // 15-18 · Flanking niches, two tiers each side of the iwan (Ruby).
            foreach (float sx in new[] { -1f, 1f })
            foreach (var (tierY, tierName) in new[] { (0.035f * W, "Lower"), (0.145f * W, "Upper") })
            {
                strokes.Add(new PaintingStroke
                {
                    name = $"{tierName} {(sx < 0 ? "West" : "East")} Niche",
                    domain = Domains.Ruby,
                    points = PointedArch(sx * 0.145f * W, y0 + tierY, fz, 0.026f * W, 0.045f * W, 0.075f * W),
                });
            }

            // 19-25 · Drum + onion dome as a rib cage (Gold).
            strokes.Add(new PaintingStroke
            {
                name = "Dome Drum",
                domain = Domains.Gold,
                points = Circle(new Vector3(0f, drumTop, 0f), Vector3.right, Vector3.forward, 0.088f * W, 18),
            });
            // Onion profile control points: (radius, height-above-drumTop) fractions of W.
            var onion = new[]
            {
                (0.092f, 0.000f), (0.108f, 0.030f), (0.115f, 0.085f), (0.108f, 0.125f),
                (0.085f, 0.155f), (0.058f, 0.172f), (0.028f, 0.195f), (0.000f, 0.210f),
            };
            foreach (var (azimuthDeg, ribName) in new[] { (0f, "East"), (45f, "Northeast"), (90f, "North"), (135f, "Northwest") })
            {
                strokes.Add(new PaintingStroke
                {
                    name = $"Dome Rib {ribName}",
                    domain = Domains.Gold,
                    points = MeridianOverTop(onion, drumTop, azimuthDeg, W),
                });
            }
            strokes.Add(new PaintingStroke
            {
                name = "Dome Girdle",
                domain = Domains.Gold,
                points = Circle(new Vector3(0f, drumTop + 0.085f * W, 0f), Vector3.right, Vector3.forward, 0.115f * W, 18),
            });
            strokes.Add(new PaintingStroke
            {
                name = "Dome Collar",
                domain = Domains.Gold,
                points = Circle(new Vector3(0f, drumTop + 0.172f * W, 0f), Vector3.right, Vector3.forward, 0.058f * W, 12),
            });

            // 26-27 · Finial + crescent (Ruby).
            strokes.Add(new PaintingStroke
            {
                name = "Finial",
                domain = Domains.Ruby,
                points = new List<Vector3>
                {
                    new(0f, domeApex, 0f),
                    new(0.012f * W, domeApex + 0.02f * W, 0f),
                    new(-0.012f * W, domeApex + 0.035f * W, 0f),
                    new(0f, finialTop, 0f),
                },
            });
            strokes.Add(new PaintingStroke
            {
                name = "Crescent Moon",
                domain = Domains.Ruby,
                points = Arc(new Vector3(0f, finialTop + 0.03f * W, 0f), Vector3.right, Vector3.up,
                    0.028f * W, 200f, 340f, 8),
            });

            // 28-35 · Four chhatris on the roof terrace (Ruby).
            foreach (var (sx, sz, label) in Corners())
            {
                var c = new Vector3(sx * 0.145f * W, 0f, sz * 0.145f * W);
                strokes.Add(new PaintingStroke
                {
                    name = $"{label} Chhatri Canopy",
                    domain = Domains.Ruby,
                    points = Circle(c + Vector3.up * (y1 + 0.055f * W), Vector3.right, Vector3.forward, 0.042f * W, 12),
                });
                // Small dome cap: a vertical arc facing outward along the roof diagonal.
                Vector3 outward = new Vector3(sx, 0f, sz).normalized;
                strokes.Add(new PaintingStroke
                {
                    name = $"{label} Chhatri Dome",
                    domain = Domains.Ruby,
                    points = Arc(c + Vector3.up * (y1 + 0.055f * W), outward, Vector3.up, 0.040f * W, 0f, 180f, 9),
                });
            }

            // 36-55 · Four minarets standing on the plinth corners (Gold).
            float minaretBaseY = plinthH;
            float minaretTopY = plinthH + 0.36f * W;
            foreach (var (sx, sz, label) in Corners())
            {
                var basePos = new Vector3(sx * 0.46f * W, minaretBaseY, sz * 0.46f * W);
                Vector3 radial = new Vector3(sx, 0f, sz).normalized;
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                float rBottom = 0.021f * W, rTop = 0.013f * W;
                float h = minaretTopY - minaretBaseY;

                // Shaft: up one flank, over the cap, down the other.
                var shaft = new List<Vector3>();
                for (int i = 0; i <= 3; i++)
                {
                    float t = i / 3f;
                    shaft.Add(basePos + tangent * Mathf.Lerp(rBottom, rTop, t) + Vector3.up * (h * t));
                }
                shaft.Add(basePos + Vector3.up * (h + 0.012f * W)); // cap point
                for (int i = 3; i >= 0; i--)
                {
                    float t = i / 3f;
                    shaft.Add(basePos - tangent * Mathf.Lerp(rBottom, rTop, t) + Vector3.up * (h * t));
                }
                strokes.Add(new PaintingStroke { name = $"{label} Minaret", domain = Domains.Gold, points = shaft });

                // Three balconies: 240° arcs opening toward the monument so the gap is invisible
                // from the approach — and the loop stays flyable at painting scale.
                var balconyNames = new[] { "Balcony I", "Balcony II", "Balcony III" };
                var balconyT = new[] { 0.33f, 0.66f, 0.92f };
                for (int b = 0; b < 3; b++)
                {
                    float t = balconyT[b];
                    float br = Mathf.Lerp(rBottom, rTop, t) + 0.008f * W;
                    float gapAzimuth = Mathf.Atan2(-radial.z, -radial.x) * Mathf.Rad2Deg; // toward center (Arc measures from +X toward +Z)
                    strokes.Add(new PaintingStroke
                    {
                        name = $"{label} Minaret {balconyNames[b]}",
                        domain = Domains.Gold,
                        points = Arc(basePos + Vector3.up * (h * t), Vector3.right, Vector3.forward,
                            br, gapAzimuth + 60f, gapAzimuth + 300f, 9),
                    });
                }

                // Crown: a small dome cap over the top, facing outward.
                strokes.Add(new PaintingStroke
                {
                    name = $"{label} Minaret Crown",
                    domain = Domains.Gold,
                    points = Arc(basePos + Vector3.up * h, radial, Vector3.up, 0.016f * W, 0f, 180f, 7),
                });
            }

            return strokes;
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  GRANDIOSE CONSTRUCTIONS
        //
        //  Each composes PaintingStrokeToolkit primitives into a non-planar monument that dwarfs the
        //  Taj Mahal (all >20·W of flight, many >100 strokes). The shared moves: parametric curve
        //  families give the structure, the impressionist curl field ("3D impressionism") fills the
        //  volume with stochastically-curved brush strokes, and every stroke is one of the three
        //  paintable domains (Jade/Ruby/Gold). Author broad→fine so the painting is its own ramp;
        //  Generate() calls RebaseToGround afterward so the base plane always lands on y=0.
        // ══════════════════════════════════════════════════════════════════════════

        static PaintingStroke St(string name, Domains d, List<Vector3> pts)
            => new() { name = name, domain = d, points = pts };

        static int DomOrder(Domains d) => d == Domains.Jade ? 0 : d == Domains.Ruby ? 1 : 2;

        /// <summary>
        /// Batch a scattered multi-domain fill by domain (Jade→Ruby→Gold) so the player recolours the
        /// trail at most twice for the whole group instead of at every stroke — the authoring rule
        /// applied to impressionist fills whose colour comes from a spatial field.
        /// </summary>
        static List<PaintingStroke> Batched(List<PaintingStroke> strokes)
        {
            strokes.Sort((a, b) => DomOrder(a.domain).CompareTo(DomOrder(b.domain)));
            return strokes;
        }

        /// <summary>
        /// Impressionist curl fill translated to the toolkit's step-based API from a target arc length.
        /// <paramref name="batch"/> sorts a multi-domain fill by domain so the trail recolours ≤2× for
        /// the whole group rather than at every scattered stroke.
        /// </summary>
        static List<PaintingStroke> Impression(int count, Func<Tk.Rng, Vector3> seed, Func<Vector3, Domains> field,
            Tk.Rng rng, int noiseSeed, float W, float curlK, float arcLen, float upBias = 0f,
            Func<Vector3, Vector3> project = null, string prefix = "Brush", bool batch = false)
        {
            float step = Mathf.Clamp(0.028f * W, 10f, 40f);
            int mn = Mathf.Max(3, Mathf.RoundToInt(arcLen * 0.7f / step));
            int mx = Mathf.Max(mn + 1, Mathf.RoundToInt(arcLen * 1.4f / step));
            var result = Tk.ImpressionistStrokes(count, seed, field, rng, noiseSeed, curlK / W, step, mn, mx,
                0.55f, upBias, project, prefix);
            if (batch) return Batched(result);
            return result;
        }

        /// <summary>A logarithmic-spiral band sampled between radii rMin..rMax (keeps the tight core flyable).</summary>
        static List<Vector3> LogSpiralBand(Vector3 c, Vector3 u, Vector3 v, Vector3 axis,
            float a, float b, float rMin, float rMax, float axialRisePerRad, int seg)
        {
            a = Mathf.Max(1e-3f, a);
            float th0 = Mathf.Log(Mathf.Max(rMin, a) / a) / b;
            float th1 = Mathf.Log(Mathf.Max(rMax, rMin * 1.05f) / a) / b;
            var pts = new List<Vector3>(seg + 1);
            for (int i = 0; i <= seg; i++)
            {
                float th = Mathf.Lerp(th0, th1, i / (float)seg);
                float r = a * Mathf.Exp(b * th);
                pts.Add(c + axis * (axialRisePerRad * (th - th0)) + (u * Mathf.Cos(th) + v * Mathf.Sin(th)) * r);
            }
            return pts;
        }

        static Domains DomainByRadius(float r, float mid, float outer)
            => r > outer ? Domains.Jade : r > mid ? Domains.Ruby : Domains.Gold;

        // ── Nautilus — chambered logarithmic-spiral shell ────────────────────────
        //
        // A true equiangular whorl (growth ×3/turn) climbing a cone, its internal septa dividing the
        // chambers, wrapped in an impressionist mother-of-pearl sheen. XZ disc, +Y rise → spins about
        // +Y showing nested chambers parallax. ~90 strokes, ~24·W.

        static List<PaintingStroke> Nautilus(float W)
        {
            var rng = new Tk.Rng(1105);
            var s = new List<PaintingStroke>();
            Vector3 U = Vector3.right, V = Vector3.forward, A = Vector3.up;
            float a = 0.0106f * W, b = 0.175f, H = 0.30f * W, thMax = 3.5f * Mathf.PI * 2f;
            float rOuter = a * Mathf.Exp(b * thMax);          // ≈0.50W
            float RisePerRad = H / thMax;

            Vector3 WhorlPt(float th, float outMul, float axMul)
            {
                float r = a * Mathf.Exp(b * th);
                float rho = 0.22f * r;
                Vector3 outward = U * Mathf.Cos(th) + V * Mathf.Sin(th);
                Vector3 c = outward * r + A * (RisePerRad * th);
                return c + outward * (outMul * rho) + A * (axMul * rho);
            }
            List<Vector3> Rim(float outMul, float axMul, int n)
            {
                var p = new List<Vector3>(n + 1);
                for (int i = 0; i <= n; i++) p.Add(WhorlPt(thMax * (i / (float)n), outMul, axMul));
                return p;
            }

            // Four tube-surface whorls (broadest curvature), banded by growth-ring age.
            s.Add(St("Outer Whorl", Domains.Jade, Rim(1f, 0f, 200)));
            s.Add(St("Crown Rim", Domains.Ruby, Rim(0f, 1f, 190)));
            s.Add(St("Keel Rim", Domains.Ruby, Rim(0f, -1f, 190)));
            s.Add(St("Inner Whorl", Domains.Gold, Rim(-1f, 0f, 180)));

            // Aperture — the open mouth at the large end.
            {
                float th = thMax, r = a * Mathf.Exp(b * th), rho = 0.22f * r;
                Vector3 outward = U * Mathf.Cos(th) + V * Mathf.Sin(th);
                Vector3 c = outward * r + A * (RisePerRad * th);
                s.Add(St("Aperture", Domains.Jade, Circle(c, A, outward, rho * 1.05f, 22)));
            }

            // Chamber septa (mid detail): walls perpendicular to the whorl, biased to the open end.
            for (int k = 0; k < 14; k++)
            {
                float th = Mathf.Lerp(thMax * 0.28f, thMax, k / 13f);
                float r = a * Mathf.Exp(b * th), rho = 0.22f * r;
                Vector3 outward = U * Mathf.Cos(th) + V * Mathf.Sin(th);
                Vector3 c = outward * r + A * (RisePerRad * th);
                s.Add(St($"Septum {k + 1}", DomainByRadius(r, 0.14f * W, 0.30f * W),
                    Arc(c, A, outward, rho, 0f, 200f, 12)));
            }

            // Mother-of-pearl sheen (finest): impressionist strokes hugging the shell annulus.
            s.AddRange(Impression(84,
                r =>
                {
                    float th = r.Range(thMax * 0.22f, thMax);
                    float rr = a * Mathf.Exp(b * th), rho = 0.22f * rr;
                    Vector3 outward = U * Mathf.Cos(th) + V * Mathf.Sin(th);
                    return outward * rr + A * (RisePerRad * th) + r.OnUnitSphere() * (rho * 0.85f);
                },
                p => DomainByRadius(new Vector2(p.x, p.z).magnitude, 0.14f * W, 0.30f * W),
                rng, 1106, W, curlK: 3.2f, arcLen: 0.14f * W, prefix: "Nacre", batch: true));

            return s;
        }

        // ── Lotus — phyllotaxis petals opening in true 3D ────────────────────────

        static List<PaintingStroke> Lotus(float W)
        {
            var rng = new Tk.Rng(606);
            var s = new List<PaintingStroke>();
            Vector3 A = Vector3.up;
            int N = 60;
            float R = 0.50f * W, c = R / Mathf.Sqrt(N);

            // Base pads (broadest): three concentric ground rings.
            foreach (var (rr, name) in new[] { (0.52f * W, "Outer Pad"), (0.42f * W, "Mid Pad"), (0.30f * W, "Inner Pad") })
                s.Add(St(name, Domains.Jade, Circle(Vector3.zero, Vector3.right, Vector3.forward, rr, Mathf.Max(24, (int)(rr / 14f)))));

            // Petals by phyllotaxis, authored outer→inner so the domain gate flips only at ring seams.
            void Ring(int nFrom, int nTo, Domains domain, string tag)
            {
                for (int n = nFrom; n <= nTo; n++)
                {
                    float ang = n * 2.39996323f;                 // golden angle (rad)
                    float rn = c * Mathf.Sqrt(n);
                    Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    float f = n / (float)N;
                    float len = 0.10f * W + 0.14f * W * f;
                    float tilt = Mathf.Deg2Rad * (80f - 70f * f);   // inner near-vertical, outer flat
                    Vector3 outDir = radial * Mathf.Cos(tilt) + A * Mathf.Sin(tilt);
                    float baseY = 0.42f * W * Mathf.Pow(Mathf.Max(0f, 1f - (rn / R) * (rn / R)), 1.6f);
                    Vector3 baseC = radial * (rn * 0.5f) + A * baseY;
                    s.Add(St($"{tag} Petal {n}", domain,
                        Tk.PetalLoop(baseC, outDir, A, len, 0.4f * len, 0.35f, 5)));
                }
            }
            Ring(41, 60, Domains.Jade, "Outer");
            Ring(16, 40, Domains.Ruby, "Mid");
            Ring(1, 15, Domains.Gold, "Bud");

            // Stamen core (finest): a shimmering impressionist ball lifted into the flower's heart.
            s.AddRange(Impression(48,
                r => r.InUnitBall() * (0.10f * W) + A * (0.37f * W),
                _ => Domains.Gold, rng, 607, W, curlK: 6f, arcLen: 0.10f * W, prefix: "Stamen"));

            return s;
        }

        // ── Rose — nested spiral bloom of cupped petals ──────────────────────────

        static List<PaintingStroke> Rose(float W)
        {
            var rng = new Tk.Rng(1414);
            var s = new List<PaintingStroke>();
            Vector3 A = Vector3.up;
            int N = 45;
            float R = 0.50f * W;

            // Collar guide spiral (broadest).
            s.Add(St("Collar", Domains.Jade,
                LogSpiralBand(Vector3.zero, Vector3.right, Vector3.forward, A, 0.05f * W, 0.20f,
                    0.06f * W, 0.48f * W, 0.14f * W / (Mathf.PI * 2f), 150)));

            void Ring(int nFrom, int nTo, Domains domain, string tag)
            {
                for (int n = nFrom; n <= nTo; n++)
                {
                    float ang = n * 2.39996323f;
                    float rn = R * Mathf.Pow(n / (float)N, 0.62f);
                    Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    float f = n / (float)N;
                    float len = 0.12f * W + 0.16f * W * f;
                    float tilt = Mathf.Deg2Rad * (88f - 78f * f);
                    float cup = 0.55f - 0.25f * f;
                    Vector3 outDir = radial * Mathf.Cos(tilt) + A * Mathf.Sin(tilt);
                    float baseY = 0.40f * W * Mathf.Pow(Mathf.Max(0f, 1f - (rn / R) * (rn / R)), 2.0f);
                    Vector3 baseC = radial * (rn * 0.5f) + A * baseY;
                    s.Add(St($"{tag} Petal {n}", domain,
                        Tk.PetalLoop(baseC, outDir, A, len, 0.45f * len, cup, 5)));
                }
            }
            Ring(31, 45, Domains.Jade, "Open");
            Ring(13, 30, Domains.Ruby, "Mid");
            Ring(1, 12, Domains.Gold, "Furl");

            // Velvet interior (finest): impressionist strokes hugging the bloom dome.
            s.AddRange(Impression(70,
                r =>
                {
                    Vector3 d = r.OnUnitSphere(); d.y = Mathf.Abs(d.y);
                    float rr = r.Range(0.05f * W, 0.46f * W);
                    Vector3 p = new Vector3(d.x, 0f, d.z).normalized * rr;
                    p.y = 0.40f * W * Mathf.Pow(Mathf.Max(0f, 1f - (rr / R) * (rr / R)), 2.0f) + r.Range(-0.03f * W, 0.03f * W);
                    return p;
                },
                p => DomainByRadius(new Vector2(p.x, p.z).magnitude, 0.15f * W, 0.33f * W),
                rng, 1415, W, curlK: 5f, arcLen: 0.14f * W, prefix: "Velvet", batch: true));

            return s;
        }

        // ── Buckyball — truncated-icosahedron soccer ball ────────────────────────

        static List<PaintingStroke> Buckyball(float W)
        {
            var rng = new Tk.Rng(32);
            var s = new List<PaintingStroke>();
            float Rc = 0.50f * W, cy = 0.52f * W;
            Vector3 C = new(0f, cy, 0f);
            Tk.SoccerBallFaces(out var pentagons, out var hexagons);

            List<Vector3> Face(Vector3[] loop, string _)
            {
                var pts = new List<Vector3>(loop.Length);
                foreach (var v in loop) pts.Add(C + v * Rc);
                return Tk.CatmullRom(pts, 4, closed: false);
            }

            // Three great-circle gimbal rings (broadest silhouette).
            s.Add(St("Ring X", Domains.Jade, Circle(C, Vector3.up, Vector3.forward, Rc, 40)));
            s.Add(St("Ring Y", Domains.Jade, Circle(C, Vector3.right, Vector3.forward, Rc, 40)));
            s.Add(St("Ring Z", Domains.Jade, Circle(C, Vector3.right, Vector3.up, Rc, 40)));

            // 20 hexagon panels (Ruby) then 12 pentagon panels (Gold) — the soccer-ball two-tone.
            for (int i = 0; i < hexagons.Count; i++) s.Add(St($"Hexagon {i + 1}", Domains.Ruby, Face(hexagons[i], "hex")));
            for (int i = 0; i < pentagons.Count; i++) s.Add(St($"Pentagon {i + 1}", Domains.Gold, Face(pentagons[i], "pent")));

            // Plasma nimbus (finest): impressionist shell just outside the cage, tri-sected by azimuth.
            s.AddRange(Impression(40,
                r => C + r.OnUnitSphere() * (Rc * 1.06f),
                p => Tk.DomainFromScalar((Mathf.Atan2(p.z, p.x) + Mathf.PI) / (Mathf.PI * 2f)),
                rng, 33, W, curlK: 3.2f, arcLen: 0.15f * W, prefix: "Nimbus", batch: true));

            return s;
        }

        // ── Torus Knot — a woven (3,2) trefoil in a plasma sheath ────────────────

        static List<PaintingStroke> TorusKnotPreset(float W)
        {
            var rng = new Tk.Rng(232);
            var s = new List<PaintingStroke>();
            float R = 0.34f * W, r = 0.13f * W, cy = 0.52f * W;
            Vector3 C = new(0f, cy, 0f);

            var spine = Tk.TorusKnot(3, 2, R, r, 240);
            for (int i = 0; i < spine.Count; i++) spine[i] += C;

            // Torus guide rings (broadest) — the surface the knot lives on.
            s.Add(St("Torus Equator", Domains.Jade, Circle(C, Vector3.right, Vector3.forward, R, 40)));
            s.Add(St("Torus Girth", Domains.Jade, Circle(C + Vector3.right * R, Vector3.up, Vector3.forward, r, 20)));

            // The primary knot centreline (one long non-planar stroke).
            s.Add(St("Trefoil", Domains.Gold, new List<Vector3>(spine)));

            // Braided rope: five strands woven around the spine, cycling colour.
            Domains[] strandCols = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Jade, Domains.Ruby };
            for (int k = 0; k < 5; k++)
                s.Add(St($"Strand {k + 1}", strandCols[k], Tk.FrameStrand(spine, k, 5, 0.35f * r, 2.0f)));

            // Plasma sheath (finest): impressionist strokes seeded on the tube surface.
            s.AddRange(Impression(44,
                rg =>
                {
                    int idx = rg.RangeInt(0, spine.Count);
                    Vector3 n = (spine[idx] - C); n.y = 0f;
                    if (n.sqrMagnitude < 1e-4f) n = Vector3.right;
                    return spine[idx] + n.normalized * (1.2f * r * rg.Range(0.6f, 1f));
                },
                p => Tk.DomainFromScalar((Mathf.Atan2(p.z, p.x) + Mathf.PI) / (Mathf.PI * 2f)),
                rng, 233, W, curlK: 4f, arcLen: 0.12f * W, prefix: "Sheath", batch: true));

            return s;
        }

        // ── Double Helix — B-DNA with base pairs and a hydration shell ───────────

        static List<PaintingStroke> DoubleHelix(float W)
        {
            var rng = new Tk.Rng(909);
            var s = new List<PaintingStroke>();
            float aRad = 0.09f * W, H = 1.0f * W, delta = 2.513f; // 144° B-DNA phase
            int turns = 5;

            Vector3 BB(float th, float phase) => new(aRad * Mathf.Cos(th + phase),
                H * th / (Mathf.PI * 2f * turns), aRad * Mathf.Sin(th + phase));
            List<Vector3> Backbone(float phase, int seg)
            {
                var p = new List<Vector3>(seg + 1);
                for (int i = 0; i <= seg; i++) p.Add(BB(Mathf.PI * 2f * turns * (i / (float)seg), phase));
                return p;
            }

            // Two backbones (broadest) + two fattening groove ribbons.
            s.Add(St("Backbone A", Domains.Jade, Backbone(0f, 220)));
            s.Add(St("Backbone B", Domains.Gold, Backbone(delta, 220)));
            s.Add(St("Groove A", Domains.Jade, Backbone(0.14f, 200)));
            s.Add(St("Groove B", Domains.Gold, Backbone(delta + 0.14f, 200)));

            // Base-pair rungs (mid) — 40 bowed cross-links, twisting with the ladder.
            for (int k = 0; k < 40; k++)
            {
                float th = Mathf.PI * 2f * turns * (k / 39f);
                Vector3 p1 = BB(th, 0f), p2 = BB(th, delta);
                Vector3 mid = (p1 + p2) * 0.5f + new Vector3(0f, 0f, 0f);
                Vector3 bow = (mid - new Vector3(0f, mid.y, 0f)); // radial-out from the axis
                if (bow.sqrMagnitude < 1e-4f) bow = Vector3.right;
                mid += bow.normalized * (0.05f * aRad);
                s.Add(St($"Base Pair {k + 1}", Domains.Ruby, Tk.CatmullRom(new List<Vector3> { p1, mid, p2 }, 3)));
            }

            // Hydration shell (finest): impressionist swirl in the [a,2a] annulus up the tower.
            s.AddRange(Impression(46,
                r =>
                {
                    float y = r.Range(0f, H);
                    float ang = r.Range(0f, Mathf.PI * 2f);
                    float rr = r.Range(aRad, 2f * aRad);
                    return new Vector3(Mathf.Cos(ang) * rr, y, Mathf.Sin(ang) * rr);
                },
                p => Tk.DomainFromScalar(p.y / H), rng, 910, W, curlK: 3f, arcLen: 0.12f * W, prefix: "Solvent", batch: true));

            return s;
        }

        // ── Spiral Galaxy — log-spiral arms + impressionist starfield ────────────
        //
        // Three interleaved log-spiral streams (one per domain) wind out of a blazing bulge into a
        // flattened impressionist disk wrapped in a faint spherical halo. Faces +Z (XY disc plane),
        // spins about +Z like a pinwheel. ~150 strokes, ~40·W.

        static List<PaintingStroke> SpiralGalaxy(float W)
        {
            var rng = new Tk.Rng(1010);
            var s = new List<PaintingStroke>();
            Vector3 U = Vector3.right, V = Vector3.up, Ax = Vector3.forward;
            float cy = 0.60f * W;
            Vector3 C = new(0f, cy, 0f);
            float Rd = 0.50f * W, b = 0.268f;
            float a0 = 0.05f * W;

            // Arm colour = which of three 120°-offset spirals a point is nearest (self-colouring streams).
            Domains ArmSector(Vector3 p)
            {
                Vector3 d = p - C;
                float u = Vector3.Dot(d, U), v = Vector3.Dot(d, V);
                float rr = Mathf.Sqrt(u * u + v * v);
                float th = Mathf.Atan2(v, u);
                float thp = th - Mathf.Log(Mathf.Max(rr, a0) / a0) / b;
                float frac = thp / (Mathf.PI * 2f);
                frac -= Mathf.Floor(frac);
                return Tk.DomainFromScalar(frac);
            }
            Func<Vector3, Vector3> flatten = p =>
            {
                Vector3 d = p - C;
                return C + U * Vector3.Dot(d, U) + V * Vector3.Dot(d, V) + Ax * (Vector3.Dot(d, Ax) * 0.24f);
            };

            // Halo shell (broadest, faintest).
            s.AddRange(Impression(28, r => C + r.OnUnitSphere() * (0.60f * W), ArmSector,
                rng, 1011, W, curlK: 0.85f, arcLen: 0.35f * W, prefix: "Halo", batch: true));

            // Disk field (the bulk of the impressionism).
            s.AddRange(Impression(58,
                r =>
                {
                    float rr = 0.5f * Rd * (-Mathf.Log(1f - r.Range(0.02f, 0.98f)));
                    rr = Mathf.Min(rr, Rd);
                    float ang = r.Range(0f, Mathf.PI * 2f);
                    return C + (U * Mathf.Cos(ang) + V * Mathf.Sin(ang)) * rr + Ax * (0.06f * W * r.Range(-1f, 1f));
                },
                ArmSector, rng, 1012, W, curlK: 2f, arcLen: 0.22f * W, project: flatten, prefix: "Disk", batch: true));

            // Three spiral arm ridges + dust lanes (structural), each domain one arm.
            Domains[] armCols = { Domains.Jade, Domains.Ruby, Domains.Gold };
            for (int arm = 0; arm < 3; arm++)
            {
                float phase = arm * (Mathf.PI * 2f / 3f);
                Vector3 u2 = U * Mathf.Cos(phase) + V * Mathf.Sin(phase);
                Vector3 v2 = -U * Mathf.Sin(phase) + V * Mathf.Cos(phase);
                var ridge = LogSpiralBand(C, u2, v2, Ax, a0, b, 0.05f * W, Rd, 0f, 120);
                for (int i = 0; i < ridge.Count; i++)
                {
                    float t = i / (float)(ridge.Count - 1);
                    ridge[i] += Ax * (0.05f * W * Mathf.Sin(t * Mathf.PI * 4f));
                }
                s.Add(St($"Arm {arm + 1}", armCols[arm], ridge));
            }

            // Bulge core (tight, brightest) + three core whirls.
            s.AddRange(Impression(40, r => C + Scale(r.InUnitBall(), new Vector3(1f, 1f, 0.6f)) * (0.15f * W),
                ArmSector, rng, 1013, W, curlK: 8f, arcLen: 0.10f * W, prefix: "Core", batch: true));
            for (int k = 0; k < 3; k++)
                s.Add(St($"Whirl {k + 1}", armCols[k], Tk.Helix(C, Ax, 0.06f * W, 0.02f * W, 3f, 40)));

            return s;
        }

        // ── Lion's Head — a golden mane of hundreds of curl strokes ──────────────
        //
        // A head volume behind a spherical spray of ~160 curl-integrated mane strands, Ruby face
        // features surfacing from the golden core. Full 3D — spun, it shimmers like a solar corona.

        static List<PaintingStroke> LionsHead(float W)
        {
            var rng = new Tk.Rng(1111);
            var s = new List<PaintingStroke>();
            float Rh = 0.20f * W, cy = 0.55f * W, front = 0.12f * W;
            Vector3 C = new(0f, cy, 0f);

            // Head latitude rings (broadest great circles).
            foreach (float phi in new[] { -40f, -15f, 10f, 35f, 60f })
            {
                float ph = Mathf.Deg2Rad * phi;
                Vector3 c = C + Vector3.up * (Rh * Mathf.Sin(ph));
                s.Add(St($"Skull {phi:0}", Domains.Gold,
                    Circle(c, Vector3.right, Vector3.forward, Rh * Mathf.Cos(ph), 40)));
            }

            // Mane spray (bulk): ~160 radial curl strands from a Fibonacci sphere of directions, biased
            // away from the face window (front-centre) so the muzzle stays clear.
            var dirs = Tk.FibonacciSphere(200);
            int mane = 0;
            float stepLen = Mathf.Clamp(0.03f * W, 8f, 0.5f * W);
            var maneStrokes = new List<PaintingStroke>();
            foreach (var dir in dirs)
            {
                if (mane >= 160) break;
                if (dir.z > 0.55f && dir.y < 0.25f && Mathf.Abs(dir.x) < 0.4f) continue; // face window
                // Mostly golden with amber (Ruby) streaks and a rare cool (Jade) highlight — wider
                // thresholds than the raw noise range so the streaks actually appear.
                float t = Mathf.Abs(Tk.ValueNoise(dir * 4f, 71));
                Domains d = t < 0.45f ? Domains.Gold : t < 0.72f ? Domains.Ruby : Domains.Jade;
                maneStrokes.Add(St($"Mane {++mane}", d,
                    Tk.RadialCurlStroke(C, dir, Rh * 1.02f, 0.55f * W, stepLen, 2.5f / W, 0.35f, 0f, 1200 + mane, 20)));
            }
            s.AddRange(Batched(maneStrokes)); // batch so the mane recolours ≤2×, not per strand

            // Face features (finest, last): Ruby eyes, muzzle, mouth on the front of the head.
            Vector3 F = C + Vector3.forward * front;
            foreach (float sx in new[] { -1f, 1f })
                s.Add(St(sx < 0 ? "Left Eye" : "Right Eye", Domains.Ruby,
                    Circle(F + new Vector3(sx * 0.08f * W, 0.05f * W, 0f), Vector3.right, Vector3.up, 0.03f * W, 12)));
            s.Add(St("Muzzle", Domains.Ruby, Circle(F + Vector3.down * 0.06f * W, Vector3.right, Vector3.up, 0.05f * W, 14)));
            s.Add(St("Nose", Domains.Ruby, new List<Vector3> {
                F + new Vector3(0f, -0.02f * W, 0.01f * W), F + new Vector3(0f, -0.08f * W, 0.02f * W) }));
            foreach (float sx in new[] { -1f, 1f })
                s.Add(St(sx < 0 ? "Mouth L" : "Mouth R", Domains.Ruby,
                    Arc(F + Vector3.down * 0.10f * W, Vector3.right * sx, Vector3.up, 0.05f * W, 200f, 340f, 8)));

            return s;
        }

        // ── Phoenix — a firebird of feather strokes and an impressionist flame tail ─

        static List<PaintingStroke> Phoenix(float W)
        {
            var rng = new Tk.Rng(1212);
            var s = new List<PaintingStroke>();
            float cy = 0.52f * W;
            Vector3 bodyC = new(0f, cy, 0f);
            Vector3 shL = new(-0.05f * W, cy + 0.10f * W, 0f), shR = new(0.05f * W, cy + 0.10f * W, 0f);
            Vector3 tipL = new(-0.50f * W, cy + 0.03f * W, -0.15f * W), tipR = new(0.50f * W, cy + 0.03f * W, -0.15f * W);

            // Wing leading spars (broadest).
            s.Add(St("Spar L", Domains.Gold, Tk.CatmullRom(new List<Vector3> {
                shL, new(-0.20f * W, cy + 0.16f * W, -0.03f * W), new(-0.38f * W, cy + 0.11f * W, -0.10f * W), tipL }, 10)));
            s.Add(St("Spar R", Domains.Gold, Tk.CatmullRom(new List<Vector3> {
                shR, new(0.20f * W, cy + 0.16f * W, -0.03f * W), new(0.38f * W, cy + 0.11f * W, -0.10f * W), tipR }, 10)));

            // Flight feathers (mid): fanned cambered quills, colour grading inner Gold → outer Ruby.
            void Wing(Vector3 shoulder, Vector3 tip, float sign, string tag)
            {
                for (int i = 0; i < 13; i++)
                {
                    float f = i / 12f;
                    Vector3 root = Vector3.Lerp(shoulder, tip, 0.15f + 0.5f * f);
                    Vector3 fTip = Vector3.Lerp(shoulder, tip, 0.6f + 0.4f * f)
                                   + new Vector3(0f, -0.06f * W - 0.14f * W * f, -0.05f * W - 0.10f * W * f);
                    Domains d = f < 0.55f ? Domains.Gold : Domains.Ruby;
                    s.Add(St($"{tag} Feather {i + 1}", d, Tk.FeatherStroke(root, fTip, 0.06f, 3f / W, 14, 1300 + (int)(sign) * 50 + i)));
                }
            }
            Wing(shL, tipL, -1f, "L");
            Wing(shR, tipR, 1f, "R");

            // Body contour + plumage (mid/fine).
            s.Add(St("Body", Domains.Gold, Tk.CatmullRom(new List<Vector3> {
                new(0f, cy - 0.14f * W, -0.05f * W), new(0.05f * W, cy, 0.03f * W),
                new(0f, cy + 0.16f * W, 0.02f * W), new(-0.05f * W, cy, 0.03f * W) }, 8, closed: true)));
            s.AddRange(Impression(30, r => bodyC + r.InUnitBall() * (0.10f * W), _ => Domains.Gold,
                rng, 1213, W, curlK: 3f, arcLen: 0.16f * W, prefix: "Plume"));

            // Head + crest (fine).
            Vector3 head = new(0f, cy + 0.20f * W, 0.03f * W);
            s.Add(St("Head", Domains.Ruby, Circle(head, Vector3.right, Vector3.up, 0.05f * W, 14)));
            s.Add(St("Beak", Domains.Gold, new List<Vector3> { head + Vector3.forward * 0.05f * W, head + new Vector3(0f, -0.02f * W, 0.11f * W) }));
            for (int k = 0; k < 4; k++)
            {
                Vector3 dir = new Vector3(Mathf.Lerp(-0.4f, 0.4f, k / 3f), 0.8f, -0.4f).normalized;
                s.Add(St($"Crest {k + 1}", Domains.Ruby,
                    Tk.RadialCurlStroke(head, dir, 0.03f * W, 0.14f * W, 0.03f * W, 3f / W, 0.4f, 0.2f, 1250 + k, 10)));
            }

            // Flame tail (finest): impressionist curl strokes writhing upward from the tail base.
            Vector3 tailBase = new(0f, cy - 0.10f * W, -0.05f * W);
            s.AddRange(Impression(55,
                r => tailBase + new Vector3(r.Range(-0.12f * W, 0.12f * W), r.Range(-0.02f * W, 0.05f * W), r.Range(-0.20f * W, 0.02f * W)),
                p => p.y > tailBase.y + 0.15f * W ? Domains.Ruby : Domains.Gold,
                rng, 1214, W, curlK: 4f, arcLen: 0.40f * W, upBias: 0.5f, prefix: "Flame"));

            return s;
        }

        // ── Peacock — a fanned 3D tail of eye-feather strokes ────────────────────

        static List<PaintingStroke> Peacock(float W)
        {
            var rng = new Tk.Rng(1313);
            var s = new List<PaintingStroke>();
            Vector3 B = new(0f, 0.15f * W, -0.05f * W);
            float Lf = 0.55f * W;
            int N = 84;

            // Body + neck (broad).
            s.Add(St("Body", Domains.Jade, Circle(B, Vector3.right, Vector3.up, 0.09f * W, 16)));
            s.Add(St("Neck", Domains.Jade, Tk.CatmullRom(new List<Vector3> {
                B, B + new Vector3(0f, 0.14f * W, 0.06f * W), new(0f, 0.42f * W, 0.10f * W) }, 10)));

            // Rachis shafts (broadest, near-straight) — golden-angle cap, tips fanning into a +Z bulge.
            var tips = new Vector3[N];
            for (int i = 0; i < N; i++)
            {
                float ang = i * 2.39996323f;
                float rCap = Lf * Mathf.Sqrt(i / (float)N);
                float x = rCap * Mathf.Cos(ang), y = rCap * Mathf.Sin(ang);
                float z = Mathf.Sqrt(Mathf.Max(0f, Lf * Lf - rCap * rCap)) * 0.9f;
                Vector3 tip = B + new Vector3(x, y * 1.2f + 0.12f * W, z);
                tips[i] = tip;
                s.Add(St($"Shaft {i + 1}", Domains.Gold, Tk.FeatherStroke(B, tip, 0.04f, 3f / W, 16, 1350 + i)));
            }

            // Barb fill (impressionist iridescent train).
            s.AddRange(Impression(56,
                r => tips[r.RangeInt(0, N)] + r.OnUnitSphere() * (0.05f * W),
                _ => Domains.Jade, rng, 1314, W, curlK: 3f, arcLen: 0.24f * W, prefix: "Barb"));

            // Ocelli (finest): a gold rim + Ruby centre on the outer feathers = the eye-spots. Two
            // passes (all Gold rims, then all Ruby centres) so the pen recolours once, not per eye.
            for (int i = N / 2; i < N; i++)
            {
                Vector3 tip = tips[i];
                Tk.Basis((tip - B).normalized, out Vector3 u, out Vector3 v, out _);
                s.Add(St($"Eye Rim {i + 1}", Domains.Gold, ArcPlane(tip, u, v, 0.04f * W, 12)));
            }
            for (int i = N / 2; i < N; i++)
            {
                Vector3 tip = tips[i];
                Tk.Basis((tip - B).normalized, out Vector3 u, out Vector3 v, out _);
                s.Add(St($"Eye Core {i + 1}", Domains.Ruby, ArcPlane(tip, u, v, 0.02f * W, 10)));
            }

            return s;
        }

        static List<Vector3> ArcPlane(Vector3 c, Vector3 u, Vector3 v, float radius, int seg)
            => Circle(c, u, v, radius, seg);

        // ── Starry Night — Van Gogh, stepped into as a 3D sky shell ──────────────
        //
        // A hemispherical sky shell you fly INTO: impressionist curl swirls banking on its curvature,
        // two counter-rotating vortex galaxies, 11 star vortices, a crescent moon, a Ruby cypress
        // flame in the near foreground, a village + church spire, and rolling hills. ~73 strokes, ~40·W.

        static List<PaintingStroke> StarryNight(float W)
        {
            var rng = new Tk.Rng(1515);
            var s = new List<PaintingStroke>();
            Vector3 Csky = new(0f, 0.42f * W, -0.38f * W);
            float R = 0.85f * W;

            Vector3 Dome(float az, float el) => Csky + R * new Vector3(
                Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), -Mathf.Cos(el) * Mathf.Cos(az));
            Func<Vector3, Vector3> shell = p =>
            {
                Vector3 d = p - Csky;
                float rr = R + 0.05f * W * Tk.ValueNoise(p * (3f / W), 707);
                return Csky + (d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.up) * rr;
            };

            // ═ PASS A — JADE (sky) ═
            s.AddRange(Impression(26,
                r => Dome(r.Range(-1.05f, 1.05f), r.Range(0.35f, 1.5f)),
                _ => Domains.Jade, rng, 101, W, curlK: 1.7f, arcLen: 0.5f * W, project: shell, prefix: "Swirl"));

            foreach (var (az, el, dir, name) in new[] { (-0.13f, 0.9f, 1f, "Vortex A"), (0.16f, 1.0f, -1f, "Vortex B") })
            {
                Vector3 vc = Dome(az, el);
                Tk.Basis((vc - Csky).normalized, out Vector3 u, out Vector3 v, out _);
                s.Add(St(name, Domains.Jade, LogSpiralBand(vc, u, v * dir, (vc - Csky).normalized,
                    0.02f * W, 0.155f, 0.02f * W, 0.16f * W, 0.03f * W, 60)));
            }
            for (int k = 0; k < 3; k++)
            {
                float ph = new[] { 0f, 1.9f, 3.6f }[k];
                float yoff = new[] { 0f, 0.05f, -0.04f }[k] * W;
                var ctrl = new List<Vector3>();
                for (int i = 0; i <= 6; i++)
                {
                    float x = Mathf.Lerp(-0.5f, 0.5f, i / 6f) * W;
                    ctrl.Add(new Vector3(x, 0.20f * W + 0.06f * W * Mathf.Sin(3.1f * x / W + ph) + yoff, -0.08f * W));
                }
                s.Add(St($"Hills {k + 1}", Domains.Jade, Tk.CatmullRom(ctrl, 16)));
            }

            // ═ PASS B — GOLD (light) ═
            Vector3 moon = Dome(0.34f, 1.15f);
            Tk.Basis((moon - Csky).normalized, out Vector3 mu, out Vector3 mv, out _);
            s.Add(St("Moon Rim", Domains.Gold, Arc(moon, mu, mv, 0.12f * W, 0f, 250f, 14)));
            s.Add(St("Moon Terminator", Domains.Gold, Arc(moon + mu * 0.045f * W, mu, mv, 0.093f * W, 20f, 230f, 12)));
            s.Add(St("Moon Halo", Domains.Gold, LogSpiralBand(moon, mu, mv, (moon - Csky).normalized,
                0.006f * W, 0.20f, 0.02f * W, 0.10f * W, 0.02f * W, 36)));

            for (int i = 0; i < 11; i++)
            {
                float el = 1.45f - 0.055f * i;
                float az = (i * 0.381966f % 1f) * 2.2f - 1.1f;
                Vector3 c = Dome(az, el);
                Tk.Basis((c - Csky).normalized, out Vector3 su, out Vector3 sv, out _);
                float dir = (i & 1) == 0 ? 1f : -1f;
                s.Add(St($"Star {i + 1}", Domains.Gold, LogSpiralBand(c, su, sv * dir, (c - Csky).normalized,
                    0.0045f * W, 0.23f, 0.02f * W, 0.06f * W, 0.015f * W, 40)));
            }
            foreach (float xw in new[] { -0.10f, -0.02f, 0.07f, 0.15f, 0.24f })
            {
                Vector3 c = new(xw * W, 0.06f * W, 0.06f * W);
                s.Add(St("Window", Domains.Gold, Tk.CatmullRom(new List<Vector3> {
                    c + new Vector3(-0.009f * W, -0.009f * W, 0f), c + new Vector3(0.009f * W, -0.009f * W, 0f),
                    c + new Vector3(0.009f * W, 0.009f * W, 0f), c + new Vector3(-0.009f * W, 0.009f * W, 0f) }, 5, closed: true)));
            }

            // ═ PASS C — RUBY (foreground) ═
            Vector3 cypress = new(-0.34f * W, 0.05f * W, 0.26f * W);
            s.AddRange(Impression(9,
                r => cypress + new Vector3(r.Range(-0.045f * W, 0.045f * W), r.Range(0f, 0.85f * W), r.Range(-0.045f * W, 0.045f * W)),
                _ => Domains.Ruby, rng, 303, W, curlK: 2.6f, arcLen: 0.58f * W, upBias: 0.5f, prefix: "Cypress"));

            s.Add(St("Village L", Domains.Ruby, Tk.CatmullRom(new List<Vector3> {
                new(-0.16f * W, 0f, 0.03f * W), new(-0.11f * W, 0.09f * W, 0.03f * W),
                new(-0.06f * W, 0.04f * W, 0.03f * W), new(0f, 0.11f * W, 0.03f * W), new(0.06f * W, 0.03f * W, 0.03f * W) }, 10)));
            s.Add(St("Village R", Domains.Ruby, Tk.CatmullRom(new List<Vector3> {
                new(0.06f * W, 0.03f * W, 0.03f * W), new(0.14f * W, 0.10f * W, 0.03f * W),
                new(0.22f * W, 0.05f * W, 0.03f * W), new(0.34f * W, 0.08f * W, 0.03f * W) }, 10)));
            s.Add(St("Spire", Domains.Ruby, Tk.CatmullRom(new List<Vector3> {
                new(0.02f * W, 0.13f * W, 0.03f * W), new(0.05f * W, 0.33f * W, 0.03f * W), new(0.09f * W, 0.13f * W, 0.03f * W) }, 12)));
            s.AddRange(Impression(6,
                r => new Vector3(r.Range(-0.40f * W, 0.35f * W), r.Range(0f, 0.10f * W), r.Range(0.05f * W, 0.22f * W)),
                _ => Domains.Ruby, rng, 404, W, curlK: 2f, arcLen: 0.42f * W, prefix: "Ground"));

            return s;
        }

        // ── Bob Ross Vista — a mountain landscape you fly into ───────────────────
        //
        // Five fractal snow-giant ridgelines stacked into 1.4·W of depth, inverted into a mirror lake,
        // a Gold sun raying through impressionist sunset clouds, and a copse of firs on the near shore.
        // ~110 strokes, ~24·W.

        static List<PaintingStroke> BobRossVista(float W)
        {
            var rng = new Tk.Rng(1616);
            var s = new List<PaintingStroke>();
            float hw = 0.40f * W;

            // Ridgelines (broadest) — near→far, with z-jitter for real depth.
            var ridges = new (float z, float yb, float amp, int seed)[] {
                (-0.55f * W, 0.95f * W, 0.22f * W, 11), (-0.35f * W, 0.80f * W, 0.18f * W, 22),
                (-0.12f * W, 0.66f * W, 0.14f * W, 33), (0.06f * W, 0.55f * W, 0.10f * W, 44),
                (0.28f * W, 0.47f * W, 0.06f * W, 55) };
            var ridgePts = new List<List<Vector3>>();
            for (int i = 0; i < ridges.Length; i++)
            {
                var rr = new Tk.Rng(ridges[i].seed * 7 + 1);
                var pts = Tk.MidpointRidge(ridges[i].seed, -0.5f * W, 0.5f * W, ridges[i].z, ridges[i].yb, ridges[i].amp, 0.85f, 6);
                for (int p = 0; p < pts.Count; p++) pts[p] += Vector3.forward * rr.Range(-0.04f * W, 0.04f * W);
                ridgePts.Add(pts);
                s.Add(St($"Ridge {i + 1}", Domains.Ruby, pts));
            }
            // Reflections in the lake.
            for (int i = 0; i < ridgePts.Count; i++)
            {
                var refl = Tk.ReflectY(ridgePts[i], hw, 0.05f * W);
                for (int p = 0; p < refl.Count; p++) refl[p] = new Vector3(refl[p].x, refl[p].y, Mathf.Lerp(refl[p].z, 0.55f * W, 0.5f));
                // Skipped above-water peaks leave gaps — subdivide so no reflection span is a long jump.
                if (refl.Count >= 2) s.Add(St($"Reflection {i + 1}", Domains.Ruby, Tk.EnforceMaxSegment(refl, 0.14f * W)));
            }

            // Near bank + shoreline shimmer (Jade).
            s.Add(St("Near Bank", Domains.Jade, Tk.MidpointRidge(88, -0.5f * W, 0.5f * W, 0.85f * W, hw + 0.03f * W, 0.04f * W, 0.85f, 5)));
            for (int k = 0; k < 2; k++)
            {
                float ph = k * 1.7f;
                s.Add(St($"Shoreline {k + 1}", Domains.Jade, Tk.Lissajous3D(new Vector3(0f, hw + 0.012f * W, 0.55f * W),
                    new Vector3(0.5f * W, 0.012f * W, 0.10f * W), 1f, 6f, 4f, -0.5f * Mathf.PI, 0f, ph, 60)));
            }

            // Sun disk + sunset clouds (Gold).
            Vector3 sun = new(0.32f * W, 1.05f * W, -0.45f * W);
            s.Add(St("Sun", Domains.Gold, Circle(sun, Vector3.right, Vector3.up, 0.09f * W, 24)));
            s.AddRange(Impression(14, r => new Vector3(r.Range(-0.45f * W, 0.45f * W), 0.95f * W + r.Range(-0.11f * W, 0.11f * W), -0.2f * W + r.Range(-0.2f * W, 0.2f * W)),
                _ => Domains.Gold, rng, 1617, W, curlK: 1.8f, arcLen: 0.16f * W, prefix: "Cloud"));

            // Low mist + water shimmer (Jade).
            s.AddRange(Impression(12, r => new Vector3(r.Range(-0.45f * W, 0.45f * W), hw + 0.03f * W + r.Range(-0.025f * W, 0.025f * W), 0.5f * W + r.Range(-0.25f * W, 0.25f * W)),
                _ => Domains.Jade, rng, 1618, W, curlK: 2.5f, arcLen: 0.12f * W, prefix: "Mist"));
            s.AddRange(Impression(16, r => new Vector3(r.Range(-0.47f * W, 0.47f * W), hw + r.Range(-0.008f * W, 0.008f * W), 0.55f * W + r.Range(-0.27f * W, 0.27f * W)),
                _ => Domains.Jade, rng, 1619, W, curlK: 3f, arcLen: 0.10f * W, prefix: "Shimmer"));

            // Snow caps + fir trees (Jade contrasts the Ruby rock → reads as snow/pine).
            for (int i = 0; i < 3; i++)
            {
                var rp = ridgePts[i];
                for (int e = 0; e < 3; e++)
                {
                    int idx = rp.Count * (e + 1) / 4;
                    Vector3 pk = rp[idx];
                    var cap = new List<Vector3>();
                    for (int j = 0; j < 6; j++)
                        cap.Add(pk + new Vector3(Mathf.Lerp(-0.05f * W, 0.05f * W, j / 5f), (j % 2 == 0 ? 0f : -0.04f * W), 0f));
                    s.Add(St($"Snow {i + 1}-{e + 1}", Domains.Jade, cap));
                }
            }
            var treeRng = new Tk.Rng(1620);
            for (int i = 0; i < 7; i++)
            {
                float x = Mathf.Lerp(-0.42f * W, 0.42f * W, i / 6f) + treeRng.Range(-0.04f * W, 0.04f * W);
                float z = treeRng.Range(0.55f * W, 0.85f * W);
                float h = treeRng.Range(0.12f * W, 0.22f * W);
                s.Add(St($"Fir {i + 1}", Domains.Jade, Tk.FirTree(new Vector3(x, hw, z), Vector3.up, h, 0.35f * h, 1620 + i)));
            }

            // Sun rays (Gold, finest).
            for (int k = 0; k < 12; k++)
            {
                float ang = Mathf.Deg2Rad * (k * 30f);
                Vector3 dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                Vector3 curl = Tk.CurlNoise(sun + dir * 0.15f * W, 4f / W, 99) * 0.02f * W;
                s.Add(St($"Ray {k + 1}", Domains.Gold, new List<Vector3> {
                    sun + dir * 0.10f * W, sun + dir * 0.17f * W + curl, sun + dir * 0.25f * W + curl + Vector3.forward * 0.02f * W }));
            }

            return s;
        }

        static Vector3 Scale(Vector3 a, Vector3 b) => new(a.x * b.x, a.y * b.y, a.z * b.z);

        // ── Geometry helpers ─────────────────────────────────────────────────────

        static (float sx, float sz, string label)[] Corners() => new[]
        {
            (1f, 1f, "Front-East"), (-1f, 1f, "Front-West"),
            (1f, -1f, "Back-East"), (-1f, -1f, "Back-West"),
        };

        /// <summary>Closed axis-aligned rectangle in a horizontal plane: centred (cx, y, cz), half-sizes (hx, hz).</summary>
        static List<Vector3> RectXZ(float cx, float y, float cz, float hx, float hz)
        {
            return new List<Vector3>
            {
                new(cx - hx, y, cz - hz), new(cx - hx, y, cz + hz),
                new(cx + hx, y, cz + hz), new(cx + hx, y, cz - hz),
                new(cx - hx, y, cz - hz),
            };
        }

        /// <summary>Closed chamfered-square plan outline at height y (the Taj's eight-sided body).</summary>
        static List<Vector3> ChamferedSquare(float y, float a, float c)
        {
            return new List<Vector3>
            {
                new(a - c, y, a), new(a, y, a - c),
                new(a, y, -(a - c)), new(a - c, y, -a),
                new(-(a - c), y, -a), new(-a, y, -(a - c)),
                new(-a, y, a - c), new(-(a - c), y, a),
                new(a - c, y, a),
            };
        }

        /// <summary>Full circle in the plane spanned by (u, v), closed back to its first point.</summary>
        static List<Vector3> Circle(Vector3 center, Vector3 u, Vector3 v, float radius, int segments)
            => Arc(center, u, v, radius, 0f, 360f, segments);

        /// <summary>Arc in the plane spanned by (u, v) from startDeg to endDeg inclusive.</summary>
        static List<Vector3> Arc(Vector3 center, Vector3 u, Vector3 v, float radius,
            float startDeg, float endDeg, int segments)
        {
            var pts = new List<Vector3>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(startDeg, endDeg, i / (float)segments) * Mathf.Deg2Rad;
                pts.Add(center + u * (Mathf.Cos(a) * radius) + v * (Mathf.Sin(a) * radius));
            }
            return pts;
        }

        /// <summary>
        /// A pointed (four-centred feel) arch outline in the vertical plane z = zPlane:
        /// up the left jamb, curve to the apex, and down the right jamb.
        /// </summary>
        static List<Vector3> PointedArch(float cx, float baseY, float zPlane,
            float halfWidth, float springHeight, float apexHeight)
        {
            var pts = new List<Vector3>
            {
                new(cx - halfWidth, baseY, zPlane),
                new(cx - halfWidth, baseY + springHeight, zPlane),
            };

            // Quadratic curve from each spring point to the apex.
            var apex = new Vector3(cx, baseY + apexHeight, zPlane);
            var springL = new Vector3(cx - halfWidth, baseY + springHeight, zPlane);
            var ctrlL = new Vector3(cx - halfWidth, baseY + springHeight + (apexHeight - springHeight) * 0.65f, zPlane);
            for (int i = 1; i <= 4; i++)
                pts.Add(Bezier(springL, ctrlL, apex, i / 4f));

            var springR = new Vector3(cx + halfWidth, baseY + springHeight, zPlane);
            var ctrlR = new Vector3(cx + halfWidth, baseY + springHeight + (apexHeight - springHeight) * 0.65f, zPlane);
            for (int i = 1; i <= 4; i++)
                pts.Add(Bezier(apex, ctrlR, springR, i / 4f));

            pts.Add(new Vector3(cx + halfWidth, baseY, zPlane));
            return pts;
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float it = 1f - t;
            return it * it * a + 2f * it * t * b + t * t * c;
        }

        /// <summary>
        /// A dome meridian flown up one side, over the apex, and down the opposite side.
        /// <paramref name="profile"/> is (radiusFraction, heightFraction) pairs from base to apex.
        /// </summary>
        static List<Vector3> MeridianOverTop((float r, float h)[] profile, float baseY, float azimuthDeg, float W)
        {
            float a = azimuthDeg * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));

            var pts = new List<Vector3>(profile.Length * 2 - 1);
            for (int i = 0; i < profile.Length; i++)
                pts.Add(dir * (profile[i].r * W) + Vector3.up * (baseY + profile[i].h * W));
            for (int i = profile.Length - 2; i >= 0; i--)
                pts.Add(-dir * (profile[i].r * W) + Vector3.up * (baseY + profile[i].h * W));
            return pts;
        }

        static string ToRoman(int n) => n switch
        {
            1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V",
            6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", 10 => "X",
            _ => n.ToString(),
        };
    }
}
