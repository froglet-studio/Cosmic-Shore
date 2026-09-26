using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A one-point-perspective TUNNEL, GENERATED rather than sprited, in the same family as
    /// <see cref="TrapezoidGraphic"/>, <see cref="BlastProfileGraphic"/> and
    /// <see cref="ScopeRingGraphic"/>: rings receding toward a vanishing point at the rect's own
    /// centre, over a wall that fades as it converges, with a point of light at the far end.
    ///
    /// <para><b>The vanishing point is the CENTRE, so the whole thing is radially symmetric</b> -
    /// which is what makes it read as looking down the axis of a ring you are about to fly through,
    /// rather than as a decorative bullseye, and what stops it implying a direction the ability it
    /// sits on does not have. It is drawn INSIDE ring art the card already has: the Squirrel's Boost
    /// Ring icon is a circle of eight prism blocks whose middle is empty, so this fills that hole and
    /// never touches a pixel of it - which is what lets the icon wear the DANGER colour and the
    /// tunnel wear the TEAM colour on one card (<c>Docs/PALETTE.md §4.3</c>: two saturated hues are
    /// separated, never blended).</para>
    ///
    /// <para><b>Three cues, and it needs all three - which is a judged result rather than a
    /// derivation.</b> Ten candidates were rendered at the size this is actually read at (60 drawn
    /// px) and the failures are instructive: rings alone read as a TARGET; a haze brightest at the
    /// CENTRE reads as a filled blob with a hole in it; three rings crowd the core into mush. What
    /// reads as a tunnel is the WALL fading as it converges (brightest where it is nearest, at
    /// <see cref="Profile.FrontRadius"/>, which is the foreshortening), TWO rings far enough apart to
    /// stay separate, and a small bright CORE at the vanishing point - the light at the end.</para>
    ///
    /// <para><b>The recession is the real projection.</b> Rings evenly spaced in DEPTH project to
    /// radii <c>FrontRadius / (1 + k * DepthStep)</c>, which compresses toward the centre the way a
    /// corridor does; a constant ratio per ring does not. Thickness is projected by the SAME factor,
    /// because a ring further away is thinner as well as smaller - dropping that is what makes a
    /// stack of rings look flat.</para>
    ///
    /// <para>Antialiasing is baked into the geometry as a zero-alpha feather either side of every
    /// band, for the reason <see cref="TrapezoidGraphic"/> records: a UGUI canvas gives a generated
    /// curve none. Raycasting is off and should stay off - this is a readout inside an ability card,
    /// and a hit target here would eat the press meant for the ability. Nothing about it is live: it
    /// is built once and rebuilt only when its colour changes, so it costs one draw call and no
    /// per-frame CPU.</para>
    /// </summary>
    [AddComponentMenu("UI/Perspective Tunnel Graphic", 15)]
    public class PerspectiveTunnelGraphic : MaskableGraphic
    {
        /// <summary>
        /// The whole shape, passed BY VALUE. Deliberately not a serialized field on this component:
        /// an instance is created at runtime, so whichever component builds one owns these numbers
        /// and exposes them on its own prefab - and a serialized STRUCT that a shipped prefab has
        /// never been re-saved with deserializes as all zeros, where a plain float field keeps its
        /// C# initializer. So the authoring surface stays plain fields on the builder, and this is
        /// only how they travel.
        /// </summary>
        public struct Profile
        {
            /// <summary>Radius of the NEAREST ring, and where the wall is brightest.</summary>
            public float FrontRadius;
            /// <summary>How many receding rings. Two reads as depth; three crowds the core.</summary>
            public int Rings;
            /// <summary>Depth between rings as a fraction of viewing distance.</summary>
            public float DepthStep;
            /// <summary>Band thickness of the nearest ring; the rest are projected from it.</summary>
            public float FrontThickness;
            /// <summary>Alpha of the nearest ring, as a fraction of this graphic's own alpha.</summary>
            public float FrontAlpha;
            /// <summary>Alpha multiplier per ring of depth - the second half of the recession.</summary>
            public float DepthFade;
            /// <summary>Alpha of the wall AT FrontRadius, fading to nothing as it converges.</summary>
            public float WallAlpha;
            /// <summary>Radius of the point of light at the vanishing point. 0 = off.</summary>
            public float CoreRadius;
            /// <summary>Alpha of that point of light.</summary>
            public float CoreAlpha;
        }

        [Tooltip("Floor on a projected thickness. Without it the furthest ring goes sub-pixel and " +
                 "simply stops being drawn, which reads as a SHORTER tunnel rather than a deeper one.")]
        [SerializeField, Min(0.1f)] private float minThickness = 1f;

        [Tooltip("Segments in a full turn. At HUD radii the front ring's chord stays under 2px here.")]
        [SerializeField, Range(12, 128)] private int segments = 48;

        [Tooltip("Width of the zero-alpha feather on each side of every band, in canvas units. " +
                 "This IS the antialiasing - a canvas provides none for generated geometry.")]
        [SerializeField, Min(0f)] private float feather = 1f;

        private Profile _profile;

        /// <summary>Sets the shape and rebuilds the mesh once, rather than once per field.</summary>
        public void Configure(Profile profile)
        {
            profile.FrontRadius = Mathf.Max(0f, profile.FrontRadius);
            profile.Rings = Mathf.Clamp(profile.Rings, 1, 8);
            profile.DepthStep = Mathf.Max(0.01f, profile.DepthStep);
            profile.FrontThickness = Mathf.Max(0.25f, profile.FrontThickness);
            profile.FrontAlpha = Mathf.Clamp01(profile.FrontAlpha);
            profile.DepthFade = Mathf.Clamp(profile.DepthFade, 0.1f, 1f);
            profile.WallAlpha = Mathf.Clamp01(profile.WallAlpha);
            profile.CoreRadius = Mathf.Max(0f, profile.CoreRadius);
            profile.CoreAlpha = Mathf.Clamp01(profile.CoreAlpha);
            _profile = profile;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_profile.FrontRadius <= 0f) return;

            var rect = GetPixelAdjustedRect();
            Vector2 centre = rect.center;
            int steps = Mathf.Max(3, segments);

            // Back to front: the wall, then the rings over it, then the core over everything. Within
            // one mesh there is no depth test, so index order is all that decides what is on top.
            if (_profile.WallAlpha > 0f)
                EmitWall(vh, centre, steps);

            float alpha = _profile.FrontAlpha;
            for (int k = 0; k < _profile.Rings; k++)
            {
                // Evenly spaced in DEPTH, so the projection is 1/(1 + k*step) - the same factor on
                // the radius and on the thickness, because they are the same foreshortening.
                float project = 1f / (1f + k * _profile.DepthStep);
                float outer = _profile.FrontRadius * project;
                float thickness = Mathf.Max(minThickness, _profile.FrontThickness * project);
                if (outer > 0.25f)
                    EmitRing(vh, centre, steps, outer, thickness, alpha);
                alpha *= _profile.DepthFade;
            }

            if (_profile.CoreRadius > 0f && _profile.CoreAlpha > 0f)
                EmitCore(vh, centre, steps);
        }

        /// <summary>
        /// The tunnel wall: a fan brightest at <see cref="Profile.FrontRadius"/>, where it is
        /// nearest, fading to nothing as it converges on the vanishing point. Vertex-coloured, so the
        /// gradient is free.
        /// </summary>
        void EmitWall(VertexHelper vh, Vector2 centre, int steps)
        {
            int first = vh.currentVertCount;
            var near = new Color(color.r, color.g, color.b, color.a * _profile.WallAlpha);
            var far = new Color(color.r, color.g, color.b, 0f);

            vh.AddVert(centre, far, Vector2.zero);
            for (int i = 0; i <= steps; i++)
                vh.AddVert(centre + Direction(i, steps) * _profile.FrontRadius, near, Vector2.zero);

            for (int i = 0; i < steps; i++)
                vh.AddTriangle(first, first + 1 + i, first + 2 + i);
        }

        /// <summary>The point of light at the far end - a feathered disc on the vanishing point.</summary>
        void EmitCore(VertexHelper vh, Vector2 centre, int steps)
        {
            int first = vh.currentVertCount;
            var hot = new Color(color.r, color.g, color.b, color.a * _profile.CoreAlpha);
            var clear = new Color(color.r, color.g, color.b, 0f);

            vh.AddVert(centre, hot, Vector2.zero);
            for (int i = 0; i <= steps; i++)
                vh.AddVert(centre + Direction(i, steps) * _profile.CoreRadius, hot, Vector2.zero);
            for (int i = 0; i <= steps; i++)
                vh.AddVert(centre + Direction(i, steps) * (_profile.CoreRadius + feather),
                           clear, Vector2.zero);

            int rim = first + 1;
            int fade = rim + steps + 1;
            for (int i = 0; i < steps; i++)
            {
                vh.AddTriangle(first, rim + i, rim + i + 1);
                vh.AddTriangle(rim + i, fade + i, fade + i + 1);
                vh.AddTriangle(rim + i, fade + i + 1, rim + i + 1);
            }
        }

        void EmitRing(VertexHelper vh, Vector2 centre, int steps, float outer, float thickness,
                      float alphaScale)
        {
            int first = vh.currentVertCount;
            float inner = Mathf.Max(0f, outer - thickness);
            var solid = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(alphaScale));
            var clear = new Color(color.r, color.g, color.b, 0f);

            for (int i = 0; i <= steps; i++)
            {
                Vector2 dir = Direction(i, steps);
                vh.AddVert(centre + dir * Mathf.Max(0f, inner - feather), clear, Vector2.zero);
                vh.AddVert(centre + dir * inner, solid, Vector2.zero);
                vh.AddVert(centre + dir * outer, solid, Vector2.zero);
                vh.AddVert(centre + dir * (outer + feather), clear, Vector2.zero);
            }

            for (int i = 0; i < steps; i++)
            {
                int a = first + i * 4;
                int b = first + (i + 1) * 4;
                for (int band = 0; band < 3; band++)
                {
                    vh.AddTriangle(a + band, b + band, b + band + 1);
                    vh.AddTriangle(a + band, b + band + 1, a + band + 1);
                }
            }
        }

        static Vector2 Direction(int i, int steps)
        {
            // Start at the TOP and run clockwise, matching ScopeRingGraphic - a shared convention so
            // two generated rings in one card can never disagree about where their seam is.
            float a = Mathf.PI * 0.5f - Mathf.PI * 2f * (i / (float)steps);
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }
    }
}
