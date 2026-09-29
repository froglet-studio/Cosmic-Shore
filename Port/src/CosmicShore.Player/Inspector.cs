using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using CosmicShore.Engine;

namespace CosmicShore.Player
{
    /// <summary>
    /// Scripted-run diagnostics: dump a live component's fields (<c>inspect OBJECT COMPONENT</c>)
    /// or a static member chain (<c>eval Type.Member.member</c>) at an exact frame.
    /// </summary>
    public static class Inspector
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public static void Print(string objectName, string componentName)
        {
            var gos = CosmicShore.Engine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => objectName == "*" || t.name == objectName).Select(t => t.gameObject)
                .Where(go => componentName == null || go.GetComponents<Component>().Any(c => c.GetType().Name == componentName)).ToList();
            Console.WriteLine($"[inspect] '{objectName}' {componentName}: {gos.Count} object(s)");
            foreach (var go in gos)
                foreach (var c in go.GetComponents<Component>())
                {
                    if (componentName != null && c.GetType().Name != componentName) continue;
                    Console.WriteLine($"  {c.GetType().FullName} on '{go.name}' (active={go.activeInHierarchy})");
                    for (var t = c.GetType(); t != null && t != typeof(MonoBehaviour) && t != typeof(object); t = t.BaseType)
                        foreach (var f in t.GetFields(Any & ~BindingFlags.Static | BindingFlags.DeclaredOnly))
                            Console.WriteLine($"    {f.Name} = {Describe(Safe(() => f.GetValue(c)))}");
                }
        }

        /// <summary>Every renderer under each object named <paramref name="objectName"/>: what it would draw and where.</summary>
        public static void Renderers(string objectName)
        {
            var roots = CosmicShore.Engine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name == objectName).ToList();
            Console.WriteLine($"[renderers] '{objectName}': {roots.Count} root(s)");
            foreach (var root in roots)
            {
                Console.WriteLine($"  root pos={root.position} scale={root.lossyScale}");
                foreach (var col in root.GetComponentsInChildren<Collider>(true))
                    if (col is BoxCollider or CapsuleCollider or SphereCollider)
                        Console.WriteLine($"    collider {col.name} [{col.GetType().Name}] trigger={col.isTrigger} bounds={col.bounds} lossy={col.transform.lossyScale}");
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    string path = r.name;
                    for (var t = r.transform.parent; t != null && t != root; t = t.parent) path = t.name + "/" + path;
                    Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                    string extra = "";
                    if (r is SkinnedMeshRenderer s)
                    {
                        extra = $" rootBone={(s.rootBone != null ? s.rootBone.name + " lossy=" + s.rootBone.lossyScale : "null")} bones={s.bones?.Length ?? 0} bind={mesh?.bindposes?.Length ?? 0} localBounds={s.localBounds}";
                        extra += " skinnedWorld=" + SkinnedWorldBounds(s, mesh);
                    }
                    if (r is TrailRenderer tr)
                        extra = $" trail pos={tr.transform.position} n={tr.positionCount} time={tr.time} width={tr.widthMultiplier} emitting={tr.emitting}"
                              + (tr.positionCount > 0 ? $" first={tr.GetPosition(0)} last={tr.GetPosition(tr.positionCount - 1)}" : "");
                    Console.WriteLine($"    {path} [{r.GetType().Name}] enabled={r.enabled} active={r.gameObject.activeInHierarchy} " +
                        $"mesh={(mesh != null ? mesh.name + " v=" + mesh.vertexCount + " b=" + mesh.bounds : "null")} lossy={r.transform.lossyScale} " +
                        $"mats=[{string.Join(",", (r.sharedMaterials ?? Array.Empty<Material>()).Select(m => m == null ? "null" : m.name + "<" + m.shader?.name + ">"))}]{extra}");
                }
            }
        }

        /// <summary>The skinned hull's world AABB exactly as the GPU skins it: Σ w · (bone.localToWorld · bindpose) · v.</summary>
        static string SkinnedWorldBounds(SkinnedMeshRenderer s, Mesh mesh)
        {
            if (mesh == null || s.bones == null) return "n/a";
            var w = mesh.RenderBoneWeights; var bp = mesh.RenderBindposes; var v = mesh.vertices;
            if (w == null || w.Length != v.Length || bp == null) return "no skin";
            var m = new Matrix4x4[s.bones.Length];
            for (int i = 0; i < m.Length; i++) m[i] = (s.bones[i] != null ? s.bones[i].localToWorldMatrix : Matrix4x4.identity) * (i < bp.Length ? bp[i] : Matrix4x4.identity);
            Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
            for (int k = 0; k < v.Length; k++)
            {
                var bw = w[k];
                Vector3 p = Vector3.zero;
                void Add(int idx, float wt) { if (wt > 0f && idx < m.Length) p += wt * m[idx].MultiplyPoint3x4(v[k]); }
                Add(bw.boneIndex0, bw.weight0); Add(bw.boneIndex1, bw.weight1); Add(bw.boneIndex2, bw.weight2); Add(bw.boneIndex3, bw.weight3);
                lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p);
            }
            // At rest every skin matrix equals the renderer's own localToWorld; report how far each strays.
            var rw = s.transform.localToWorldMatrix;
            string worst = ""; float worstErr = 0f;
            for (int i = 0; i < m.Length; i++)
            {
                float err = 0f;
                for (int c = 0; c < 16; c++) err = MathF.Max(err, MathF.Abs(m[i][c] - rw[c]));
                if (err > worstErr) { worstErr = err; worst = s.bones[i] != null ? s.bones[i].name : "null"; }
            }
            var devs = new System.Collections.Generic.List<(float err, string name)>();
            for (int i = 0; i < m.Length; i++)
            {
                float err = 0f;
                for (int c = 0; c < 16; c++) err = MathF.Max(err, MathF.Abs(m[i][c] - rw[c]));
                devs.Add((err, s.bones[i] != null ? s.bones[i].name : "null"));
            }
            devs.Sort((a, b) => a.err.CompareTo(b.err));
            worst += " devs[" + string.Join(" ", devs.Select(d => $"{d.name}:{d.err:0.##}")) + "]";
            float smin = float.MaxValue, smax = 0f; string sminName = "";
            foreach (var bone in s.bones)
            {
                if (bone == null) continue;
                var ls = bone.lossyScale; float mag = (MathF.Abs(ls.x) + MathF.Abs(ls.y) + MathF.Abs(ls.z)) / 3f;
                if (mag < smin) { smin = mag; sminName = bone.name + " local=" + bone.localScale; }
                smax = MathF.Max(smax, mag);
            }
            worst += $" boneScale[min={smin:0.###}({sminName}) max={smax:0.###}]";
            var b0 = s.bones.Length > 0 && s.bones[0] != null ? s.bones[0] : null;
            return $"center={(lo + hi) * 0.5f} size={hi - lo} restDeviation={worstErr:0.###}@{worst}"
                 + (b0 != null ? $" bone0={b0.name} lossy={b0.lossyScale} bp0scale=({new Vector3(bp[0].GetColumn(0).magnitude, bp[0].GetColumn(1).magnitude, bp[0].GetColumn(2).magnitude)})" : "");
        }

        /// <summary>Every live trail/line renderer: owner path, sample span, width and material.</summary>
        public static void Trails()
        {
            var cam = Camera.main;
            foreach (var r in CosmicShore.Engine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r is not TrailRenderer && r is not LineRenderer) continue;
                string path = r.name;
                for (var t = r.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
                int n = r is TrailRenderer tr ? tr.positionCount : ((LineRenderer)r).positionCount;
                Vector3 P(int i) => r is TrailRenderer t2 ? t2.GetPosition(i) : ((LineRenderer)r).GetPosition(i);
                float w = r is TrailRenderer t3 ? t3.widthMultiplier : ((LineRenderer)r).widthMultiplier;
                string span = n > 0 ? $" first={P(0)} last={P(n - 1)}" : "";
                string ahead = "";
                if (cam != null && n > 0)
                {
                    int front = 0;
                    for (int i = 0; i < n; i++) if (Vector3.Dot(P(i) - cam.transform.position, cam.transform.forward) > 0f) front++;
                    ahead = $" inFront={front}/{n}";
                }
                var m = r.sharedMaterials is { Length: > 0 } ms ? ms[0] : null;
                Console.WriteLine($"[trail] {path} enabled={r.enabled} active={r.gameObject.activeInHierarchy} n={n} width={w}{span}{ahead} mat={(m != null ? m.name + "<" + m.shader?.name + ">" : "null")}");
            }
        }

        /// <summary>Each object named NAME and every ancestor: rect, anchors, scale, canvas components.</summary>
        public static void Ancestry(string objectName)
        {
            foreach (var t0 in CosmicShore.Engine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == objectName))
            {
                Console.WriteLine($"[ancestry] '{objectName}' active={t0.gameObject.activeInHierarchy}");
                for (var t = t0; t != null; t = t.parent)
                {
                    string rect = "";
                    if (t is RectTransform rt)
                    {
                        var c = new Vector3[4]; rt.GetWorldCorners(c);
                        rect = $" rect=({c[0].x:0},{c[0].y:0})-({c[2].x:0},{c[2].y:0}) aMin={rt.anchorMin} aMax={rt.anchorMax} pivot={rt.pivot} size={rt.sizeDelta} pos={rt.anchoredPosition}";
                    }
                    var comps = string.Join(",", t.GetComponents<Component>().Where(c => c is not Transform).Select(c => c.GetType().Name));
                    Console.WriteLine($"  {t.name}{rect} localScale={t.localScale} rot={t.localRotation.eulerAngles} [{comps}]");
                }
            }
        }

        /// <summary>
        /// <c>blast N</c>: destroys the N live prisms nearest the main camera through the game's own
        /// <c>Prism.Damage(devastate: true)</c>, blown outward from the camera — the same call an AOE
        /// blast makes, so the death visual is exactly the game's (debris, stats, audio).
        /// </summary>
        public static void Blast(int count)
        {
            var cam = Camera.main;
            if (cam == null) { Console.WriteLine("[blast] no main camera"); return; }
            var origin = cam.transform.position;
            var prisms = CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.Prism>(FindObjectsSortMode.None)
                .Where(pr => pr && pr.isActiveAndEnabled && !pr.destroyed)
                .OrderBy(pr => (pr.transform.position - origin).sqrMagnitude)
                .Take(count)
                .ToList();
            foreach (var pr in prisms)
            {
                var dir = (pr.transform.position - origin).normalized;
                pr.Damage(dir * 30f, CosmicShore.Data.Domains.Blue, "script", devastate: true);
            }
            Console.WriteLine($"[blast] damaged {prisms.Count} prism(s)");
        }

        public static void PrintStatic(string chain)
        {
            var parts = chain.Split('.');
            // Longest prefix that names a type.
            for (int split = parts.Length - 1; split >= 1; split--)
            {
                string typeName = string.Join('.', parts.Take(split));
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.Where(x => x != null); } })
                    .FirstOrDefault(x => x.FullName == typeName || x.Name == typeName);
                if (type == null) continue;
                object cur = null; Type curType = type;
                foreach (var member in parts.Skip(split))
                {
                    var f = curType.GetField(member, Any);
                    var p = f == null ? curType.GetProperty(member, Any) : null;
                    cur = Safe(() => f != null ? f.GetValue(cur) : p?.GetValue(cur));
                    if (cur == null) break;
                    curType = cur.GetType();
                }
                Console.WriteLine($"[eval] {chain} = {Describe(cur)}");
                return;
            }
            Console.WriteLine($"[eval] {chain}: no such type");
        }

        static object Safe(Func<object> get)
        {
            try { return get(); } catch (Exception e) { return "<" + (e.InnerException ?? e).GetType().Name + ">"; }
        }

        static string Describe(object v) => v switch
        {
            null => "null",
            string s => '"' + s + '"',
            CosmicShore.Engine.Object o => o ? $"{o.GetType().Name}('{o.name}')" : "destroyed",
            ICollection c when v is not string => $"{v.GetType().Name}[{c.Count}]",
            _ when v.GetType().Name.StartsWith("NetworkVariable", StringComparison.Ordinal)
                => $"NetworkVariable({Describe(Safe(() => v.GetType().GetProperty("Value")?.GetValue(v)))})",
            _ => v.ToString(),
        };
    }
}
