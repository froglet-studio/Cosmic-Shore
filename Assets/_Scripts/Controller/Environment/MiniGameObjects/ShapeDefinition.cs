using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public enum ShapePreset { None, Circle, Star, Heart, Lightning, Smiley, Spiral, Diamond, Infinity, Arrow, Wave }

    /// <summary>
    /// Defines a drawable shape for Shape Drawing Mode.
    /// Create via: Assets > Create > CosmicShore > Shape Drawing > Shape Definition
    ///
    /// Waypoints are defined in LOCAL space, normalized to roughly a 200-unit bounding box.
    /// The ShapeDrawingManager will offset them into world space at runtime.
    ///
    /// trailEnabledPerSegment controls whether the player's trail is active WHILE flying
    /// TOWARD that waypoint. Index 0 = trail state while flying to waypoint[0], etc.
    /// A false entry = "pen up" (useful for smiley eyes -> mouth gap).
    /// </summary>
    [CreateAssetMenu(
        fileName = "Shape_New",
        menuName = "ScriptableObjects/Shape Drawing/Shape Definition")]
    public class ShapeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display name shown on the 3D sign in the world.")]
        public string shapeName = "Shape";

        [Tooltip("Short description shown briefly on selection.")]
        [TextArea(1, 3)]
        public string description = "";

        [Tooltip("Thumbnail shown on the selection sign. Optional.")]
        public Sprite thumbnail;

        [Header("Auto-Generation")]
        [Tooltip("If set to anything other than None AND waypoints is empty, " +
                 "waypoints will be auto-generated at runtime from this preset.")]
        public ShapePreset autoGeneratePreset = ShapePreset.None;

        [Tooltip("Radius used when auto-generating waypoints.")]
        public float autoGenerateRadius = 100f;

        [Header("Waypoints")]
        [Tooltip("Ordered list of crystal spawn positions in local space (~200 unit bounding box).")]
        public List<Vector3> waypoints = new();

        [Tooltip("Whether the player trail is ACTIVE while flying toward each waypoint. " +
                 "Must match waypoints count. If shorter, remaining waypoints default to true.")]
        public List<bool> trailEnabledPerSegment = new();

        [Header("Player Start")]
        [Tooltip("Where to place the player when this shape mode begins, in local space.")]
        public Vector3 playerStartOffset = new Vector3(0f, 0f, -150f);

        [Tooltip("Player's starting rotation (Euler angles).")]
        public Vector3 playerStartEuler = Vector3.zero;

        [Header("Reveal Camera")]
        [Tooltip("Euler angles for the reveal cinemachine camera (top-down = 90,0,0).")]
        public Vector3 revealCameraEuler = new Vector3(90f, 0f, 0f);

        [Tooltip("Distance the reveal camera sits from the shape center.")]
        public float revealCameraDistance = 400f;

        [Header("Scoring")]
        [Tooltip("Par time in seconds. Used to grade performance.")]
        public float parTime = 60f;

        // ── Runtime auto-generation ───────────────────────────────────────────

        /// <summary>
        /// If waypoints is empty and autoGeneratePreset is set, generates waypoints procedurally.
        /// Safe to call multiple times — no-ops if waypoints already exist.
        /// </summary>
        public void EnsureWaypoints()
        {
            if (waypoints != null && waypoints.Count > 0) return;
            if (autoGeneratePreset == ShapePreset.None) return;

            GeneratePreset(autoGeneratePreset, autoGenerateRadius);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>Returns whether the trail should be active while flying to waypointIndex.</summary>
        public bool IsTrailEnabledForSegment(int waypointIndex)
        {
            if (trailEnabledPerSegment == null || waypointIndex >= trailEnabledPerSegment.Count)
                return true; // default: trail on
            return trailEnabledPerSegment[waypointIndex];
        }

        /// <summary>Returns the world-space position of a waypoint given a world origin and scale.</summary>
        public Vector3 GetWorldWaypoint(int index, Vector3 worldOrigin, float scale = 1f)
        {
            if (index < 0 || index >= waypoints.Count) return worldOrigin;
            return worldOrigin + waypoints[index] * scale;
        }

        /// <summary>Returns the world-space player start position.</summary>
        public Vector3 GetWorldPlayerStart(Vector3 worldOrigin, float scale = 1f)
        {
            return worldOrigin + playerStartOffset * scale;
        }

        /// <summary>
        /// Returns world-space positions for all waypoints. Used for ghost shape rendering.
        /// </summary>
        public Vector3[] GetAllWorldWaypoints(Vector3 worldOrigin, float scale = 1f)
        {
            EnsureWaypoints();
            var result = new Vector3[waypoints.Count];
            for (int i = 0; i < waypoints.Count; i++)
                result[i] = worldOrigin + waypoints[i] * scale;
            return result;
        }

        // ── Procedural shape generation (available at runtime) ────────────────

        public void GeneratePreset(ShapePreset preset, float radius = 100f)
        {
            waypoints.Clear();
            trailEnabledPerSegment.Clear();

            switch (preset)
            {
                case ShapePreset.Circle:
                    GenerateCircle(radius, 24);
                    break;
                case ShapePreset.Star:
                    GenerateStar(radius, radius * 0.4f, 6);
                    break;
                case ShapePreset.Heart:
                    GenerateHeart(radius);
                    break;
                case ShapePreset.Lightning:
                    GenerateLightning(radius);
                    break;
                case ShapePreset.Smiley:
                    GenerateSmiley(radius);
                    break;
                case ShapePreset.Spiral:
                    GenerateSpiral(radius);
                    break;
                case ShapePreset.Diamond:
                    GenerateDiamond(radius);
                    break;
                case ShapePreset.Infinity:
                    GenerateInfinity(radius);
                    break;
                case ShapePreset.Arrow:
                    GenerateArrow(radius);
                    break;
                case ShapePreset.Wave:
                    GenerateWave(radius);
                    break;
            }
        }

        void GenerateCircle(float r, int points)
        {
            for (int i = 0; i <= points; i++)
            {
                float angle = (i / (float)points) * Mathf.PI * 2f;
                waypoints.Add(new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f));
                trailEnabledPerSegment.Add(true);
            }
        }

        void GenerateStar(float outerR, float innerR, int points)
        {
            int totalPoints = points * 2;
            for (int i = 0; i <= totalPoints; i++)
            {
                float angle = (i / (float)totalPoints) * Mathf.PI * 2f - Mathf.PI / 2f;
                float r = (i % 2 == 0) ? outerR : innerR;
                waypoints.Add(new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f));
                trailEnabledPerSegment.Add(true);
            }
        }

        void GenerateHeart(float r)
        {
            int points = 30;
            for (int i = 0; i <= points; i++)
            {
                float t = (i / (float)points) * Mathf.PI * 2f;
                float x = r * 0.9f * Mathf.Pow(Mathf.Sin(t), 3f);
                float y = r * (0.8125f * Mathf.Cos(t)
                               - 0.3125f * Mathf.Cos(2f * t)
                               - 0.125f  * Mathf.Cos(3f * t)
                               - 0.0625f * Mathf.Cos(4f * t));
                waypoints.Add(new Vector3(x, y, 0f));
                trailEnabledPerSegment.Add(true);
            }
        }

        void GenerateLightning(float r)
        {
            // Main bolt — dense zigzag from top to fork point
            waypoints.AddRange(new[]
            {
                new Vector3( 0.1f  * r,  r,          0f),   // top
                new Vector3(-0.2f  * r,  0.7f  * r,  0f),   // zag left
                new Vector3( 0.15f * r,  0.5f  * r,  0f),   // zig right
                new Vector3(-0.25f * r,  0.25f * r,  0f),   // zag left
                new Vector3( 0.2f  * r,  0.05f * r,  0f),   // zig right — fork point
            });

            // Main bolt continues down from fork
            waypoints.AddRange(new[]
            {
                new Vector3(-0.15f * r, -0.2f  * r,  0f),   // zag left
                new Vector3( 0.1f  * r, -0.4f  * r,  0f),   // zig right
                new Vector3(-0.2f  * r, -0.65f * r,  0f),   // zag left
                new Vector3( 0.05f * r, -r,          0f),   // bottom tip
            });

            // Pen up — jump back to fork point for branch
            trailEnabledPerSegment.AddRange(new[] { true, true, true, true, true, true, true, true, false });

            // Branch bolt going right
            waypoints.AddRange(new[]
            {
                new Vector3( 0.2f  * r,  0.05f * r,  0f),   // fork point (same as main[4])
                new Vector3( 0.45f * r, -0.15f * r,  0f),   // branch right
                new Vector3( 0.35f * r, -0.35f * r,  0f),   // branch zig
                new Vector3( 0.55f * r, -0.55f * r,  0f),   // branch tip
            });
            trailEnabledPerSegment.AddRange(new[] { true, true, true, true });
        }

        void GenerateSmiley(float r)
        {
            float eyeR = r * 0.12f;
            float eyeY = r * 0.25f;
            float eyeX = r * 0.3f;
            float browY = eyeY + eyeR + r * 0.08f;
            int eyePoints = 8;

            // ── Left eyebrow (arc) ──
            for (int i = 0; i <= 4; i++)
            {
                float t = (float)i / 4;
                float x = -eyeX - eyeR + t * eyeR * 2f;
                float y = browY + Mathf.Sin(t * Mathf.PI) * r * 0.06f;
                waypoints.Add(new Vector3(x, y, 0f));
                trailEnabledPerSegment.Add(true);
            }

            // Pen up → left eye
            trailEnabledPerSegment[trailEnabledPerSegment.Count - 1] = false;

            // ── Left eye (circle) ──
            for (int i = 0; i <= eyePoints; i++)
            {
                float angle = (i / (float)eyePoints) * Mathf.PI * 2f;
                waypoints.Add(new Vector3(-eyeX + Mathf.Cos(angle) * eyeR,
                    eyeY + Mathf.Sin(angle) * eyeR, 0f));
                trailEnabledPerSegment.Add(true);
            }

            // Pen up → right eyebrow
            trailEnabledPerSegment[trailEnabledPerSegment.Count - 1] = false;

            // ── Right eyebrow (arc) ──
            for (int i = 0; i <= 4; i++)
            {
                float t = (float)i / 4;
                float x = eyeX - eyeR + t * eyeR * 2f;
                float y = browY + Mathf.Sin(t * Mathf.PI) * r * 0.06f;
                waypoints.Add(new Vector3(x, y, 0f));
                trailEnabledPerSegment.Add(true);
            }

            // Pen up → right eye
            trailEnabledPerSegment[trailEnabledPerSegment.Count - 1] = false;

            // ── Right eye (circle) ──
            for (int i = 0; i <= eyePoints; i++)
            {
                float angle = (i / (float)eyePoints) * Mathf.PI * 2f;
                waypoints.Add(new Vector3(eyeX + Mathf.Cos(angle) * eyeR,
                    eyeY + Mathf.Sin(angle) * eyeR, 0f));
                trailEnabledPerSegment.Add(true);
            }

            // Pen up → nose
            trailEnabledPerSegment[trailEnabledPerSegment.Count - 1] = false;

            // ── Nose (small vertical line) ──
            waypoints.Add(new Vector3(0f, r * 0.08f, 0f));
            trailEnabledPerSegment.Add(true);
            waypoints.Add(new Vector3(0f, -r * 0.05f, 0f));
            trailEnabledPerSegment.Add(true);

            // Pen up → mouth
            trailEnabledPerSegment[trailEnabledPerSegment.Count - 1] = false;

            // ── Mouth (wide smile arc) ──
            float mouthR = r * 0.5f;
            float mouthY = -r * 0.2f;
            int mouthPoints = 12;
            for (int i = 0; i <= mouthPoints; i++)
            {
                float angle = Mathf.PI - (i / (float)mouthPoints) * Mathf.PI;
                waypoints.Add(new Vector3(Mathf.Cos(angle) * mouthR,
                    mouthY - Mathf.Sin(angle) * mouthR * 0.35f, 0f));
                trailEnabledPerSegment.Add(true);
            }
        }

        void GenerateSpiral(float r)
        {
            int points = 36;
            float revolutions = 3.5f;
            for (int i = 0; i <= points; i++)
            {
                float t = (float)i / points;
                float angle = t * revolutions * Mathf.PI * 2f;
                float radius = t * r;
                waypoints.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
                trailEnabledPerSegment.Add(true);
            }
        }

        void GenerateDiamond(float r)
        {
            // Gem-cut diamond: crown (top facets) + girdle (widest) + pavilion (bottom point)
            float crownTop = r * 0.7f;
            float girdle = r * 0.5f;     // half-width at widest
            float girdleY = r * 0.1f;    // girdle sits slightly above center
            float crownMid = r * 0.45f;  // crown facet midpoint width

            // Outline: top → right crown → girdle right → bottom point → girdle left → left crown → top
            var verts = new Vector3[]
            {
                new(0f, r, 0f),                            // table top center
                new(crownMid * 0.5f, crownTop, 0f),       // right table edge
                new(crownMid, girdleY + r * 0.25f, 0f),   // right crown facet
                new(girdle, girdleY, 0f),                  // right girdle
                new(girdle * 0.6f, -r * 0.3f, 0f),        // right pavilion facet
                new(0f, -r, 0f),                           // culet (bottom point)
                new(-girdle * 0.6f, -r * 0.3f, 0f),       // left pavilion facet
                new(-girdle, girdleY, 0f),                 // left girdle
                new(-crownMid, girdleY + r * 0.25f, 0f),  // left crown facet
                new(-crownMid * 0.5f, crownTop, 0f),      // left table edge
                new(0f, r, 0f),                            // close at top
            };

            // Pen up → draw the girdle line across
            foreach (var v in verts)
            {
                waypoints.Add(v);
                trailEnabledPerSegment.Add(true);
            }

            // Pen up → draw horizontal girdle line
            trailEnabledPerSegment[trailEnabledPerSegment.Count - 1] = false;
            waypoints.Add(new Vector3(-girdle, girdleY, 0f));
            trailEnabledPerSegment.Add(true);
            waypoints.Add(new Vector3(girdle, girdleY, 0f));
            trailEnabledPerSegment.Add(true);
        }

        void GenerateInfinity(float r)
        {
            int points = 32;
            for (int i = 0; i <= points; i++)
            {
                float t = (float)i / points * Mathf.PI * 2f;
                float denom = 1f + Mathf.Sin(t) * Mathf.Sin(t);
                float x = r * Mathf.Cos(t) / denom;
                float y = r * Mathf.Sin(t) * Mathf.Cos(t) / denom;
                waypoints.Add(new Vector3(x, y, 0f));
                trailEnabledPerSegment.Add(true);
            }
        }

        void GenerateArrow(float r)
        {
            float hw = r * 0.8f;   // arrowhead half-width
            float hh = r * 0.6f;   // arrowhead height
            float sl = r * 1.2f;   // shaft length
            float sw = r * 0.15f;  // shaft half-width
            float shaftTop = -hh * 0.1f;
            float fletchY = -sl + r * 0.15f; // fletching start Y
            float fletchW = r * 0.3f;        // fletching width

            // Arrowhead outline
            var verts = new Vector3[]
            {
                new(0f, hh, 0f),                          // tip
                new(hw * 0.5f, shaftTop, 0f),              // right wing
                new(sw, shaftTop, 0f),                     // right shaft top
                new(sw, fletchY, 0f),                      // right shaft before fletching
                new(fletchW, fletchY - r * 0.1f, 0f),     // right fletching out
                new(sw, fletchY - r * 0.2f, 0f),          // right fletching back in
                new(sw, -sl, 0f),                          // right shaft bottom
                new(-sw, -sl, 0f),                         // left shaft bottom
                new(-sw, fletchY - r * 0.2f, 0f),         // left fletching back in
                new(-fletchW, fletchY - r * 0.1f, 0f),    // left fletching out
                new(-sw, fletchY, 0f),                     // left shaft before fletching
                new(-sw, shaftTop, 0f),                    // left shaft top
                new(-hw * 0.5f, shaftTop, 0f),             // left wing
                new(0f, hh, 0f),                           // close at tip
            };
            foreach (var v in verts)
            {
                waypoints.Add(v);
                trailEnabledPerSegment.Add(true);
            }
        }

        void GenerateWave(float r)
        {
            int points = 36;
            int cycles = 3;
            float totalWidth = r * 2.5f;
            for (int i = 0; i <= points; i++)
            {
                float t = (float)i / points;
                float x = t * totalWidth - totalWidth * 0.5f;
                float y = Mathf.Sin(t * cycles * Mathf.PI * 2f) * r * 0.5f;
                waypoints.Add(new Vector3(x, y, 0f));
                trailEnabledPerSegment.Add(true);
            }
        }
    }
}
