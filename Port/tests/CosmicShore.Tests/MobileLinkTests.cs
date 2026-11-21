using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CosmicShore.Tests;

/// <summary>
/// The phone build (CosmicShore.Mobile) compiles the desktop player's core by linking its files.
/// When a linked file starts using another Player file, that file must be linked too, or every
/// phone build stops compiling - and only the on-demand iOS/Android builds would notice (it
/// happened: PlayerWindow gained the control port and the run report, and the iOS build broke).
/// This catches it in an ordinary test run, with no phone SDK installed.
/// </summary>
public class MobileLinkTests
{
    [Fact]
    public void EveryPlayerFileTheSharedCoreUses_IsLinkedIntoThePhoneBuild()
    {
        if (ContentYamlTests.ProjectRoot == null) return;
        var src = Path.Combine(ContentYamlTests.ProjectRoot, "Port", "src");
        var player = Path.Combine(src, "CosmicShore.Player");
        var csproj = File.ReadAllText(Path.Combine(src, "CosmicShore.Mobile", "CosmicShore.Mobile.csproj"));
        var linked = Regex.Matches(csproj, @"CosmicShore\.Player/([\w.]+\.cs)").Select(m => m.Groups[1].Value).ToHashSet();
        Assert.Contains("PlayerWindow.cs", linked);

        // One type per file, named after it (TrainingHost.Eval.cs is part of TrainingHost).
        // Program.cs is the desktop entry point; phones have their own.
        var files = Directory.GetFiles(player, "*.cs").Select(Path.GetFileName).Where(f => f != "Program.cs").ToList();
        static string TypeOf(string file) => file!.Split('.')[0];

        var missing = new List<string>();
        foreach (var file in linked)
        {
            var code = Regex.Replace(File.ReadAllText(Path.Combine(player, file)), @"//[^\n]*|/\*.*?\*/", "", RegexOptions.Singleline);
            foreach (var other in files.Where(o => !linked.Contains(o!) && TypeOf(o!) != TypeOf(file)))
                if (Regex.IsMatch(code, $@"\b{Regex.Escape(TypeOf(other!))}\b"))
                    missing.Add($"{file} uses {TypeOf(other!)} ({other})");
        }
        Assert.True(missing.Count == 0, "Link these into CosmicShore.Mobile.csproj: " + string.Join("; ", missing));
    }
}
