using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using CosmicShore.Build;
using CosmicShore.Engine;
using CosmicShore.Engine.InputSystem;
using CosmicShore.Render;
using TouchPhase = CosmicShore.Engine.InputSystem.TouchPhase;
using ETouch = CosmicShore.Engine.InputSystem.EnhancedTouch.Touch;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Mobile builds (cs-build android|ios): Unity's inclusion rules for player data,
// the GLSL → GLSL ES translation every renderer shader goes through on a phone,
// and the touch feed the phone's input backend writes.
// ─────────────────────────────────────────────────────────────────────────────

[Collection("TouchFeed")]
public class TouchFeedTests : IDisposable
{
    public void Dispose() => ETouch.activeTouches.Clear();

    static TouchFeed.Finger F(long key, float x, float y) => new(key, new Vector2(x, y));

    [Fact]
    public void A_finger_begins_moves_holds_and_ends_for_exactly_one_frame()
    {
        var feed = new TouchFeed(null);
        feed.Update(new[] { F(7, 10, 10) }, 0.016f);
        Assert.Equal(TouchPhase.Began, ETouch.activeTouches.Single().phase);
        int id = ETouch.activeTouches[0].touchId;

        feed.Update(new[] { F(7, 30, 10) }, 0.016f);
        var moved = ETouch.activeTouches.Single();
        Assert.Equal(TouchPhase.Moved, moved.phase);
        Assert.Equal(id, moved.touchId);
        Assert.Equal(new Vector2(20, 0), moved.delta);
        Assert.Equal(new Vector2(10, 10), moved.startScreenPosition);

        feed.Update(new[] { F(7, 30, 10) }, 0.016f);
        Assert.Equal(TouchPhase.Stationary, ETouch.activeTouches.Single().phase);

        feed.Update(Array.Empty<TouchFeed.Finger>(), 0.016f);
        var ended = ETouch.activeTouches.Single();
        Assert.Equal(TouchPhase.Ended, ended.phase);
        Assert.Equal(new Vector2(30, 10), ended.screenPosition);

        feed.Update(Array.Empty<TouchFeed.Finger>(), 0.016f);
        Assert.Empty(ETouch.activeTouches);
    }

    [Fact]
    public void Two_fingers_keep_their_own_ids_and_a_new_contact_never_reuses_one()
    {
        var feed = new TouchFeed(null);
        feed.Update(new[] { F(1, 0, 0), F(2, 100, 0) }, 0.016f);
        var ids = ETouch.activeTouches.Select(t => t.touchId).ToArray();
        Assert.Equal(2, ids.Distinct().Count());

        feed.Update(new[] { F(2, 100, 0) }, 0.016f);           // finger 1 lifts
        Assert.Contains(ETouch.activeTouches, t => t.touchId == ids[0] && t.phase == TouchPhase.Ended);
        Assert.Contains(ETouch.activeTouches, t => t.touchId == ids[1] && t.phase == TouchPhase.Stationary);

        feed.Update(new[] { F(1, 5, 5), F(2, 100, 0) }, 0.016f); // the same platform key touches down again
        var again = ETouch.activeTouches.Single(t => t.phase == TouchPhase.Began);
        Assert.DoesNotContain(again.touchId, ids);
    }

    [Fact]
    public void The_touchscreen_device_mirrors_the_active_touches()
    {
        var screen = InputSystem.AddDevice<Touchscreen>();
        try
        {
            var feed = new TouchFeed(screen);
            feed.Update(new[] { F(3, 40, 60) }, 0.016f);
            InputSystem.Update();   // the frame commit, as the engine's input update does
            Assert.Equal(TouchPhase.Began, screen.primaryTouch.phase);
            Assert.Equal(new Vector2(40, 60), screen.primaryTouch.position.ReadValue());
            Assert.True(screen.primaryTouch.press.isPressed);
            feed.Update(Array.Empty<TouchFeed.Finger>(), 0.016f);
            feed.Update(Array.Empty<TouchFeed.Finger>(), 0.016f);
            InputSystem.Update();
            Assert.Equal(TouchPhase.None, screen.primaryTouch.phase);
            Assert.False(screen.primaryTouch.press.isPressed);
        }
        finally { InputSystem.RemoveDevice(screen); }
    }
}

[Collection("GlCaps")]
public class GlslEsTranslationTests : IDisposable
{
    public GlslEsTranslationTests() => GlCaps.ForceEsTranslation = true;
    public void Dispose() => GlCaps.ForceEsTranslation = false;

