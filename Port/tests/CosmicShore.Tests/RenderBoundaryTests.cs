using System.IO;
using System.Linq;

namespace CosmicShore.Tests;

/// <summary>
/// The graphics API stays behind one boundary (architecture review D11, 2026-10-06): GL calls live
/// in CosmicShore.Render, plus the player window's present and read-back. Then a Metal or WebGPU
/// backend replaces one project instead of hunting calls across the engine. The launcher (its own
/// ImGui app) and the legacy Client are outside the game's rendering and exempt.
/// </summary>
public class RenderBoundaryTests
{
    static readonly string[] Allowed =
    {
        "CosmicShore.Render/",
        "CosmicShore.Player/PlayerWindow.cs",
        "CosmicShore.Launcher/",
        "CosmicShore.Client/",
    };

    [Fact]
    public void OnlyTheRenderBoundaryUsesTheGlBinding()
    {
        if (ContentYamlTests.ProjectRoot == null) return;
        var src = Path.Combine(ContentYamlTests.ProjectRoot, "Port", "src");
        var offenders = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(f => (path: Path.GetRelativePath(src, f).Replace('\\', '/'), text: File.ReadAllText(f)))
            .Where(f => f.text.Split('\n').Any(l => l.TrimStart().StartsWith("using Silk.NET.OpenGL")))
            .Select(f => f.path)
            .Where(p => !Allowed.Any(a => a.EndsWith('/') ? p.StartsWith(a) : p == a))
            .ToList();
        Assert.True(offenders.Count == 0, "GL used outside the render boundary - move it into CosmicShore.Render: " + string.Join(", ", offenders));
    }
}
