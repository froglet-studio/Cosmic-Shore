using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The colour grade: every texture pixel the game draws follows the worn colour set, as every authored colour does
    /// (Garrett, 2026-11-10, LOCKED: every colour comes from the colour set; "if rerendering textures per set is the
    /// best you can do, but perhaps you can get clever"). Docs/VesselModels/COLOR_TRACE.md.
    ///
    /// <para><c>Tools/Build/colour_grade.py</c> applies the colour trace's own rule to every colour of an sRGB grid,
    /// smoothly across family borders, and writes a 33^3 table per set and role
    /// (<c>Resources/ColorGrades/&lt;SetAsset&gt;_ui.bytes</c>, <c>_world.bytes</c>). When a set is worn
    /// (<see cref="ColorTrace.Changed"/>) this builds them into two Texture3Ds, hands them to every shader and switches
    /// <see cref="Keyword"/> on; <c>ColorGrade.cginc</c> samples them. Photic, the set the game was authored in, has no
    /// table: the keyword is off and nothing costs anything. Nothing is copied per set: the card art, the vessel
    /// renders, the pilot portraits (never overwritten, Docs/Pilots/PORTRAITS.md), the codex, the flat-coloured sprites
    /// and the static sky are graded as they are drawn.</para>
    ///
    /// <para>What draws through it: every Graphic that names no material (the default UI material becomes
    /// <c>CosmicShore/UI/GradedDefault</c>, <see cref="GradedDefaultUI"/>), the repo's UI shaders that sample a picture
    /// (CardDrift, HullLit, SilhouetteHolo, ElementRim) and the static sky. A Graphic that must keep its authored
    /// colours (a sign-in provider's mark) calls <see cref="KeepAsAuthored"/>.</para>
    /// </summary>
    public static class ColorGrade
    {
        /// <summary>The shader keyword the graded shaders compile for.</summary>
        public const string Keyword = "COSMIC_COLOR_GRADE";

        const string Folder = "ColorGrades/";
        const string GradedUIMaterial = Folder + "GradedUI";
        static readonly int UiId = Shader.PropertyToID("_CosmicGradeUI");
        static readonly int WorldId = Shader.PropertyToID("_CosmicGradeWorld");
        static readonly int ScaleOffsetId = Shader.PropertyToID("_CosmicGradeScaleOffset");
        static readonly byte[] Magic = { (byte)'C', (byte)'S', (byte)'G', (byte)'R' };
        const byte Version = 1;

        static Texture3D _ui, _world;
        static SO_ColorSet _graded;
        static bool _installed, _graphicDefaultSet;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Release();
            _graded = null;
            _installed = false;
            Shader.DisableKeyword(Keyword);
        }

        // Before the first scene: every canvas that draws from now on draws with the graded default, and the grade
        // follows every wear. The handlers are removed first: with domain reload off, Unity keeps the last session's.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (_installed) return;
            _installed = true;
            ColorTrace.Changed -= Wear;
            ColorTrace.Changed += Wear;
            Application.quitting -= Stop;
            Application.quitting += Stop;
            var graded = Resources.Load<Material>(GradedUIMaterial);
            if (graded)
            {
                GradedDefaultUI.Use(graded);
                _graphicDefaultSet = true;
            }
            else CSDebug.LogWarning($"ColorGrade: Resources/{GradedUIMaterial}.mat is missing; canvas pictures keep their authored colours.");
        }

        /// <summary>Play is ending: uGUI gets its own default material back and the grade goes off.</summary>
        public static void Stop()
        {
            Shader.DisableKeyword(Keyword);
            if (_graphicDefaultSet) GradedDefaultUI.Use(null);
            _graphicDefaultSet = false;
            Release();
            _graded = null;
        }

        /// <summary>True while a worn set's grade is on (a set other than the authored one).</summary>
        public static bool IsOn => _graded && _ui && _world;

        static void Wear()
        {
            if (WorldColorTrace.IsStopped) return;
            var set = ColorTrace.Worn;
            if (!set || set == ColorTrace.Authored)
            {
                Shader.DisableKeyword(Keyword);
                return;
            }
            if (set != _graded)
            {
                Release();
                _ui = Load(set, "ui");
                _world = Load(set, "world");
                _graded = set;
                if (!_ui || !_world)
                    CSDebug.LogWarning($"ColorGrade: {set.name} has no grade tables (Resources/{Folder}{set.name}_ui/_world): " +
                                       "run python3 Tools/Build/colour_grade.py. Its pictures keep their authored colours.");
            }
            if (!_ui || !_world)
            {
                Shader.DisableKeyword(Keyword);
                return;
            }
            float n = _ui.width;
            Shader.SetGlobalTexture(UiId, _ui);
            Shader.SetGlobalTexture(WorldId, _world);
            Shader.SetGlobalVector(ScaleOffsetId, new Vector4((n - 1f) / n, 0.5f / n, 0f, 0f));
            Shader.EnableKeyword(Keyword);
        }

        /// <summary>
        /// Draws <paramref name="graphic"/> with uGUI's plain material, so its picture keeps the colours it was authored
        /// in: for the marks whose owners fix them (a sign-in provider's own button and mark; colour_grade.py EXEMPT).
        /// </summary>
        public static void KeepAsAuthored(Graphic graphic)
        {
            if (graphic) graphic.material = Canvas.GetDefaultCanvasMaterial();
        }

        // One set's table for a role, as a Texture3D: the header, then n^3 RGB bytes, r fastest (colour_grade.py).
        static Texture3D Load(SO_ColorSet set, string role)
        {
            var asset = Resources.Load<TextAsset>(Folder + set.name + "_" + role);
            if (!asset) return null;
            var blob = asset.bytes;
            Resources.UnloadAsset(asset);
            if (blob.Length < 6 || blob[0] != Magic[0] || blob[1] != Magic[1] || blob[2] != Magic[2] || blob[3] != Magic[3] ||
                blob[4] != Version)
            {
                CSDebug.LogWarning($"ColorGrade: {set.name}_{role} is not a colour grade table.");
                return null;
            }
            int n = blob[5];
            int count = n * n * n;
            if (blob.Length != 6 + count * 3)
            {
                CSDebug.LogWarning($"ColorGrade: {set.name}_{role} is {blob.Length} bytes, not a {n}^3 table.");
                return null;
            }
            var rgba = new byte[count * 4];
            for (int i = 0, j = 6, k = 0; i < count; i++)
            {
                rgba[k++] = blob[j++];
                rgba[k++] = blob[j++];
                rgba[k++] = blob[j++];
                rgba[k++] = 255;
            }
            // Raw values (the sRGB encoding the shader decodes itself), filtered between grid points, clamped at the ends.
            var texture = new Texture3D(n, n, n, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None)
            {
                name = set.name + " grade (" + role + ")",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixelData(rgba, 0);
            texture.Apply(false, true);
            return texture;
        }

        static void Release()
        {
            if (_ui) Object.Destroy(_ui);
            if (_world) Object.Destroy(_world);
            _ui = _world = null;
        }
    }
}