    /// <summary>Every GLSL source the renderer compiles: the string constants that open with a 3.30 core header.</summary>
    static IEnumerable<(string Name, string Source)> RendererShaders()
    {
        var assemblies = new[] { typeof(GlProgram).Assembly, typeof(CosmicShore.Engine.UI.TmpSdfShader).Assembly };
        foreach (var asm in assemblies)
            foreach (var type in asm.GetTypes())
                foreach (var f in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    if (f.IsLiteral && f.FieldType == typeof(string) && f.GetRawConstantValue() is string s && s.StartsWith("#version 330 core", StringComparison.Ordinal))
                        yield return (type.Name + "." + f.Name, s);
    }

    [Fact]
    public void Every_renderer_shader_becomes_GLSL_ES_300_with_explicit_precision()
    {
        var shaders = RendererShaders().ToList();
        Assert.True(shaders.Count >= 15, $"found only {shaders.Count} shaders");
        foreach (var (name, src) in shaders)
        {
            var es = GlCaps.Translate(src, fragment: true);
            Assert.StartsWith("#version 300 es\n", es);
            Assert.Contains("precision highp float;", es);
            Assert.Contains("#define CS_EXT_2D 1", es);
            Assert.DoesNotContain("330 core", es);
            Assert.EndsWith(src.Substring(src.IndexOf('\n') + 1), es);
        }
    }

    [Fact]
    public void Desktop_sources_pass_through_untouched()
    {
        GlCaps.ForceEsTranslation = false;
        foreach (var (_, src) in RendererShaders()) Assert.Same(src, GlCaps.Translate(src, fragment: true));
    }

    /// <summary>The real check, when the Khronos reference compiler is on PATH: every translated stage compiles as GLSL ES.</summary>
    [Fact]
    public void Every_translated_stage_compiles_with_the_Khronos_reference_compiler()
    {
        string validator = FindOnPath("glslangValidator");
        if (validator == null) return;   // optional tool (apt install glslang-tools); the translation tests above still run
        var dir = Directory.CreateTempSubdirectory("cs-glsl-es");
        try
        {
            var failures = new List<string>();
            foreach (var (name, src) in RendererShaders())
            {
                string stage = src.Contains("gl_Position") ? "vert" : "frag";
                var file = Path.Combine(dir.FullName, name + "." + stage);
                File.WriteAllText(file, GlCaps.Translate(src, stage == "frag"));
                var psi = new ProcessStartInfo(validator, file) { RedirectStandardOutput = true, UseShellExecute = false };
                using var p = Process.Start(psi)!;
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0) failures.Add(name + ": " + string.Join(" | ", output.Split('\n').Where(l => l.Contains("ERROR"))));
            }
            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }
        finally { dir.Delete(recursive: true); }
    }

    static string FindOnPath(string exe)
        => (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Select(d => Path.Combine(d, exe)).FirstOrDefault(File.Exists);
}

public class PlayerDataBuilderTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("cs-playerdata").FullName;
    public void Dispose() => Directory.Delete(_root, recursive: true);

    static string G(int n) => n.ToString("x32");

