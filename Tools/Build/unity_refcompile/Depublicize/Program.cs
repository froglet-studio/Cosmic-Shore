// Restores member accessibility on "publicized" Unity reference assemblies (every member made
// public by the re-packer), so the ref-compile neither accepts code that touches Unity internals
// nor rejects legal `protected override`s (CS0507).
//
//   Depublicize <inDir> <outDir> <oracleDll[;oracleDll...]> <overridesFile>
//
// 1. Oracle: for every type/method/field that also exists in a non-publicized Unity DLL (the
//    monolithic UnityEngine.dll of Unity 2021.1, from nuget Unity3D.SDK), matched by type full name
//    whatever module it lives in now, copy that member's original accessibility (accessibility of
//    long-lived engine API does not change between 2021 and 6000.x in practice).
// 2. Overrides file: lines "Assembly|Namespace.Type|Member-or-<type>|public|family|famorassem|assembly|private",
//    applied after the oracle. Two kinds, both learned from packages that compile cleanly in Unity:
//    * CS0507 - a package overrides the member as `protected`: the member is protected (members new
//      since 2021.1, and UI/TMP, which have no oracle);
//    * CS0122 - a package uses a member/type the 2021.1 oracle calls internal: it was made public
//      since 2021.1 (oracle drift in the other direction).
//    A property name also matches its get_/set_ accessors.
// Members found in neither stay public: that is the residual blind spot, stated in README.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

static class Program
{
    static string Sig(MethodDefinition m) =>
        m.Name + "(" + string.Join(",", m.Parameters.Select(p => p.ParameterType.FullName)) + ")`" + m.GenericParameters.Count;

