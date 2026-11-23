using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using TMPro;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Ark wears its own numbers (Frogma Rev 120 B, "On the Ark"; Shombith, 2026-11-20: "I like B with the HUD above
    /// the ark, thats cool"). A lit, faceted plate hovers over the hull, in the world: the crystals aboard big with the goal
    /// small beside them (on an escort the hull, which is its score; on the way home what is aboard), the hull as
    /// <see cref="Segments"/> lit segments, and one state line (+7 to bank, Sails in 0:05, Under attack, Leaking, Goal
    /// met). Sea blue at rest, red while the Ark is hit or leaking, lime when the goal is met. It turns to face the camera
    /// with a slight tilt, and keeps a readable size near the Ark while shrinking with distance far from it (a thing in the
    /// world, not a screen label). The same numbers for when the Ark is off screen are the chip top left
    /// (<see cref="VoyageObjectiveBox"/>). Every word is <see cref="VoyageLines"/>' Plate* lines; it reads
    /// <see cref="VoyageObjective.Source"/> ten times a second. Escort the Ark and a job voyage each own one while they sail.
    /// </summary>
    public sealed class ArkHudPlate : MonoBehaviour
    {
        const float RefreshSeconds = 0.1f;
        public const int Segments = 16;

        // Plate proportions, in plate units (the plate is 1 wide; the root is scaled to its world width).
        const float HalfWidth = 0.5f, HalfHeight = 0.2f, Bevel = 0.12f, Depth = 0.05f;
        const float SegWidth = 0.026f, SegHeight = 0.045f, SegGap = 0.008f, SegY = -0.035f;
        /// <summary>World width = distance x this: about a sixth of the screen's width wherever the camera is, so the plate
        /// reads at the same size as Rev 120's (the escort camera sits hundreds of units off the Ark). Never under
        /// <see cref="MinWidth"/> right beside the hull.</summary>
        const float WidthPerDistance = 0.34f, MinWidth = 10f;
        /// <summary>The plate leans back this far, so it reads as a plate standing on the Ark, not a sticker.</summary>
        const float TiltDegrees = 14f;
        const float Lift = 6f;
        const float FadeSeconds = 0.3f;

        // Rev 120's three moods, as authored, traced through the worn colour set (world role).
        static readonly Color BodyCalmAuthored = new(0.09f, 0.17f, 0.36f, 1f);
        static readonly Color BodyHotAuthored = new(0.36f, 0.05f, 0.06f, 1f);
        static readonly Color LitCalmAuthored = new(0.624f, 0.91f, 1f, 1f);        // #9FE8FF
        static readonly Color LitHotAuthored = new(1f, 0.294f, 0.227f, 1f);        // #FF4B3A
        static readonly Color LitGoodAuthored = new(0.6f, 1f, 0.502f, 1f);         // #99FF80
        static readonly Color DarkAuthored = new(0.263f, 0.298f, 0.537f, 1f);      // #434C89
        static readonly Color InkAuthored = new(0.902f, 0.969f, 1f, 1f);           // #E6F7FF
        static readonly Color SoftAuthored = new(0.624f, 0.706f, 0.902f, 1f);      // #9FB4E6
        static readonly Color ThreatInkAuthored = new(0.984f, 0.557f, 0.494f, 1f); // #FB8E7E

        enum Mood { Calm, Hot, Good }

        System.Func<Ark> _ark;
        VoyageLines _lines;
        Transform _plate, _stem;
        MeshRenderer _body, _stemRenderer;
        readonly MeshRenderer[] _segments = new MeshRenderer[Segments];
        TextMeshPro _figure, _line;
        Material _bodyMat, _litMat, _darkMat;
        float _nextRefresh, _alpha, _top = 30f, _nextMeasure;
        bool _shown;
        int _lit, _colourVersion = -1;
        Mood _mood = (Mood)(-1);

        /// <summary>Put the plate over <paramref name="ark"/>.</summary>
        public static ArkHudPlate Create(Transform parent, System.Func<Ark> ark)
        {
            var go = new GameObject("ArkHudPlate");
            go.transform.SetParent(parent, false);
            var plate = go.AddComponent<ArkHudPlate>();
            plate._ark = ark;
            return plate;
        }

        void Awake()
        {
            var content = VoyageContentSO.Load();
            _lines = content ? content.Lines : new VoyageLines();
            _bodyMat = ToyModelBuilder.BuildPreviewMaterial(Color.black);
            _litMat = ToyModelBuilder.BuildPreviewMaterial(Color.black);
            _darkMat = ToyModelBuilder.BuildPreviewMaterial(Color.black);

            _plate = new GameObject("Plate").transform;
            _plate.SetParent(transform, false);
            _body = Part("Body", HexSlab(), _plate, _bodyMat);

            var box = Box();
            float row = Segments * SegWidth + (Segments - 1) * SegGap;
            for (int i = 0; i < Segments; i++)
            {
                var seg = Part("Segment" + i, box, _plate, _darkMat);
                seg.transform.localPosition = new Vector3(-row * 0.5f + SegWidth * 0.5f + i * (SegWidth + SegGap), SegY, -Depth * 0.5f - 0.004f);
                seg.transform.localScale = new Vector3(SegWidth, SegHeight, 0.012f);
                _segments[i] = seg;
            }

            _figure = Words("Figure", new Vector2(0f, 0.09f), new Vector2(0.8f, 0.16f), 1.3f);
            _line = Words("Line", new Vector2(0f, -0.115f), new Vector2(0.86f, 0.08f), 0.5f);

            _stemRenderer = Part("Stem", box, transform, _litMat);
            _stem = _stemRenderer.transform;
            SetVisible(false);
        }

        MeshRenderer Part(string name, Mesh mesh, Transform parent, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        TextMeshPro Words(string name, Vector2 at, Vector2 size, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_plate, false);
            var t = go.AddComponent<TextMeshPro>();
            if (TMP_Settings.defaultFontAsset) t.font = TMP_Settings.defaultFontAsset;
            var rt = t.rectTransform;
            rt.sizeDelta = size;
            rt.localPosition = new Vector3(at.x, at.y, -Depth * 0.5f - 0.006f);
            t.fontSize = fontSize;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            return t;
        }

        void OnDestroy()
        {
            if (_bodyMat) Destroy(_bodyMat);
            if (_litMat) Destroy(_litMat);
            if (_darkMat) Destroy(_darkMat);
        }

        // ── Each frame ──────────────────────────────────────────────────────

        void LateUpdate()
        {
            var ark = _ark?.Invoke();
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshSeconds;
                _shown = ark && !ark.IsHullLost && Refresh();
            }
            _alpha = Mathf.MoveTowards(_alpha, _shown ? 1f : 0f, Time.unscaledDeltaTime / FadeSeconds);
            SetVisible(_alpha > 0f && ark);
            if (_alpha <= 0f || !ark) return;
            if (_colourVersion != ColorTrace.Version) Paint(_mood, force: true);
            Place(ark);
        }

        bool Refresh()
        {
            var source = VoyageObjective.Source;
            VoyageObjective o = default;
            if (source == null || VoyageScoreboard.IsOpen || VoyageIntro.IsPlaying || !source.TryGetObjective(out o) ||
                o.Goal == VoyageObjective.Kind.None)
                return false;

            bool raid = o.Goal == VoyageObjective.Kind.CrystalRun;
            bool home = o.Goal == VoyageObjective.Kind.SailingHome;
            bool hot = o.Leaking || o.UnderAttack || o.Assault == ArkAssaultPhase.Active;
            bool good = raid && o.Target > 0 && o.Done >= o.Target && !hot;
            var mood = hot ? Mood.Hot : good ? Mood.Good : Mood.Calm;
            Paint(mood, force: false);

            string hull = Mathf.RoundToInt(o.Hull * 100f).ToString();
            string big, small;
            if (raid)
            {
                big = TemplateText.Fill(_lines.PlateCount, "done", o.Done.ToString());
                small = TemplateText.Fill(_lines.PlateGoal, "goal", o.Target.ToString());
            }
            else if (home)
            {
                big = TemplateText.Fill(_lines.PlateCount, "done", Mathf.Max(o.AtStake, o.Done).ToString());
                small = _lines.PlateAboard;
            }
            else
            {
                big = TemplateText.Fill(_lines.PlateHull, "hull", hull);
                small = _lines.PlateHullTag;
            }
            var bigInk = mood == Mood.Good ? LitGood : Ink;
            SetText(_figure, string.IsNullOrEmpty(small)
                ? $"<color=#{Hex(bigInk)}>{big}</color>"
                : $"<color=#{Hex(bigInk)}>{big}</color><size=55%><color=#{Hex(Soft)}>  {small}</color></size>");

            var (line, threat) = StateLine(o, raid, home);
            _line.color = threat ? ThreatInk : mood == Mood.Good ? LitGood : Soft;
            SetText(_line, line);

            int lit = Mathf.Clamp(Mathf.RoundToInt(o.Hull * Segments), 0, Segments);
            if (lit != _lit)
            {
                _lit = lit;
                for (int i = 0; i < Segments; i++) _segments[i].sharedMaterial = i < lit ? _litMat : _darkMat;
            }
            return true;
        }

        /// <summary>The plate's one state line, and whether it is a threat: an assault first, then the leak, then the Ark
        /// losing plates, then the raid window closing, then what the pilot carries, then a met goal, then the way home,
        /// then where the Ark is.</summary>
        (string line, bool threat) StateLine(in VoyageObjective o, bool raid, bool home)
        {
            if (o.Assault == ArkAssaultPhase.Active)
                return (TemplateText.Fill(_lines.PlateAssault, "kills", o.AssaultKills.ToString(),
                    "needed", o.AssaultKillsNeeded.ToString()), true);
            if (o.Assault == ArkAssaultPhase.Incoming)
                return (TemplateText.Fill(_lines.PlateIncoming, "seconds",
                    Mathf.CeilToInt(Mathf.Max(0f, o.AssaultSeconds)).ToString()), true);
            if (o.Leaking) return (_lines.PlateLeaking, true);
            if (o.UnderAttack) return (_lines.PlateUnderAttack, true);
            if (raid && o.Anchored && o.AnchorClosing)
                return (TemplateText.Fill(_lines.PlateSails, "time", VoyageHudColors.Clock(o.AnchorSeconds)), true);
            if (raid && o.Carried > 0) return (TemplateText.Fill(_lines.PlateCarried, "count", o.Carried.ToString()), false);
            if (raid && o.Target > 0 && o.Done >= o.Target) return (_lines.PlateGoalMet, false);
            if (home) return (_lines.PlateHome, false);
            if (o.Cells > 0 && o.Cell > 0)
                return (TemplateText.Fill(_lines.PlatePlace, "place", VoyageObjectiveText.StopName(_lines, o, o.Cell - 1),
                    "cell", o.Cell.ToString(), "cells", o.Cells.ToString()), false);
            return (string.Empty, false);
        }

        /// <summary>Over the hull, facing the camera with a slight lean back, sized to the camera's distance.</summary>
        void Place(Ark ark)
        {
            if (Time.unscaledTime >= _nextMeasure)
            {
                _nextMeasure = Time.unscaledTime + 1f;
                _top = HullTop(ark);
            }
            var cam = Camera.main;
            if (!cam) return;
            var arkAt = ark.Position;
            var camAt = cam.transform.position;
            float distance = Vector3.Distance(camAt, arkAt);
            float width = Mathf.Max(distance * WidthPerDistance, MinWidth);
            float plateHalf = HalfHeight * width;
            var up = cam.transform.up;
            var foot = arkAt + up * (_top + Lift + width * 0.08f);
            var centre = foot + up * plateHalf;
            var look = centre - camAt;
            if (look.sqrMagnitude < 1e-4f) return;
            var facing = Quaternion.LookRotation(look, up) * Quaternion.Euler(-TiltDegrees, 0f, 0f);
            _plate.SetPositionAndRotation(centre, facing);
            _plate.localScale = Vector3.one * (width * Mathf.Lerp(0.85f, 1f, _alpha));
            // The stem: a thin lit post from the hull's top to the plate's foot.
            var stemFrom = arkAt + up * _top;
            var stemTo = foot;
            _stem.SetPositionAndRotation((stemFrom + stemTo) * 0.5f, Quaternion.LookRotation(look, up));
            _stem.localScale = new Vector3(width * 0.006f, Mathf.Max(0.01f, Vector3.Distance(stemFrom, stemTo)), width * 0.006f);
        }

        /// <summary>How far above the Ark's centre its hull reaches (its renderers' bounds), measured once a second.</summary>
        float HullTop(Ark ark)
        {
            var renderers = ark.GetComponentsInChildren<Renderer>(false);
            bool any = false;
            var b = new Bounds(ark.Position, Vector3.zero);
            foreach (var r in renderers)
            {
                if (!r || r.transform.IsChildOf(transform)) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any ? Mathf.Max(8f, b.extents.magnitude * 0.55f) : 30f;
        }

        void Paint(Mood mood, bool force)
        {
            if (mood == _mood && !force) return;
            _mood = mood;
            _colourVersion = ColorTrace.Version;
            var body = mood == Mood.Hot ? BodyHot : BodyCalm;
            var lit = mood switch { Mood.Hot => LitHot, Mood.Good => LitGood, _ => LitCalm };
            Tint(_bodyMat, body, 0.35f);
            Tint(_litMat, lit, 0.9f);
            Tint(_darkMat, Dark, 0.2f);
        }

        static void Tint(Material m, Color c, float glow)
        {
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * glow);
            // Matte: a flat face turned to the camera catches the key light as one pale sheen at the default smoothness.
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        }

        void SetVisible(bool on)
        {
            if (_plate.gameObject.activeSelf != on) _plate.gameObject.SetActive(on);
            if (_stem && _stem.gameObject.activeSelf != on) _stem.gameObject.SetActive(on);
        }

        static void SetText(TMP_Text t, string value)
        {
            value ??= string.Empty;
            if (t.text != value) t.text = value;
        }

        // Every colour the plate paints, read through the worn colour set (LOCKED rule).
        static Color BodyCalm => ColorTrace.Retrace(ColorTrace.World, BodyCalmAuthored);
        static Color BodyHot => ColorTrace.Retrace(ColorTrace.World, BodyHotAuthored);
        static Color LitCalm => ColorTrace.Retrace(ColorTrace.World, LitCalmAuthored);
        static Color LitHot => ColorTrace.Retrace(ColorTrace.World, LitHotAuthored);
        static Color LitGood => ColorTrace.Retrace(ColorTrace.World, LitGoodAuthored);
        static Color Dark => ColorTrace.Retrace(ColorTrace.World, DarkAuthored);
        static Color Ink => ColorTrace.Retrace(ColorTrace.World, InkAuthored);
        static Color Soft => ColorTrace.Retrace(ColorTrace.World, SoftAuthored);
        static Color ThreatInk => ColorTrace.Retrace(ColorTrace.World, ThreatInkAuthored);
        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        // ── Meshes ──────────────────────────────────────────────────────────

        /// <summary>The plate: a long hexagon (pointed ends), extruded, every face its own normal so it lights faceted.</summary>
        static Mesh HexSlab()
        {
            var outline = new[]
            {
                new Vector2(-HalfWidth, 0f), new Vector2(-HalfWidth + Bevel, HalfHeight), new Vector2(HalfWidth - Bevel, HalfHeight),
                new Vector2(HalfWidth, 0f), new Vector2(HalfWidth - Bevel, -HalfHeight), new Vector2(-HalfWidth + Bevel, -HalfHeight),
            };
            int n = outline.Length;
            var verts = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            float f = -Depth * 0.5f, b = Depth * 0.5f;
            // Front (toward the camera, -z) and back caps, fanned from the centre.
            Cap(verts, normals, tris, outline, f, Vector3.back, front: true);
            Cap(verts, normals, tris, outline, b, Vector3.forward, front: false);
            for (int i = 0; i < n; i++)
            {
                var p = outline[i];
                var q = outline[(i + 1) % n];
                var edge = (q - p).normalized;
                var normal = new Vector3(-edge.y, edge.x, 0f); // the outline runs clockwise: outward is left of it
                int s = verts.Count;
                verts.Add(new Vector3(p.x, p.y, f)); verts.Add(new Vector3(q.x, q.y, f));
                verts.Add(new Vector3(q.x, q.y, b)); verts.Add(new Vector3(p.x, p.y, b));
                for (int k = 0; k < 4; k++) normals.Add(normal);
                tris.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
            }
            var mesh = new Mesh { name = "ArkHudPlate" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Cap(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<Vector3> normals,
            System.Collections.Generic.List<int> tris, Vector2[] outline, float z, Vector3 normal, bool front)
        {
            int c = verts.Count;
            verts.Add(new Vector3(0f, 0f, z));
            normals.Add(normal);
            foreach (var p in outline)
            {
                verts.Add(new Vector3(p.x, p.y, z));
                normals.Add(normal);
            }
            int n = outline.Length;
            for (int i = 0; i < n; i++)
            {
                int a = c + 1 + i, b = c + 1 + (i + 1) % n;
                if (front) tris.AddRange(new[] { c, a, b });
                else tris.AddRange(new[] { c, b, a });
            }
        }

        /// <summary>A unit cube with a normal per face (segments and the stem).</summary>
        static Mesh Box()
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            var dirs = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var d in dirs)
            {
                var u = Mathf.Abs(d.y) > 0.5f ? Vector3.forward : Vector3.up;
                var v = Vector3.Cross(d, u);
                int s = verts.Count;
                var c = d * 0.5f;
                verts.Add(c - u * 0.5f - v * 0.5f); verts.Add(c + u * 0.5f - v * 0.5f);
                verts.Add(c + u * 0.5f + v * 0.5f); verts.Add(c - u * 0.5f + v * 0.5f);
                for (int k = 0; k < 4; k++) normals.Add(d);
                tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }
            var mesh = new Mesh { name = "ArkHudBox" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
