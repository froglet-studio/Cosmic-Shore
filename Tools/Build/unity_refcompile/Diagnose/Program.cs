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
// An error that is not itself an unresolved name (see Roots) is suffixed with the named error types
// its expression involves: " [unresolved types: A, B]". `out var v` from an unknown TryGetValue ->
// CS0165, `unknown.Count > 0` -> CS0019: the build files such a cascade with its root, not as a gate.
//
// --declarations parses (does not bind) the rsp's sources with its defines and prints what they
// declare: "N\t<namespace>" per namespace and "T\t<namespace>\t<name>" per PUBLIC top-level type
// (no package grants Assets code InternalsVisibleTo). The build reads it for a package assembly that
// failed, and to write unobtainable_declarations.tsv, to tell which project errors stem from its absence.
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
        var models = new Dictionary<SyntaxTree, SemanticModel>();
        foreach (var d in comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
                     .OrderBy(d => d.Location.SourceTree?.FilePath).ThenBy(d => d.Location.SourceSpan.Start))
        {
            Console.WriteLine(Format(d) + Unresolved(comp, models, d));
            errors++;
        }
        return errors == 0 ? 0 : 1;
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
                    BaseTypeDeclarationSyntax t when IsPublic(t.Modifiers) => "T\t" + ns + "\t" + t.Identifier.Text,
                    DelegateDeclarationSyntax d when IsPublic(d.Modifiers) => "T\t" + ns + "\t" + d.Identifier.Text,
                    _ => null,
                };
                if (line != null && seen.Add(line)) Console.WriteLine(line);
            }
        }
        return 0;
    }

    // A partial type is public when any of its parts says so; seen dedups the parts.
    static bool IsPublic(SyntaxTokenList modifiers) => modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));

    static string Format(Diagnostic d)
    {
        var p = d.Location.GetLineSpan();
        var where = d.Location.IsInSource ? $"{p.Path}({p.StartLinePosition.Line + 1},{p.StartLinePosition.Character + 1}): " : "";
        return $"{where}error {d.Id}: {d.GetMessage()}";
    }
}
