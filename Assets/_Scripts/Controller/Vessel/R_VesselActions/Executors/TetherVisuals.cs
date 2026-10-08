using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What the Tether draws: light-sword beams (a bright core inside a soft team-coloured halo,
    /// both widening and brightening with tension), sparks that run along a beam when it slices,
    /// a bracket flash where a beam clamps onto its anchor, a flickering retract when a beam lets
    /// go, and — for the local pilot only — ghost target markers, the swept-arc swing indicator
    /// and the dashed release line.
    ///
    /// Immediate-mode on purpose: the executor calls <see cref="Beam"/> etc. every frame for what
    /// exists THIS frame, and anything that stops being drawn starts retracting by itself. So a
    /// line that is released, snapped, or dropped by a reset can never strand a beam on screen.
    ///
    /// Runtime <c>LineRenderer</c>s on two shared materials (soft beam, dash) built once per
    /// session — the <c>SniperBeam</c> approach: <c>Sprites/Default</c> honours per-vertex colour,
    /// and a missing shader (headless server) turns the whole thing into a silent no-op.
    /// Colour channels stay ≤ 1: the gameplay bloom clamps at 0.5, so glow comes from width, not
    /// from HDR intensity (<c>Docs/PALETTE.md</c>).
    /// </summary>
    public sealed class TetherVisuals
    {
        const int RingSegments = 16;
        const int ArcSegments = 40;
        const int SparkPool = 24;
        const int BracketPool = 8;

        static Material s_beamMaterial;
        static Material s_dashMaterial;

        readonly Transform _root;
        readonly TetherConfigSO _config;

        sealed class BeamState
        {
            public int Key;
            public LineRenderer Core, Halo;
            public bool DrawnThisFrame;
            public bool Retracting;
            public float RetractAge;
            public Vector3 Hull, Anchor;
            public float Flash;          // seconds left of a hook flash
            public float FlashStrength;
            public bool Hero;
            public Color Team;
            public float Tension;
        }

        struct SparkState
        {
            public bool Live;
            public int BeamKey;
            public float T;      // 0 = anchor, 1 = hull
            public float Dir;    // ±1 along the beam
            public float Age;
            public LineRenderer Line;
        }

        struct BracketState
        {
            public bool Live;
            public Vector3 Position, Normal;
            public float Age, Size;
            public Color Colour;
            public LineRenderer Ring;
        }

        readonly List<BeamState> _beams = new();
        readonly Stack<BeamState> _freeBeams = new();
        readonly SparkState[] _sparks = new SparkState[SparkPool];
        readonly BracketState[] _brackets = new BracketState[BracketPool];
        readonly LineRenderer[] _ghosts = new LineRenderer[2];
        LineRenderer _arc, _releaseLine;
        readonly Vector3[] _ring = new Vector3[RingSegments];   // a LOOP line closes itself
        readonly Vector3[] _arcPoints = new Vector3[ArcSegments + 1];
        bool _ghostDrawn0, _ghostDrawn1, _arcDrawn, _releaseDrawn;
        int _nextSpark, _nextBracket;

        public TetherVisuals(Transform root, TetherConfigSO config)
        {
            _root = root;
            _config = config;
        }

        bool Ready => _config && BeamMaterial() != null;

        // ------------------------------------------------------------------ frame

        public void BeginFrame()
        {
            for (int i = 0; i < _beams.Count; i++) _beams[i].DrawnThisFrame = false;
            _ghostDrawn0 = _ghostDrawn1 = _arcDrawn = _releaseDrawn = false;
        }

        /// <summary>Draw one live beam this frame. <paramref name="key"/> is stable for the
        /// line's life (an auto-tether's id, or <see cref="LongKey"/>).</summary>
        public void Beam(int key, Vector3 hull, Vector3 anchor, float tension01, bool hero, Color team)
        {
            if (!Ready) return;
            var b = Find(key) ?? Rent(key);
            b.DrawnThisFrame = true;
            b.Retracting = false;
            b.Hull = hull;
            b.Anchor = anchor;
            b.Hero = hero;
            b.Team = team;
            b.Tension = Mathf.Clamp01(tension01);
        }

        /// <summary>The long tether's beam key (auto-tether ids are positive).</summary>
        public const int LongKey = -1;

        /// <summary>A taut snap's flash along the beam, scaled 0..1.</summary>
        public void Flash(int key, float strength01)
        {
            var b = Find(key);
            if (b == null || !_config) return;
            b.Flash = _config.HookFlashSeconds;
            b.FlashStrength = Mathf.Clamp01(strength01);
        }

        /// <summary>The anchor end clamping on: a small ring that flashes and closes.</summary>
        public void Bracket(Vector3 anchor, Vector3 towardHull, Color team, float sizeScale)
        {
            if (!Ready) return;
            ref var br = ref _brackets[_nextBracket];
            _nextBracket = (_nextBracket + 1) % BracketPool;
            br.Live = true;
            br.Position = anchor;
            br.Normal = towardHull.sqrMagnitude > 1e-6f ? towardHull.normalized : Vector3.up;
            br.Age = 0f;
            br.Size = _config.BracketRadius * Mathf.Max(0.1f, sizeScale);
            br.Colour = Color.Lerp(team, Color.white, 0.5f);
            if (!br.Ring) br.Ring = NewLine("TetherBracket", loop: true, RingSegments, BeamMaterial());
        }

        /// <summary>A slice: two short sparks run away from the cut along the beam.</summary>
        public void Spark(int key, float t01)
        {
            if (!Ready || Find(key) == null) return;
            SpawnSpark(key, t01, +1f);
            SpawnSpark(key, t01, -1f);
        }

        public void Ghost(int sideIndex, Vector3 position, Vector3 viewer, Color colour)
        {
            if (!Ready) return;
            var ring = _ghosts[sideIndex] ? _ghosts[sideIndex]
                : (_ghosts[sideIndex] = NewLine("TetherGhost", loop: true, RingSegments, BeamMaterial()));
            Vector3 normal = position - viewer;
            FillRing(position, normal, _config.GhostRadius);
            ring.positionCount = RingSegments;
            ring.SetPositions(_ring);
            ring.widthMultiplier = 0.25f;
            float pulse = 0.55f + 0.25f * Mathf.Sin(Time.time * 6f);
            Color c = colour; c.a = pulse;
            ring.startColor = ring.endColor = c;
            ring.enabled = true;
            if (sideIndex == 0) _ghostDrawn0 = true; else _ghostDrawn1 = true;
        }

        /// <summary>
        /// The swept-arc indicator: the swing's own path round the anchor, from the hook to now,
        /// drawn just inside the rope's circle. Fills to <c>HalfTurnColor</c> at a half turn.
        /// </summary>
        public void SwingArc(Vector3 anchor, Vector3 startRadial, Vector3 startTangent, float radius, float sweptRadians, Color team)
        {
            if (!Ready) return;
            if (!_arc) _arc = NewLine("TetherSwingArc", loop: false, ArcSegments + 1, BeamMaterial());
            float sweep = Mathf.Min(sweptRadians, Mathf.PI * 2f);
            int count = Mathf.Clamp(Mathf.CeilToInt(ArcSegments * sweep / (Mathf.PI * 2f)) + 1, 2, ArcSegments + 1);
            float r = radius * 0.97f;
            for (int i = 0; i < count; i++)
            {
                float a = sweep * i / (count - 1);
                _arcPoints[i] = anchor + (startRadial * Mathf.Cos(a) + startTangent * Mathf.Sin(a)) * r;
            }
            _arc.positionCount = count;
            for (int i = 0; i < count; i++) _arc.SetPosition(i, _arcPoints[i]);   // only the points this sweep uses
            bool half = sweptRadians >= TetherMath.HalfTurnRadians;
            float progress = TetherMath.HalfTurnProgress01(sweptRadians);
            Color c = half ? _config.HalfTurnColor : Color.Lerp(team, _config.HalfTurnColor, progress * 0.5f);
            c.a = half ? 0.9f : 0.35f + 0.35f * progress;
            _arc.startColor = _arc.endColor = c;
            _arc.widthMultiplier = half ? 0.6f : 0.35f;
            _arc.enabled = true;
            _arcDrawn = true;
        }

        /// <summary>The dashed line along the exit velocity: where a release throws you.</summary>
        public void ReleaseLine(Vector3 from, Vector3 direction, float length, bool boosted)
        {
            if (!Ready || DashMaterial() == null) return;
            if (!_releaseLine)
            {
                _releaseLine = NewLine("TetherReleaseLine", loop: false, 2, DashMaterial());
                _releaseLine.textureMode = LineTextureMode.Tile;
            }
            _releaseLine.SetPosition(0, from);
            _releaseLine.SetPosition(1, from + direction.normalized * length);
            Color c = boosted ? _config.HalfTurnColor : new Color(1f, 1f, 1f, 1f);
            c.a = boosted ? 0.85f : 0.45f;
            _releaseLine.startColor = c;
            c.a = 0f;
            _releaseLine.endColor = c;
            _releaseLine.widthMultiplier = 0.35f;
            _releaseLine.enabled = true;
            _releaseDrawn = true;
        }

        /// <summary>
        /// Finish the frame: animate what was drawn, start retracting what was not, advance sparks
        /// and brackets, hide everything idle. <paramref name="hullNow"/> is where a retracting
        /// beam's anchor end flies back to.
        /// </summary>
        public void EndFrame(float dt, Vector3 hullNow)
        {
            if (!_config) return;
            for (int i = _beams.Count - 1; i >= 0; i--)
            {
                var b = _beams[i];
                if (!b.DrawnThisFrame && !b.Retracting)
                {
                    b.Retracting = true;
                    b.RetractAge = 0f;
                }
                if (b.Retracting)
                {
                    b.RetractAge += dt;
                    b.Hull = hullNow;
                    if (b.RetractAge >= _config.RetractSeconds)
                    {
                        Return(b, i);
                        continue;
                    }
                }
                if (b.Flash > 0f) b.Flash -= dt;
                Render(b);
            }

            UpdateSparks(dt);
            UpdateBrackets(dt);

            if (_ghosts[0]) _ghosts[0].enabled = _ghostDrawn0;
            if (_ghosts[1]) _ghosts[1].enabled = _ghostDrawn1;
            if (_arc) _arc.enabled = _arcDrawn;
            if (_releaseLine) _releaseLine.enabled = _releaseDrawn;
        }

        /// <summary>Hide everything at once (vessel disabled / despawned).</summary>
        public void HideAll()
        {
            for (int i = _beams.Count - 1; i >= 0; i--) Return(_beams[i], i);
            for (int i = 0; i < SparkPool; i++) { _sparks[i].Live = false; if (_sparks[i].Line) _sparks[i].Line.enabled = false; }
            for (int i = 0; i < BracketPool; i++) { _brackets[i].Live = false; if (_brackets[i].Ring) _brackets[i].Ring.enabled = false; }
            if (_ghosts[0]) _ghosts[0].enabled = false;
            if (_ghosts[1]) _ghosts[1].enabled = false;
            if (_arc) _arc.enabled = false;
            if (_releaseLine) _releaseLine.enabled = false;
        }

        // ------------------------------------------------------------------ beams

        void Render(BeamState b)
        {
            float tension = b.Tension;
            float flash = b.Flash > 0f ? b.FlashStrength * (b.Flash / Mathf.Max(0.01f, _config.HookFlashSeconds)) : 0f;
            float widthGain = 1f + _config.TensionWidthGain * tension + 0.8f * flash;

            float coreWidth = (b.Hero ? _config.LongCoreWidth : _config.AutoCoreWidth) * widthGain;
            float haloWidth = (b.Hero ? _config.LongHaloWidth : _config.AutoHaloWidth) * widthGain;
            float alpha = b.Hero ? _config.LongAlpha : _config.AutoAlpha;

            Vector3 anchor = b.Anchor;
            if (b.Retracting)
            {
                // The anchor end flies back to the hull, flickering as it goes.
                float k = Mathf.Clamp01(b.RetractAge / Mathf.Max(0.01f, _config.RetractSeconds));
                anchor = Vector3.Lerp(b.Anchor, b.Hull, k * k);
                float flicker = 0.35f + 0.65f * Mathf.PerlinNoise(Time.time * 40f, b.Key * 0.37f);
                alpha *= (1f - k) * flicker;
                coreWidth *= 1f - 0.5f * k;
                haloWidth *= 1f - 0.5f * k;
            }

            Vector2 haloRange = _config.HaloAlphaRestToTaut;
            Color core = Color.Lerp(b.Team, Color.white, Mathf.Clamp01(_config.CoreWhiteness + 0.3f * flash));
            core.a = Mathf.Clamp01(alpha * (0.75f + 0.25f * tension + flash));
            Color halo = b.Team;
            halo.a = Mathf.Clamp01(alpha * (Mathf.Lerp(haloRange.x, haloRange.y, tension) + 0.4f * flash));

            Place(b.Core, b.Hull, anchor, coreWidth, core);
            Place(b.Halo, b.Hull, anchor, haloWidth, halo);
        }

        static void Place(LineRenderer line, Vector3 from, Vector3 to, float width, Color colour)
        {
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.widthMultiplier = width;
            line.startColor = line.endColor = colour;
            line.enabled = true;
        }

        BeamState Find(int key)
        {
            for (int i = 0; i < _beams.Count; i++)
                if (_beams[i].Key == key && !_beams[i].Retracting) return _beams[i];
            return null;
        }

        BeamState Rent(int key)
        {
            var b = _freeBeams.Count > 0 ? _freeBeams.Pop() : new BeamState
            {
                Halo = NewLine("TetherBeamHalo", loop: false, 2, BeamMaterial()),
                Core = NewLine("TetherBeamCore", loop: false, 2, BeamMaterial()),
            };
            b.Key = key;
            b.Flash = 0f;
            b.Retracting = false;
            b.RetractAge = 0f;
            // Halo first, core over it.
            b.Halo.sortingOrder = 0;
            b.Core.sortingOrder = 1;
            _beams.Add(b);
            return b;
        }

        void Return(BeamState b, int index)
        {
            b.Core.enabled = false;
            b.Halo.enabled = false;
            _beams.RemoveAt(index);
            _freeBeams.Push(b);
        }

        // ------------------------------------------------------------------ sparks + brackets

        void SpawnSpark(int key, float t01, float dir)
        {
            ref var s = ref _sparks[_nextSpark];
            _nextSpark = (_nextSpark + 1) % SparkPool;
            s.Live = true;
            s.BeamKey = key;
            s.T = Mathf.Clamp01(t01);
            s.Dir = dir;
            s.Age = 0f;
            if (!s.Line) s.Line = NewLine("TetherSpark", loop: false, 2, BeamMaterial());
        }

        void UpdateSparks(float dt)
        {
            for (int i = 0; i < SparkPool; i++)
            {
                ref var s = ref _sparks[i];
                if (!s.Live) continue;
                s.Age += dt;
                var beam = FindAny(s.BeamKey);
                if (beam == null || s.Age >= _config.SparkLifetime)
                {
                    s.Live = false;
                    if (s.Line) s.Line.enabled = false;
                    continue;
                }

                s.T = Mathf.Clamp01(s.T + s.Dir * _config.SparkSpeed * dt);
                Vector3 along = beam.Hull - beam.Anchor;
                float len = Mathf.Max(0.01f, along.magnitude);
                Vector3 head = beam.Anchor + along * s.T;
                Vector3 tail = head - along * (s.Dir * Mathf.Min(1f, _config.SparkLength / len));

                float life = 1f - s.Age / _config.SparkLifetime;
                Color c = Color.Lerp(beam.Team, Color.white, 0.8f);
                c.a = life;
                Place(s.Line, tail, head, (beam.Hero ? 0.5f : 0.3f) * (0.5f + life), c);
            }
        }

        BeamState FindAny(int key)
        {
            for (int i = 0; i < _beams.Count; i++)
                if (_beams[i].Key == key) return _beams[i];
            return null;
        }

        void UpdateBrackets(float dt)
        {
            for (int i = 0; i < BracketPool; i++)
            {
                ref var br = ref _brackets[i];
                if (!br.Live) continue;
                br.Age += dt;
                float k = br.Age / Mathf.Max(0.01f, _config.BracketFlashSeconds);
                if (k >= 1f)
                {
                    br.Live = false;
                    if (br.Ring) br.Ring.enabled = false;
                    continue;
                }
                // Snaps in from wide to tight while it fades: a clamp closing on the prism.
                FillRing(br.Position, br.Normal, br.Size * Mathf.Lerp(1.6f, 0.8f, k));
                br.Ring.positionCount = RingSegments;
                br.Ring.SetPositions(_ring);
                br.Ring.widthMultiplier = Mathf.Lerp(0.6f, 0.15f, k);
                Color c = br.Colour; c.a = 1f - k;
                br.Ring.startColor = br.Ring.endColor = c;
                br.Ring.enabled = true;
            }
        }

        void FillRing(Vector3 centre, Vector3 normal, float radius)
        {
            Vector3 n = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            Vector3 a = Vector3.Cross(n, Mathf.Abs(n.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
            Vector3 c = Vector3.Cross(n, a);
            for (int i = 0; i < RingSegments; i++)
            {
                float t = i * Mathf.PI * 2f / RingSegments;
                _ring[i] = centre + (a * Mathf.Cos(t) + c * Mathf.Sin(t)) * radius;
            }
        }

        // ------------------------------------------------------------------ construction

        LineRenderer NewLine(string name, bool loop, int points, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = points;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = material;
            line.enabled = false;
            return line;
        }

        /// <summary>One soft-edged beam material for every Tether in the session: alpha falls off
        /// across the line's width, so a wide halo reads as glow rather than a flat ribbon.</summary>
        static Material BeamMaterial()
        {
            if (s_beamMaterial) return s_beamMaterial;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;   // no graphics device: a silent no-op, as SniperBeam
            s_beamMaterial = new Material(shader) { name = "TetherBeam (runtime)", renderQueue = 3000 };
            s_beamMaterial.mainTexture = SoftTexture();
            return s_beamMaterial;
        }

        static Material DashMaterial()
        {
            if (s_dashMaterial) return s_dashMaterial;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            s_dashMaterial = new Material(shader) { name = "TetherDash (runtime)", renderQueue = 3000 };
            s_dashMaterial.mainTexture = DashTexture();
            return s_dashMaterial;
        }

        static Texture2D SoftTexture()
        {
            const int h = 32;
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false)
            {
                name = "TetherSoft (runtime)", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            for (int y = 0; y < h; y++)
            {
                float v = Mathf.Abs((y + 0.5f) / h * 2f - 1f);    // 0 at the centre line, 1 at the edge
                float a = Mathf.Pow(1f - v, 2.2f);
                var c = new Color(1f, 1f, 1f, a);
                tex.SetPixel(0, y, c);
                tex.SetPixel(1, y, c);
            }
            tex.Apply(false, true);
            return tex;
        }

        static Texture2D DashTexture()
        {
            const int w = 8;
            var tex = new Texture2D(w, 2, TextureFormat.RGBA32, false)
            {
                name = "TetherDash (runtime)", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point,
            };
            for (int x = 0; x < w; x++)
            {
                var c = new Color(1f, 1f, 1f, x < w / 2 ? 1f : 0f);
                tex.SetPixel(x, 0, c);
                tex.SetPixel(x, 1, c);
            }
            tex.Apply(false, true);
            return tex;
        }
    }
}
