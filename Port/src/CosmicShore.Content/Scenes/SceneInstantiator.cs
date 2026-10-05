using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Content.Serialization;
using CosmicShore.Content.Yaml;
using CosmicShore.Engine;
using EngineObject = CosmicShore.Engine.Object;

namespace CosmicShore.Content.Scenes
{
    /// <summary>Options for <see cref="SceneInstantiator"/>.</summary>
    public sealed class InstantiateOptions
    {
        /// <summary>
        /// Decides whether a script (MonoBehaviour) type is instantiated. Default: all.
        /// Returning false leaves the component out (as if its script were missing) —
        /// used to bring up a scene's visual tree without running game logic.
        /// </summary>
        public Func<Type, bool> IncludeScript = _ => true;

        /// <summary>Wire inspector (persistent) UnityEvent calls. Default true.</summary>
        public bool WirePersistentCalls = true;

        /// <summary>Activate the loaded hierarchy (runs Awake/OnEnable). Default true.</summary>
        public bool Activate = true;

        /// <summary>
        /// Where the loaded roots end up (null = the scene root). An inactive parent keeps an
        /// un-activated load dormant: prefab-asset templates go straight from the inactive
        /// holder to the inactive template root, so no Awake/OnEnable ever runs on an asset.
        /// </summary>
        public Transform Parent;

        /// <summary>
        /// Runs on the fully wired roots after they reach their final place and are active in
        /// hierarchy (findable), but BEFORE any Awake/OnEnable is delivered (see
        /// GameObject.ActivateSceneRoots). The scene load uses it for Reflex
        /// scene injection: the original's <c>ContainerScope</c> injects the whole scene from its
        /// own Awake at execution order -1,000,000,000, i.e. ahead of every other Awake/OnEnable
        /// in the scene, and game code relies on that (e.g. MultiplayerHUD subscribes to
        /// GameDataSO.OnDomainMetricSumsChanged in OnEnable, GameSetting reads its injected
        /// UGSDataService in Awake).
        /// </summary>
        public Action<IReadOnlyList<GameObject>> BeforeActivate;
    }

    /// <summary>What a load produced, for diagnostics and lookups.</summary>
    public sealed class LoadedScene
    {
        public readonly List<GameObject> Roots = new();
        public readonly Dictionary<long, EngineObject> ById = new();
        public int GameObjects;
        public int Components;
        public int SkippedComponents;
        public readonly Dictionary<string, int> MissingScripts = new(StringComparer.Ordinal);
        public readonly Dictionary<string, int> SkippedBuiltIns = new(StringComparer.Ordinal);
        public readonly List<string> Warnings = new();

        public GameObject Find(string path)
        {
            // "Root/Child/Grandchild"
            var parts = path.Split('/');
            foreach (var root in Roots)
            {
                if (root.name != parts[0]) continue;
                var t = parts.Length == 1 ? root.transform : root.transform.Find(string.Join('/', parts.Skip(1)));
                if (t != null) return t.gameObject;
            }
            return null;
        }
    }

    /// <summary>
    /// Builds live engine GameObjects from an expanded <see cref="PrefabGraph"/> — the
    /// port's equivalent of Unity loading a scene or instantiating a prefab:
    /// every object is created under a hidden inactive holder, components are attached
    /// in their serialized order, transforms are parented in serialized child order,
    /// serialized fields are filled (object references resolved once everything exists),
    /// active/enabled states are applied, and only then is the holder activated so
    /// Awake/OnEnable run over a fully-wired hierarchy.
    /// </summary>
    public sealed class SceneInstantiator : IReferenceResolver
    {
        readonly AssetLoader _assets;
        readonly SerializedReader _reader;
        readonly InstantiateOptions _options;
        PrefabGraph _graph;
        LoadedScene _result;
        readonly List<(object evt, YNode calls, AssetFile origin)> _pendingEvents = new();

        public SceneInstantiator(AssetLoader assets, InstantiateOptions options = null)
        {
            _assets = assets;
            _options = options ?? new InstantiateOptions();
            _reader = new SerializedReader(this);
            // Outside any instantiation, a UnityEvent read from a lazily-loaded asset
            // (a ScriptableObject pulled in by Resources.Load or a later reference) wires at once.
            _assets.UnityEventSink ??= WireAssetEventNow;
        }

