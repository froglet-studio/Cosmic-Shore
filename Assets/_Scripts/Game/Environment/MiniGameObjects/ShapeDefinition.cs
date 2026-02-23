using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.ShapeDrawing
{
    /// <summary>
    /// Defines a drawable shape for Shape Drawing Mode.
    /// Create via: Assets → Create → CosmicShore → Shape Drawing → Shape Definition
    ///
    /// Waypoints are defined in LOCAL space, normalized to roughly a 200-unit bounding box.
    /// The ShapeDrawingManager will offset them into world space at runtime.
    ///
    /// trailEnabledPerSegment controls whether the player's trail is active WHILE flying
    /// TOWARD that waypoint. Index 0 = trail state while flying to waypoint[0], etc.
    /// A false entry = "pen up" (useful for smiley eyes → mouth gap).
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

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: auto-fills waypoints with a procedural shape for quick iteration.
        /// Call from a custom Editor button, not at runtime.
        /// </summary>
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
            // Parametric heart curve, 20 points
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
            // Simple lightning bolt: 6 points, no pen lifts
            float h = r;
            waypoints.AddRange(new[]
            {
                new Vector3( 0.3f * r,  h,       0f),
                new Vector3(-0.1f * r,  0.1f*h,  0f),
                new Vector3( 0.2f * r,  0.05f*h, 0f),
                new Vector3(-0.3f * r, -h,        0f),
                new Vector3( 0.1f * r, -0.05f*h, 0f),
                new Vector3(-0.2f * r, -0.1f*h,  0f),
                new Vector3( 0.3f * r,  h,        0f),  // close
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
                float angle = Mathf.PI + (i / 8f) * Mathf.PI; // π → 2π (bottom arc)
                waypoints.Add(new Vector3(Mathf.Cos(angle) * mouthR,
                    -r * 0.1f + Mathf.Sin(angle) * mouthR * 0.5f, 0f));
                trailEnabledPerSegment.Add(true);
            }
        }
        
        public enum ShapePreset { Circle, Star, Heart, Lightning, Smiley }
#endif
    }
}