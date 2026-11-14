using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Paints every colour the world draws from the worn colour set (Garrett, 2026-11-10: "no pixel anywhere should
    /// ever receive a color that can be traced somewhere but a colorset ... i notice many other colors that are not
    /// respected in the skybox etc."). The UI's twin is <see cref="CosmicShore.UI.UIThemeLive"/>.
    ///
    /// <para>On every <see cref="ColorTrace.Changed"/> (a set worn, previewed or applied) and every scene load, each
    /// colour below that the colour trace knows becomes the worn set's value for it (<see cref="ColorTrace.TryRetrace"/>,
    /// role world; world text and UI-shader materials, role ui), alpha kept:</para>
    /// <list type="bullet">
    /// <item>every Color property of every loaded material (the HyperSea sky, flora, fauna, effects), except the
    /// colours ThemeManager paints on its domain material sets from the domain blocks (<see cref="Own"/>);</item>
    /// <item>particle systems (start colour, colour over lifetime and by speed, trails, colour custom data), trails and
    /// lines, lights, solid-colour game camera clears, sprites, world-space TextMesh Pro, fog and ambient light, and
    /// post-processing colour parameters. When the sky's material changes, the ambient light and reflections it gives
    /// are worked out again (<see cref="DynamicGI.UpdateEnvironment"/>).</item>
    /// </list>
    /// Prefab assets and materials are painted where they live in memory, so everything instantiated afterwards is
    /// born in the worn set and nothing is repainted per frame or per spawn. A material loaded later (a
    /// <c>Resources.Load</c> on first use) is painted by its loader (<see cref="Paint(Material)"/>; the gate,
    /// <c>colour_trace.py --check</c>, names any loader that does not). In a player nothing is saved; in the editor
    /// every asset colour a repaint gave is put back when play ends, or before the editor saves assets during play,
    /// unless a person changed it since (<c>WorldColorTraceEditor</c>), so a .mat or prefab in git never takes a set's
    /// colours. Nothing is painted once play is stopping.
    /// </summary>
    public static class WorldColorTrace
    {
        /// <summary>Raised after a repaint, with the number of colours changed.</summary>
        public static event Action<int> Repainted;

        /// <summary>What the editor tells the painter (null in a player, where nothing is recorded or skipped).</summary>
        public interface IEditorHooks
        {
            /// <summary>True for an object that lives in an asset file: what a repaint changes in it is recorded and put
            /// back. Asked before the first change to it.</summary>
            bool IsAsset(Object o);

            /// <summary>True for an object the editor itself shows (Prefab Mode's stage, an inspector preview): never
            /// painted, since saving the stage would write the set's colours into the prefab.</summary>
            bool IsPreview(Object o);

            /// <summary>After each change a repaint made to an asset.</summary>
            void Changed(Object asset);
        }

        /// <summary>Set by the editor (<c>WorldColorTraceEditor</c>).</summary>
        public static IEditorHooks Editor;

        static readonly List<Slot> Slots = new();
        static readonly Dictionary<long, Slot> SlotOf = new();
        static readonly HashSet<long> Owned = new();
        static readonly List<Material> Painted = new(64);
        static readonly Dictionary<Shader, (int[] ids, string role)> Shaders = new();
        static readonly List<int> Scratch = new(32);
        static readonly ParticleSystemCustomData[] CustomStreams = { ParticleSystemCustomData.Custom1, ParticleSystemCustomData.Custom2 };
        static bool _installed, _stopped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Repainted = null;
            Slots.Clear();
            SlotOf.Clear();
            Owned.Clear();
            Painted.Clear();
            Shaders.Clear();
            _installed = false;
            _stopped = false;
        }

        // After SubsystemRegistration (which clears ColorTrace.Changed) and before the first scene's Awake. The scene and
        // quit hooks are removed first: with domain reload off, Unity keeps the last play session's handlers.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (_installed) return;
            _installed = true;
            ColorTrace.Changed -= Repaint;
            ColorTrace.Changed += Repaint;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.quitting -= Stop;
            Application.quitting += Stop;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Repaint();

        /// <summary>
        /// Play is ending (Application.quitting; the editor calls it when play stops, before it puts the assets back):
        /// nothing is painted from now on, so a set worn while the scenes are torn down (ThemeManager going) never
        /// writes into an asset again.
        /// </summary>
        public static void Stop() => _stopped = true;

        /// <summary>True once play is ending (<see cref="Stop"/>).</summary>
        public static bool IsStopped => _stopped;

        /// <summary>The materials the last repaint changed (ThemeManager re-stamps the prism entities drawn with them).</summary>
        public static IReadOnlyList<Material> LastPainted => Painted;

        /// <summary>True while an asset holds a colour a repaint gave it (the editor puts it back).</summary>
        public static bool HasAssetChanges => Slots.Count > 0;

        /// <summary>Paints everything loaded from the worn set. Play mode only.</summary>
        public static void Repaint()
        {
            Painted.Clear();
            if (!CanPaint) return;
            int changed = 0;
            foreach (var m in Resources.FindObjectsOfTypeAll<Material>()) changed += PaintMaterial(m);
            foreach (var ps in Resources.FindObjectsOfTypeAll<ParticleSystem>()) changed += PaintParticles(ps);
            foreach (var t in Resources.FindObjectsOfTypeAll<TrailRenderer>()) changed += PaintTrail(t);
            foreach (var l in Resources.FindObjectsOfTypeAll<LineRenderer>()) changed += PaintLine(l);
            foreach (var l in Resources.FindObjectsOfTypeAll<Light>()) changed += PaintLight(l);
            foreach (var c in Resources.FindObjectsOfTypeAll<Camera>()) changed += PaintCamera(c);
            foreach (var s in Resources.FindObjectsOfTypeAll<SpriteRenderer>()) changed += PaintSprite(s);
            foreach (var t in Resources.FindObjectsOfTypeAll<TextMeshPro>()) changed += PaintWorldText(t);
            foreach (var v in Resources.FindObjectsOfTypeAll<VolumeProfile>()) changed += PaintProfile(v);
            changed += PaintRenderSettings();
            var sky = RenderSettings.skybox;
            if (sky && Painted.Contains(sky)) DynamicGI.UpdateEnvironment();
            Repainted?.Invoke(changed);
        }

        /// <summary>
        /// Paints one material from the worn set: for a material loaded after the last repaint (a lazy
        /// <c>Resources.Load</c>), so it is born in the set like everything already in memory. Returns the colours changed.
        /// </summary>
        public static int Paint(Material material) => CanPaint ? PaintMaterial(material) : 0;

        static bool CanPaint => !_stopped && Application.isPlaying && ColorTrace.Worn;

        /// <summary>
        /// A material colour another painter owns: ThemeManager paints the domain material sets' colours from the worn
        /// set's domain blocks, and the trace must never read one of those as a traced colour (a domain colour can equal
        /// some set's value for a traced id). Left alone by every repaint.
        /// </summary>
        public static void Own(Material material, int propertyId)
        {
            if (material) Owned.Add(OwnedKey(material, propertyId));
        }

        static long OwnedKey(Material m, int propertyId) => ((long)m.GetInstanceID() << 32) ^ (uint)propertyId;

        /// <summary>
        /// For another painter that changes an asset's colour in memory (UIThemeLive paints UI prefab assets, so rows
        /// instantiated from them are born in the worn set): asked before the change, true when the editor must put it
        /// back. Always false in a player.
        /// </summary>
        public static bool RecordsAsset(Object owner) => Records(owner);

        /// <summary>
        /// Records a change another painter made to an asset (<see cref="RecordsAsset"/>): put back when play ends, or
        /// before the editor saves, if the asset still holds <paramref name="painted"/>. <paramref name="slot"/> tells
        /// one colour of the owner from another.
        /// </summary>
        public static void RecordAssetChange<T>(Object owner, long slot, T was, T painted, Func<T> get, Action<T> set,
                                                Func<T, T, bool> same) =>
            Record(owner, slot, was, painted, get, set, same);

        /// <summary>The assets a repaint changed since play began (or since they were last put back).</summary>
        public static IEnumerable<Object> ChangedAssets
        {
            get
            {
                var seen = new HashSet<Object>();
                foreach (var s in Slots)
                    if (s.Owner && seen.Add(s.Owner)) yield return s.Owner;
            }
        }

        /// <summary>
        /// Puts back every asset colour a repaint gave, if it still holds that colour; one a person (or the game) changed
        /// since is theirs and is left alone. Then hands each asset to <paramref name="restored"/>, with true when every
        /// colour recorded on it was put back (the editor clears its dirty flag only then).
        /// </summary>
        public static void RestoreAssets(Action<Object, bool> restored = null)
        {
            var whole = new Dictionary<Object, bool>();
            for (int i = Slots.Count - 1; i >= 0; i--)
            {
                var s = Slots[i];
                if (!s.Owner) continue;
                bool back = false;
                try { back = s.PutBack(); }
                catch (Exception e) { CSDebug.LogWarning($"WorldColorTrace: could not put back a colour on {s.Owner.name}: {e.Message}"); }
                whole[s.Owner] = (!whole.TryGetValue(s.Owner, out var all) || all) && back;
            }
            Slots.Clear();
            SlotOf.Clear();
            if (restored == null) return;
            foreach (var pair in whole) restored(pair.Key, pair.Value);
        }

        // ------------------------------------------------------------------ painters

        static bool Retrace(string role, Color c, out Color worn) =>
            ColorTrace.TryRetrace(role, c, out worn) && !Same(c, worn);

        static bool Same(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 1e-6f && Mathf.Abs(a.g - b.g) < 1e-6f && Mathf.Abs(a.b - b.b) < 1e-6f &&
            Mathf.Abs(a.a - b.a) < 1e-6f;

        // A component the editor shows outside play (Prefab Mode's stage, an inspector preview) is never painted.
        static bool Skips(Object o) => Editor != null && Editor.IsPreview(o);

        // True when a change to `owner` must be recorded for the editor to put back (never in a player).
        static bool Records(Object owner) => Editor != null && Editor.IsAsset(owner);

        // The colour properties of a shader and the role its colours are traced in (colour_trace.py is_ui_shader), cached.
        static (int[] ids, string role) ShaderInfo(Shader shader)
        {
            if (Shaders.TryGetValue(shader, out var info)) return info;
            Scratch.Clear();
            for (int i = 0, n = shader.GetPropertyCount(); i < n; i++)
                if (shader.GetPropertyType(i) == ShaderPropertyType.Color) Scratch.Add(shader.GetPropertyNameId(i));
            var name = shader.name;
            string role = name.StartsWith("UI/", StringComparison.Ordinal) ||
                          name.StartsWith("TextMeshPro/", StringComparison.Ordinal) ||
                          name.StartsWith("CosmicShore/UI/", StringComparison.Ordinal) ||
                          name.StartsWith("Hidden/UI", StringComparison.Ordinal) ||
                          name.StartsWith("Sprites/", StringComparison.Ordinal)
                ? ColorTrace.Ui : ColorTrace.World;
            info = (Scratch.ToArray(), role);
            Shaders[shader] = info;
            return info;
        }

        static int PaintMaterial(Material m)
        {
            if (!m || !m.shader) return 0;
            var (props, role) = ShaderInfo(m.shader);
            if (props.Length == 0) return 0;
            bool records = Records(m);
            int changed = 0;
            foreach (var id in props)
            {
                if (Owned.Count > 0 && Owned.Contains(OwnedKey(m, id))) continue;
                var c = m.GetColor(id);
                if (!Retrace(role, c, out var worn)) continue;
                m.SetColor(id, worn);
                if (records) RecordColor(m, id, c, worn);
                changed++;
            }
            if (changed > 0) Painted.Add(m);
            return changed;
        }

        static int PaintParticles(ParticleSystem ps)
        {
            if (!ps || Skips(ps)) return 0;
            bool records = Records(ps);
            int changed = 0;
            var main = ps.main;
            var start = main.startColor;
            if (Paint(start, out var wornStart)) { main.startColor = wornStart; if (records) RecordGradient(ps, 1, start, wornStart); changed++; }
            var col = ps.colorOverLifetime;
            if (col.enabled)
            {
                var life = col.color;
                if (Paint(life, out var wornLife)) { col.color = wornLife; if (records) RecordGradient(ps, 2, life, wornLife); changed++; }
            }
            var speed = ps.colorBySpeed;
            if (speed.enabled)
            {
                var bySpeed = speed.color;
                if (Paint(bySpeed, out var wornSpeed)) { speed.color = wornSpeed; if (records) RecordGradient(ps, 3, bySpeed, wornSpeed); changed++; }
            }
            var trails = ps.trails;
            if (trails.enabled)
            {
                var tl = trails.colorOverLifetime;
                if (Paint(tl, out var wornTl)) { trails.colorOverLifetime = wornTl; if (records) RecordGradient(ps, 4, tl, wornTl); changed++; }
                var tt = trails.colorOverTrail;
                if (Paint(tt, out var wornTt)) { trails.colorOverTrail = wornTt; if (records) RecordGradient(ps, 5, tt, wornTt); changed++; }
            }
            var custom = ps.customData;
            if (custom.enabled)
            {
                foreach (var stream in CustomStreams)
                {
                    if (custom.GetMode(stream) != ParticleSystemCustomDataMode.Color) continue;
                    var cc = custom.GetColor(stream);
                    if (!Paint(cc, out var wornCc)) continue;
                    custom.SetColor(stream, wornCc);
                    if (records) RecordGradient(ps, 6 + (int)stream, cc, wornCc);
                    changed++;
                }
            }
            return changed;
        }

        // A MinMaxGradient as the worn set paints it: the parts its mode draws (a colour, two colours, one or two gradients).
        static bool Paint(ParticleSystem.MinMaxGradient g, out ParticleSystem.MinMaxGradient worn)
        {
            worn = g;
            bool any = false;
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color:
                    if (Retrace(ColorTrace.World, g.color, out var c)) { worn = new ParticleSystem.MinMaxGradient(c); any = true; }
                    break;
                case ParticleSystemGradientMode.TwoColors:
                    bool a = Retrace(ColorTrace.World, g.colorMin, out var lo), b = Retrace(ColorTrace.World, g.colorMax, out var hi);
                    if (a || b) { worn = new ParticleSystem.MinMaxGradient(a ? lo : g.colorMin, b ? hi : g.colorMax); any = true; }
                    break;
                case ParticleSystemGradientMode.Gradient:
                case ParticleSystemGradientMode.RandomColor:
                    if (Paint(g.gradient, out var gr)) { worn = new ParticleSystem.MinMaxGradient(gr) { mode = g.mode }; any = true; }
                    break;
                case ParticleSystemGradientMode.TwoGradients:
                    bool ga = Paint(g.gradientMin, out var gmin), gb = Paint(g.gradientMax, out var gmax);
                    if (ga || gb) { worn = new ParticleSystem.MinMaxGradient(ga ? gmin : g.gradientMin, gb ? gmax : g.gradientMax); any = true; }
                    break;
            }
            return any;
        }

        static bool Paint(Gradient g, out Gradient worn)
        {
            worn = g;
            if (g == null) return false;
            var keys = g.colorKeys;
            bool any = false;
            for (int i = 0; i < keys.Length; i++)
            {
                if (!Retrace(ColorTrace.World, keys[i].color, out var c)) continue;
                keys[i].color = new Color(c.r, c.g, c.b, keys[i].color.a);
                any = true;
            }
            if (!any) return false;
            worn = new Gradient { mode = g.mode, colorSpace = g.colorSpace };
            worn.SetKeys(keys, g.alphaKeys);
            return true;
        }

        static int PaintTrail(TrailRenderer t)
        {
            if (!t || Skips(t)) return 0;
            var was = t.colorGradient;
            if (!Paint(was, out var g)) return 0;
            bool records = Records(t);
            t.colorGradient = g;
            if (records) RecordTrail(t, was, g);
            return 1;
        }

        static int PaintLine(LineRenderer l)
        {
            if (!l || Skips(l)) return 0;
            var was = l.colorGradient;
            if (!Paint(was, out var g)) return 0;
            bool records = Records(l);
            l.colorGradient = g;
            if (records) RecordLine(l, was, g);
            return 1;
        }

        static int PaintLight(Light l)
        {
            if (!l || Skips(l)) return 0;
            var was = l.color;
            if (!Retrace(ColorTrace.World, was, out var c)) return 0;
            bool records = Records(l);
            l.color = c;
            if (records) RecordLight(l, was, c);
            return 1;
        }

        // Game cameras only: the scene view's, a preview's or a reflection probe's clear colour is the editor's.
        static int PaintCamera(Camera cam)
        {
            if (!cam || cam.cameraType != CameraType.Game || cam.clearFlags != CameraClearFlags.SolidColor || Skips(cam)) return 0;
            var was = cam.backgroundColor;
            if (!Retrace(ColorTrace.World, was, out var c)) return 0;
            bool records = Records(cam);
            cam.backgroundColor = c;
            if (records) RecordCamera(cam, was, c);
            return 1;
        }

        static int PaintSprite(SpriteRenderer s)
        {
            if (!s || Skips(s)) return 0;
            var was = s.color;
            if (!Retrace(ColorTrace.World, was, out var c)) return 0;
            bool records = Records(s);
            s.color = c;
            if (records) RecordSprite(s, was, c);
            return 1;
        }

        static int PaintWorldText(TextMeshPro t)
        {
            if (!t || Skips(t)) return 0;
            var was = t.color;
            if (!Retrace(ColorTrace.Ui, was, out var c)) return 0;
            bool records = Records(t);
            t.color = c;
            if (records) RecordText(t, was, c);
            return 1;
        }

        static int PaintProfile(VolumeProfile profile)
        {
            if (!profile || profile.components == null) return 0;
            bool records = Records(profile);
            int changed = 0;
            foreach (var comp in profile.components)
            {
                if (!comp) continue;
                for (int i = 0; i < comp.parameters.Count; i++)
                {
                    if (comp.parameters[i] is not ColorParameter p || !p.overrideState) continue;
                    var was = p.value;
                    if (!Retrace(ColorTrace.World, was, out var c)) continue;
                    p.value = c;
                    if (records) RecordParameter(profile, ((long)comp.GetInstanceID() << 8) + i, p, was, c);
                    changed++;
                }
            }
            return changed;
        }

        // The scene's render settings belong to the scene (never an asset), so nothing is recorded.
        static int PaintRenderSettings()
        {
            int changed = 0;
            if (Retrace(ColorTrace.World, RenderSettings.fogColor, out var fog)) { RenderSettings.fogColor = fog; changed++; }
            if (Retrace(ColorTrace.World, RenderSettings.ambientSkyColor, out var sky)) { RenderSettings.ambientSkyColor = sky; changed++; }
            if (Retrace(ColorTrace.World, RenderSettings.ambientEquatorColor, out var eq)) { RenderSettings.ambientEquatorColor = eq; changed++; }
            if (Retrace(ColorTrace.World, RenderSettings.ambientGroundColor, out var gr)) { RenderSettings.ambientGroundColor = gr; changed++; }
            if (Retrace(ColorTrace.World, RenderSettings.subtractiveShadowColor, out var sub)) { RenderSettings.subtractiveShadowColor = sub; changed++; }
            return changed;
        }

        // ------------------------------------------------------------------ the editor's record (assets only)
        // Each painter above captures nothing; the closures below are made only when an asset's change is recorded,
        // which never happens in a player (Editor is null there).

        static void RecordColor(Material m, int id, Color was, Color painted) =>
            Record(m, id, was, painted, () => m.GetColor(id), v => m.SetColor(id, v), SameColor);

        static void RecordGradient(ParticleSystem ps, int slot, ParticleSystem.MinMaxGradient was, ParticleSystem.MinMaxGradient painted) =>
            Record(ps, slot, was, painted, () => Read(ps, slot), v => Write(ps, slot, v), SameGradient);

        static void RecordTrail(TrailRenderer t, Gradient was, Gradient painted) =>
            Record(t, 1, was, painted, () => t.colorGradient, v => t.colorGradient = v, SameGradientKeys);

        static void RecordLine(LineRenderer l, Gradient was, Gradient painted) =>
            Record(l, 1, was, painted, () => l.colorGradient, v => l.colorGradient = v, SameGradientKeys);

        static void RecordLight(Light l, Color was, Color painted) =>
            Record(l, 1, was, painted, () => l.color, v => l.color = v, SameColor);

        static void RecordCamera(Camera cam, Color was, Color painted) =>
            Record(cam, 1, was, painted, () => cam.backgroundColor, v => cam.backgroundColor = v, SameColor);

        static void RecordSprite(SpriteRenderer s, Color was, Color painted) =>
            Record(s, 1, was, painted, () => s.color, v => s.color = v, SameColor);

        static void RecordText(TextMeshPro t, Color was, Color painted) =>
            Record(t, 1, was, painted, () => t.color, v => t.color = v, SameColor);

        static void RecordParameter(VolumeProfile profile, long slot, ColorParameter p, Color was, Color painted) =>
            Record(profile, slot, was, painted, () => p.value, v => p.value = v, SameColor);

        static ParticleSystem.MinMaxGradient Read(ParticleSystem ps, int slot) => slot switch
        {
            1 => ps.main.startColor,
            2 => ps.colorOverLifetime.color,
            3 => ps.colorBySpeed.color,
            4 => ps.trails.colorOverLifetime,
            5 => ps.trails.colorOverTrail,
            _ => ps.customData.GetColor((ParticleSystemCustomData)(slot - 6)),
        };

        static void Write(ParticleSystem ps, int slot, ParticleSystem.MinMaxGradient v)
        {
            switch (slot)
            {
                case 1: { var m = ps.main; m.startColor = v; break; }
                case 2: { var m = ps.colorOverLifetime; m.color = v; break; }
                case 3: { var m = ps.colorBySpeed; m.color = v; break; }
                case 4: { var m = ps.trails; m.colorOverLifetime = v; break; }
                case 5: { var m = ps.trails; m.colorOverTrail = v; break; }
                default: { var m = ps.customData; m.SetColor((ParticleSystemCustomData)(slot - 6), v); break; }
            }
        }

        static readonly Func<Color, Color, bool> SameColor = Same;
        static readonly Func<ParticleSystem.MinMaxGradient, ParticleSystem.MinMaxGradient, bool> SameGradient = SameMinMax;
        static readonly Func<Gradient, Gradient, bool> SameGradientKeys = SameKeys;

        static bool SameMinMax(ParticleSystem.MinMaxGradient a, ParticleSystem.MinMaxGradient b)
        {
            if (a.mode != b.mode) return false;
            switch (a.mode)
            {
                case ParticleSystemGradientMode.Color: return Same(a.color, b.color);
                case ParticleSystemGradientMode.TwoColors: return Same(a.colorMin, b.colorMin) && Same(a.colorMax, b.colorMax);
                case ParticleSystemGradientMode.TwoGradients: return SameKeys(a.gradientMin, b.gradientMin) && SameKeys(a.gradientMax, b.gradientMax);
                default: return SameKeys(a.gradient, b.gradient);
            }
        }

        static bool SameKeys(Gradient a, Gradient b)
        {
            if (a == null || b == null) return a == b;
            var ka = a.colorKeys;
            var kb = b.colorKeys;
            if (ka.Length != kb.Length) return false;
            for (int i = 0; i < ka.Length; i++)
                if (!Same(ka[i].color, kb[i].color) || Mathf.Abs(ka[i].time - kb[i].time) > 1e-6f) return false;
            return true;
        }

        // An asset's colour: the value before the first repaint changed it (what is put back) and the value the last
        // repaint gave (what it must still hold to be put back).
        static void Record<T>(Object owner, long slot, T was, T painted, Func<T> get, Action<T> set, Func<T, T, bool> same)
        {
            long key = ((long)owner.GetInstanceID() << 20) ^ slot;
            if (SlotOf.TryGetValue(key, out var s) && s is Slot<T> known) known.Painted = painted;
            else
            {
                var fresh = new Slot<T>(owner, was, painted, get, set, same);
                SlotOf[key] = fresh;
                Slots.Add(fresh);
            }
            Editor?.Changed(owner);
        }

        abstract class Slot
        {
            public readonly Object Owner;
            protected Slot(Object owner) => Owner = owner;

            // Puts the first value back if the slot still holds what the last repaint gave; true when it did.
            public abstract bool PutBack();
        }

        sealed class Slot<T> : Slot
        {
            readonly T _was;
            readonly Func<T> _get;
            readonly Action<T> _set;
            readonly Func<T, T, bool> _same;
            public T Painted;

            public Slot(Object owner, T was, T painted, Func<T> get, Action<T> set, Func<T, T, bool> same) : base(owner)
            {
                _was = was;
                Painted = painted;
                _get = get;
                _set = set;
                _same = same;
            }

            public override bool PutBack()
            {
                if (!_same(_get(), Painted)) return false;
                _set(_was);
                return true;
            }
        }
    }
}
