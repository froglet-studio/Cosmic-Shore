// Full-diagnostics pass for one assembly, from the same .rsp the build hands csc.
//
//   Diagnose <file.rsp> [--source-ref <dependency.rsp>]...
//
// Why it exists: csc stops after the DECLARATION phase when any declaration error exists (e.g. a
// file whose `using` names a package we cannot fetch), so method-body errors in every OTHER file go
// unreported. Compilation.GetDiagnostics() binds every method body regardless, so one unobtainable
// package can no longer hide a real error elsewhere. Source generators (-analyzer:) run first, as in
// csc. Output lines use csc's "path(line,col): error CSxxxx: message" format.
//
// --source-ref: a dependency whose own compile FAILED has no DLL, so the assembly that references it
// would otherwise be bound without it and report every use of its types as missing. Each one given
// is built in memory from its .rsp and referenced as a compilation (Roslyn binds against its source
// symbols, errors and all), so only the main assembly's own errors are reported. List them in
// dependency order: each also references the ones listed before it.
//
// Every compilation is named after its -out: file, as csc names it, so [InternalsVisibleTo] grants
// (Assembly-CSharp -> Assembly-CSharp-Editor) apply here exactly as they do in csc.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

sealed class Loader : IAnalyzerAssemblyLoader
{
    public void AddDependencyLocation(string fullPath) { }
    public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
}

static class Program
{
    static IEnumerable<string> Tokens(string rsp)
    {
        foreach (var raw in File.ReadAllLines(rsp))
        {
            var line = raw.Trim();
            int i = 0;
            while (i < line.Length)
            {
                if (line[i] == ' ') { i++; continue; }
                var sb = new System.Text.StringBuilder();
                bool q = false;
                for (; i < line.Length && (q || line[i] != ' '); i++)
                {
                    if (line[i] == '"') { q = !q; continue; }
                    sb.Append(line[i]);
                }
                yield return sb.ToString();
            }
        }
    }

    static int Main(string[] args)
    {
        var sourceRefs = new List<MetadataReference>();
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] != "--source-ref" || i + 1 >= args.Length)
            {
                Console.Error.WriteLine("usage: Diagnose <file.rsp> [--source-ref <dependency.rsp>]...");
                return 2;
            }
            // a referenced compilation's own errors are its own report's business, not this one's
            sourceRefs.Add(Build(args[++i], sourceRefs.ToList(), out _).ToMetadataReference());
        }
        var comp = Build(args[0], sourceRefs, out var genDiags);
        foreach (var d in genDiags) Console.WriteLine(Format(d));
        int errors = 0;
        foreach (var d in comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
                     .OrderBy(d => d.Location.SourceTree?.FilePath).ThenBy(d => d.Location.SourceSpan.Start))
        {
            Console.WriteLine(Format(d));
            errors++;
        }
        return errors == 0 && genDiags.Count == 0 ? 0 : 1;
    }

    // The compilation one .rsp describes, source generators applied; generator errors come back separately.
    static Compilation Build(string rsp, IEnumerable<MetadataReference> extraRefs, out List<Diagnostic> genErrors)
    {
        var refs = new List<string>(); var files = new List<string>(); var analyzers = new List<string>();
        var defines = new List<string>(); var nowarn = new List<string>(); bool unsafeCode = false;
        string outPath = null;
        foreach (var t in Tokens(rsp))
        {
            if (t.StartsWith("-r:")) refs.Add(t.Substring(3));
            else if (t.StartsWith("-out:")) outPath = t.Substring(5);
            else if (t.StartsWith("-analyzer:")) analyzers.Add(t.Substring(10));
            else if (t.StartsWith("-define:")) defines.AddRange(t.Substring(8).Split(';', StringSplitOptions.RemoveEmptyEntries));
            else if (t.StartsWith("-nowarn:")) nowarn.AddRange(t.Substring(8).Split(',').Select(x => x.StartsWith("CS") ? x : "CS" + x.PadLeft(4, '0')));
            else if (t == "-unsafe") unsafeCode = true;
            else if (!t.StartsWith("-")) files.Add(t);
        }
        var po = new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: defines);
        var trees = files.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), po, f)).ToList();
        var opts = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: unsafeCode,
            specificDiagnosticOptions: nowarn.Select(n => new KeyValuePair<string, ReportDiagnostic>(n, ReportDiagnostic.Suppress)));
        var name = outPath != null ? Path.GetFileNameWithoutExtension(outPath) : "Diagnose";
        Compilation comp = CSharpCompilation.Create(name, trees,
            refs.Select(r => (MetadataReference)MetadataReference.CreateFromFile(r)).Concat(extraRefs), opts);
        genErrors = new List<Diagnostic>();
        var loader = new Loader();
        var gens = analyzers.SelectMany(a => new AnalyzerFileReference(a, loader).GetGenerators(LanguageNames.CSharp)).ToList();
        if (gens.Count > 0)
        {
            var driver = CSharpGeneratorDriver.Create(gens, parseOptions: po);
            driver.RunGeneratorsAndUpdateCompilation(comp, out comp, out var genDiags);
            genErrors.AddRange(genDiags.Where(d => d.Severity == DiagnosticSeverity.Error));
        }
        return comp;
    }

    static string Format(Diagnostic d)
    {
        var p = d.Location.GetLineSpan();
        var where = d.Location.IsInSource ? $"{p.Path}({p.StartLinePosition.Line + 1},{p.StartLinePosition.Character + 1}): " : "";
        return $"{where}error {d.Id}: {d.GetMessage()}";
    }
}