    void Asset(string rel, int guid, string text = "x")
    {
        var path = Path.Combine(_root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        File.WriteAllText(path + ".meta", $"fileFormatVersion: 2\nguid: {G(guid)}\n");
    }

    void Settings(params (string Path, int Guid, bool Enabled)[] scenes)
    {
        Directory.CreateDirectory(Path.Combine(_root, "ProjectSettings"));
        File.WriteAllText(Path.Combine(_root, "ProjectSettings", "EditorBuildSettings.asset"),
            "EditorBuildSettings:\n  m_Scenes:\n" + string.Concat(scenes.Select(s => $"  - enabled: {(s.Enabled ? 1 : 0)}\n    path: {s.Path}\n    guid: {G(s.Guid)}\n")));
        File.WriteAllText(Path.Combine(_root, "ProjectSettings", "ProjectSettings.asset"),
            "PlayerSettings:\n  companyName: Froglet Games\n  productName: Cosmic Shore\n  bundleVersion: 1.2.3\n  preloadedAssets:\n  - {fileID: 11400000, guid: " + G(40) + ", type: 2}\n"
            + "  applicationIdentifier:\n    Android: com.example.game\n    iPhone: com.example.game-ios\n  buildNumber:\n    iPhone: 7\n  AndroidBundleVersionCode: 42\n");
    }

    [Fact]
    public void Ships_what_Unity_ships_and_nothing_else()
    {
        Asset("Assets/Scenes/Boot.unity", 1, "m_Script: {fileID: 11500000, guid: " + G(10) + "}\nprefab: {guid: " + G(2) + "}");
        Asset("Assets/Scenes/Disabled.unity", 3, "ref: {guid: " + G(4) + "}");
        Asset("Assets/Prefabs/Ship.prefab", 2, "mat: {guid: " + G(5) + "}");
        Asset("Assets/Materials/Hull.mat", 5, "tex: {guid: " + G(6) + "}");
        Asset("Assets/Textures/Hull.png", 6, "PNGDATA");
        Asset("Assets/Prefabs/OnlyInDisabledScene.prefab", 4);
        Asset("Assets/Scripts/Ship.cs", 10, "namespace Game.Vessels\n{ public class Ship {} }");
        Asset("Assets/Resources/Config.asset", 20);
        Asset("Assets/Editor/Resources/EditorOnly.asset", 21);
        Asset("Assets/Tools/Editor/Gizmo.prefab", 22);
        Asset("Assets/Data/Preloaded.asset", 40);
        Asset("Assets/Unused/Big.png", 30, "UNUSED");
        Asset("Assets/Lfs/Pointer.png", 31, "version https://git-lfs.github.com/spec/v1\noid sha256:abc\nsize 10\n");
        Asset("Assets/Resources/UsesPointer.asset", 32, "tex: {guid: " + G(31) + "}");
        Settings(("Assets/Scenes/Boot.unity", 1, true), ("Assets/Scenes/Disabled.unity", 3, false));

        var outDir = Path.Combine(_root, "out");
        var report = new PlayerDataBuilder(_root).Build(outDir);
        bool Shipped(string rel) => File.Exists(Path.Combine(outDir, rel));

        Assert.True(Shipped("Assets/Scenes/Boot.unity"));
        Assert.True(Shipped("Assets/Prefabs/Ship.prefab"));
        Assert.True(Shipped("Assets/Materials/Hull.mat"));
        Assert.True(Shipped("Assets/Textures/Hull.png") && Shipped("Assets/Textures/Hull.png.meta"));
        Assert.True(Shipped("Assets/Resources/Config.asset"));
        Assert.True(Shipped("Assets/Data/Preloaded.asset"));
        Assert.False(Shipped("Assets/Scenes/Disabled.unity"));
        Assert.False(Shipped("Assets/Prefabs/OnlyInDisabledScene.prefab"));
        Assert.False(Shipped("Assets/Editor/Resources/EditorOnly.asset"));
        Assert.False(Shipped("Assets/Unused/Big.png"));
        // Source never ships; its identity does.
        Assert.False(Shipped("Assets/Scripts/Ship.cs"));
        Assert.True(Shipped("Assets/Scripts/Ship.cs.meta"));
        Assert.Contains(G(10) + "\tGame.Vessels\tShip", File.ReadAllText(Path.Combine(outDir, "ScriptTypes.tsv")));
        Assert.True(Shipped("ProjectSettings/EditorBuildSettings.asset"));
        Assert.True(Shipped("PlayerData.json"));
        Assert.Equal(1, report.LfsPointers);
        Assert.Equal(new[] { "Assets/Scenes/Boot.unity" }, report.BuildScenes);
    }

    [Fact]
    public void Player_settings_name_and_version_the_build()
    {
        Settings();
        Directory.CreateDirectory(Path.Combine(_root, "Assets"));
        var b = new PlayerDataBuilder(_root);
        Assert.Equal("Cosmic Shore", b.ProductName());
        Assert.Equal("1.2.3", b.BundleVersion());
        Assert.Equal("com.example.game", b.ApplicationIdentifier("Android"));
        Assert.Equal("com.example.game-ios", b.ApplicationIdentifier("iPhone"));
        Assert.Equal(42, b.AndroidVersionCode());
        Assert.Equal("7", b.IosBuildNumber());
    }

    [Fact]
    public void The_content_runtime_reads_packaged_data_without_source()
    {
        Asset("Assets/Scenes/Boot.unity", 1, "m_Script: {fileID: 11500000, guid: " + G(10) + "}");
        Asset("Assets/Scripts/Ship.cs", 10, "namespace Game.Vessels\n{ public class Ship {} }");
        Settings(("Assets/Scenes/Boot.unity", 1, true));
        var outDir = Path.Combine(_root, "out");
        new PlayerDataBuilder(_root).Build(outDir);

        var db = new CosmicShore.Content.AssetDatabase(outDir);
        Assert.True(db.IsPlayerData);
        var id = db.BakedScript(G(10));
        Assert.Equal("Game.Vessels", id.Namespace);
        Assert.Equal("Ship", id.ClassName);
        Assert.EndsWith("Ship.cs", db.PathOf(G(10)));
    }
}