        void WireAssetEventNow(object evt, YNode calls, AssetFile origin)
        {
            if (_options.WirePersistentCalls) WirePersistentCalls(evt, calls, origin);
        }

        public LoadedScene Instantiate(PrefabGraph graph)
        {
            _graph = graph;
            _result = new LoadedScene();
            _result.Warnings.AddRange(graph.Warnings);
            _pendingEvents.Clear();
            // While this graph instantiates, asset-borne UnityEvents (ScriptableObjects its
            // components reference) queue with the scene's own and wire before Awake.
            var previousSink = _assets.UnityEventSink;
            _assets.UnityEventSink = (e, c, o) => _pendingEvents.Add((e, c, o));
            try { return InstantiateCore(graph); }
            finally { _assets.UnityEventSink = previousSink; }
        }

        LoadedScene InstantiateCore(PrefabGraph graph)
        {
            var holder = new GameObject("__content_load_holder");
            holder.SetActive(false);

            // 1. GameObjects.
            var goObjs = graph.Objects.Values.Where(o => o.ClassId == 1 && !o.Removed).ToList();
            foreach (var o in goObjs)
            {
                var go = new GameObject(o.Body.Str("m_Name") ?? "GameObject");
                go.transform.SetParent(holder.transform, false);
                go.layer = o.Body.Int("m_Layer");
                go.tag = o.Body.Str("m_TagString") ?? "Untagged";
                _result.ById[o.Id] = go;
                _result.GameObjects++;
            }

            // 2. Components, in each GameObject's serialized order (transform first).
            var componentBodies = new List<(EngineObject comp, GraphObject obj)>();
            foreach (var o in goObjs)
            {
                var go = (GameObject)_result.ById[o.Id];
                foreach (var entry in o.Body["m_Component"]?.Items ?? Array.Empty<YNode>())
                {
                    long cid = graph.Resolve(ObjRef.From(entry["component"]).FileId);
                    var co = graph.Get(cid);
                    if (co == null) continue;
                    var comp = CreateComponent(go, co);
                    if (comp == null) continue;
                    _result.ById[co.Id] = comp;
                    componentBodies.Add((comp, co));
                    _result.Components++;
                }
            }
            // Components added by prefab overrides point at their GameObject but aren't listed on it.
            // Unity orders them by the instance's m_AddedComponents list, a prefab's own additions first.
            foreach (var co in graph.Objects.Values.Select((o, i) => (o, i))
                                  .OrderByDescending(x => graph.AddedComponentRank(x.o.Id).Depth)
                                  .ThenBy(x => graph.AddedComponentRank(x.o.Id).Index).ThenBy(x => x.i)
                                  .Select(x => x.o))
            {
                if (co.Removed || co.ClassId == 1 || _result.ById.ContainsKey(co.Id)) continue;
                var goRef = ObjRef.From(co.Body["m_GameObject"]);
                if (goRef.IsNull) continue;
                if (!_result.ById.TryGetValue(graph.Resolve(goRef.FileId), out var owner) || owner is not GameObject ogo) continue;
                var comp = CreateComponent(ogo, co);
                if (comp == null) continue;
                _result.ById[co.Id] = comp;
                componentBodies.Add((comp, co));
                _result.Components++;
            }

            // 3. Hierarchy, in serialized child order.
            var roots = BuildHierarchy(holder.transform);

            // 4. Serialized fields (all objects exist now, so references resolve).
            foreach (var (comp, obj) in componentBodies)
            {
                if (comp is Transform) continue; // applied in BuildHierarchy
                try { _reader.ReadInto(comp, obj.Body, obj.Origin); }
                catch (Exception e) { _result.Warnings.Add($"{obj}: {e.Message}"); }
                ApplyBuiltInExtras(comp, obj);
            }

            // 5. Enabled / active state.
            foreach (var (comp, obj) in componentBodies)
            {
                if (obj.Body["m_Enabled"] == null) continue;
                bool enabled = obj.Body.Bool("m_Enabled", true);
                if (comp is Behaviour b) b.enabled = enabled;
                else if (comp is Renderer r) r.enabled = enabled;
            }
            foreach (var o in goObjs)
                ((GameObject)_result.ById[o.Id]).SetActive(o.Body.Bool("m_IsActive", true));

            // 6. Persistent UnityEvent calls.
            if (_options.WirePersistentCalls)
                foreach (var (evt, calls, origin) in _pendingEvents)
                    WirePersistentCalls(evt, calls, origin);
            _pendingEvents.Clear();

            // 7. Un-hold, then activate (Awake/OnEnable over the fully wired tree). Each root
            //    moves to its final place while still dormant — its authored active flag is
            //    parked — so Awake runs with the object where Unity has it (a root in the scene):
            //    DontDestroyOnLoad(gameObject) in Awake must mark THIS root, not the holder.
            var authoredActive = new List<bool>(roots.Count);
            foreach (var root in roots)
            {
                authoredActive.Add(root.gameObject.activeSelf);
                if (_options.Activate) root.gameObject.SetActive(false);
                root.SetParent(_options.Parent, false);
            }
            Destroy(holder);
            var rootObjects = roots.Select(r => r.gameObject).ToList();
            if (!_options.Activate)
                _options.BeforeActivate?.Invoke(rootObjects);
            else if (_options.BeforeActivate != null && _options.Parent == null)
            {
                // Two-phase, like Unity's scene load: the scene is active (and findable) when the
                // hook runs, but no Awake/OnEnable has been delivered yet.
                var toActivate = new List<GameObject>();
                for (int i = 0; i < roots.Count; i++)
                    if (authoredActive[i]) toActivate.Add(rootObjects[i]);
                GameObject.ActivateSceneRoots(toActivate, () => _options.BeforeActivate(rootObjects));
            }
            else
            {
                _options.BeforeActivate?.Invoke(rootObjects);
                for (int i = 0; i < roots.Count; i++)
                    if (authoredActive[i]) roots[i].gameObject.SetActive(true);
            }

            _result.Roots.AddRange(roots.Select(r => r.gameObject));
            return _result;
        }

