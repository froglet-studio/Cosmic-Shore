using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CosmicShore.Player
{
    /// <summary>
    /// Diagnostic (COSMIC_SHORE_LEAKSCAN=1): counts every static collection in the process and
    /// reports the ones that keep growing between samples. A long headless run reloads the same
    /// scene hundreds of times, so anything a scene reload leaves reachable shows up here as a
    /// static whose count climbs by a fixed amount per match.
    /// </summary>
    static class LeakScan
    {
        public static readonly bool Enabled = Environment.GetEnvironmentVariable("COSMIC_SHORE_LEAKSCAN") == "1";
        static List<(FieldInfo field, string name)> s_fields;
        static Dictionary<string, long> s_first;
        static Dictionary<string, long> s_last;
        static int s_samples;

        public static void Sample()
        {
            if (!Enabled) return;
            s_fields ??= Discover();
            var now = new Dictionary<string, long>();
            foreach (var (f, name) in s_fields)
            {
                object v;
                try { v = f.GetValue(null); } catch { continue; }
                long n = v switch
                {
                    null => -1,
                    ICollection c => c.Count,
                    _ => v.GetType().GetProperty("Count")?.GetValue(v) is int k ? k : -1,
                };
                if (n < 0) continue;
                now[name] = n;
            }
            s_samples++;
            if (s_first == null) { s_first = now; s_last = now; return; }
            var growers = now.Where(kv => s_first.TryGetValue(kv.Key, out var a) && kv.Value > a + 8
                                          && s_last.TryGetValue(kv.Key, out var b) && kv.Value >= b)
                             .OrderByDescending(kv => kv.Value - s_first[kv.Key]).Take(15).ToList();
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Console.WriteLine($"[leakscan] sample {s_samples}: live heap {GC.GetTotalMemory(false) >> 20} MB, {growers.Count} growing statics");
            foreach (var kv in growers)
                Console.WriteLine($"[leakscan]   {kv.Key}: {s_first[kv.Key]} -> {kv.Value}");
            s_last = now;
            if (s_samples == 6 || s_samples == 16) CensusByRoot();
            if (s_samples == 10) ChainToDestroyedPrism();
        }

        /// <summary>
        /// Walks the object graph from every static root and reports, per root, how many
        /// DESTROYED GameObjects it still reaches, with one example path. A destroyed object
        /// reachable from a static is exactly what a scene reload was supposed to free.
        /// </summary>
        static void FindRetainers()
        {
            // Pass 1: everything reachable WITHOUT crossing a destroyed object is live mass.
            // Pass 2: from each destroyed edge, whatever is reachable only through it is mass a
            // scene reload failed to free; it is attributed to the live holder of that edge.
            var fieldCache = new Dictionary<Type, FieldInfo[]>();
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var edges = new List<(object target, string holder)>();
            long liveBytes = 0;
            foreach (var (f, name) in DiscoverAll())
            {
                object root;
                try { root = f.GetValue(null); } catch { continue; }
                if (root != null) Walk(root, name, seen, fieldCache, edges, ref liveBytes, stopAtDestroyed: true);
            }
            var perHolder = new Dictionary<string, (long bytes, int edges)>();
            foreach (var (target, holder) in edges)
            {
                long bytes = 0;
                var none = new List<(object, string)>();
                Walk(target, holder, seen, fieldCache, none, ref bytes, stopAtDestroyed: false);
                string key = System.Text.RegularExpressions.Regex.Replace(holder, @"\[\d+\]", "[*]");
                perHolder.TryGetValue(key, out var e);
                perHolder[key] = (e.bytes + bytes, e.edges + 1);
            }
            Console.WriteLine($"[leakscan] live ~{liveBytes >> 20} MB; retained through destroyed objects ~{perHolder.Values.Sum(v => v.bytes) >> 20} MB:");
            foreach (var kv in perHolder.OrderByDescending(kv => kv.Value.bytes).Take(15))
                Console.WriteLine($"[leakscan]   {kv.Value.bytes >> 10,8} KB {kv.Value.edges,6} edges  {kv.Key}");
        }

        /// <summary>BFS from static roots, remembering each engine object's discoverer, then print the chain to destroyed prisms.</summary>
        static void ChainToDestroyedPrism()
        {
            var prismType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CosmicShore.Gameplay.Prism")).FirstOrDefault(t => t != null);
            var fieldCache = new Dictionary<Type, FieldInfo[]>();
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var parents = new Dictionary<object, (object parent, string via)>(ReferenceEqualityComparer.Instance);
            var found = new List<object>();
            int destroyedPrisms = 0;
            var queue = new Queue<(object o, object lastEo, string via)>();
            foreach (var (f, name) in DiscoverAll())
            {
                object root;
                try { root = f.GetValue(null); } catch { continue; }
                if (root != null) queue.Enqueue((root, name, ""));
            }
            while (queue.Count > 0)
            {
                var (o, lastEo, via) = queue.Dequeue();
                if (!seen.Add(o)) continue;
                if (o is CosmicShore.Engine.Object eo)
                {
                    parents[o] = (lastEo, via);
                    if (prismType != null && prismType.IsInstanceOfType(o) && (eo.IsDestroyed || ((CosmicShore.Engine.Component)o).gameObject?.IsDestroyed == true))
                    {
                        destroyedPrisms++;
                        found.Add(o);
                        continue;
                    }
                    lastEo = o; via = "";
                }
                var t = o.GetType();
                if (t.IsArray)
                {
                    var et = t.GetElementType()!;
                    if (et.IsPrimitive || et.IsEnum || (et.IsValueType && !HasRefs(et))) continue;
                    foreach (var item in (Array)o)
                        if (item != null && item is not string && item is not Type) queue.Enqueue((item, lastEo, via.Length < 200 ? via + "[]" : via));
                    continue;
                }
                if (!fieldCache.TryGetValue(t, out var fields))
                {
                    var list = new List<FieldInfo>();
                    for (var bt = t; bt != null && bt != typeof(object); bt = bt.BaseType)
                        foreach (var fi in bt.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                            if (!fi.FieldType.IsPrimitive && !fi.FieldType.IsPointer && !fi.FieldType.IsEnum && fi.FieldType != typeof(string)
                                && !(fi.FieldType.IsValueType && !HasRefs(fi.FieldType)))
                                list.Add(fi);
                    fieldCache[t] = fields = list.ToArray();
                }
                foreach (var fi in fields)
                {
                    object v;
                    try { v = fi.GetValue(o); } catch { continue; }
                    if (v == null || v is string || v is Type || v is MemberInfo || v is Assembly || v.GetType().IsPrimitive) continue;
                    queue.Enqueue((v, lastEo, via.Length < 200 ? via + "." + fi.Name : via));
                }
            }
            // Attribute every destroyed prism to the static root and the first live holder on its
            // chain, and print one example chain per holder.
            var byHolder = new Dictionary<string, (int count, string chain)>();
            foreach (var p in found)
            {
                var chain = new List<string>();
                string holder = null;
                object cur = p;
                for (int k = 0; k < 60 && cur != null; k++)
                {
                    if (cur is string rootName) { chain.Add("static " + rootName); holder ??= rootName; break; }
                    bool dead = cur is CosmicShore.Engine.Object d && (d.IsDestroyed || (cur as CosmicShore.Engine.Component)?.gameObject?.IsDestroyed == true);
                    var label = cur is CosmicShore.Engine.Component c ? $"{c.GetType().Name}@'{c.gameObject?.name}'{(dead ? "(destroyed)" : "")}"
                              : cur is CosmicShore.Engine.GameObject g ? $"GO '{g.name}'{(dead ? "(destroyed)" : "")}" : cur.GetType().Name;
                    if (!parents.TryGetValue(cur, out var pv)) { chain.Add(label + " <- ?"); break; }
                    // The first LIVE engine object up the chain is what holds the dead ones.
                    if (holder == null && !dead && cur is CosmicShore.Engine.Object) holder = $"{label} .{pv.via}";
                    chain.Add($"{label} <-{pv.via}- ");
                    cur = pv.parent;
                }
                holder ??= "?";
                string key = System.Text.RegularExpressions.Regex.Replace(holder, @"'[^']*\(Clone\)'", "'*(Clone)'");
                byHolder.TryGetValue(key, out var e);
                byHolder[key] = (e.count + 1, e.chain ?? string.Join("", chain));
            }
            Console.WriteLine($"[leakscan] {destroyedPrisms} destroyed prisms reachable, by first live holder:");
            foreach (var kv in byHolder.OrderByDescending(kv => kv.Value.count).Take(10))
            {
                Console.WriteLine($"[leakscan]   {kv.Value.count,7}  {kv.Key}");
                Console.WriteLine($"[leakscan]            {(kv.Value.chain.Length > 700 ? kv.Value.chain[..700] + "..." : kv.Value.chain)}");
            }
        }

        static Type s_prismType;
        static void PrismsByRoot()
        {
            s_prismType ??= AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CosmicShore.Gameplay.Prism")).FirstOrDefault(t => t != null);
            var scene = CosmicShore.Engine.GameLoop.Current?.Scene;
            if (scene == null || s_prismType == null) return;
            var rows = new List<(string, int, int)>();
            foreach (var root in scene.GetRootGameObjects())
            {
                var all = root.GetComponentsInChildren(s_prismType, true);
                int active = all.Count(c => c.gameObject.activeInHierarchy);
                if (all.Length > 0) rows.Add(($"{root.name}{(root.IsDontDestroyOnLoad ? " [DDOL]" : "")}", all.Length, active));
            }
            Console.WriteLine($"[leakscan] prisms by root: {string.Join(", ", rows.OrderByDescending(r => r.Item2).Take(10).Select(r => $"{r.Item1}={r.Item2} ({r.Item3} active)"))}");
        }

        static Dictionary<(string root, string type), (long count, long bytes)> s_census;

        /// <summary>Per (static root, type): instances reachable, first root wins. Diffed between two samples.</summary>
        static void CensusByRoot()
        {
            var fieldCache = new Dictionary<Type, FieldInfo[]>();
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var census = new Dictionary<(string, string), (long, long)>();
            foreach (var (f, name) in DiscoverAll())
            {
                object root;
                try { root = f.GetValue(null); } catch { continue; }
                if (root == null) continue;
                var stack = new Stack<object>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    var o = stack.Pop();
                    if (!seen.Add(o)) continue;
                    var t = o.GetType();
                    long b = 24;
                    if (t.IsArray)
                    {
                        var arr = (Array)o; var et = t.GetElementType()!;
                        b += arr.Length * (et.IsValueType ? 12L : 8L);
                        if (!et.IsPrimitive && !et.IsEnum && (!et.IsValueType || HasRefs(et)))
                            foreach (var item in arr) if (item != null && item is not string && item is not Type) stack.Push(item);
                    }
                    else
                    {
                        if (!fieldCache.TryGetValue(t, out var fields))
                        {
                            var list = new List<FieldInfo>();
                            for (var bt = t; bt != null && bt != typeof(object); bt = bt.BaseType)
                                foreach (var fi in bt.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                                    if (!fi.FieldType.IsPrimitive && !fi.FieldType.IsPointer && !fi.FieldType.IsEnum && fi.FieldType != typeof(string)
                                        && !(fi.FieldType.IsValueType && !HasRefs(fi.FieldType)))
                                        list.Add(fi);
                            fieldCache[t] = fields = list.ToArray();
                        }
                        b += 8L * fields.Length;
                        foreach (var fi in fields)
                        {
                            object v;
                            try { v = fi.GetValue(o); } catch { continue; }
                            if (v == null || v is string || v is Type || v is MemberInfo || v is Assembly || v.GetType().IsPrimitive) continue;
                            stack.Push(v);
                        }
                    }
                    var key = (name, t.IsArray ? t.GetElementType()!.Name + "[]" : t.Name);
                    census.TryGetValue(key, out var e);
                    census[key] = (e.Item1 + 1, e.Item2 + b);
                }
            }
            if (s_census != null)
            {
                Console.WriteLine("[leakscan] census growth (root :: type  +count  +KB):");
                foreach (var kv in census.Select(kv => (kv.Key, d: kv.Value.Item2 - (s_census.TryGetValue(kv.Key, out var a) ? a.bytes : 0),
                                                          c: kv.Value.Item1 - (s_census.TryGetValue(kv.Key, out var a2) ? a2.count : 0)))
                                         .OrderByDescending(x => x.d).Take(30))
                    Console.WriteLine($"[leakscan]   {kv.d >> 10,8} KB {kv.c,8}  {kv.Key.Item1} :: {kv.Key.Item2}");
            }
            s_census = census.ToDictionary(kv => kv.Key, kv => (kv.Value.Item1, kv.Value.Item2));
        }

        static void Walk(object start, string startPath, HashSet<object> seen, Dictionary<Type, FieldInfo[]> fieldCache,
                         List<(object, string)> edges, ref long bytes, bool stopAtDestroyed)
        {
            var stack = new Stack<(object obj, string path, int depth)>();
            stack.Push((start, startPath, 0));
            while (stack.Count > 0)
            {
                var (o, path, depth) = stack.Pop();
                if (stopAtDestroyed && o is CosmicShore.Engine.Object eo && eo.IsDestroyed)
                {
                    if (!seen.Contains(o)) edges.Add((o, path.Contains('.') ? path[..path.LastIndexOf('.')] : path));
                    continue;
                }
                if (!seen.Add(o)) continue;
                var t = o.GetType();
                if (t.IsArray)
                {
                    var arr = (Array)o;
                    var et = t.GetElementType()!;
                    bytes += 24 + (long)arr.Length * (et.IsValueType ? Math.Max(1, System.Runtime.InteropServices.Marshal.SizeOf(et.IsEnum ? Enum.GetUnderlyingType(et) : (et.IsPrimitive ? et : typeof(IntPtr)))) : 8);
                    if (et.IsPrimitive || et.IsEnum || (et.IsValueType && !HasRefs(et))) continue;
                    int i = 0;
                    foreach (var item in arr)
                    {
                        if (item != null && item is not string && item is not Type)
                            stack.Push((item, depth < 24 ? $"{path}[{i}]" : path, depth + 1));
                        i++;
                    }
                    continue;
                }
                if (!fieldCache.TryGetValue(t, out var fields))
                {
                    var list = new List<FieldInfo>();
                    for (var b = t; b != null && b != typeof(object); b = b.BaseType)
                        foreach (var fi in b.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                            if (!fi.FieldType.IsPrimitive && !fi.FieldType.IsPointer && !fi.FieldType.IsEnum && fi.FieldType != typeof(string)
                                && !(fi.FieldType.IsValueType && !HasRefs(fi.FieldType)))
                                list.Add(fi);
                    fieldCache[t] = fields = list.ToArray();
                }
                bytes += 24 + 8L * fields.Length;
                foreach (var fi in fields)
                {
                    object v;
                    try { v = fi.GetValue(o); } catch { continue; }
                    if (v == null || v is string || v is Type || v is MemberInfo || v is Assembly || v.GetType().IsPrimitive) continue;
                    stack.Push((v, depth < 24 ? path + "." + fi.Name : path, depth + 1));
                }
            }
        }

        static readonly Dictionary<Type, bool> s_hasRefs = new();
        static bool HasRefs(Type t)
        {
            if (s_hasRefs.TryGetValue(t, out var r)) return r;
            s_hasRefs[t] = false;
            r = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                 .Any(f => !f.FieldType.IsValueType || (!f.FieldType.IsPrimitive && !f.FieldType.IsEnum && HasRefs(f.FieldType)));
            return s_hasRefs[t] = r;
        }

        static IEnumerable<(FieldInfo, string)> DiscoverAll()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var an = asm.GetName().Name ?? "";
                if (!an.StartsWith("CosmicShore", StringComparison.Ordinal) && !an.StartsWith("Obvious", StringComparison.Ordinal)) continue;
                Type[] types;
                try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; }
                foreach (var t in types)
                {
                    if (t.ContainsGenericParameters) continue;
                    foreach (var f in t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (!f.FieldType.IsPrimitive && !f.IsLiteral && f.FieldType != typeof(string))
                            yield return (f, t.FullName + "." + f.Name);
                }
            }
        }

        static List<(FieldInfo, string)> Discover()
        {
            var list = new List<(FieldInfo, string)>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var an = asm.GetName().Name ?? "";
                if (!an.StartsWith("CosmicShore", StringComparison.Ordinal) && !an.StartsWith("Obvious", StringComparison.Ordinal)) continue;
                Type[] types;
                try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; }
                foreach (var t in types)
                {
                    if (t.ContainsGenericParameters) continue;
                    foreach (var f in t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (f.FieldType.IsArray || typeof(ICollection).IsAssignableFrom(f.FieldType)
                            || f.FieldType.GetProperty("Count")?.PropertyType == typeof(int))
                            list.Add((f, t.FullName + "." + f.Name));
                }
            }
            return list;
        }
    }
}
