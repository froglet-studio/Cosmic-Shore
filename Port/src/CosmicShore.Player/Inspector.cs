using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;

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
        /// <summary><c>buttons FILTER</c>: every active button whose path contains FILTER (or all), with its screen rect and persistent click calls.</summary>
        public static void Buttons(string filter)
        {
            foreach (var b in CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Engine.UI.Button>(FindObjectsSortMode.None))
            {
                if (!b.isActiveAndEnabled) continue;
                var path = b.name;
                for (var t = b.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
                if (!string.IsNullOrEmpty(filter) && path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string rect = "";
                if (b.transform is RectTransform rt) { var c = new Vector3[4]; rt.GetWorldCorners(c); rect = $"({c[0].x:0},{Screen.height - c[2].y:0})-({c[2].x:0},{Screen.height - c[0].y:0})"; }
                var calls = string.Join("; ", Enumerable.Range(0, b.onClick.GetPersistentEventCount())
                    .Select(i => $"{b.onClick.GetPersistentTarget(i)?.name}.{b.onClick.GetPersistentMethodName(i)}"));
                Console.WriteLine($"[button] {path} top-left rect {rect} interactable={b.interactable} calls=[{calls}]");
            }
        }

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

        /// <summary>
        /// A test light for the Lit fundamental: a sphere of <paramref name="radius"/> on the prism
        /// nearest the camera, reported through the game's own PrismLit every frame the returned
        /// action runs (a light nobody reports fades out).
        /// </summary>
        public static Action LitSphere(float radius)
        {
            var cam = Camera.main;
            if (cam == null) { Console.WriteLine("[lit] no main camera"); return null; }
            var origin = cam.transform.position;
            var prism = CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.Prism>(FindObjectsSortMode.None)
                .Where(pr => pr && pr.isActiveAndEnabled && !pr.destroyed)
                .OrderBy(pr => ViewCentreScore(cam, pr.transform.position))
                .Take(40) // the prisms nearest the view centre, then the one that looks biggest
                .OrderByDescending(pr => ApparentSize(cam, pr))
                .FirstOrDefault();
            if (prism == null) { Console.WriteLine("[lit] no prisms"); return null; }
            var centre = prism.transform.position;
            Console.WriteLine($"[lit] sphere r={radius} at {centre}");
            return () => CosmicShore.Utility.PrismLit.PublishLight(
                0x5C817, CosmicShore.Utility.LitVolume.Sphere(centre, radius), 1f, new Color(1f, 0.25f, 0.2f));
        }

        /// <summary>
        /// A test drape for the Urchin cradle: a stand-in hull of <paramref name="radius"/> parked
        /// just in front of the prism nearest the camera, reported through the game's own
        /// PrismCradle every frame the returned action runs.
        /// </summary>
        public static Action CradleHull(float radius)
        {
            var cam = Camera.main;
            if (cam == null) { Console.WriteLine("[cradle] no main camera"); return null; }
            var origin = cam.transform.position;
            // A prism the camera sees up close: in view, within a short band, largest on screen.
            // (The drape reach is a few units, so a distant prism shows nothing at all.)
            var prism = CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.Prism>(FindObjectsSortMode.None)
                .Where(pr => pr && pr.isActiveAndEnabled && !pr.destroyed)
                .Where(pr =>
                {
                    float d = (pr.transform.position - origin).magnitude;
                    if (d < 4f || d > 160f) return false;
                    var vp = cam.WorldToViewportPoint(pr.transform.position);
                    return vp.z > 0f && vp.x > 0.2f && vp.x < 0.8f && vp.y > 0.2f && vp.y < 0.8f;
                })
                .OrderByDescending(pr => ApparentSize(cam, pr))
                .FirstOrDefault();
            if (prism == null) { Console.WriteLine("[cradle] no prism close enough"); return null; }
            var hull = new GameObject("ScriptCradleHull");
            var toCam = (origin - prism.transform.position).normalized;
            var ls = prism.transform.lossyScale;
            float half = Mathf.Min(ls.x, Mathf.Min(ls.y, ls.z)) * 0.5f;
            // Sunk 40% of its radius into the prism's camera-facing side: the drape closes the
            // face over the part the hull clips through and lifts the rim within reach to meet it.
            hull.transform.position = prism.transform.position + toCam * (half + radius * 0.6f);
            var sp = cam.WorldToScreenPoint(prism.transform.position);
            Console.WriteLine($"[cradle] hull r={radius} at {hull.transform.position} beside {prism.name} " +
                $"(scale {ls}, {(prism.transform.position - origin).magnitude:F0} u away, screen {sp.x:F0},{Screen.height - sp.y:F0})");
            return () => CosmicShore.Utility.PrismCradle.Publish(0x5C818, hull.transform, radius, 1f);
        }

        /// <summary>
        /// "lookat &lt;distance&gt;": stand the camera rig down and frame the largest live prism in
        /// the scene from <paramref name="distance"/> prism-sizes away, three-quarter on. For
        /// close-up checks of per-prism shading (the cradle drape) wherever the pilot is.
        /// </summary>
        public static void LookAt(float distance)
        {
            var cam = Camera.main;
            if (cam == null) { Console.WriteLine("[lookat] no main camera"); return; }
            foreach (var mb in cam.GetComponentsInParent<MonoBehaviour>())
                if (mb.enabled) { mb.enabled = false; Console.WriteLine($"[lookat] stood down {mb.GetType().Name}"); }
            var prism = CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.Prism>(FindObjectsSortMode.None)
                .Where(pr => pr && pr.isActiveAndEnabled && !pr.destroyed)
                .OrderByDescending(pr => { var l = pr.transform.lossyScale; return Mathf.Min(l.x, Mathf.Min(l.y, l.z)); })
                .FirstOrDefault();
            if (prism == null) { Console.WriteLine("[lookat] no prisms"); return; }
            var ls = prism.transform.lossyScale;
            float size = Mathf.Max(ls.x, Mathf.Max(ls.y, ls.z));
            var dir = (prism.transform.forward + prism.transform.up * 0.6f + prism.transform.right * 0.4f).normalized;
            cam.transform.position = prism.transform.position + dir * size * distance;
            cam.transform.rotation = Quaternion.LookRotation(prism.transform.position - cam.transform.position, prism.transform.up);
            Console.WriteLine($"[lookat] {prism.name} scale {ls} at {prism.transform.position}; camera {size * distance:F1} u back");
        }

        /// <summary>A prism's largest dimension over its distance: roughly the fraction of the view it spans.</summary>
        static float ApparentSize(Camera cam, CosmicShore.Gameplay.Prism pr)
        {
            var ls = pr.transform.lossyScale;
            float d = (pr.transform.position - cam.transform.position).magnitude;
            return Mathf.Max(ls.x, Mathf.Max(ls.y, ls.z)) / Mathf.Max(d, 1e-3f);
        }

        /// <summary>Angle off the view axis, then distance: the prism the camera is looking at.</summary>
        static float ViewCentreScore(Camera cam, Vector3 p)
        {
            var to = p - cam.transform.position;
            float d = to.magnitude;
            if (d < 1e-3f || Vector3.Dot(to, cam.transform.forward) <= 0f) return float.MaxValue;
            return Vector3.Angle(cam.transform.forward, to) * 1000f + d;
        }

        /// <summary>
        /// "domain Ruby" asks the server for a domain the way the Domain Changer toy does (the
        /// owner's RequestSetDomain_ServerRpc); "domain" alone prints every player's replicated
        /// domain as this process sees it.
        /// </summary>
        public static void Domain(string arg)
        {
            foreach (var p in CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.Player>(FindObjectsSortMode.None))
            {
                if (!p.IsSpawned) continue;
                if (arg.Length > 0 && p.IsOwner && !p.NetIsAI.Value && Enum.TryParse<CosmicShore.Data.Domains>(arg, true, out var d))
                {
                    Console.WriteLine($"[domain] {p.NetName.Value} requests {d}");
                    p.RequestSetDomain_ServerRpc(d);
                }
                else if (arg.Length == 0)
                    Console.WriteLine($"[domain] {p.NetName.Value} owner={p.OwnerClientId}{(p.IsOwner ? " (mine)" : "")} = {p.NetDomain.Value}");
            }
        }

        /// <summary>
        /// "arcade Bloomrush" presses that mode's arcade card (ArcadeExploreView.SelectGame, the
        /// card's own click handler); "arcade start" presses Start in the open launch modal. A
        /// party guest's modal follows the host's pick through the lobby sync, so the guest only
        /// needs "arcade start".
        /// </summary>
        /// <summary>
        /// "score" prints the in-game score chain as this process sees it, end to end: each
        /// RoundStats (Score / crystals / prisms), the server-synced per-domain sums in GameDataSO,
        /// and what every top-bar DomainScorePanel and the centre score text actually show.
        /// "score collect" first moves the local vessel onto the nearest live crystal, so a real
        /// collision drives the chain.
        /// </summary>
        public static void Score(string arg)
        {
            var ctrl = CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.MiniGameControllerBase>(FindObjectsSortMode.None).FirstOrDefault();
            var gd = ctrl == null ? null : typeof(CosmicShore.Gameplay.MiniGameControllerBase)
                .GetField("gameData", Any)?.GetValue(ctrl) as CosmicShore.Utility.GameDataSO;
            if (gd == null) { Console.WriteLine("[score] no controller / gameData"); return; }

            if (arg == "collect")
            {
                var vessel = gd.LocalPlayer?.Vessel?.Transform;
                var crystal = CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.Crystal>(FindObjectsSortMode.None)
                    .Where(c => c.isActiveAndEnabled)
                    .OrderBy(c => vessel == null ? 0f : (c.transform.position - vessel.position).sqrMagnitude).FirstOrDefault();
                if (vessel == null || crystal == null) Console.WriteLine($"[score] collect: vessel={vessel != null} crystal={crystal != null}");
                else
                {
                    Console.WriteLine($"[score] collect: vessel {vessel.position} -> crystal '{crystal.name}' {crystal.transform.position}");
                    vessel.position = crystal.transform.position;
                }
                return;
            }

            foreach (var s in gd.RoundStatsList)
                if (s != null)
                    Console.WriteLine($"[score] stats '{s.Name}' domain={s.Domain} Score={s.Score} crystals={s.CrystalsCollected}");
            foreach (var d in CosmicShore.Utility.GameDataSO.ActiveDomains)
                Console.WriteLine($"[score] gameData sum {d} = {gd.GetDomainMetricSum(d)}");
            foreach (var p in CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.UI.DomainScorePanel>(FindObjectsSortMode.None))
            {
                var t = typeof(CosmicShore.UI.DomainScorePanel).GetField("domainSumText", Any)?.GetValue(p) as CosmicShore.Engine.UI.TMP_Text;
                Console.WriteLine($"[score] panel {p.Domain} text='{t?.text}'");
            }
            var sumsEvent = typeof(CosmicShore.Utility.GameDataSO).GetField("OnDomainMetricSumsChanged", Any)?.GetValue(gd) as Delegate;
            Console.WriteLine($"[score] OnDomainMetricSumsChanged subscribers={sumsEvent?.GetInvocationList().Length ?? 0}");
        }

        public static void Arcade(string arg)
        {
            if (arg == "start")
            {
                foreach (var m in CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.UI.ArcadeGameConfigureModal>(FindObjectsSortMode.None))
                {
                    if (!m.isActiveAndEnabled) continue;
                    Console.WriteLine("[arcade] start pressed");
                    m.OnStartGameClicked();
                }
                return;
            }
            if (arg == "ready")
            {
                // The in-game HUD's Ready button (MiniGameHUD wires it to OnReadyClicked).
                foreach (var c in CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.MiniGameControllerBase>(FindObjectsSortMode.None))
                {
                    Console.WriteLine($"[arcade] ready pressed ({c.GetType().Name})");
                    c.OnReadyClicked();
                }
                return;
            }
            if (!Enum.TryParse<CosmicShore.Data.GameModes>(arg, true, out var mode)) { Console.WriteLine($"[arcade] no mode '{arg}'"); return; }
            foreach (var v in CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.UI.ArcadeExploreView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var game = v.FindGameByMode(mode);
                if (game == null) continue;
                Console.WriteLine($"[arcade] card pressed: {game.DisplayName}");
                v.SelectGame(game);
                return;
            }
            Console.WriteLine($"[arcade] no card for {mode}");
        }

        /// <summary>
        /// "stage 900" puts the first vessel that is not the local pilot's straight ahead of the
        /// camera at that distance, a little off-axis so the local hull does not cover it, facing
        /// the camera - a fixed subject for checking the vessel vision band's distance ramps.
        /// </summary>
        public static void Stage(float distance)
        {
            var cam = Camera.main;
            if (cam == null) { Console.WriteLine("[stage] no camera"); return; }
            foreach (var v in CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.VesselController>(FindObjectsSortMode.None))
            {
                var status = v.GetComponent<CosmicShore.Gameplay.IVesselStatus>();
                if (status?.Player != null && status.Player.IsLocalPilot) continue;
                var ct = cam.transform;
                var p = ct.position + ct.forward * distance + ct.right * (distance * 0.12f);
                v.transform.SetPositionAndRotation(p, Quaternion.LookRotation(ct.up, -ct.forward)); // top toward the camera: a flat hull shows its planform
                Console.WriteLine($"[stage] {v.name} at {distance:F0} from the camera");
                return;
            }
            Console.WriteLine("[stage] no other vessel");
        }

        /// <summary>Every vessel: distance from the camera, screen position, and its vision tint.</summary>
        public static void Vessels()
        {
            var cam = Camera.main;
            var tintId = Shader.PropertyToID("_VesselVisionTint");
            var block = new MaterialPropertyBlock();
            foreach (var v in CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.VesselController>(FindObjectsSortMode.None))
            {
                var p = v.transform.position;
                string where = "";
                if (cam != null)
                {
                    var sp = cam.WorldToScreenPoint(p);
                    where = $" dist {(p - cam.transform.position).magnitude:F0} screen {sp.x:F0},{Screen.height - sp.y:F0}{(sp.z < 0 ? " (behind)" : "")}";
                }
                int stamped = 0, total = 0;
                Color tint = default;
                foreach (var r in v.GetComponentsInChildren<Renderer>())
                    for (int i = 0; i < r.sharedMaterials.Length; i++)
                    {
                        total++;
                        r.GetPropertyBlock(block, i);
                        if (block.HasColor(tintId) && block.GetColor(tintId).a > 0f) { stamped++; tint = block.GetColor(tintId); }
                    }
                int drawn = 0, rends = 0;
                foreach (var r in v.GetComponentsInChildren<Renderer>(true))
                    if (r is MeshRenderer or SkinnedMeshRenderer) { rends++; if (r.enabled && r.gameObject.activeInHierarchy) drawn++; }
                where += $" active={v.gameObject.activeInHierarchy} hull {drawn}/{rends} drawn";
                var no = v.GetComponent<NetworkObject>();
                string net = no != null && no.IsSpawned ? $" net#{no.NetworkObjectId} owner={no.OwnerClientId}{(no.IsOwner ? " (mine)" : "")}" : "";
                foreach (var nt in v.GetComponentsInChildren<CosmicShore.Engine.Networking.Components.NetworkTransform>(true))
                    net += $" [{nt.GetType().Name}{(nt.enabled ? "" : " off")}{(nt.IsSpawned ? "" : " unspawned")} auth={NetDriver.IsTransformAuthority(nt)} sent={nt.PortSent} recv={nt.PortReceived}]";
                Console.WriteLine($"[vessels] {v.name}{net} at {p}{where} vision tint {stamped}/{total} {tint}");
            }
        }

        /// <summary>
        /// Cuts the <paramref name="count"/> prisms nearest the centre of view the way the Rhino's
        /// blade does (Prism.Slice): a plane through each prism's centre, normal along the camera's
        /// right so the halves part across the screen.
        /// </summary>
        public static void Slice(int count)
        {
            var cam = Camera.main;
            if (cam == null) { Console.WriteLine("[slice] no main camera"); return; }
            var prisms = CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.Prism>(FindObjectsSortMode.None)
                .Where(pr => pr && pr.isActiveAndEnabled && !pr.destroyed)
                .Where(pr => ApparentSize(cam, pr) > 0.03f)   // something you can see being cut
                .OrderBy(pr => ViewCentreScore(cam, pr.transform.position))
                .Take(count)
                .ToList();
            foreach (var pr in prisms)
            {
                var sp = cam.WorldToScreenPoint(pr.transform.position);
                Console.WriteLine($"[slice]   {pr.name} scale {pr.transform.lossyScale} screen {sp.x:F0},{Screen.height - sp.y:F0} shielded={pr.prismProperties?.IsShielded}/{pr.prismProperties?.IsSuperShielded}");
            }
            foreach (var pr in prisms)
                pr.Slice(cam.transform.forward * 20f, CosmicShore.Data.Domains.Blue, "script",
                         pr.transform.position, cam.transform.right, devastate: true);
            Console.WriteLine($"[slice] cut {prisms.Count} prism(s); live slices {CosmicShore.Utility.PrismSlice.LiveSliceCount}");
        }

        /// <summary>
        /// Party helpers for scripted multi-instance runs: <c>party online</c> lists the presence
        /// lobby as the game sees it; <c>party invite NAME</c> invites that online player through the
        /// game's own HostConnectionService.SendInviteAsync (what the Invite button calls).
        /// </summary>
        static readonly System.Collections.Generic.HashSet<string> s_invited = new();

        public static void Party(string arg)
        {
            var parts = arg.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var svcType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.Where(x => x != null); } })
                .FirstOrDefault(t => t.Name == "HostConnectionService");
            var svc = svcType?.GetProperty("Instance", Any)?.GetValue(null);
            if (svc == null) { Console.WriteLine("[party] no HostConnectionService"); return; }
            var data = svcType.GetField("connectionData", Any)?.GetValue(svc);
            var online = data?.GetType().GetField("OnlinePlayers", Any)?.GetValue(data) as IEnumerable;
            var members = data?.GetType().GetField("PartyMembers", Any)?.GetValue(data) as IEnumerable;
            string Name(object p) => p?.GetType().GetProperty("DisplayName")?.GetValue(p) as string;
            string Id(object p) => p?.GetType().GetProperty("PlayerId")?.GetValue(p) as string;
            if (parts.Length == 0 || parts[0] == "online")
            {
                Console.WriteLine($"[party] online: {string.Join(", ", (online ?? Array.Empty<object>()).Cast<object>().Select(p => $"{Name(p)}({Id(p)?[..Math.Min(8, Id(p)?.Length ?? 0)]})"))}");
                Console.WriteLine($"[party] members: {string.Join(", ", (members ?? Array.Empty<object>()).Cast<object>().Select(p => Name(p)))}");
                return;
            }
            if (parts[0] == "accept")
            {
                // Press the invite popup's Accept (a no-op unless an invite is pending).
                foreach (var mb in CosmicShore.Engine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (mb.GetType().Name != "PartyInviteNotificationPanel") continue;
                    var pending = mb.GetType().GetField("_pendingInvite", Any)?.GetValue(mb);
                    if (pending == null) continue;
                    Console.WriteLine("[party] accepting pending invite");
                    mb.GetType().GetMethod("OnAcceptPressed", Any)?.Invoke(mb, null);
                    return;
                }
                // The popup missed it (the invite landed before the menu was up, or it timed
                // out): a player finds it in the friends panel's Requests section instead - the
                // panel rehydrates it from HostConnectionService.LastPendingInvite. Open the
                // panel, then press the party-invite row's Accept.
                foreach (var row in CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.UI.RequestInfoEntry>(FindObjectsSortMode.None))
                {
                    if (!row.isActiveAndEnabled || !Equals(row.GetType().GetField("_kind", Any)?.GetValue(row), CosmicShore.UI.RequestInfoEntry.Kind.PartyInvite)) continue;
                    if ((bool)(row.GetType().GetField("_responded", Any)?.GetValue(row) ?? true)) continue;
                    Console.WriteLine("[party] accepting invite from the friends panel");
                    row.GetType().GetMethod("HandleAcceptClicked", Any)?.Invoke(row, null);
                    return;
                }
                if (svc != null && svcType.GetProperty("LastPendingInvite")?.GetValue(svc) != null)
                    foreach (var panel in CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.UI.FriendsListPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (!panel.gameObject.activeSelf)
                        {
                            Console.WriteLine("[party] opening the friends panel for a missed invite");
                            panel.Show();
                            break;
                        }
                return;
            }
            if (parts[0] == "invite" && parts.Length > 1)
            {
                if (s_invited.Contains(parts[1])) return;
                var target = (online ?? Array.Empty<object>()).Cast<object>().FirstOrDefault(p => Name(p) == parts[1]);
                if (target == null) return; // retried by the script until they are online
                s_invited.Add(parts[1]);
                svcType.GetMethod("SendInviteAsync", new[] { typeof(string) })?.Invoke(svc, new object[] { Id(target) });
                Console.WriteLine($"[party] invite sent to {parts[1]}");
            }
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

        internal static object Safe(Func<object> get)
        {
            try { return get(); } catch (Exception e) { return "<" + (e.InnerException ?? e).GetType().Name + ">"; }
        }

        internal static string Describe(object v) => v switch
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