        static void Destroy(GameObject go)
        {
            // Holder is empty by now; destroy immediately so it never shows up as a root.
            EngineObject.DestroyImmediate(go);
        }

        EngineObject CreateComponent(GameObject go, GraphObject co)
        {
            switch (co.ClassId)
            {
                case 4: return go.transform;
                case 224: return go.AddComponent<RectTransform>();
                case 114:
                {
                    var scriptGuid = ObjRef.From(co.Body["m_Script"]).Guid;
                    var type = _assets.Scripts.Resolve(scriptGuid);
                    if (type == null || !typeof(Component).IsAssignableFrom(type) || type.IsAbstract)
                    {
                        string key = scriptGuid ?? "(none)";
                        _result.MissingScripts[key] = _result.MissingScripts.TryGetValue(key, out var n) ? n + 1 : 1;
                        _result.SkippedComponents++;
                        return null;
                    }
                    if (!_options.IncludeScript(type)) { _result.SkippedComponents++; return null; }
                    try { using (GameObject.ComponentGraphRestoreScope()) return go.AddComponent(type); }
                    catch (Exception e)
                    {
                        _result.Warnings.Add($"AddComponent<{type.Name}> on '{go.name}': {e.Message}");
                        return null;
                    }
                }
                default:
                {
                    var type = _assets.Scripts.BuiltIn(co.ClassId);
                    if (type == null)
                    {
                        string key = co.TypeName ?? co.ClassId.ToString();
                        _result.SkippedBuiltIns[key] = _result.SkippedBuiltIns.TryGetValue(key, out var n) ? n + 1 : 1;
                        return null;
                    }
                    try { using (GameObject.ComponentGraphRestoreScope()) return go.GetComponent(type) ?? go.AddComponent(type); }
                    catch (Exception e)
                    {
                        _result.Warnings.Add($"AddComponent<{type.Name}> on '{go.name}': {e.Message}");
                        return null;
                    }
                }
            }
        }

