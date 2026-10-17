using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The navigator's board, drawn as one UI mesh on <see cref="KiteNavigator"/>'s own overlay canvas: the disc of the
    /// hull's x-z plane, the hull at its origin, every prism and rival hull within it as a shadow in its domain's colour
    /// (solid when the kite can touch it, faint when it is further than reachY off the plane), the navigator's motes as
    /// element glyphs, the kite and its tail, loop and effect rings, the kite's charge as pips round the left of the rim
    /// and the pilot's navigator boost as an arc round the right. Board +Z (the hull's forward) is screen up.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class KiteBoardGraphic : MaskableGraphic
    {
        // the lab's element colours (navigator-only; deliberately not the game's crystal colours)
        static readonly Color ChargeColor = new(0.949f, 0.757f, 0.306f);
        static readonly Color MassColor = new(1f, 0.420f, 0.290f);
        static readonly Color SpaceColor = new(0.561f, 0.518f, 1f);
        static readonly Color TimeColor = new(0.212f, 0.827f, 0.604f);
        static readonly Color DiscColor = new(0.02f, 0.05f, 0.10f, 0.55f);

        KiteNavigator _nav;

        internal void Bind(KiteNavigator nav)
        {
            _nav = nav;
            SetVerticesDirty();
        }

        /// <summary>The board's centre and radius in screen pixels.</summary>
        public static void BoardScreen(KiteNavigatorConfigSO cfg, out Vector2 centre, out float radiusPx)
        {
            float side = Mathf.Min(Screen.width, Screen.height);
            radiusPx = cfg.BoardScreenSize * side * 0.5f;
            centre = new Vector2(cfg.BoardScreenCentre.x * Screen.width, cfg.BoardScreenCentre.y * Screen.height);
        }

        /// <summary>A screen pixel as a point on the board (u).</summary>
        public static KiteVec ScreenToBoard(KiteNavigatorConfigSO cfg, in KiteSettings s, Vector2 screen)
        {
            BoardScreen(cfg, out var c, out float rpx);
            float k = s.BoardRadius / Mathf.Max(1f, rpx);
            return new KiteVec((screen.x - c.x) * k, (screen.y - c.y) * k + s.Anchor * s.BoardRadius);
        }

        /// <summary>A point on the board (u) as a screen pixel.</summary>
        public static Vector2 BoardToScreen(KiteNavigatorConfigSO cfg, in KiteSettings s, float x, float z)
        {
            BoardScreen(cfg, out var c, out float rpx);
            float k = rpx / s.BoardRadius;
            return new Vector2(c.x + x * k, c.y + (z - s.Anchor * s.BoardRadius) * k);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_nav == null || !_nav.Config) return;

            var cfg = _nav.Config;
            var s = _nav.Settings;
            BoardScreen(cfg, out var c, out float rpx);
            float k = rpx / s.BoardRadius;
            float now = Time.time;
            var own = _nav.OwnDomain;
            var ownColor = _nav.DomainColor(own);

            Disc(vh, c, rpx, DiscColor, 64);
            Ring(vh, c, rpx, 2f, WithAlpha(ownColor, _nav.HumanNavigates ? 0.9f : 0.4f), 64, 0f, 1f);

            // prisms
            foreach (var p in _nav.Prisms)
            {
                if (!p.Prism) continue;
                var at = BoardToScreen(cfg, s, p.X, p.Z);
                var fwd = p.Prism.transform.forward;
                var dir = new Vector2(Vector3.Dot(fwd, _nav.BoardRight), Vector3.Dot(fwd, _nav.BoardForward));
                if (dir.sqrMagnitude < 1e-4f) dir = Vector2.up;
                bool touch = Mathf.Abs(p.Y) <= cfg.ReachY;
                Rect(vh, at, dir.normalized, Mathf.Max(1.5f, 1f * k), Mathf.Max(2.5f, 2.5f * k),
                     WithAlpha(_nav.DomainColor(p.Domain), touch ? (p.Hostile ? 0.95f : 0.6f) : 0.2f));
            }

            // motes
            foreach (var m in _nav.Motes)
            {
                if (!KiteNavigatorCore.InDisc(s, m.X, m.Z)) continue;
                var at = BoardToScreen(cfg, s, m.X, m.Z);
                float a = Mathf.Abs(m.Y) <= cfg.ReachY ? 1f : 0.25f;
                Glyph(vh, m.Element, at, 6f, a);
            }

            // rival hulls
            foreach (var r in _nav.Rivals)
            {
                if (!KiteNavigatorCore.InDisc(s, r.X, r.Z)) continue;
                var at = BoardToScreen(cfg, s, r.X, r.Z);
                var fwd = r.Status.Transform ? r.Status.Transform.forward : Vector3.forward;
                var dir = new Vector2(Vector3.Dot(fwd, _nav.BoardRight), Vector3.Dot(fwd, _nav.BoardForward));
                if (dir.sqrMagnitude < 1e-4f) dir = Vector2.up;
                float a = Mathf.Abs(r.Y) <= cfg.ReachY ? 1f : 0.3f;
                Triangle(vh, at, dir.normalized, 10f, WithAlpha(_nav.DomainColor(r.Domain), a));
            }

            // our hull, at the board's origin, pointing up
            Triangle(vh, BoardToScreen(cfg, s, 0f, 0f), Vector2.up, 10f, ownColor);

            // effects
            foreach (var fx in _nav.Effects)
            {
                float t = now - fx.T0, life = Mathf.Clamp01(1f - t / 1.4f);
                var at = BoardToScreen(cfg, s, fx.X, fx.Z);
                switch (fx.Kind)
                {
                    case KiteNavigator.FxKind.Loop:
                        if (fx.Loop is { Length: > 2 })
                            for (int i = 0; i < fx.Loop.Length; i++)
                            {
                                var a0 = fx.Loop[i]; var a1 = fx.Loop[(i + 1) % fx.Loop.Length];
                                Line(vh, BoardToScreen(cfg, s, a0.X, a0.Z), BoardToScreen(cfg, s, a1.X, a1.Z), 2f, new Color(1f, 1f, 1f, 0.6f * life));
                            }
                        break;
                    case KiteNavigator.FxKind.Space:
                    {
                        float br = KiteNavigatorCore.BlastRadius(fx.MaxScale, t, KiteNavigator.BlastDelay, KiteNavigator.BlastDuration) * k;
                        float fade = Mathf.Clamp01(1f - (t - KiteNavigator.BlastDelay - KiteNavigator.BlastDuration) / 0.6f);
                        if (br > 1f) Ring(vh, at, Mathf.Min(br, rpx * 1.5f), 3f, WithAlpha(SpaceColor, 0.85f * fade), 48, 0f, 1f);
                        break;
                    }
                    case KiteNavigator.FxKind.Pop:
                        Ring(vh, at, 4f + t * 30f, 1.5f, new Color(1f, 1f, 1f, 0.8f * life), 12, 0f, 1f);
                        break;
                    default:
                        Ring(vh, at, 8f + t * 40f, 2.5f, WithAlpha(FxColor(fx.Kind), life), 24, 0f, 1f);
                        break;
                }
            }

            // the tail, fading toward its end, gold while the kite is charged
            var tail = _nav.Kite.Tail;
            var tailColor = _nav.Charge >= 1f ? ChargeColor : Color.white;
            for (int i = 1; i < tail.Count; i++)
            {
                float f = (float)i / tail.Count;
                Line(vh, BoardToScreen(cfg, s, tail[i - 1].X, tail[i - 1].Z), BoardToScreen(cfg, s, tail[i].X, tail[i].Z),
                     1f + 2f * f, WithAlpha(tailColor, 0.15f + 0.75f * f));
            }

            // the kite
            var kite = BoardToScreen(cfg, s, _nav.Kite.Position.X, _nav.Kite.Position.Z);
            float kr = Mathf.Max(6f, cfg.KiteRadius * k);
            Diamond(vh, kite, kr, tailColor);
            if (_nav.Charge >= 1f) Ring(vh, kite, kr + 4f, 2f, WithAlpha(ChargeColor, 0.7f), 20, 0f, 1f);

            // the human's thumb and where it aims
            if (_nav.HumanNavigates && _nav.Down)
            {
                var aim = BoardToScreen(cfg, s, _nav.Aim.X, _nav.Aim.Z);
                Ring(vh, aim, 5f, 1.5f, new Color(1f, 1f, 1f, 0.7f), 16, 0f, 1f);
                if ((aim - _nav.ThumbScreen).sqrMagnitude > 4f)
                {
                    Ring(vh, _nav.ThumbScreen, 18f, 1.5f, new Color(1f, 1f, 1f, 0.35f), 24, 0f, 1f);
                    Line(vh, _nav.ThumbScreen, aim, 1f, new Color(1f, 1f, 1f, 0.3f));
                }
            }

            // meters: charge pips (one per whole petal of kite power) and the pilot's boost from the navigator
            int pips = Mathf.Min(30, Mathf.FloorToInt(_nav.Charge));
            for (int i = 0; i < pips; i++)
            {
                float ang = Mathf.PI * (0.62f + 0.76f * (i + 0.5f) / 30f);
                Disc(vh, c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (rpx + 8f), 3f, ChargeColor, 8);
            }
            float boost = Mathf.Clamp01(_nav.BoostEnergy / Mathf.Max(0.01f, cfg.TimeBoostCap));
            if (boost > 0f) Ring(vh, c, rpx + 8f, 4f, TimeColor, 48, -0.12f, -0.12f + 0.24f * boost);
        }

        static Color FxColor(KiteNavigator.FxKind kind) => kind switch
        {
            KiteNavigator.FxKind.Mass => MassColor,
            KiteNavigator.FxKind.Time => TimeColor,
            KiteNavigator.FxKind.Charge => ChargeColor,
            KiteNavigator.FxKind.Steal => ChargeColor,
            _ => new Color(0.6f, 0.6f, 0.65f),
        };

        static Color WithAlpha(Color c, float a) { c.a *= a; return c; }

        // ------------------------------------------------------------------ glyphs (each element its own shape, not only its colour)

        static void Glyph(VertexHelper vh, int element, Vector2 at, float r, float alpha)
        {
            switch (element)
            {
                case 0: Diamond(vh, at, r, WithAlpha(ChargeColor, alpha)); break;                     // Charge
                case 1: Rect(vh, at, Vector2.up, r * 0.8f, r * 0.8f, WithAlpha(MassColor, alpha)); break; // Mass
                case 2: Triangle(vh, at, Vector2.up, r * 1.1f, WithAlpha(SpaceColor, alpha)); break;    // Space
                default: Disc(vh, at, r * 0.85f, WithAlpha(TimeColor, alpha), 10); break;               // Time
            }
        }

        // ------------------------------------------------------------------ mesh primitives

        static void Vert(VertexHelper vh, Vector2 p, Color c)
        {
            vh.AddVert(new Vector3(p.x, p.y, 0f), c, Vector4.zero);
        }

        static void Disc(VertexHelper vh, Vector2 c, float r, Color col, int seg)
        {
            int start = vh.currentVertCount;
            Vert(vh, c, col);
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vert(vh, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, col);
            }
            for (int i = 1; i <= seg; i++) vh.AddTriangle(start, start + i, start + i + 1);
        }

        // an arc of the ring from turn `from` to turn `to` (0..1 of a full turn, counter-clockwise from +x)
        static void Ring(VertexHelper vh, Vector2 c, float r, float width, Color col, int seg, float from, float to)
        {
            int start = vh.currentVertCount;
            float r0 = Mathf.Max(0f, r - width * 0.5f), r1 = r + width * 0.5f;
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(from, to, (float)i / seg) * Mathf.PI * 2f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vert(vh, c + d * r0, col);
                Vert(vh, c + d * r1, col);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = start + i * 2;
                vh.AddTriangle(a, a + 1, a + 3);
                vh.AddTriangle(a, a + 3, a + 2);
            }
        }

        static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color col)
        {
            var d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            var n = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
            int start = vh.currentVertCount;
            Vert(vh, a - n, col); Vert(vh, a + n, col); Vert(vh, b + n, col); Vert(vh, b - n, col);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        // a rectangle of half-width hw across `dir` and half-length hl along it
        static void Rect(VertexHelper vh, Vector2 c, Vector2 dir, float hw, float hl, Color col)
        {
            var n = new Vector2(-dir.y, dir.x) * hw;
            var f = dir * hl;
            int start = vh.currentVertCount;
            Vert(vh, c - f - n, col); Vert(vh, c - f + n, col); Vert(vh, c + f + n, col); Vert(vh, c + f - n, col);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        static void Triangle(VertexHelper vh, Vector2 c, Vector2 dir, float r, Color col)
        {
            var n = new Vector2(-dir.y, dir.x);
            int start = vh.currentVertCount;
            Vert(vh, c + dir * r, col);
            Vert(vh, c - dir * (r * 0.7f) + n * (r * 0.7f), col);
            Vert(vh, c - dir * (r * 0.7f) - n * (r * 0.7f), col);
            vh.AddTriangle(start, start + 1, start + 2);
        }

        static void Diamond(VertexHelper vh, Vector2 c, float r, Color col)
        {
            int start = vh.currentVertCount;
            Vert(vh, c + new Vector2(0f, r), col);
            Vert(vh, c + new Vector2(r * 0.7f, 0f), col);
            Vert(vh, c + new Vector2(0f, -r), col);
            Vert(vh, c + new Vector2(-r * 0.7f, 0f), col);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
