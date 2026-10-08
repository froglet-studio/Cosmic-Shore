using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using CosmicShore.AssetTool;
using CosmicShore.Content;
using CosmicShore.Content.Models;

// The JSON commands write to Console.Out: run them one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CosmicShore.AssetTool.Tests
{
    public class EditorDataTests
    {
        static string Root => AssetDatabase.FindProjectRoot(AppContext.BaseDirectory) ?? throw new InvalidOperationException("no project root");

        static JsonDocument Capture(Func<int> run)
        {
            var old = Console.Out;
            var sw = new StringWriter();
            Console.SetOut(sw);
            try { Assert.Equal(0, run()); }
            finally { Console.SetOut(old); }
            // What Windows' OEM code page would mangle never reaches stdout: the JSON is plain ASCII.
            var bad = sw.ToString().FirstOrDefault(c => c > 127 || (c < 32 && c != '\n' && c != '\r'));
            Assert.True(bad == default(char), $"stdout carries U+{(int)bad:X4}");
            return JsonDocument.Parse(sw.ToString());
        }

        static string Temp()
        {
            var d = Path.Combine(Path.GetTempPath(), "cs-asset-tests-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(d);
            return d;
        }

        [Fact]
        public void Tools_reads_every_FrogletTools_menu_item_in_the_project()
        {
            var assets = Path.Combine(Root, "Assets");
            var tools = EditorData.ScanTools(assets);
            // An independent count: every FrogletTools MenuItem path that is not a validator.
            var expected = Directory.EnumerateFiles(assets, "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\[MenuItem\(""(FrogletTools/[^""]+)""(?!\s*,\s*true)").Cast<Match>().Select(m => m.Groups[1].Value))
                .Distinct().Count();
            Assert.Equal(expected, tools.Count);
            var launch = Assert.Single(tools, t => t.Menu == "FrogletTools/Prisma/Launch Prisma");
            Assert.Equal(("Build", 5, "Launch"), (launch.Category, launch.Importance, launch.Method));
            Assert.Equal("Assets/_Scripts/Editor/LaunchPrisma.cs", launch.File);
            Assert.StartsWith("Opens Prisma", launch.Description);
            // " & " inside a name is not a hotkey.
            Assert.Contains(tools, t => t.Menu.EndsWith("Warnings & Errors Only", StringComparison.Ordinal));
            // Text with non-ASCII characters (the occlusion tools' summaries use "↔") still come out as JSON that parses.
            using var doc = Capture(EditorData.Tools);
            Assert.Contains(doc.RootElement.GetProperty("tools").EnumerateArray(), t => t.TryGetProperty("summary", out var d) && d.GetString()!.Contains('↔'));
        }

        [Fact]
        public void Tools_parses_attribute_forms_the_way_the_registry_reads_them()
        {
            var dir = Temp();
            File.WriteAllText(Path.Combine(dir, "Sample.cs"), """
                /// <summary>Audits things. READER: writes nothing.</summary>
                public static class SampleAuditor
                {
                    [MenuItem("FrogletTools/Validation/Audit Things %#a", false, 20)]
                    [FrogletTool(FrogletToolCategory.Validation, Importance = 4,
                        Description = "Checks every " + "thing" + @" in ""quotes"".", DocPath = "Docs/X.md#a")]
                    public static void Run() { }

                    [MenuItem("FrogletTools/Validation/Audit Things %#a", true)]
                    static bool Can() => true;

                    [MenuItem("FrogletTools/Vessel Rig Planner", priority = 120)]
                    static void Plan() { }
                }
                """);
            var tools = EditorData.ScanTools(dir);
            Assert.Equal(2, tools.Count);
            var a = Assert.Single(tools, t => t.Name == "Audit Things");
            Assert.Equal(("Validation", 4, "Checks every thing in \"quotes\".", "Docs/X.md#a", "Run", "reader"),
                (a.Category, a.Importance, a.Description, a.Doc, a.Method, a.Writes));
            var p = Assert.Single(tools, t => t.Name == "Vessel Rig Planner");
            // No attribute: inferred like FrogletToolRegistry - path AND class name, checked in its order, so the class
            // "SampleAuditor" files it under Validation before "Vessel" in the path is reached; priority 120 -> importance 4.
            Assert.Equal(("Validation", 4, false), (p.Category, p.Importance, p.Annotated));
            Directory.Delete(dir, true);
        }

        [Theory]
        [InlineData("m_maxSpeed", "Max Speed")]
        [InlineData("_boostTrailSeconds", "Boost Trail Seconds")]
        [InlineData("<MaxPlayers>k__BackingField", "Max Players")]
        [InlineData("kMaxCount", "Max Count")]
        [InlineData("isUIVisible", "Is UI Visible")]
        [InlineData("tier2Cost", "Tier 2 Cost")]
        public void Labels_read_like_Unitys_inspector(string key, string label) => Assert.Equal(label, EditorData.Nicify(key));

        [Fact]
        public void Datasets_group_every_data_file_by_script()
        {
            using var doc = Capture(EditorData.Datasets);
            var types = doc.RootElement.GetProperty("types").EnumerateArray().ToList();
            int files = doc.RootElement.GetProperty("files").GetInt32();
            Assert.Equal(files, types.Sum(t => t.GetProperty("count").GetInt32()));
            var arcade = types.Single(t => t.GetProperty("type").GetString() == "SO_ArcadeGame");
            Assert.Contains(arcade.GetProperty("items").EnumerateArray(), i => i.GetProperty("path").GetString() == "Assets/_SO_Assets/Games/ArcadeGameScurry.asset");
        }

        [Fact]
        public void Dataset_describes_fields_with_the_scripts_attributes()
        {
            using var doc = Capture(() => EditorData.Dataset("Assets/_SO_Assets/Games/ArcadeGameScurry.asset"));
            var o = doc.RootElement.GetProperty("objects")[0];
            Assert.Equal(11400000, o.GetProperty("fileId").GetInt64());
            Assert.True(o.GetProperty("resolved").GetBoolean());
            var fields = o.GetProperty("fields").EnumerateArray().ToDictionary(f => f.GetProperty("key").GetString()!);
            var max = fields["MaxIntensity"];
            Assert.Equal("number", max.GetProperty("kind").GetString());
            Assert.Equal(new[] { 1f, 4f }, max.GetProperty("range").EnumerateArray().Select(x => x.GetSingle()));
            var mode = fields["Mode"];
            Assert.Equal("enum", mode.GetProperty("kind").GetString());
            Assert.Contains(mode.GetProperty("options").EnumerateArray(), x => x[0].GetString() == "Scurry");
            Assert.Equal("list", fields["Vessels"].GetProperty("kind").GetString());
            Assert.EndsWith(".asset", fields["Vessels"].GetProperty("children")[0].GetProperty("refPath").GetString());
            Assert.False(fields["MaxIntensity"].GetProperty("stale").GetBoolean());
        }

        [Fact]
        public void Dataset_marks_a_key_the_script_no_longer_has_as_stale()
        {
            // A copy of a real data file carrying a key its script dropped (as SO_Game.PreviewClip
            // was): Unity ignores it, so the DATA page shows it amber. A copy, because the project's
            // own files get cleaned of such keys over time.
            var dir = Temp();
            var copy = Path.Combine(dir, "ArcadeGameScurry.asset");
            var text = File.ReadAllText(Path.Combine(Root, "Assets/_SO_Assets/Games/ArcadeGameScurry.asset"));
            File.WriteAllText(copy, text.TrimEnd('\n') + "\n  RetiredByATest: 1\n");
            using (var doc = Capture(() => EditorData.Dataset(copy)))
            {
                var fields = doc.RootElement.GetProperty("objects")[0].GetProperty("fields").EnumerateArray()
                    .ToDictionary(f => f.GetProperty("key").GetString()!);
                Assert.True(fields["RetiredByATest"].GetProperty("stale").GetBoolean());
                Assert.False(fields["MaxIntensity"].GetProperty("stale").GetBoolean());
            }
            Directory.Delete(dir, true);
        }

        [Fact]
        public void Model_reports_what_the_importer_makes_and_previews_it()
        {
            const string fbx = "Assets/_Models/Vessel Models/Dolphin_Test.fbx";
            using (var doc = Capture(() => EditorData.Model(fbx)))
            {
                var m = doc.RootElement;
                Assert.True(m.GetProperty("triangles").GetInt32() > 1000);
                Assert.Equal(m.GetProperty("meshCount").GetInt32(), m.GetProperty("meshes").GetArrayLength());
                Assert.Contains("BASE", m.GetProperty("materials").EnumerateArray().Select(x => x.GetString()));
                Assert.True(m.GetProperty("bounds").GetProperty("size")[0].GetDouble() > 1);
            }
            var png = Path.Combine(Temp(), "dolphin.png");
            Capture(() => EditorData.ModelPreview(fbx, new() { ["out"] = png, ["size"] = "128" })).Dispose();
            var bytes = File.ReadAllBytes(png);
            Assert.Equal(new byte[] { 137, 80, 78, 71 }, bytes[..4]);
            Assert.Equal(128, (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19]); // IHDR width
            Directory.Delete(Path.GetDirectoryName(png)!, true);
        }

        [Fact]
        public void A_model_shows_the_materials_the_prefabs_that_draw_it_give_it()
        {
            // Manta.prefab's renderer draws the Manta mesh with the vessel graph's materials; the
            // model's own .meta remaps nothing, so only the prefab scan can find them.
            const string fbx = "Assets/_Models/Vessel Models/Manta_shapekey_rigged.fbx";
            using var doc = Capture(() => EditorData.Model(fbx));
            var m = doc.RootElement;
            var used = m.GetProperty("usedBy").EnumerateArray().Select(x => x.GetString()).ToList();
            Assert.Contains("Assets/_Prefabs/Spacevessels/Manta.prefab", used);
            // Of the prefabs that draw it alike, the one named like the model wins.
            Assert.Equal("Assets/_Prefabs/Spacevessels/Manta.prefab", m.GetProperty("materialSource").GetString());
            var mats = m.GetProperty("gameMaterials")[0].GetProperty("materials").EnumerateArray().ToList();
            Assert.Contains(mats, x => x.GetProperty("shader").GetString() == "Shader Graphs/VesselGraph");
            // VesselGraph's colour is _Color1 (its _BaseColor is white): BlueBaseShipMaterial is navy.
            var blue = mats.Single(x => x.GetProperty("name").GetString() == "BlueBaseShipMaterial");
            Assert.Equal("#000029", blue.GetProperty("color").GetString());
        }

        [Fact]
        public void A_turntable_sheet_holds_every_view_around_the_model()
        {
            const string fbx = "Assets/_Models/Vessel Models/Dolphin_Test.fbx";
            var png = Path.Combine(Temp(), "turn.png");
            using (var doc = Capture(() => EditorData.ModelPreview(fbx, new() { ["out"] = png, ["size"] = "64", ["turntable"] = "12" })))
            {
                var r = doc.RootElement;
                Assert.Equal(12, r.GetProperty("frames").GetInt32());
                Assert.Equal(6, r.GetProperty("cols").GetInt32());
                Assert.Equal(30, r.GetProperty("step").GetDouble());
            }
            var bytes = File.ReadAllBytes(png);
            Assert.Equal(64 * 6, (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19]); // IHDR width
            Assert.Equal(64 * 2, (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23]); // IHDR height
            Directory.Delete(Path.GetDirectoryName(png)!, true);
        }

        /// <summary>
        /// A Maya file goes through mayapy, as Unity's import does: a stand-in mayapy that writes a
        /// known FBX proves the hand-off (script, source, output), the import of what it wrote, and
        /// the cache (the second import runs no mayapy at all).
        /// </summary>
        [Fact]
        public void A_Maya_file_imports_through_mayapy_and_is_cached()
        {
            if (OperatingSystem.IsWindows()) return; // the stand-in is a shell script
            var dir = Temp();
            var cacheBefore = DccModelConverter.CacheDir;
            var envBefore = Environment.GetEnvironmentVariable("PRISMA_MAYAPY");
            try
            {
                DccModelConverter.CacheDir = Path.Combine(dir, "cache");
                var ma = Path.Combine(dir, "Probe.ma");
                File.WriteAllText(ma, "//Maya ASCII scene");
                var calls = Path.Combine(dir, "calls.txt");
                var dolphin = Path.Combine(Root, "Assets/_Models/Vessel Models/Dolphin_Test.fbx");
                var fake = Path.Combine(dir, "mayapy");
                File.WriteAllText(fake, $"#!/bin/sh\necho \"$2\" >> '{calls}'\ncp '{dolphin}' \"$3\"\n");
                File.SetUnixFileMode(fake, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                Environment.SetEnvironmentVariable("PRISMA_MAYAPY", fake);

                var model = DccModelConverter.Import(ma, new CosmicShore.Content.Models.ModelImportSettings(), null, out var error);
                Assert.True(model != null, error);
                Assert.Equal(ma, model!.Path);
                Assert.Equal("Probe", model.Name); // the root takes the source's name: fileIDs hash from it
                Assert.True(model.Meshes.Count > 0);
                Assert.NotNull(DccModelConverter.Import(ma, new CosmicShore.Content.Models.ModelImportSettings(), null, out _));
                Assert.Single(File.ReadAllLines(calls));
                Assert.Equal(Path.GetFullPath(ma), File.ReadAllLines(calls)[0]);
            }
            finally
            {
                DccModelConverter.CacheDir = cacheBefore;
                Environment.SetEnvironmentVariable("PRISMA_MAYAPY", envBefore);
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void A_Blender_file_says_Blender_is_missing_instead_of_failing_blindly()
        {
            var pathBefore = Environment.GetEnvironmentVariable("PATH");
            var envBefore = Environment.GetEnvironmentVariable("PRISMA_BLENDER");
            try
            {
                Environment.SetEnvironmentVariable("PRISMA_BLENDER", null);
                Environment.SetEnvironmentVariable("PATH", "");
                if (DccModelConverter.FindBlender() != null) return; // installed where Prisma looks: nothing to prove here
                Assert.Null(DccModelConverter.ToFbx(Path.Combine(Root, "Assets/_Models/shield1.blend"), out var error));
                Assert.Contains("Blender is not installed", error);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", pathBefore);
                Environment.SetEnvironmentVariable("PRISMA_BLENDER", envBefore);
            }
        }

        /// <summary>The real thing, where Blender is installed (PRISMA_BLENDER or PATH): shield1.blend, a hex-tiled sphere.</summary>
        [Fact]
        public void A_Blender_file_imports_through_Blender_when_it_is_installed()
        {
            if (DccModelConverter.FindBlender() == null) return;
            using var doc = Capture(() => EditorData.Model("Assets/_Models/shield1.blend"));
            var m = doc.RootElement;
            Assert.Equal("Blender", m.GetProperty("convertedBy").GetString());
            Assert.Equal(636, m.GetProperty("triangles").GetInt32());
            Assert.InRange(m.GetProperty("bounds").GetProperty("size")[0].GetDouble(), 1.9, 2.0);
        }

        /// <summary>
        /// The vessels' elemental hull morphs: --shapes poses the preview with them (the picture
        /// changes, the JSON echoes the weights), a name the mesh lacks changes nothing, and the
        /// model's takes are the clips the engine viewer plays.
        /// </summary>
        [Fact]
        public void Blend_shapes_pose_the_preview_and_the_takes_import_as_clips()
        {
            const string fbx = "Assets/_Models/Vessel Models/dolphin_shapekey_with_animations.fbx";
            var db = new AssetDatabase(Root);
            var model = db.LoadModel(db.GuidOf(Path.Combine(Root, fbx))!);
            Assert.NotNull(model);
            var mesh = model.Meshes.First(m => m.Mesh != null && m.Mesh.blendShapeCount > 0);
            var posed = EditorData.Posed(model, new System.Collections.Generic.Dictionary<string, float> { ["mass"] = 100 });
            Assert.True(posed.ContainsKey(mesh));
            Assert.Contains(posed[mesh].Zip(mesh.Mesh.vertices), p => (p.First - p.Second).magnitude > 1e-3f);
            Assert.Empty(EditorData.Posed(model, new System.Collections.Generic.Dictionary<string, float> { ["no such shape"] = 100 }));

            var dir = Temp();
            byte[] Shot(string name, string shapes)
            {
                var png = Path.Combine(dir, name);
                var o = new System.Collections.Generic.Dictionary<string, string> { ["out"] = png, ["size"] = "96" };
                if (shapes != null) o["shapes"] = shapes;
                using var doc = Capture(() => EditorData.ModelPreview(fbx, o));
                if (shapes != null) Assert.Equal(100, doc.RootElement.GetProperty("shapes").GetProperty(shapes.Split('=')[0]).GetDouble());
                return File.ReadAllBytes(png);
            }
            var plain = Shot("plain.png", null);
            Assert.NotEqual(plain, Shot("mass.png", "mass=100"));
            Assert.Equal(plain, Shot("none.png", "nothing=100"));
            Directory.Delete(dir, true);

            var takes = FbxAnimationImporter.ListClips(model);
            Assert.Equal(10, takes.Count);
            var clip = FbxAnimationImporter.ImportClip(model, takes[0].FileId);
            Assert.NotNull(clip);
            Assert.True(clip.length > 0);
            Assert.Contains(clip.Bindings, b => b.Attribute.StartsWith("blendShape.", StringComparison.Ordinal));
        }

        /// <summary>SETTINGS' Blender/Maya paths reach cs-asset as PRISMA_BLENDER / PRISMA_MAYAPY, ahead of any search.</summary>
        [Fact]
        public void A_configured_Blender_or_Maya_path_wins_and_Maya_reads_its_version_from_the_folder()
        {
            var dir = Temp();
            var fake = Path.Combine(dir, "my-blender");
            File.WriteAllText(fake, "");
            var old = Environment.GetEnvironmentVariable("PRISMA_BLENDER");
            try
            {
                Environment.SetEnvironmentVariable("PRISMA_BLENDER", fake);
                Assert.Equal(fake, Prisma.DccLocator.FindBlender());
                Assert.Equal(fake, DccModelConverter.FindBlender());
            }
            finally { Environment.SetEnvironmentVariable("PRISMA_BLENDER", old); Directory.Delete(dir, true); }
            Assert.Equal("2025", Prisma.DccLocator.MayaVersion("/usr/autodesk/maya2025/bin/mayapy"));
            Assert.Equal("2024", Prisma.DccLocator.MayaVersion(@"C:/Program Files/Autodesk/Maya2024/bin/mayapy.exe"));
            Assert.Null(Prisma.DccLocator.MayaVersion("/opt/tools/mayapy"));
        }

        [Fact]
        public void Preview_draws_the_model_into_the_frame()
        {
            var guid = new AssetDatabase(Root).GuidOf(Path.Combine(Root, "Assets/_Models/Vessel Models/Dolphin_Test.fbx"));
            var model = new AssetDatabase(Root).LoadModel(guid!);
            var png = EditorData.Render(model!, 96, 145, 20);
            // Decode the one IDAT back and count pixels that are not the dark background.
            int idat = IndexOf(png, "IDAT"u8.ToArray());
            int len = (png[idat - 4] << 24) | (png[idat - 3] << 16) | (png[idat - 2] << 8) | png[idat - 1];
            using var z = new System.IO.Compression.ZLibStream(new MemoryStream(png, idat + 4, len), System.IO.Compression.CompressionMode.Decompress);
            var raw = new MemoryStream(); z.CopyTo(raw);
            var px = raw.ToArray();
            int lit = 0;
            for (int y = 0; y < 96; y++)
                for (int x = 0; x < 96; x++)
                {
                    int i = y * (96 * 4 + 1) + 1 + x * 4;
                    if (px[i] + px[i + 1] > 120) lit++;
                }
            Assert.InRange(lit, 96 * 96 / 20, 96 * 96 * 9 / 10); // fills a fair share of the frame, not all of it
        }

        /// <summary>
        /// What the DATA page sends: text quoted the way the launcher quotes it (EditorTool.YamlScalar's
        /// rules: plain, single-quoted, or double-quoted with escapes for several lines), written by
        /// cs-asset set, read back by cs-asset dataset - the text must come back unchanged.
        /// </summary>
        [Theory]
        [InlineData("Plain words")]
        [InlineData("Fly: fast, then 'stop' # now")]
        [InlineData("Two\nlines with \"quotes\"")]
        public void Text_written_by_set_reads_back_unchanged(string text)
        {
            text = text.Replace("\\n", "\n").Replace("\\\"", "\"");
            var dir = Temp();
            var file = Path.Combine(dir, "Copy.asset");
            File.Copy(Path.Combine(Root, "Assets/_SO_Assets/Games/ArcadeGameScurry.asset"), file);
            var dll = Path.Combine(AppContext.BaseDirectory, "cs-asset.dll");
            var psi = new System.Diagnostics.ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
                { RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Root };
            foreach (var arg in new[] { dll, "set", file, "&11400000", "Description", Quote(text) }) psi.ArgumentList.Add(arg);
            using (var p = System.Diagnostics.Process.Start(psi)!)
            {
                var err = p.StandardError.ReadToEnd(); p.StandardOutput.ReadToEnd(); p.WaitForExit();
                Assert.True(p.ExitCode == 0, err);
            }
            using var doc = Capture(() => EditorData.Dataset(file));
            var value = doc.RootElement.GetProperty("objects")[0].GetProperty("fields").EnumerateArray()
                .Single(f => f.GetProperty("key").GetString() == "Description").GetProperty("value").GetString()!;
            Assert.Equal(text, Unquote(value));
            Directory.Delete(dir, true);
        }

        // The launcher's EditorTool.YamlScalar and the DATA page's unquote, mirrored here (the launcher is a separate app).
        static string Quote(string t)
        {
            if (t.Contains('\n')) return "\"" + t.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
            bool plain = !t.Any(c => ":#'\"{}[],&*!|>%@`".Contains(c)) && !char.IsWhiteSpace(t[0]) && !char.IsWhiteSpace(t[^1]) && t[0] != '-' && t[0] != '?';
            return plain ? t : "'" + t.Replace("'", "''") + "'";
        }

        static string Unquote(string v) =>
            v.Length >= 2 && v[0] == '\'' && v[^1] == '\'' ? v[1..^1].Replace("''", "'") :
            v.Length >= 2 && v[0] == '"' && v[^1] == '"' ? v[1..^1].Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\") : v;

        static int IndexOf(byte[] hay, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= hay.Length; i++)
                if (hay.AsSpan(i, needle.Length).SequenceEqual(needle)) return i;
            return -1;
        }
    }
}
