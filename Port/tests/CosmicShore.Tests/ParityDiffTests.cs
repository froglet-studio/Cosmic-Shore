using System;
using System.IO;
using System.Linq;
using Prisma.Parity;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The parity diff (ROADMAP C1, tolerances C9). Gate G1: three planted differences - a state
    /// value, an event order and a transform beyond its bar - must each be reported, and the
    /// bars must hold at their edges.
    /// </summary>
    public class ParityDiffTests : IDisposable
    {
        readonly string _root = Path.Combine(Path.GetTempPath(), "parity-tests-" + Guid.NewGuid().ToString("N"));
        readonly Tolerances _tol = new();

        string Golden => Path.Combine(_root, "golden");
        string Run => Path.Combine(_root, "run");

        public ParityDiffTests()
        {
            foreach (var d in new[] { Golden, Run })
            {
                Directory.CreateDirectory(d);
                File.WriteAllText(Path.Combine(d, "state.jsonl"),
                    "{\"frame\":0,\"t\":0.0167,\"scene\":\"Bootstrap\"}\n{\"frame\":30,\"t\":0.5167,\"scene\":\"MinigameSkimRace\",\"stats\":{\"p\":{\"score\":12.5,\"crystals\":1}}}\n");
                File.WriteAllText(Path.Combine(d, "events.jsonl"),
                    "{\"t\":0.0167,\"kind\":\"fmod\",\"name\":\"event:/Music/Music\"}\n{\"t\":1.5,\"kind\":\"fmod\",\"name\":\"event:/SFX/Crystal Collect\"}\n{\"t\":1.5,\"kind\":\"fmod\",\"name\":\"event:/SFX/Shield\"}\n");
                File.WriteAllText(Path.Combine(d, "transforms.jsonl"),
                    "{\"frame\":30,\"t\":0.5,\"id\":\"Squirrel\",\"p\":[100,0,0],\"r\":[0,0,0,1]}\n{\"frame\":900,\"t\":15,\"id\":\"Squirrel\",\"p\":[200,0,0],\"r\":[0,0,0,1]}\n");
            }
        }

        public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

        static void Replace(string path, string from, string to)
        {
            var s = File.ReadAllText(path);
            Assert.Contains(from, s);
            File.WriteAllText(path, s.Replace(from, to));
        }

        [Fact]
        public void IdenticalRunsPassEveryChannelWithAGolden()
        {
            var r = ParityDiff.Compare(Golden, Run, _tol);
            Assert.All(r.Where(c => c.Channel is "state" or "events" or "transforms"), c => Assert.Equal(ChannelStatus.Pass, c.Status));
            Assert.Equal(ChannelStatus.Missing, r.Single(c => c.Channel == "frames").Status);
            Assert.Equal(ChannelStatus.Missing, r.Single(c => c.Channel == "random").Status);
        }

        [Fact]
        public void PlantedScoreDifferenceFailsState()
        {
            Replace(Path.Combine(Run, "state.jsonl"), "\"score\":12.5", "\"score\":12.6");
            var c = ParityDiff.State(Golden, Run);
            Assert.Equal(ChannelStatus.Fail, c.Status);
            Assert.Contains("checkpoint 1", c.Detail);
        }

        [Fact]
        public void StateIgnoresGameTime()
        {
            Replace(Path.Combine(Run, "state.jsonl"), "\"t\":0.5167", "\"t\":0.5333");
            Assert.Equal(ChannelStatus.Pass, ParityDiff.State(Golden, Run).Status);
        }

        [Fact]
        public void PlantedEventReorderFails()
        {
            File.WriteAllText(Path.Combine(Run, "events.jsonl"),
                "{\"t\":0.0167,\"kind\":\"fmod\",\"name\":\"event:/Music/Music\"}\n{\"t\":1.5,\"kind\":\"fmod\",\"name\":\"event:/SFX/Shield\"}\n{\"t\":1.5,\"kind\":\"fmod\",\"name\":\"event:/SFX/Crystal Collect\"}\n");
            var c = ParityDiff.Events(Golden, Run, _tol);
            Assert.Equal(ChannelStatus.Fail, c.Status);
            Assert.Contains("event 1", c.Detail);
        }

        [Fact]
        public void EventTimeWithinOneStepPassesBeyondFails()
        {
            Replace(Path.Combine(Run, "events.jsonl"), "\"t\":1.5,\"kind\":\"fmod\",\"name\":\"event:/SFX/Crystal Collect\"", "\"t\":1.539,\"kind\":\"fmod\",\"name\":\"event:/SFX/Crystal Collect\"");
            Assert.Equal(ChannelStatus.Pass, ParityDiff.Events(Golden, Run, _tol).Status);
            Replace(Path.Combine(Run, "events.jsonl"), "\"t\":1.539", "\"t\":1.541");
            Assert.Equal(ChannelStatus.Fail, ParityDiff.Events(Golden, Run, _tol).Status);
        }

        [Fact]
        public void MissingEventFailsOnCount()
        {
            File.WriteAllText(Path.Combine(Run, "events.jsonl"), "{\"t\":0.0167,\"kind\":\"fmod\",\"name\":\"event:/Music/Music\"}\n{\"t\":1.5,\"kind\":\"fmod\",\"name\":\"event:/SFX/Crystal Collect\"}\n");
            var c = ParityDiff.Events(Golden, Run, _tol);
            Assert.Equal(ChannelStatus.Fail, c.Status);
            Assert.Contains("3 golden events vs 2", c.Detail);
        }

        const string GameAndContact =
            "{\"t\":0.0167,\"kind\":\"game\",\"name\":\"scene:MinigameSkimRace\"}\n{\"t\":1.5,\"kind\":\"contact\",\"name\":\"Crystal|Skimmer\"}\n{\"t\":2.0,\"kind\":\"game\",\"name\":\"OnMiniGameTurnEnd\"}\n";

        [Fact]
        public void PlantedGameEventDifferenceFails()
        {
            File.AppendAllText(Path.Combine(Golden, "events.jsonl"), GameAndContact);
            File.AppendAllText(Path.Combine(Run, "events.jsonl"), GameAndContact.Replace("OnMiniGameTurnEnd", "OnMiniGameRoundEnd"));
            var c = ParityDiff.Events(Golden, Run, _tol);
            Assert.Equal(ChannelStatus.Fail, c.Status);
            Assert.Contains("game:OnMiniGameTurnEnd", c.Detail);
        }

        [Fact]
        public void PlantedContactDifferenceFails()
        {
            File.AppendAllText(Path.Combine(Golden, "events.jsonl"), GameAndContact);
            File.AppendAllText(Path.Combine(Run, "events.jsonl"), GameAndContact.Replace("Crystal|Skimmer", "Prism|Skimmer"));
            var c = ParityDiff.Events(Golden, Run, _tol);
            Assert.Equal(ChannelStatus.Fail, c.Status);
            Assert.Contains("contact:Crystal|Skimmer", c.Detail);
        }

        [Fact]
        public void EventKindsWithoutAGoldenAreNamedNotCompared()
        {
            File.AppendAllText(Path.Combine(Run, "events.jsonl"), GameAndContact);
            var c = ParityDiff.Events(Golden, Run, _tol);
            Assert.Equal(ChannelStatus.Pass, c.Status);
            Assert.Contains("no golden for kind(s) contact, game", c.Detail);
        }

        [Fact]
        public void PlantedTransformBeyondItsBarFails()
        {
            // 100 m from the origin: the bar is 1e-4 * 100 = 1 cm.
            Replace(Path.Combine(Run, "transforms.jsonl"), "\"p\":[100,0,0]", "\"p\":[100.009,0,0]");
            Assert.Equal(ChannelStatus.Pass, ParityDiff.Transforms(Golden, Run, _tol).Status);
            Replace(Path.Combine(Run, "transforms.jsonl"), "\"p\":[100.009,0,0]", "\"p\":[100.011,0,0]");
            var c = ParityDiff.Transforms(Golden, Run, _tol);
            Assert.Equal(ChannelStatus.Fail, c.Status);
            Assert.Contains("Squirrel", c.Detail);
        }

        [Fact]
        public void RotationBarIsATenthOfADegree()
        {
            double half(double deg) => Math.Sin(deg * Math.PI / 360.0);
            double cosHalf(double deg) => Math.Cos(deg * Math.PI / 360.0);
            Replace(Path.Combine(Run, "transforms.jsonl"), "\"p\":[100,0,0],\"r\":[0,0,0,1]", $"\"p\":[100,0,0],\"r\":[0,{half(0.09):R},0,{cosHalf(0.09):R}]");
            Assert.Equal(ChannelStatus.Pass, ParityDiff.Transforms(Golden, Run, _tol).Status);
            File.Copy(Path.Combine(Golden, "transforms.jsonl"), Path.Combine(Run, "transforms.jsonl"), true);
            Replace(Path.Combine(Run, "transforms.jsonl"), "\"p\":[100,0,0],\"r\":[0,0,0,1]", $"\"p\":[100,0,0],\"r\":[0,{half(0.11):R},0,{cosHalf(0.11):R}]");
            Assert.Equal(ChannelStatus.Fail, ParityDiff.Transforms(Golden, Run, _tol).Status);
        }

        [Fact]
        public void TransformsAfterTheWindowAreNotCompared()
        {
            Replace(Path.Combine(Run, "transforms.jsonl"), "\"p\":[200,0,0]", "\"p\":[260,0,0]");
            Assert.Equal(ChannelStatus.Pass, ParityDiff.Transforms(Golden, Run, _tol).Status);
        }

        [Fact]
        public void RandomIsExactPerSeed()
        {
            File.WriteAllText(Path.Combine(Golden, "random_42.json"), "{\"seed\":42,\"value\":[0.5,0.25],\"range\":[3,999]}");
            File.WriteAllText(Path.Combine(Run, "random_42.json"), "{\"seed\":42,\"value\":[0.5,0.25],\"range\":[3,999]}");
            Assert.Equal(ChannelStatus.Pass, ParityDiff.Random(Golden, Run).Status);
            File.WriteAllText(Path.Combine(Run, "random_42.json"), "{\"seed\":42,\"value\":[0.5,0.25000003],\"range\":[3,999]}");
            var c = ParityDiff.Random(Golden, Run);
            Assert.Equal(ChannelStatus.Fail, c.Status);
            Assert.Contains("value[1]", c.Detail);
        }

        static byte[] Image(int w, int h, Func<int, int, byte> f)
        {
            var rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = (y * w + x) * 4;
                    rgba[i] = rgba[i + 1] = rgba[i + 2] = f(x, y); rgba[i + 3] = 255;
                }
            return rgba;
        }

        void Frame(string dir, string name, byte[] rgba, int w, int h)
        {
            var path = Path.Combine(dir, "frames", name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            CosmicShore.Client.MiniPng.Write(path, rgba, w, h, flipY: false);
        }

        [Fact]
        public void PngRoundTripsThroughTheDecoder()
        {
            var src = Image(37, 21, (x, y) => (byte)(x * 7 + y * 3));
            Frame(Golden, "a.png", src, 37, 21);
            var img = Png.Decode(File.ReadAllBytes(Path.Combine(Golden, "frames", "a.png")));
            Assert.Equal(37, img.Width);
            Assert.Equal(21, img.Height);
            Assert.Equal(src, img.Rgba);
        }

        [Fact]
        public void SsimSeparatesSameFromDifferentAndMasksRegions()
        {
            var a = Image(64, 64, (x, y) => (byte)((x / 8 + y / 8) % 2 == 0 ? 40 : 200));
            var b = Image(64, 64, (x, y) => (byte)(x < 32 ? ((x / 8 + y / 8) % 2 == 0 ? 40 : 200) : 128));
            Frame(Golden, "f00000.png", a, 64, 64);
            Frame(Run, "f00000.png", a, 64, 64);
            Frame(Golden, "ui/menu.png", a, 64, 64);
            Frame(Run, "ui/menu.png", a, 64, 64);
            var same = ParityDiff.Frames(Golden, Run, _tol);
            Assert.Equal(ChannelStatus.Pass, same.Status);
            Assert.Contains("1.0000", same.Detail);

            Frame(Run, "f00000.png", b, 64, 64);
            var diff = ParityDiff.Frames(Golden, Run, _tol);
            Assert.Equal(ChannelStatus.Fail, diff.Status);
            Assert.Contains("f00000.png", diff.Detail);

            // Masking the right half (particles, until C2) makes the frames equal again.
            File.WriteAllText(Path.Combine(Golden, "frames", "masks.json"), "{\"f00000.png\": [[32,0,32,64]]}");
            Assert.Equal(ChannelStatus.Pass, ParityDiff.Frames(Golden, Run, _tol).Status);
        }

        [Fact]
        public void TolerancesLoadFromTheRepoFile()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "../../../../../parity/tolerances.json");
            Assert.True(File.Exists(path), path);
            var t = Tolerances.Load(path);
            Assert.Equal(0.04, t.EventTimeSeconds);
            Assert.Equal(1e-4, t.PositionRelative);
            Assert.Equal(0.1, t.RotationDegrees);
            Assert.Equal(10, t.TransformWindowSeconds);
            Assert.Equal(0.97, t.SsimGameplay);
            Assert.Equal(0.98, t.SsimUi);
            Assert.Equal(1.0, t.UiRectPixels);
            Assert.Equal(0.5, t.UiFontSize);
        }

        // ---- ui: rect dumps (C5) --------------------------------------------------------------------

        const string UiDump =
            "{\"path\":\"Canvas\",\"x0\":0,\"y0\":0,\"x1\":1920,\"y1\":1080,\"kind\":\"none\"}