        static readonly string TraceCall = Environment.GetEnvironmentVariable("CS_PORT_TRACE_CALL");
        static readonly bool TraceParent = Environment.GetEnvironmentVariable("CS_PORT_TRACE_PARENT") == "1";

        List<Transform> BuildHierarchy(Transform holder)
        {
            var transforms = new Dictionary<long, (Transform t, GraphObject obj)>();
            foreach (var kv in _result.ById)
                if (kv.Value is Transform t && _graph.Objects.TryGetValue(kv.Key, out var obj))
                    transforms[kv.Key] = (t, obj);

            // child → parent from m_Father; ordering from the parent's m_Children, then leftovers.
            var childrenOf = new Dictionary<long, List<long>>();
            var roots = new List<long>();
            foreach (var (id, (t, obj)) in transforms)
            {
                long father = _graph.Resolve(ObjRef.From(obj.Body["m_Father"]).FileId);
                if (father != 0 && transforms.ContainsKey(father))
                {
                    if (!childrenOf.TryGetValue(father, out var list)) childrenOf[father] = list = new List<long>();
                    list.Add(id);
                }
                else roots.Add(id);
            }

            foreach (var (pid, kids) in childrenOf)
            {
                var order = new List<long>();
                foreach (var c in transforms[pid].obj.Body["m_Children"]?.Items ?? Array.Empty<YNode>())
                {
                    long cid = _graph.Resolve(ObjRef.From(c).FileId);
                    if (kids.Contains(cid) && !order.Contains(cid)) order.Add(cid);
                }
                foreach (var k in kids) if (!order.Contains(k)) order.Add(k);
                kids.Clear();
                kids.AddRange(order);
            }

            // Scene root order (SceneRoots), then anything else.
            var orderedRoots = new List<long>();
            foreach (var r in _graph.RootOrder)
            {
                long id = _graph.Resolve(r);
                if (roots.Contains(id) && !orderedRoots.Contains(id)) orderedRoots.Add(id);
            }
            foreach (var r in roots) if (!orderedRoots.Contains(r)) orderedRoots.Add(r);

            var rootTransforms = new List<Transform>();
            void Attach(long id, Transform parent)
            {
                var (t, obj) = transforms[id];
                if (TraceParent && parent != null && parent.IsChildOf(t))
                {
                    long fid = 0; foreach (var kv in transforms) if (ReferenceEquals(kv.Value.t, parent)) fid = kv.Key;
                    Console.WriteLine($"[trace-parent] attach id={id} '{t.name}' origin={obj.Origin?.Path} under id={fid} '{parent.name}'; father-raw={ObjRef.From(obj.Body["m_Father"]).FileId}");
                    var fobj = transforms[fid].obj;
                    var chain = new List<string>(); for (var a = parent; a != null; a = a.parent) { long aid = 0; foreach (var kv in transforms) if (ReferenceEquals(kv.Value.t, a)) aid = kv.Key; chain.Add($"{a.name}#{aid}"); }
                    Console.WriteLine("[trace-parent]   chain " + string.Join(" <- ", chain));
                    Console.WriteLine($"[trace-parent]   parent origin={fobj.Origin?.Path} father-raw={ObjRef.From(fobj.Body["m_Father"]).FileId} resolved={_graph.Resolve(ObjRef.From(fobj.Body["m_Father"]).FileId)}");
                }
                t.SetParent(parent, false);
                ApplyTransform(t, obj.Body);
                if (childrenOf.TryGetValue(id, out var kids))
                    foreach (var k in kids) Attach(k, t);
            }
            foreach (var r in orderedRoots)
            {
                Attach(r, holder);
                rootTransforms.Add(transforms[r].t);
            }
            return rootTransforms;
        }

        static Vector2 V2(YNode n) => new(n?.Float("x") ?? 0f, n?.Float("y") ?? 0f);
        static Vector3 V3(YNode n, float d = 0f) => new(n?.Float("x", d) ?? d, n?.Float("y", d) ?? d, n?.Float("z", d) ?? d);

