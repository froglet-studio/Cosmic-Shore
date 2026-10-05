// Serialization schema of the project's types, for Tools/Build/check_generated_assets.py.
//
//   Schema <out.json> <Assembly-CSharp.rsp> [<other project assembly>.rsp ...]
//
// Builds the same compilation the refcompile build hands csc (errors are irrelevant here: Roslyn
// still binds every declaration) and writes, for every class/struct/enum declared in source or in a
// project/package assembly compiled by unity_refcompile: its base chain, declaring files, and the
// fields Unity would serialize (Unity's rules: instance, non-const, non-readonly; public without
// [NonSerialized], or [SerializeField]/[SerializeReference]; auto-properties with
// [field: SerializeField] serialize as <Name>k__BackingField), with [FormerlySerializedAs] names.
// Assets are written and read by the EDITOR, so the field set is the union of a player parse and a
// UNITY_EDITOR parse (e.g. FMOD's EventReference.Path exists only in the editor).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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

    static string Name(ITypeSymbol t) => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");

    static bool Has(ISymbol s, string attr) =>
        s.GetAttributes().Any(a => a.AttributeClass != null && (a.AttributeClass.Name == attr || a.AttributeClass.Name == attr + "Attribute"));

    static object TypeInfo(ITypeSymbol t)
    {
        bool array = false;
        if (t is IArrayTypeSymbol at) { array = true; t = at.ElementType; }
        else if (t is INamedTypeSymbol nt && nt.IsGenericType && nt.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.List<T>")
        { array = true; t = nt.TypeArguments[0]; }
        return new { type = Name(t), array };
    }

    static int Main(string[] args)
    {
        var types = new Dictionary<string, Dictionary<string, object>>();
        var fieldSets = new Dictionary<string, Dictionary<string, object>>();
        var fileSets = new Dictionary<string, HashSet<string>>();
        for (int ri = 1; ri < args.Length; ri++)
        {
        var rsp = args[ri];
        var refs = new List<string>(); var files = new List<string>(); var defines = new List<string>();
        foreach (var tok in Tokens(rsp))
        {
            if (tok.StartsWith("-r:")) refs.Add(tok.Substring(3));
            else if (tok.StartsWith("-define:")) defines.AddRange(tok.Substring(8).Split(';', StringSplitOptions.RemoveEmptyEntries));
            else if (!tok.StartsWith("-")) files.Add(tok);
        }
        foreach (var defs in new[] { defines, defines.Concat(new[] { "UNITY_EDITOR", "UNITY_EDITOR_64", "UNITY_EDITOR_WIN" }).ToList() })
        {
        var po = new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: defs);
        var trees = files.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), po, f)).ToList();
        var opts = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true)
            .WithMetadataImportOptions(MetadataImportOptions.All);
        var comp = CSharpCompilation.Create("Schema", trees, refs.Select(r => (MetadataReference)MetadataReference.CreateFromFile(r)), opts);

        // project + package assemblies (everything unity_refcompile compiled), not the engine/BCL
        var outRoot = Path.GetFullPath(rsp);
        while (Path.GetFileName(outRoot) != "unity_refcompile_out" && Path.GetDirectoryName(outRoot) != null)
            outRoot = Path.GetDirectoryName(outRoot)!;
        var wanted = new List<INamespaceSymbol> { comp.Assembly.GlobalNamespace };
        if (ri == 1)
        foreach (var r in comp.References)
            if (r is PortableExecutableReference pe && pe.FilePath != null && Path.GetFullPath(pe.FilePath).StartsWith(outRoot)
                && comp.GetAssemblyOrModuleSymbol(r) is IAssemblySymbol asm)
                wanted.Add(asm.GlobalNamespace);
        // UnityEngine.UI / TMP live in the engine reference set but carry serializable project-facing types
        if (ri == 1)
        foreach (var r in comp.References)
            if (r is PortableExecutableReference pe && pe.FilePath != null
                && (pe.FilePath.EndsWith("UnityEngine.UI.dll") || pe.FilePath.EndsWith("Unity.TextMeshPro.dll"))
                && comp.GetAssemblyOrModuleSymbol(r) is IAssemblySymbol asm)
                wanted.Add(asm.GlobalNamespace);

        void Walk(INamespaceOrTypeSymbol ns)
        {
            foreach (var m in ns.GetMembers())
            {
                if (m is INamespaceSymbol n) { Walk(n); continue; }
                if (m is not INamedTypeSymbol t) continue;
                Emit(t);
                Walk(t);
            }
        }
        void Emit(INamedTypeSymbol t)
        {
            var key = Name(t.OriginalDefinition);
            var chain = new List<string>();
            for (var b = t.BaseType; b != null; b = b.BaseType) chain.Add(Name(b.OriginalDefinition));
            var fields = new List<object>();
            var enumMembers = new Dictionary<string, long>();
            if (t.TypeKind == TypeKind.Enum)
            {
                foreach (var f in t.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue))
                    enumMembers[f.Name] = Convert.ToInt64(f.ConstantValue);
            }
            else
            {
                foreach (var f in t.GetMembers().OfType<IFieldSymbol>())
                {
                    if (f.IsStatic || f.IsConst || f.IsReadOnly) continue;
                    bool serRef = Has(f, "SerializeReference");
                    bool ser = serRef || Has(f, "SerializeField") || (f.DeclaredAccessibility == Accessibility.Public && !Has(f, "NonSerialized"));
                    if (!ser) continue;
                    var former = f.GetAttributes().Where(a => a.AttributeClass?.Name == "FormerlySerializedAsAttribute")
                        .Select(a => a.ConstructorArguments.FirstOrDefault().Value as string).Where(x => x != null).ToList();
                    fields.Add(new Dictionary<string, object> { ["name"] = f.Name, ["info"] = TypeInfo(f.Type), ["serializeReference"] = serRef, ["former"] = former });
                }
            }
            if (!fieldSets.TryGetValue(key, out var fs)) fieldSets[key] = fs = new Dictionary<string, object>();
            foreach (Dictionary<string, object> f in fields) fs.TryAdd((string)f["name"], f);
            if (!fileSets.TryGetValue(key, out var fl)) fileSets[key] = fl = new HashSet<string>();
            foreach (var l in t.Locations.Where(l => l.IsInSource)) fl.Add(l.SourceTree!.FilePath);
            if (types.ContainsKey(key)) return;
            types[key] = new Dictionary<string, object>
            {
                ["name"] = t.Name,
                ["kind"] = t.TypeKind.ToString(),
                ["isAbstract"] = t.IsAbstract,
                ["isGeneric"] = t.IsGenericType,
                ["serializable"] = t.TypeKind == TypeKind.Enum || Has(t, "Serializable") || t.IsSerializable,
                ["flags"] = Has(t, "Flags"),
                ["bases"] = chain,
                ["files"] = t.Locations.Where(l => l.IsInSource).Select(l => l.SourceTree!.FilePath).Distinct().ToList(),
                ["enumMembers"] = enumMembers,
            };
        }
        foreach (var ns in wanted) Walk(ns);
        }
        }
        foreach (var kv in types)
        {
            kv.Value["fields"] = fieldSets[kv.Key].Values.ToList();
            kv.Value["files"] = fileSets[kv.Key].OrderBy(x => x).ToList();
        }
        File.WriteAllText(args[0], JsonSerializer.Serialize(types));
        Console.WriteLine($"schema: {types.Count} types");
        return 0;
    }
}
