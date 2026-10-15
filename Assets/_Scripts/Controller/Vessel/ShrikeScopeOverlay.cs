using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Shrike's screen overlay, for the LOCAL pilot only (design: <c>R_VesselActions/SHRIKE.md</c> §3.4): the zoom's
    /// scope, whose SHAPE is the selected mode's (Garrett, 2026-10-15: "the zoom should change shape depending on what mode
    /// the player is in"), and the marker on the painted target ("there should be an indicator on the painted target").
    ///
    /// <list type="bullet">
    /// <item><b>Space</b> (Void Lance) — a sniper's ring with a split crosshair.</item>
    /// <item><b>Charge</b> (Targeting Array) — four lock brackets round a diamond.</item>
    /// <item><b>Time</b> (Void Sling) — a tether line with chevrons running down it.</item>
    /// </list>
    ///
    /// <para>Built entirely at runtime (a screen-space canvas and three textures drawn from distance fields once per
    /// process), so the prefab carries nothing of it and a pooled or AI-flown hull never makes one.</para>
    /// </summary>
    public sealed class ShrikeScopeOverlay : MonoBehaviour
    {
        public enum ScopeShape { Space = 0, Charge = 1, Time = 2 }

        const int TextureSize = 256;
        static Texture2D[] _shapeTextures;
        static Sprite[] _shapeSprites;
        static Sprite _markerSprite;

        Image _scope;
        Image _marker;
        RectTransform _markerRect;
        float _scopeAlpha;
        float _markerSpin;

        /// <summary>Make the overlay (one per local Shrike pilot).</summary>
        public static ShrikeScopeOverlay Create(string label)
        {
            var go = new GameObject(label);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            var overlay = go.AddComponent<ShrikeScopeOverlay>();
            overlay.Build();
            return overlay;
        }

        void Build()
        {
            EnsureSprites();
            _scope = MakeImage("Scope", new Vector2(760f, 760f));
            _scope.color = new Color(1f, 1f, 1f, 0f);
            _marker = MakeImage("LockMarker", new Vector2(90f, 90f));
            _marker.sprite = _markerSprite;
            _markerRect = _marker.rectTransform;
            _marker.enabled = false;
        }

        Image MakeImage(string label, Vector2 size)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Per frame from the executor: the scope fades in while zooming, in the mode's shape and colour; the
        /// marker sits on the painted target's screen point (hidden behind the camera or with no lock).</summary>
        public void Drive(bool zooming, ScopeShape shape, Color colour, bool hasLock, Vector3 lockWorld, Camera cam)
        {
            float dt = Time.unscaledDeltaTime;
            _scopeAlpha = Mathf.MoveTowards(_scopeAlpha, zooming ? 1f : 0f, dt * 6f);
            _scope.sprite = _shapeSprites[(int)shape];
            _scope.enabled = _scopeAlpha > 0.001f;
            _scope.color = new Color(colour.r, colour.g, colour.b, 0.85f * _scopeAlpha);

            bool show = hasLock && cam;
            if (show)
            {
                Vector3 screen = cam.WorldToScreenPoint(lockWorld);
                show = screen.z > 0f;
                if (show)
                {
                    var canvasRect = (RectTransform)transform;
                    Vector2 local;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local);
                    _markerRect.anchoredPosition = local;
                    _markerSpin += dt * 90f;
                    _markerRect.localRotation = Quaternion.Euler(0f, 0f, _markerSpin);
                    float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 8f);
                    _markerRect.localScale = Vector3.one * pulse;
                }
            }
            _marker.enabled = show;
            _marker.color = colour;
        }

        public void Hide()
        {
            _scopeAlpha = 0f;
            if (_scope) _scope.enabled = false;
            if (_marker) _marker.enabled = false;
        }

        // ---- the shapes: anti-aliased strokes from distance fields, white (tinted by Image.color) ----

        static void EnsureSprites()
        {
            if (_shapeSprites != null && _markerSprite) return;
            _shapeTextures = new Texture2D[3];
            _shapeSprites = new Sprite[3];
            for (int s = 0; s < 3; s++)
            {
                _shapeTextures[s] = Draw((ScopeShape)s);
                _shapeSprites[s] = Sprite.Create(_shapeTextures[s], new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f));
            }
            var marker = DrawMarker();
            _markerSprite = Sprite.Create(marker, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Coverage of a stroke at distance <paramref name="d"/> from its centre line, half-width <paramref name="w"/>
        /// (texture pixels), one pixel of anti-aliasing.</summary>
        static float Stroke(float d, float w) => Mathf.Clamp01(w + 0.5f - Mathf.Abs(d));

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        static Texture2D Draw(ScopeShape shape)
        {
            var tex = NewTexture();
            var px = new Color32[TextureSize * TextureSize];
            float c = TextureSize * 0.5f;
            for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                {
                    var p = new Vector2(x + 0.5f - c, y + 0.5f - c);
                    float a = shape switch
                    {
                        ScopeShape.Space => SpaceScope(p),
                        ScopeShape.Charge => ChargeScope(p),
                        _ => TimeScope(p),
                    };
                    px[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        // Space: the sniper's ring and a crosshair split at the centre.
        static float SpaceScope(Vector2 p)
        {
            float r = p.magnitude;
            float a = Stroke(r - 118f, 2.2f);
            float cross = Mathf.Min(Segment(p, new Vector2(-104, 0), new Vector2(-14, 0)), Segment(p, new Vector2(14, 0), new Vector2(104, 0)));
            cross = Mathf.Min(cross, Mathf.Min(Segment(p, new Vector2(0, -104), new Vector2(0, -14)), Segment(p, new Vector2(0, 14), new Vector2(0, 104))));
            a = Mathf.Max(a, Stroke(cross, 1.4f));
            return Mathf.Max(a, Stroke(r - 2.5f, 2f));
        }

        // Charge: four lock brackets round a diamond.
        static float ChargeScope(Vector2 p)
        {
            float a = 0f;
            const float k = 100f, arm = 34f;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    var corner = new Vector2(sx * k, sy * k);
                    float d = Mathf.Min(Segment(p, corner, corner - new Vector2(sx * arm, 0)), Segment(p, corner, corner - new Vector2(0, sy * arm)));
                    a = Mathf.Max(a, Stroke(d, 3f));
                }
            float diamond = Mathf.Abs(Mathf.Abs(p.x) + Mathf.Abs(p.y) - 26f) / Mathf.Sqrt(2f);
            return Mathf.Max(a, Stroke(diamond, 2f));
        }

        // Time: the tether running up the screen, chevrons riding it.
        static float TimeScope(Vector2 p)
        {
            float a = Stroke(Segment(p, new Vector2(0, -110), new Vector2(0, 110)), 1.4f);
            for (int i = -1; i <= 2; i++)
            {
                float y = i * 34f;
                float d = Mathf.Min(Segment(p, new Vector2(-30, y - 22), new Vector2(0, y)), Segment(p, new Vector2(30, y - 22), new Vector2(0, y)));
                a = Mathf.Max(a, Stroke(d, 2.6f) * (i == 2 ? 1f : 0.55f + 0.15f * i));
            }
            return Mathf.Max(a, Stroke(p.magnitude - 118f, 1.2f) * 0.5f);
        }

        // The painted-target marker: a diamond outline with four ticks pointing in.
        static Texture2D DrawMarker()
        {
            var tex = NewTexture();
            var px = new Color32[TextureSize * TextureSize];
            float c = TextureSize * 0.5f;
            for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                {
                    var p = new Vector2(x + 0.5f - c, y + 0.5f - c);
                    float diamond = Mathf.Abs(Mathf.Abs(p.x) + Mathf.Abs(p.y) - 96f) / Mathf.Sqrt(2f);
                    float a = Stroke(diamond, 6f);
                    float ticks = Mathf.Min(Mathf.Min(Segment(p, new Vector2(0, 120), new Vector2(0, 70)), Segment(p, new Vector2(0, -120), new Vector2(0, -70))),
                                            Mathf.Min(Segment(p, new Vector2(120, 0), new Vector2(70, 0)), Segment(p, new Vector2(-120, 0), new Vector2(-70, 0))));
                    a = Mathf.Max(a, Stroke(ticks, 5f));
                    px[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static Texture2D NewTexture() => new(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "ShrikeScope",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
    }
}
