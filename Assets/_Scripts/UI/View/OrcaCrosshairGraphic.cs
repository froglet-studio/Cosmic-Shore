using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Orca gunners' crosshairs, each bespoke to its weapon (Crew of Four Lab round 6, Garrett 2026-10-21: "all
    /// gunners need crosshairs that are bespoke to their weapon"). Generated geometry on a screen-space canvas, as
    /// <see cref="ScopeCrosshairGraphic"/> is, so it needs no sprite and stays crisp at any resolution: every line carries a
    /// zero-alpha feather (a canvas gives generated geometry no antialiasing) and a dark halo under it so it reads over a
    /// lit arena.
    /// <list type="bullet">
    /// <item><b>Scope</b> (the sniper, always scoped): a dark surround outside a round eyepiece, fine cross wires with mil
    /// ticks and heavy outer posts, the sniper cone drawn at its true angular size, and the recharge as a ring round the
    /// eyepiece.</item>
    /// <item><b>Nib</b> (the fast gun, a pen): a fine dot, a slanted nib stroke at the pen's 45 degrees, a lime tip while
    /// the next round is a pen round, and the heat as an arc under it.</item>
    /// <item><b>Bomb</b>: before the throw, a dashed ring the size of the blast where the view converges, with a chevron;
    /// with a bomb in the air, the same ring round the live bomb, pulsing, so the gunner sees what a press would catch.</item>
    /// <item><b>Missile</b>: a small ring (the missile's own view steers by its nose).</item>
    /// </list>
    /// Positions are canvas pixels from the rect's centre; <c>OrcaCrewView</c> sets them every frame. A HUD crosshair is
    /// UI, so it is flat by design (the LOCKED "nothing in the world looks flat" rule is the world's).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class OrcaCrosshairGraphic : MaskableGraphic
    {
        public enum Style { None = 0, Scope = 1, Nib = 2, Bomb = 3, Missile = 4 }

        public Style Kind = Style.None;
        /// <summary>Where the weapon's line meets the view (canvas px from the centre).</summary>
        public Vector2 Aim;
        /// <summary>Scope: the eyepiece's radius and the cone's radius (px). Bomb: the blast ring's radius at the aim (px).</summary>
        public float Radius, ConeRadius;
        /// <summary>Scope: the recharge left (1 just fired, 0 ready). Nib: the heat (0-1).</summary>
        public float Value01;
        public bool Ready, Hot, PenReady, BombOut;
        /// <summary>A bomb in the air: where it is on screen, whether it is in front of the camera, and its blast ring (px).</summary>
        public Vector2 BombAt;
        public bool BombVisible;
        public float BombRadius, Pulse01;
        /// <summary>Line widths scale with the screen (1 at 1080 px tall).</summary>
        public float Scale = 1f;
        public Color LineColor = Color.white, ReadyColor = Color.green, HotColor = Color.red, WarmColor = new(1f, 0.65f, 0f, 1f);
        public Color SurroundColor = new(0f, 0f, 0f, 0.82f);

        static readonly Color Halo = new(0f, 0f, 0f, 0.5f);
        const float Feather = 1f;

        /// <summary>Call after changing the public state: rebuilds the mesh.</summary>
        public void Refresh() => SetVerticesDirty();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2 c = GetPixelAdjustedRect().center;
            float s = Mathf.Max(0.5f, Scale);
            switch (Kind)
            {
                case Style.Scope: Scope(vh, c, s); break;
                case Style.Nib: Nib(vh, c + Aim, s); break;
                case Style.Bomb: Bomb(vh, c, s); break;
                case Style.Missile: Arc(vh, c + Aim, 7f * s, 0f, 360f, 1.5f * s, LineColor, true); break;
            }
        }

        void Scope(VertexHelper vh, Vector2 c, float s)
        {
            var rect = GetPixelAdjustedRect();
            float r = Mathf.Max(20f, Radius), far = Mathf.Max(rect.width, rect.height) * 1.5f;
            Annulus(vh, c, r, far, SurroundColor);
            var ring = Ready ? ReadyColor : HotColor;
            Arc(vh, c, r, 0f, 360f, 2f * s, new Color(1f, 1f, 1f, 0.18f), false);
            Arc(vh, c, r + 5f * s, 90f, 90f - 360f * (Ready ? 1f : 1f - Mathf.Clamp01(Value01)), 3f * s, ring, false);
            float cone = Mathf.Max(5f * s, ConeRadius), gap = cone + 6f * s;
            var wire = new Color(LineColor.r, LineColor.g, LineColor.b, 0.85f);
            foreach (var d in Axes)
            {
                Line(vh, c + d * gap, c + d * r, 1f * s, wire, false);
                if (d.y <= 0f) Line(vh, c + d * (r * 0.55f), c + d * r, 3f * s, wire, false);   // heavy posts below and to the sides
                for (int k = 1; k <= 4; k++)
                {
                    float o = gap + k * (r * 0.5f - gap) / 4f;
                    var side = new Vector2(-d.y, d.x) * 3f * s;
                    Line(vh, c + d * o - side, c + d * o + side, 1f * s, wire, false);
                }
            }
            Arc(vh, c, cone, 0f, 360f, 1.5f * s, ring, false);
        }

        void Nib(VertexHelper vh, Vector2 c, float s)
        {
            var col = Hot ? HotColor : LineColor;
            float r = 24f * s;
            var nib = new Vector2(1f, 1f).normalized;   // the pen's 45 degrees (screen up-right)
            Line(vh, c + nib * 6f * s, c + nib * r, 2f * s, col, true);
            Line(vh, c - nib * 6f * s, c - nib * r, 2f * s, col, true);
            Line(vh, c + Vector2.left * r, c + Vector2.left * (r - 8f * s), 2f * s, col, true);
            Line(vh, c + Vector2.right * r, c + Vector2.right * (r - 8f * s), 2f * s, col, true);
            Disc(vh, c, 1.8f * s, col);
            if (PenReady && !Hot)
            {
                var tip = c + nib * (r + 5f * s);
                Tri(vh, tip + new Vector2(4f, 4f) * s, tip + new Vector2(-3f, 1f) * s, tip + new Vector2(1f, -3f) * s, ReadyColor);
            }
            float ar = r + 9f * s;
            Arc(vh, c, ar, 216f, 324f, 3f * s, new Color(1f, 1f, 1f, 0.15f), false);   // under the nib, left to right
            if (Value01 > 0f) Arc(vh, c, ar, 216f, 216f + 108f * Mathf.Clamp01(Value01), 3f * s, Hot ? HotColor : WarmColor, false);
        }

        void Bomb(VertexHelper vh, Vector2 c, float s)
        {
            if (BombOut)
            {
                if (BombVisible)
                {
                    var col = WarmColor; col.a = 0.6f + 0.4f * Pulse01;
                    Arc(vh, c + BombAt, Mathf.Max(8f * s, BombRadius), 0f, 360f, 2f * s, col, true);
                }
                Arc(vh, c + Aim, 6f * s, 0f, 360f, 1f * s, new Color(1f, 1f, 1f, 0.35f), false);
                return;
            }
            var at = c + Aim;
            float rr = Mathf.Max(10f * s, Radius);
            for (int k = 0; k < 24; k++) if ((k & 1) == 0) Arc(vh, at, rr, k * 15f, k * 15f + 9f, 1.5f * s, ReadyColor, true);   // dashed
            Line(vh, at + new Vector2(-9f, 6f) * s, at + new Vector2(0f, -3f) * s, 2f * s, ReadyColor, true);
            Line(vh, at + new Vector2(0f, -3f) * s, at + new Vector2(9f, 6f) * s, 2f * s, ReadyColor, true);
            Line(vh, at + Vector2.left * (rr + 8f * s), at + Vector2.left * (rr - 4f * s), 2f * s, ReadyColor, true);
            Line(vh, at + Vector2.right * (rr - 4f * s), at + Vector2.right * (rr + 8f * s), 2f * s, ReadyColor, true);
        }

        static readonly Vector2[] Axes = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };

        // ------------------------------------------------------------------ primitives

        // A feathered strip from a to b; with a halo, a wider dark strip under it first.
        static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color, bool halo)
        {
            if (halo) Strip(vh, a, b, width + 2.5f, Halo);
            Strip(vh, a, b, width, color);
        }

        static void Strip(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            var d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            var side = new Vector2(-d.y, d.x).normalized;
            float h = width * 0.5f;
            Color32 solid = color, clear = new Color(color.r, color.g, color.b, 0f);
            int i = vh.currentVertCount;
            vh.AddVert(a - side * (h + Feather), clear, Vector2.zero);
            vh.AddVert(a - side * h, solid, Vector2.zero);
            vh.AddVert(a + side * h, solid, Vector2.zero);
            vh.AddVert(a + side * (h + Feather), clear, Vector2.zero);
            vh.AddVert(b - side * (h + Feather), clear, Vector2.zero);
            vh.AddVert(b - side * h, solid, Vector2.zero);
            vh.AddVert(b + side * h, solid, Vector2.zero);
            vh.AddVert(b + side * (h + Feather), clear, Vector2.zero);
            for (int band = 0; band < 3; band++)
            {
                vh.AddTriangle(i + band, i + 4 + band, i + 5 + band);
                vh.AddTriangle(i + band, i + 5 + band, i + 1 + band);
            }
        }

        // An arc of a circle from deg0 to deg1 (degrees, counter-clockwise from screen right), as short strips.
        static void Arc(VertexHelper vh, Vector2 c, float r, float deg0, float deg1, float width, Color color, bool halo)
        {
            float span = deg1 - deg0;
            int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(span) / 360f * Mathf.Max(12f, r * 0.6f)), 2, 160);
            for (int pass = halo ? 0 : 1; pass < 2; pass++)
            {
                var prev = c + Dir(deg0) * r;
                for (int k = 1; k <= n; k++)
                {
                    var next = c + Dir(deg0 + span * k / n) * r;
                    Strip(vh, prev, next, pass == 0 ? width + 2.5f : width, pass == 0 ? Halo : color);
                    prev = next;
                }
            }
        }

        static Vector2 Dir(float deg) => new(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

        static void Disc(VertexHelper vh, Vector2 c, float r, Color color)
        {
            const int n = 12;
            int i0 = vh.currentVertCount;
            vh.AddVert(c, color, Vector2.zero);
            for (int k = 0; k <= n; k++) vh.AddVert(c + Dir(k * 360f / n) * r, color, Vector2.zero);
            for (int k = 0; k < n; k++) vh.AddTriangle(i0, i0 + 1 + k, i0 + 2 + k);
        }

        static void Tri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(c, color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
        }

        // The scope's surround: everything between the eyepiece and far off screen, with a feathered inner edge.
        static void Annulus(VertexHelper vh, Vector2 c, float inner, float outer, Color color)
        {
            const int n = 96;
            Color32 solid = color, clear = new Color(color.r, color.g, color.b, 0f);
            int i0 = vh.currentVertCount;
            for (int k = 0; k <= n; k++)
            {
                var d = Dir(k * 360f / n);
                vh.AddVert(c + d * (inner - 1.5f), clear, Vector2.zero);
                vh.AddVert(c + d * (inner + 1.5f), solid, Vector2.zero);
                vh.AddVert(c + d * outer, solid, Vector2.zero);
            }
            for (int k = 0; k < n; k++)
            {
                int a = i0 + k * 3, b = a + 3;
                vh.AddTriangle(a, b, b + 1); vh.AddTriangle(a, b + 1, a + 1);
                vh.AddTriangle(a + 1, b + 1, b + 2); vh.AddTriangle(a + 1, b + 2, a + 2);
            }
        }
    }
}
