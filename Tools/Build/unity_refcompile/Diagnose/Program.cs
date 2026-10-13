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
//
// An error that is not itself an unresolved name (see Roots) is suffixed with the named error types its
// expression involves: " [unresolved types: A, B]". `out var v` from an unknown TryGetValue -> CS0165,
// `unknown.Count > 0` -> CS0019: the build files such a cascade with its root instead of gating it.
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
        var models = new Dictionary<SyntaxTree, SemanticModel>();
        foreach (var d in comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
                     .OrderBy(d => d.Location.SourceTree?.FilePath).ThenBy(d => d.Location.SourceSpan.Start))
        {
            Console.WriteLine(Format(d) + Unresolved(comp, models, d));
            errors++;
        }
        return errors == 0 && genDiags.Count == 0 ? 0 : 1;
    }

    // Diagnostics that ARE the unresolved name; every other error may be a cascade of one.
    static readonly HashSet<string> Roots = new HashSet<string> { "CS0246", "CS0234", "CS0103", "CS1069", "CS0012", "CS0538" };

    static string Unresolved(Compilation comp, Dictionary<SyntaxTree, SemanticModel> models, Diagnostic d)
    {
        var tree = d.Location.SourceTree;
        if (tree == null || Roots.Contains(d.Id)) return "";
        if (!models.TryGetValue(tree, out var model)) models[tree] = model = comp.GetSemanticModel(tree);
        var node = tree.GetRoot().FindNode(d.Location.SourceSpan, getInnermostNodeForTie: true);
        var scopes = new List<SyntaxNode>();
        if (d.Id == "CS0165" && model.GetSymbolInfo(node).Symbol is ILocalSymbol local)
            // an unassigned local: what failed to assign it is the expression that declares it
            // (`if (unknown.TryGetValue(k, out var v) && int.TryParse(v.Value, out int n)) use(n);`)
            scopes.AddRange(local.DeclaringSyntaxReferences.Select(r => Outermost(r.GetSyntax())));
        else
            // `x.Missing`: the receiver is part of the error
            scopes.Add(node is SimpleNameSyntax && node.Parent is MemberAccessExpressionSyntax ma && ma.Name == node ? ma : node);
        var names = new SortedSet<string>(StringComparer.Ordinal);
        var followed = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        for (int i = 0; i < scopes.Count; i++)
            foreach (var e in scopes[i].DescendantNodesAndSelf().OfType<ExpressionSyntax>())
            {
                var info = model.GetTypeInfo(e);
                // a `var` local inferred from an unresolved expression has an unnamed error type: its
                // root is in the expression that declares it (`var c = unknown.Items; c.Count > 0`)
                if ((Collect(info.Type, names) | Collect(info.ConvertedType, names)) &&
                    model.GetSymbolInfo(e).Symbol is ILocalSymbol l && followed.Add(l))
                    scopes.AddRange(l.DeclaringSyntaxReferences.Select(r => Outermost(r.GetSyntax())));
            }
        return names.Count == 0 ? "" : " [unresolved types: " + string.Join(", ", names) + "]";
    }

    // The largest expression around n that stays inside one statement or member declaration.
    static SyntaxNode Outermost(SyntaxNode n)
    {
        var best = n;
        for (var p = n.Parent; p != null && !(p is StatementSyntax) && !(p is MemberDeclarationSyntax); p = p.Parent)
            if (p is ExpressionSyntax) best = p;
        return best;
    }

    // Adds the named error types in t (type arguments and element types included); true when t holds
    // an error type with no name of its own (`?`, or `var` that could not be inferred).
    static bool Collect(ITypeSymbol t, ISet<string> names)
    {
        switch (t)
        {
            case IErrorTypeSymbol e:
                bool unnamed = e.Name.Length == 0 || e.Name == "var";
                if (!unnamed)
                    names.Add(e.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() + "." + e.Name : e.Name);
                return e.TypeArguments.Aggregate(unnamed, (u, a) => Collect(a, names) | u);
            case INamedTypeSymbol n:
                return n.TypeArguments.Aggregate(false, (u, a) => Collect(a, names) | u);
            case IArrayTypeSymbol a:
                return Collect(a.ElementType, names);
            case IPointerTypeSymbol p:
                return Collect(p.PointedAtType, names);
            default:
                return false;
        }
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
