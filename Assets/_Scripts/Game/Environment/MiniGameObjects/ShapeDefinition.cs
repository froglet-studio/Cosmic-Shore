using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.ShapeDrawing
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
        menuName = "CosmicShore/Shape Drawing/Shape Definition")]
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
                    GenerateCircle(radius, 16);
                    break;
                case ShapePreset.Star:
                    GenerateStar(radius, radius * 0.45f, 5);
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
            for (int i = 0; i <= 20; i++)
            {
                float t = (i / 20f) * Mathf.PI * 2f;
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
            float h = r;
            waypoints.AddRange(new[]
            {
                new Vector3( 0.3f * r,  h,       0f),
                new Vector3(-0.1f * r,  0.1f*h,  0f),
                new Vector3( 0.2f * r,  0.05f*h, 0f),
                new Vector3(-0.3f * r, -h,        0f),
                new Vector3( 0.1f * r, -0.05f*h, 0f),
                new Vector3(-0.2f * r, -0.1f*h,  0f),
                new Vector3( 0.3f * r,  h,        0f),
            });
            for (int i = 0; i < waypoints.Count; i++)
                trailEnabledPerSegment.Add(true);
        }

        void GenerateSmiley(float r)
        {
            // Left eye (3 points)
            float eyeR = r * 0.12f;
            float eyeY = r * 0.25f;
            for (int i = 0; i <= 3; i++)
            {
                float angle = (i / 3f) * Mathf.PI * 2f;
                waypoints.Add(new Vector3(-r * 0.3f + Mathf.Cos(angle) * eyeR,
                    eyeY + Mathf.Sin(angle) * eyeR, 0f));
                trailEnabledPerSegment.Add(true);
            }

            // Pen up: travel to right eye
            trailEnabledPerSegment[trailEnabledPerSegment.Count - 1] = false;

            // Right eye (3 points)
            for (int i = 0; i <= 3; i++)
            {
                float angle = (i / 3f) * Mathf.PI * 2f;
                waypoints.Add(new Vector3(r * 0.3f + Mathf.Cos(angle) * eyeR,
                    eyeY + Mathf.Sin(angle) * eyeR, 0f));
                trailEnabledPerSegment.Add(true);
            }

            // Pen up: travel to mouth start
            trailEnabledPerSegment[trailEnabledPerSegment.Count - 1] = false;

            // Mouth arc (8 points, lower semicircle)
            float mouthR = r * 0.45f;
            for (int i = 0; i <= 8; i++)
            {
                float angle = Mathf.PI + (i / 8f) * Mathf.PI;
                waypoints.Add(new Vector3(Mathf.Cos(angle) * mouthR,
                    -r * 0.1f + Mathf.Sin(angle) * mouthR * 0.5f, 0f));
                trailEnabledPerSegment.Add(true);
            }
        }

        void GenerateSpiral(float r)
        {
            int points = 24;
            float revolutions = 3f;
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
            float w = r * 0.6f;
            var verts = new Vector3[]
            {
                new(0f, r, 0f),
                new(w, 0f, 0f),
                new(0f, -r, 0f),
                new(-w, 0f, 0f),
                new(0f, r, 0f),
            };
            foreach (var v in verts)
            {
                waypoints.Add(v);
                trailEnabledPerSegment.Add(true);
            }
        }

        void GenerateInfinity(float r)
        {
            int points = 24;
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
            float hw = r * 0.8f;
            float hh = r * 0.6f;
            float sl = r * 1.2f;
            float sw = r * 0.2f;
            float shaftTop = -hh * 0.1f;

            var verts = new Vector3[]
            {
                new(0f, hh, 0f),
                new(hw * 0.5f, shaftTop, 0f),
                new(sw * 0.5f, shaftTop, 0f),
                new(sw * 0.5f, -sl, 0f),
                new(-sw * 0.5f, -sl, 0f),
                new(-sw * 0.5f, shaftTop, 0f),
                new(-hw * 0.5f, shaftTop, 0f),
                new(0f, hh, 0f),
            };
            foreach (var v in verts)
            {
                waypoints.Add(v);
                trailEnabledPerSegment.Add(true);
            }
        }

        void GenerateWave(float r)
        {
            int points = 24;
            int cycles = 2;
            float totalWidth = r * 2f;
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
