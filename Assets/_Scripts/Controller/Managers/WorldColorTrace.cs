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
    /// role world; world text, role ui), alpha kept:</para>
    /// <list type="bullet">
    /// <item>every Color property of every loaded material (the HyperSea sky, flora, fauna, effects; the prism and
    /// crystal materials ThemeManager builds already wear the set and are left as they are);</item>
    /// <item>particle systems (start colour, colour over lifetime and by speed, trails, colour custom data), trails and
    /// lines, lights, solid-colour camera clears, sprites, world-space TextMesh Pro, fog and ambient light, and
    /// post-processing colour parameters.</item>
    /// </list>
    /// Prefab assets and materials are painted where they live in memory, so everything instantiated afterwards is
    /// born in the worn set and nothing is repainted per frame or per spawn. In a player nothing is saved; in the editor
    /// every asset touched is put back when play ends, or before the editor saves assets during play
    /// (<c>WorldColorTraceEditor</c>), so a .mat or prefab in git never takes a set's colours.
    /// </summary>
    public static class WorldColorTrace
    {
        /// <summary>Raised after a repaint, with the number of colours changed.</summary>
        public static event Action<int> Repainted;

        /// <summary>Set by the editor: true for an object that lives in an asset file (what must be put back).</summary>
        public static Func<Object, bool> IsPersistent;

        static readonly List<(Object owner, Action restore)> Restores = new();
        static readonly HashSet<long> Recorded = new();
        static readonly List<Material> Painted = new(64);
        static readonly Dictionary<Shader, int[]> ColorProps = new();
        static readonly List<int> Scratch = new(32);
        static readonly ParticleSystemCustomData[] CustomStreams = { ParticleSystemCustomData.Custom1, ParticleSystemCustomData.Custom2 };
        static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Repainted = null;
            Restores.Clear();
            Recorded.Clear();
            Painted.Clear();
            ColorProps.Clear();
            ShaderRoles.Clear();
            _installed = false;
        }

        // After SubsystemRegistration (which clears ColorTrace.Changed) and before the first scene's Awake. The scene
        // hook is removed first: with domain reload off, SceneManager keeps the last play session's handler.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (_installed) return;
            _installed = true;
            ColorTrace.Changed -= Repaint;
            ColorTrace.Changed += Repaint;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Repaint();

        /// <summary>The materials the last repaint changed (ThemeManager re-stamps the prism entities drawn with them).</summary>
        public static IReadOnlyList<Material> LastPainted => Painted;

        /// <summary>True while an asset holds a colour a repaint gave it (the editor puts it back).</summary>
        public static bool HasAssetChanges => Restores.Count > 0;

        /// <summary>Paints everything loaded from the worn set. Play mode only.</summary>
        public static void Repaint()
        {
            Painted.Clear();
            if (!Application.isPlaying || !ColorTrace.Worn) return;
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
            Repainted?.Invoke(changed);
        }

        /// <summary>
        /// Puts back every asset a repaint touched (the editor calls this when play ends or before a save), then hands
        /// each one to <paramref name="restored"/> (the editor clears its dirty flag: its file is unchanged).
        /// </summary>
        public static void RestoreAssets(Action<Object> restored = null)
        {
            for (int i = Restores.Count - 1; i >= 0; i--)
            {
                var (owner, restore) = Restores[i];
                if (!owner) continue;
                try
                {
                    restore();
                    restored?.Invoke(owner);
                }
                catch (Exception e) { CSDebug.LogWarning($"WorldColorTrace: could not put back a colour on {owner.name}: {e.Message}"); }
            }
            Restores.Clear();
            Recorded.Clear();
        }

        // ------------------------------------------------------------------ painters

        static bool Retrace(string role, Color c, out Color worn) =>
            ColorTrace.TryRetrace(role, c, out worn) && !Same(c, worn);

        static bool Same(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 1e-6f && Mathf.Abs(a.g - b.g) < 1e-6f && Mathf.Abs(a.b - b.b) < 1e-6f;

        static readonly Dictionary<Shader, string> ShaderRoles = new();

        // The role a material's colours are traced in, by its shader's name (colour_trace.py is_ui_shader), cached.
        static string RoleOf(Shader shader)
        {
            if (ShaderRoles.TryGetValue(shader, out var role)) return role;
            var n = shader.name;
            role = n.StartsWith("UI/") || n.StartsWith("TextMeshPro/") || n.StartsWith("CosmicShore/UI/") ||
                   n.StartsWith("Hidden/UI") || n.StartsWith("Sprites/")
                ? ColorTrace.Ui : ColorTrace.World;
            ShaderRoles[shader] = role;
            return role;
        }

        static int PaintMaterial(Material m)
        {
            if (!m || !m.shader) return 0;
            var props = ColorPropertiesOf(m.shader);
            if (props.Length == 0) return 0;
            string role = RoleOf(m.shader);
            int changed = 0;
            foreach (var id in props)
            {
                var c = m.GetColor(id);
                if (!Retrace(role, c, out var worn)) continue;
                if (Records(m)) RememberColor(m, id, c);
                m.SetColor(id, worn);
                changed++;
            }
            if (changed > 0) Painted.Add(m);
            return changed;
        }

        static int[] ColorPropertiesOf(Shader shader)
        {
            if (ColorProps.TryGetValue(shader, out var ids)) return ids;
            Scratch.Clear();
            for (int i = 0, n = shader.GetPropertyCount(); i < n; i++)
                if (shader.GetPropertyType(i) == ShaderPropertyType.Color) Scratch.Add(shader.GetPropertyNameId(i));
            ids = Scratch.ToArray();
            ColorProps[shader] = ids;
            return ids;
        }

        static int PaintParticles(ParticleSystem ps)
        {
            if (!ps) return 0;
            int changed = 0;
            var main = ps.main;
            if (Paint(main.startColor, out var start)) { var was = main.startColor; if (Records(ps)) Remember(ps, 1, () => { var mm = ps.main; mm.startColor = was; }); main.startColor = start; changed++; }
            var col = ps.colorOverLifetime;
            if (col.enabled && Paint(col.color, out var life)) { var was = col.color; if (Records(ps)) Remember(ps, 2, () => { var m = ps.colorOverLifetime; m.color = was; }); col.color = life; changed++; }
            var speed = ps.colorBySpeed;
            if (speed.enabled && Paint(speed.color, out var bySpeed)) { var was = speed.color; if (Records(ps)) Remember(ps, 3, () => { var m = ps.colorBySpeed; m.color = was; }); speed.color = bySpeed; changed++; }
            var trails = ps.trails;
            if (trails.enabled)
            {
                if (Paint(trails.colorOverLifetime, out var tl)) { var was = trails.colorOverLifetime; if (Records(ps)) Remember(ps, 4, () => { var m = ps.trails; m.colorOverLifetime = was; }); trails.colorOverLifetime = tl; changed++; }
                if (Paint(trails.colorOverTrail, out var tt)) { var was = trails.colorOverTrail; if (Records(ps)) Remember(ps, 5, () => { var m = ps.trails; m.colorOverTrail = was; }); trails.colorOverTrail = tt; changed++; }
            }
            var custom = ps.customData;
            if (custom.enabled)
            {
                foreach (var stream in CustomStreams)
                {
                    if (custom.GetMode(stream) != ParticleSystemCustomDataMode.Color) continue;
                    var s = stream;
                    if (Paint(custom.GetColor(s), out var cc)) { var was = custom.GetColor(s); if (Records(ps)) Remember(ps, 6 + (int)s, () => { var m = ps.customData; m.SetColor(s, was); }); custom.SetColor(s, cc); changed++; }
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
            if (!t || !Paint(t.colorGradient, out var g)) return 0;
            var was = t.colorGradient;
            if (Records(t)) Remember(t, 1, () => t.colorGradient = was);
            t.colorGradient = g;
            return 1;
        }

        static int PaintLine(LineRenderer l)
        {
            if (!l || !Paint(l.colorGradient, out var g)) return 0;
            var was = l.colorGradient;
            if (Records(l)) Remember(l, 1, () => l.colorGradient = was);
            l.colorGradient = g;
            return 1;
        }

        static int PaintLight(Light l)
        {
            if (!l || !Retrace(ColorTrace.World, l.color, out var c)) return 0;
            var was = l.color;
            if (Records(l)) Remember(l, 1, () => l.color = was);
            l.color = c;
            return 1;
        }

        static int PaintCamera(Camera cam)
        {
            if (!cam || cam.clearFlags != CameraClearFlags.SolidColor) return 0;
            if (!Retrace(ColorTrace.World, cam.backgroundColor, out var c)) return 0;
            var was = cam.backgroundColor;
            if (Records(cam)) Remember(cam, 1, () => cam.backgroundColor = was);
            cam.backgroundColor = c;
            return 1;
        }

        static int PaintSprite(SpriteRenderer s)
        {
            if (!s || !Retrace(ColorTrace.World, s.color, out var c)) return 0;
            var was = s.color;
            if (Records(s)) Remember(s, 1, () => s.color = was);
            s.color = c;
            return 1;
        }

        static int PaintWorldText(TextMeshPro t)
        {
            if (!t || !Retrace(ColorTrace.Ui, t.color, out var c)) return 0;
            var was = t.color;
            if (Records(t)) Remember(t, 1, () => t.color = was);
            t.color = c;
            return 1;
        }

        static int PaintProfile(VolumeProfile profile)
        {
            if (!profile || profile.components == null) return 0;
            int changed = 0;
            foreach (var comp in profile.components)
            {
                if (!comp) continue;
                for (int i = 0; i < comp.parameters.Count; i++)
                {
                    if (comp.parameters[i] is not ColorParameter p || !p.overrideState) continue;
                    if (!Retrace(ColorTrace.World, p.value, out var c)) continue;
                    var was = p.value;
                    if (Records(profile)) Remember(profile, ((long)comp.GetInstanceID() << 8) + i, () => p.value = was);
                    p.value = c;
                    changed++;
                }
            }
            return changed;
        }

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

        // The lambda lives here, so the loop that calls it allocates nothing unless a change is recorded.
        static void RememberColor(Material m, int id, Color was) => Remember(m, id, () => m.SetColor(id, was));

        // True when a change to `owner` must be recorded for the editor to put back (never in a player).
        static bool Records(Object owner) => IsPersistent != null && IsPersistent(owner);

        // An asset's colour, recorded the first time a repaint changes it, so the editor can put it back.
        static void Remember(Object owner, long slot, Action restore)
        {
            if (!Records(owner)) return;
            long key = ((long)owner.GetInstanceID() << 20) ^ slot;
            if (Recorded.Add(key)) Restores.Add((owner, restore));
        }
    }
}