" +
            "{\"path\":\"Canvas/Title\",\"x0\":760,\"y0\":900,\"x1\":1160,\"y1\":980,\"kind\":\"text\",\"alpha\":1,\"text\":\"COSMIC SHORE\",\"fontSize\":48,\"overflow\":false,\"lines\":1}
" +
            "{\"path\":\"Canvas/Item#1\",\"x0\":10,\"y0\":10,\"x1\":110,\"y1\":60,\"kind\":\"image\",\"alpha\":1}
";

        void WriteUi(string dir, string body)
        {
            Directory.CreateDirectory(Path.Combine(dir, "ui"));
            File.WriteAllText(Path.Combine(dir, "ui", "menu-home_1920x1080.jsonl"), body);
        }

        [Fact]
        public void UiIdenticalDumpsPassAndNoGoldenIsMissing()
        {
            Assert.Equal(ChannelStatus.Missing, ParityDiff.Ui(Golden, Run, _tol).Status);
            WriteUi(Golden, UiDump); WriteUi(Run, UiDump);
            var c = ParityDiff.Ui(Golden, Run, _tol);
            Assert.Equal(ChannelStatus.Pass, c.Status);
            Assert.Contains("3 rects", c.Detail);
        }

        [Fact]
        public void UiRectBarIsOnePixel()
        {
            WriteUi(Golden, UiDump);
            WriteUi(Run, UiDump.Replace("\"x1\":1160", "\"x1\":1161"));
            Assert.Equal(ChannelStatus.Pass, ParityDiff.Ui(Golden, Run, _tol).Status);
            WriteUi(Run, UiDump.Replace("\"x1\":1160", "\"x1\":1161.5"));
            var c = ParityDiff.Ui(Golden, Run, _tol);
            Assert.Equal(ChannelStatus.Fail, c.Status);
            Assert.Contains("Canvas/Title rect off by 1.5 px", c.Detail);
        }

        [Fact]
        public void UiPlantedTextAndMembershipDifferencesFail()
        {
            WriteUi(Golden, UiDump);
            WriteUi(Run, UiDump.Replace("\"overflow\":false", "\"overflow\":true"));
            Assert.Contains("overflow", ParityDiff.Ui(Golden, Run, _tol).Detail);
            WriteUi(Run, UiDump.Replace("\"fontSize\":48", "\"fontSize\":44"));
            Assert.Contains("font size golden 48 vs 44", ParityDiff.Ui(Golden, Run, _tol).Detail);
            WriteUi(Run, UiDump.Replace("Canvas/Item#1", "Canvas/Item"));
            Assert.Contains("Canvas/Item#1 is active in the golden, not in the run", ParityDiff.Ui(Golden, Run, _tol).Detail);
            WriteUi(Run, UiDump + "{\"path\":\"Canvas/Extra\",\"x0\":0,\"y0\":0,\"x1\":1,\"y1\":1,\"kind\":\"none\"}
");
            Assert.Contains("Canvas/Extra is active in the run, not in the golden", ParityDiff.Ui(Golden, Run, _tol).Detail);
        }
    }
}
