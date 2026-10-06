// Full-diagnostics pass for one assembly, from the same .rsp the build hands csc.
//
//   Diagnose <file.rsp>
//   Diagnose --declarations <file.rsp>
//
// Why it exists: csc stops after the DECLARATION phase when any declaration error exists (e.g. a
// file whose `using` names a package we cannot fetch), so method-body errors in every OTHER file go
// unreported. Compilation.GetDiagnostics() binds every method body regardless, so one unobtainable
// package can no longer hide a real error elsewhere. Source generators (-analyzer:) run first, as in
// csc. Output lines use csc's "path(line,col): error CSxxxx: message" format.
//
// --declarations parses (does not bind) the rsp's sources with its defines and prints what they
// declare: "N\t<namespace>" per namespace and "T\t<namespace>\t<name>" per top-level type. The build
// reads it for a package assembly that failed, to tell which project errors can stem from its absence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        if (args[0] == "--declarations") return Declarations(args[1]);
        var refs = new List<string>(); var files = new List<string>(); var analyzers = new List<string>();
        var defines = new List<string>(); var nowarn = new List<string>(); bool unsafeCode = false;
        foreach (var t in Tokens(args[0]))
        {
            if (t.StartsWith("-r:")) refs.Add(t.Substring(3));
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
        Compilation comp = CSharpCompilation.Create("Diagnose", trees,
            refs.Select(r => (MetadataReference)MetadataReference.CreateFromFile(r)), opts);
        var loader = new Loader();
        var gens = analyzers.SelectMany(a => new AnalyzerFileReference(a, loader).GetGenerators(LanguageNames.CSharp)).ToList();
        if (gens.Count > 0)
        {
            var driver = CSharpGeneratorDriver.Create(gens, parseOptions: po);
            driver.RunGeneratorsAndUpdateCompilation(comp, out comp, out var genDiags);
            foreach (var d in genDiags.Where(d => d.Severity == DiagnosticSeverity.Error)) Console.WriteLine(Format(d));
        }
        int errors = 0;
        foreach (var d in comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
                     .OrderBy(d => d.Location.SourceTree?.FilePath).ThenBy(d => d.Location.SourceSpan.Start))
        {
            Console.WriteLine(Format(d));
            errors++;
        }
        return errors == 0 ? 0 : 1;
    }

    static int Declarations(string rsp)
    {
        var defines = new List<string>(); var files = new List<string>();
        foreach (var t in Tokens(rsp))
        {
            if (t.StartsWith("-define:")) defines.AddRange(t.Substring(8).Split(';', StringSplitOptions.RemoveEmptyEntries));
            else if (!t.StartsWith("-")) files.Add(t);
        }
        var po = new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: defines);
        var seen = new HashSet<string>();
        foreach (var f in files)
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(f), po, f).GetRoot();
            foreach (var node in root.DescendantNodes(n => n is CompilationUnitSyntax || n is BaseNamespaceDeclarationSyntax))
            {
                var ns = string.Join(".", node.AncestorsAndSelf().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString()));
                string line = node switch
                {
                    BaseNamespaceDeclarationSyntax _ => "N\t" + ns,
                    BaseTypeDeclarationSyntax t => "T\t" + ns + "\t" + t.Identifier.Text,
                    DelegateDeclarationSyntax d => "T\t" + ns + "\t" + d.Identifier.Text,
                    _ => null,
                };
                if (line != null && seen.Add(line)) Console.WriteLine(line);
            }
        }
        return 0;
    }

    static string Format(Diagnostic d)
    {
        var p = d.Location.GetLineSpan();
        var where = d.Location.IsInSource ? $"{p.Path}({p.StartLinePosition.Line + 1},{p.StartLinePosition.Character + 1}): " : "";
        return $"{where}error {d.Id}: {d.GetMessage()}";
    }
}