        static void ApplyTransform(Transform t, YMap body)
        {
            var q = body["m_LocalRotation"];
            t.localRotation = q != null ? new Quaternion(q.Float("x"), q.Float("y"), q.Float("z"), q.Float("w", 1f)) : Quaternion.identity;
            t.localScale = V3(body["m_LocalScale"], 1f);
            if (t is RectTransform rt)
            {
                rt.localPosition = V3(body["m_LocalPosition"]);
                rt.anchorMin = V2(body["m_AnchorMin"]);
                rt.anchorMax = V2(body["m_AnchorMax"]);
                rt.pivot = body["m_Pivot"] != null ? V2(body["m_Pivot"]) : new Vector2(0.5f, 0.5f);
                rt.sizeDelta = V2(body["m_SizeDelta"]);
                rt.anchoredPosition = V2(body["m_AnchoredPosition"]);
            }
            else
            {
                t.localPosition = V3(body["m_LocalPosition"]);
            }
        }

        // Fields whose serialized name has no reflective counterpart on the engine type.
        void ApplyBuiltInExtras(EngineObject comp, GraphObject obj)
        {
            if (comp is Canvas canvas)
            {
                var cam = ObjRef.From(obj.Body["m_Camera"]);
                if (!cam.IsNull) canvas.worldCamera = Resolve(cam, typeof(Camera), obj.Origin) as Camera;
            }
            // m_Mesh is the SHARED mesh (the asset): reading it through MeshFilter.mesh would
            // make the asset the filter's private instance, and SkinnedMeshRenderer has no
            // member that answers m_Mesh at all.
            else if (comp is MeshFilter filter && obj.Body["m_Mesh"] != null)
            {
                filter.sharedMesh = Resolve(ObjRef.From(obj.Body["m_Mesh"]), typeof(Mesh), obj.Origin) as Mesh;
            }
            else if (comp is SkinnedMeshRenderer smr)
            {
                if (obj.Body["m_Mesh"] != null)
                    smr.sharedMesh = Resolve(ObjRef.From(obj.Body["m_Mesh"]), typeof(Mesh), obj.Origin) as Mesh;
                var weights = obj.Body["m_BlendShapeWeights"]?.Items;
                if (weights != null)
                    for (int i = 0; i < weights.Count; i++)
                        if (YScalar.TryFloat(weights[i].Scalar, out var w)) smr.SetBlendShapeWeight(i, w);
            }
            else if (comp is TrailRenderer trail)
            {
                var b = obj.Body;
                trail.time = b.Float("m_Time", trail.time);
                trail.minVertexDistance = b.Float("m_MinVertexDistance", trail.minVertexDistance);
                trail.emitting = b.Int("m_Emitting", 1) != 0;
                trail.autodestruct = b.Int("m_Autodestruct") != 0;
                if (b["m_Parameters"] is YMap par)
                {
                    trail.widthMultiplier = par.Float("widthMultiplier", 1f);
                    if (par["widthCurve"] != null) trail.widthCurve = SerializedReader.ReadCurve(par["widthCurve"]);
                    if (par["colorGradient"] != null) trail.colorGradient = SerializedReader.ReadGradient(par["colorGradient"]);
                    trail.numCornerVertices = par.Int("numCornerVertices");
                    trail.numCapVertices = par.Int("numCapVertices");
                    trail.alignment = (LineAlignment)par.Int("alignment");
                    trail.textureMode = (LineTextureMode)par.Int("textureMode");
                }
            }
            else if (comp is LineRenderer line)
            {
                var b = obj.Body;
                line.useWorldSpace = b.Int("m_UseWorldSpace", 1) != 0;
                line.loop = b.Int("m_Loop") != 0;
                var pts = b["m_Positions"]?.Items;
                if (pts != null)
                {
                    line.positionCount = pts.Count;
                    for (int i = 0; i < pts.Count; i++) line.SetPosition(i, V3(pts[i]));
                }
                if (b["m_Parameters"] is YMap par)
                {
                    line.widthMultiplier = par.Float("widthMultiplier", 1f);
                    if (par["widthCurve"] != null) line.widthCurve = SerializedReader.ReadCurve(par["widthCurve"]);
                    if (par["colorGradient"] != null) line.colorGradient = SerializedReader.ReadGradient(par["colorGradient"]);
                    line.numCornerVertices = par.Int("numCornerVertices");
                    line.numCapVertices = par.Int("numCapVertices");
                    line.alignment = (LineAlignment)par.Int("alignment");
                    line.textureMode = (LineTextureMode)par.Int("textureMode");
                }
            }
            else if (comp is Animator animator && obj.Body["m_Controller"] != null)
            {
                var controller = ObjRef.From(obj.Body["m_Controller"]);
                if (!controller.IsNull)
                    animator.runtimeAnimatorController = Resolve(controller, typeof(RuntimeAnimatorController), obj.Origin) as RuntimeAnimatorController;
            }
        }

