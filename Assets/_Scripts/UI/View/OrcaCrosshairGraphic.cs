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
    /// <item><b>Nib</b> (the fast gun, a pen): a fine dot, a slanted nib stroke at the pen's 45 degrees, a lime tip while
    /// the next round is a pen round, and the heat as an arc under it.</item>
    /// <item><b>Bomb</b>: a chevron where the bomb will fly, dimmed while a bomb is out. What a press would catch is not
    /// drawn here: the prisms inside the live bomb's blast are LIT in the world (round 9).</item>
    /// <item><b>Missile</b>: a small ring (the missile's own view steers by its nose).</item>
    /// </list>
    /// The sniper's sight is the Serpent's own pip scope (<c>SniperScopeOverlay</c>, round 7), not drawn here.
    /// Positions are canvas pixels from the rect's centre; <c>OrcaCrewView</c> sets them every frame. A HUD crosshair is
    /// UI, so it is flat by design (the LOCKED "nothing in the world looks flat" rule is the world's).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class OrcaCrosshairGraphic : MaskableGraphic
    {
        public enum Style { None = 0, Nib = 2, Bomb = 3, Missile = 4 }

        public Style Kind = Style.None;
        /// <summary>Where the weapon's line meets the view (canvas px from the centre).</summary>
        public Vector2 Aim;
        /// <summary>Nib: the heat (0-1).</summary>
        public float Value01;
        public bool Hot, PenReady, BombOut;
        /// <summary>Line widths scale with the screen (1 at 1080 px tall).</summary>
        public float Scale = 1f;
        public Color LineColor = Color.white, ReadyColor = Color.green, HotColor = Color.red, WarmColor = new(1f, 0.65f, 0f, 1f);

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
                case Style.Nib: Nib(vh, c + Aim, s); break;
                case Style.Bomb: Bomb(vh, c, s); break;
                case Style.Missile: Arc(vh, c + Aim, 7f * s, 0f, 360f, 1.5f * s, LineColor, true); break;
            }
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
            // the aim mark only: a chevron where the bomb will fly. What a live bomb's blast will catch is LIT in the
            // world (PrismLit), never drawn here (round 9, Garrett 2026-10-21: "the bomb should not have a ring that says
            // burst. it should use a lit state to show what it will destroy")
            var at = c + Aim;
            var col = BombOut ? new Color(ReadyColor.r, ReadyColor.g, ReadyColor.b, 0.45f) : ReadyColor;
            Line(vh, at + new Vector2(-9f, 6f) * s, at + new Vector2(0f, -3f) * s, 2f * s, col, true);
            Line(vh, at + new Vector2(0f, -3f) * s, at + new Vector2(9f, 6f) * s, 2f * s, col, true);
            Line(vh, at + Vector2.left * 22f * s, at + Vector2.left * 12f * s, 2f * s, col, true);
            Line(vh, at + Vector2.right * 12f * s, at + Vector2.right * 22f * s, 2f * s, col, true);
        }

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
    }
}