    static int Main(string[] args)
    {
        if (args[0] == "--index") return Index(args[1], args[2]);
        string inDir = args[0], outDir = args[1], overridesFile = args[3];
        var oracleTypes = new Dictionary<string, TypeDefinition>();
        foreach (var o in args[2].Split(';'))
            foreach (var t in AllTypes(AssemblyDefinition.ReadAssembly(o, new ReaderParameters { InMemory = true }).MainModule.Types))
                oracleTypes[t.FullName] = t;
        Directory.CreateDirectory(outDir);
        var overrides = new Dictionary<string, string>();
        if (File.Exists(overridesFile))
            foreach (var raw in File.ReadAllLines(overridesFile))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var p = line.Split('|');
                overrides[p[0] + "|" + p[1] + "|" + p[2]] = p[3];
            }

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(inDir);
        int restored = 0, overridden = 0, files = 0;
        foreach (var path in Directory.GetFiles(inDir, "*.dll"))
        {
            var asm = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver, InMemory = true });
            var name = asm.Name.Name;

            foreach (var t in AllTypes(asm.MainModule.Types))
            {
                if (oracleTypes.TryGetValue(t.FullName, out var ot))
                {
                    var vis = ot.Attributes & TypeAttributes.VisibilityMask;
                    if ((t.Attributes & TypeAttributes.VisibilityMask) != vis)
                    {
                        t.Attributes = (t.Attributes & ~TypeAttributes.VisibilityMask) | vis;
                        restored++;
                    }
                    var om = new Dictionary<string, MethodDefinition>();
                    foreach (var m in ot.Methods) om[Sig(m)] = m;
                    foreach (var m in t.Methods)
                        if (om.TryGetValue(Sig(m), out var o))
                        {
                            var acc = o.Attributes & MethodAttributes.MemberAccessMask;
                            if ((m.Attributes & MethodAttributes.MemberAccessMask) != acc)
                            {
                                m.Attributes = (m.Attributes & ~MethodAttributes.MemberAccessMask) | acc;
                                restored++;
                            }
                        }
                    var of = ot.Fields.ToDictionary(f => f.Name, f => f);
                    foreach (var f in t.Fields)
                        if (of.TryGetValue(f.Name, out var o))
                        {
                            var acc = o.Attributes & FieldAttributes.FieldAccessMask;
                            if ((f.Attributes & FieldAttributes.FieldAccessMask) != acc)
                            {
                                f.Attributes = (f.Attributes & ~FieldAttributes.FieldAccessMask) | acc;
                                restored++;
                            }
                        }
                }
                var prefix = name + "|" + t.FullName + "|";
                if (overrides.TryGetValue(prefix + "<type>", out var tacc))
                {
                    var vis = tacc == "public"
                        ? (t.IsNested ? TypeAttributes.NestedPublic : TypeAttributes.Public)
                        : (t.IsNested ? TypeAttributes.NestedAssembly : TypeAttributes.NotPublic);
                    t.Attributes = (t.Attributes & ~TypeAttributes.VisibilityMask) | vis;
                    overridden++;
                }
                foreach (var m in t.Methods)
                {
                    string acc = null;
                    foreach (var n in new[] { m.Name, StripAccessor(m.Name) })
                        if (n != null && overrides.TryGetValue(prefix + n, out acc)) break;
                    if (acc == null) continue;
                    m.Attributes = (m.Attributes & ~MethodAttributes.MemberAccessMask) | MethodAccess(acc);
                    overridden++;
                }
                foreach (var f in t.Fields)
                {
                    if (!overrides.TryGetValue(prefix + f.Name, out var acc)) continue;
                    var fa = acc switch
                    {
                        "family" => FieldAttributes.Family,
                        "famorassem" => FieldAttributes.FamORAssem,
                        "assembly" => FieldAttributes.Assembly,
                        "private" => FieldAttributes.Private,
                        _ => FieldAttributes.Public,
                    };
                    f.Attributes = (f.Attributes & ~FieldAttributes.FieldAccessMask) | fa;
                    overridden++;
                }
            }
            var outName = name + ".dll";
            asm.Write(Path.Combine(outDir, outName));
            files++;
        }
        Console.WriteLine($"[depublicize] {files} assemblies, {restored} accessibilities restored from the 2021.1 oracle, {overridden} from overrides");
        return 0;
    }

    // --index <dir> <out.tsv>: assembly, type full name, C#-style short name, member - so the build
    // can map a diagnostic's "Type.Member" back to the definition to override.
    static int Index(string dir, string outPath)
    {
        using var w = new StreamWriter(outPath);
        foreach (var path in Directory.GetFiles(dir, "*.dll"))
        {
            var asm = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { InMemory = true });
            foreach (var t in AllTypes(asm.MainModule.Types))
            {
                var shortName = t.Name.Split('`')[0];
                for (var d = t.DeclaringType; d != null; d = d.DeclaringType) shortName = d.Name.Split('`')[0] + "." + shortName;
                w.WriteLine($"{asm.Name.Name}\t{t.FullName}\t{shortName}\t<type>");
                foreach (var m in t.Methods) w.WriteLine($"{asm.Name.Name}\t{t.FullName}\t{shortName}\t{StripAccessor(m.Name) ?? m.Name}");
                foreach (var f in t.Fields) w.WriteLine($"{asm.Name.Name}\t{t.FullName}\t{shortName}\t{f.Name}");
            }
        }
        return 0;
    }

    static string StripAccessor(string n)
    {
        foreach (var p in new[] { "get_", "set_", "add_", "remove_" })
            if (n.StartsWith(p)) return n.Substring(p.Length);
        return null;
    }

    static MethodAttributes MethodAccess(string acc) => acc switch
    {
        "family" => MethodAttributes.Family,
        "famorassem" => MethodAttributes.FamORAssem,
        "assembly" => MethodAttributes.Assembly,
        "private" => MethodAttributes.Private,
        _ => MethodAttributes.Public,
    };

    static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types)
    {
        foreach (var t in types)
        {
            yield return t;
            foreach (var n in AllTypes(t.NestedTypes)) yield return n;
        }
    }
}