        // ── IReferenceResolver ───────────────────────────────────────────────

        public object Resolve(ObjRef reference, Type fieldType, AssetFile origin)
        {
            if (reference.IsNull) return null;
            // Local refs (and refs that name this very file by guid) resolve inside the scene.
            bool local = reference.IsLocal || (origin == _graph.File && reference.Guid == _graph.File.Guid);
            if (local && origin != null && origin != _graph.File && !reference.IsLocal)
                local = false;
            if (local)
            {
                long id = _graph.Resolve(reference.FileId);
                if (_result.ById.TryGetValue(id, out var o)) return AdaptSceneObject(o, fieldType);
                // A model prefab's own guid refs name its SUB-ASSETS (meshes, materials, avatar),
                // which are not scene objects — the asset loader owns those.
                if (reference.IsLocal || !AssetDatabase.IsModelPath(_graph.File?.Path)) return null;
            }
            return _assets.Load(reference, fieldType);
        }

        static object AdaptSceneObject(EngineObject o, Type fieldType)
        {
            if (fieldType.IsInstanceOfType(o)) return o;
            if (o is Component c)
            {
                if (fieldType == typeof(GameObject)) return c.gameObject;
                if (typeof(Component).IsAssignableFrom(fieldType)) return c.gameObject.GetComponent(fieldType);
            }
            if (o is GameObject go && typeof(Component).IsAssignableFrom(fieldType)) return go.GetComponent(fieldType);
            return null;
        }

        public void OnUnityEvent(object unityEvent, YNode persistentCalls, AssetFile origin)
            => _pendingEvents.Add((unityEvent, persistentCalls, origin));

        // ── Persistent calls ─────────────────────────────────────────────────

        void WirePersistentCalls(object evt, YNode calls, AssetFile origin)
        {
            var list = calls?["m_Calls"]?.Items;
            if (list == null || list.Count == 0) return;
            var addListener = evt.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == "AddListener" && m.GetParameters().Length == 1);
            // Inspector calls go on the PERSISTENT tier when the event type has one (they
            // survive RemoveAllListeners and run before runtime listeners, like the original).
            var addPersistent = evt.GetType().GetMethod("AddPersistentListener", BindingFlags.Instance | BindingFlags.Public);
            if (addListener == null) return;
            var actionType = addListener.GetParameters()[0].ParameterType;
            var eventArgs = actionType.IsGenericType ? actionType.GetGenericArguments() : Type.EmptyTypes;

            foreach (var call in list)
            {
                if (call.Int("m_CallState", 2) == 0) continue; // Off
                var targetObj = Resolve(ObjRef.From(call["m_Target"]), typeof(EngineObject), origin) as EngineObject;
                string methodName = call.Str("m_MethodName");
                if (TraceCall != null && methodName == TraceCall)
                    Console.Error.WriteLine($"[trace-call] wire {methodName} on {evt.GetType().Name}#{evt.GetHashCode()} target={(targetObj == null ? "NULL fileID " + ObjRef.From(call["m_Target"]).FileId : targetObj.name + "/" + targetObj.GetType().Name)} origin={origin?.Path}");
                if (targetObj == null || string.IsNullOrEmpty(methodName)) continue;
                int mode = call.Int("m_Mode", 1);
                var args = call["m_Arguments"];
                var invoker = BuildInvoker(targetObj, methodName, mode, args, eventArgs, origin);
                if (invoker == null)
                {
                    _result.Warnings.Add($"persistent call {targetObj.GetType().Name}.{methodName} (mode {mode}) not found");
                    continue;
                }
                var del = MakeListener(actionType, eventArgs, invoker);
                if (del != null && del.GetType() != actionType)
                    del = Delegate.CreateDelegate(actionType, del.Target, del.Method);
                try { (addPersistent ?? addListener).Invoke(evt, new object[] { del }); }
                catch (Exception e) { _result.Warnings.Add($"AddListener {methodName}: {e.Message}"); }
            }
        }

