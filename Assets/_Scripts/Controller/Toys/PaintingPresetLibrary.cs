using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

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
            return preset switch
            {
                PaintingPreset.Star => Star(s),
                PaintingPreset.Rainbow => Rainbow(s),
                PaintingPreset.Saturn => Saturn(s),
                PaintingPreset.TajMahal => TajMahal(s),
                _ => new List<PaintingStroke>(),
            };
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