        Func<object[], object> BuildInvoker(EngineObject target, string methodName, int mode, YNode args, Type[] eventArgs, AssetFile origin)
        {
            // Unity modes: 0 EventDefined, 1 Void, 2 Object, 3 Int, 4 Float, 5 String, 6 Bool.
            Type argType = mode switch
            {
                2 => typeof(EngineObject), 3 => typeof(int), 4 => typeof(float), 5 => typeof(string), 6 => typeof(bool), _ => null,
            };
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var candidates = target.GetType().GetMethods(flags).Where(m => m.Name == methodName).ToList();
            MethodInfo method = mode switch
            {
                1 => candidates.FirstOrDefault(m => m.GetParameters().Length == 0),
                0 => candidates.FirstOrDefault(m => m.GetParameters().Length == eventArgs.Length
                        && m.GetParameters().Select(p => p.ParameterType).Zip(eventArgs).All(z => z.First.IsAssignableFrom(z.Second))),
                _ => candidates.FirstOrDefault(m => m.GetParameters().Length == 1
                        && (m.GetParameters()[0].ParameterType == argType || argType.IsAssignableFrom(m.GetParameters()[0].ParameterType)
                            || m.GetParameters()[0].ParameterType.IsAssignableFrom(argType)
                            || (mode == 3 && m.GetParameters()[0].ParameterType.IsEnum))),
            };
            if (method == null) return null;

            object fixedArg = null;
            if (mode >= 2)
            {
                var ptype = method.GetParameters()[0].ParameterType;
                fixedArg = mode switch
                {
                    2 => Resolve(ObjRef.From(args?["m_ObjectArgument"]), ptype, origin),
                    3 => ptype.IsEnum ? Enum.ToObject(ptype, args?.Int("m_IntArgument") ?? 0) : args?.Int("m_IntArgument") ?? 0,
                    4 => args?.Float("m_FloatArgument") ?? 0f,
                    5 => args?.Str("m_StringArgument") ?? "",
                    6 => args?.Bool("m_BoolArgument") ?? false,
                    _ => null,
                };
            }
            return eventValues =>
            {
                if (target is EngineObject eo && !eo) return null; // destroyed target
                object[] callArgs = mode switch { 1 => Array.Empty<object>(), 0 => eventValues, _ => new[] { fixedArg } };
                if (TraceCall != null && methodName == TraceCall)
                    Console.Error.WriteLine($"[trace-call] invoke {methodName} on {target.name} args=[{string.Join(",", callArgs)}] frame {Time.frameCount}");
                try { return method.Invoke(target, callArgs); }
                catch (TargetInvocationException e) { Debug.LogException(e.InnerException ?? e); return null; }
            };
        }

        static Delegate MakeListener(Type actionType, Type[] eventArgs, Func<object[], object> invoker)
        {
            switch (eventArgs.Length)
            {
                case 0: return new Action(() => invoker(Array.Empty<object>()));
                case 1:
                {
                    var m = typeof(SceneInstantiator).GetMethod(nameof(Listener1), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(eventArgs);
                    return (Delegate)m.Invoke(null, new object[] { invoker });
                }
                case 2:
                {
                    var m = typeof(SceneInstantiator).GetMethod(nameof(Listener2), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(eventArgs);
                    return (Delegate)m.Invoke(null, new object[] { invoker });
                }
                default: return null;
            }
        }

        static Action<T0> Listener1<T0>(Func<object[], object> invoker) => a => invoker(new object[] { a });
        static Action<T0, T1> Listener2<T0, T1>(Func<object[], object> invoker) => (a, b) => invoker(new object[] { a, b });
    }
}
