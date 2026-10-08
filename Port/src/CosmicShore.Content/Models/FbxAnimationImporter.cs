using System;
using System.Collections.Generic;
using CosmicShore.Engine;

namespace CosmicShore.Content.Models
{
    /// <summary>
    /// Unity's ModelImporter animation import for a Generic rig: one <see cref="AnimationClip"/>
    /// per <c>clipAnimations</c> entry of the meta, cut from the FBX take (AnimationStack) it
    /// names between <c>firstFrame</c> and <c>lastFrame</c> at the file's frame rate.
    ///
    /// Every Model node a take animates (any Lcl Translation / Rotation / Scaling curve node,
    /// constant or not) is resampled once per frame through the same transform evaluation and
    /// FBX→Unity conversion the static hierarchy uses (<see cref="FbxModelImporter.LocalMatrix(FbxObject, DVec3, DVec3, DVec3)"/>,
    /// axis conversion on top-level nodes, handedness and unit scale), then decomposed into
    /// m_LocalPosition / m_LocalRotation / m_LocalScale curves on the node's path under the
    /// model root. Rotation keys are kept in the rest pose's hemisphere so a blend tree's
    /// weighted sum of several clips never mixes q and -q. Blend-shape channel weights
    /// (DeformPercent) become "blendShape.&lt;channel&gt;" curves on the skinned mesh's node.
    ///
    /// The model root's own transform is not animated (its curves would overwrite the
    /// transform a prefab places the model at; the game's rigs never animate it).
    /// </summary>
    public static class FbxAnimationImporter
    {
        public const double TicksPerSecond = 46186158000.0;

        /// <summary>The clip whose internal ID (the sub-asset fileID) is <paramref name="fileId"/>, or null.</summary>
        public static AnimationClip ImportClip(ImportedModel model, long fileId)
        {
            if (model?.Scene == null || !model.Settings.ImportAnimation) return null;
            foreach (var c in model.Settings.Clips)
                if (c.InternalId == fileId) return Import(model, c);
            // No clipAnimations entry: Unity's default clip per take.
            foreach (var st in model.Scene.ObjectList)
            {
                if (st.Kind != "AnimationStack") continue;
                if (ModelFileIds.Hash("AnimationClip", st.Name) != fileId) continue;
                double fps = FrameRate(model.Scene);
                return Import(model, new ModelClipSettings
                {
                    Name = st.Name, TakeName = st.Name,
                    FirstFrame = (float)(st.PropLong("LocalStart") / TicksPerSecond * fps),
                    LastFrame = (float)(st.PropLong("LocalStop") / TicksPerSecond * fps),
                });
            }
            return null;
        }

        /// <summary>
        /// The clips Unity makes from <paramref name="model"/>, as (name, sub-asset fileID): the
        /// meta's <c>clipAnimations</c>, or one per take when it lists none. Empty when the meta turns
        /// animation import off.
        /// </summary>
        public static List<(string Name, long FileId)> ListClips(ImportedModel model)
        {
            var list = new List<(string, long)>();
            if (model?.Scene == null || !model.Settings.ImportAnimation) return list;
            if (model.Settings.Clips.Count > 0)
            {
                foreach (var c in model.Settings.Clips) list.Add((c.Name, c.InternalId));
                return list;
            }
            foreach (var st in model.Scene.ObjectList)
                if (st.Kind == "AnimationStack") list.Add((st.Name, ModelFileIds.Hash("AnimationClip", st.Name)));
            return list;
        }

        /// <summary>FBX GlobalSettings TimeMode → frames per second (FbxTime::EMode).</summary>
        public static double FrameRate(FbxScene scene)
        {
            int mode = scene.GlobalInt("TimeMode", 0);
            return mode switch
            {
                1 => 120, 2 => 100, 3 => 60, 4 => 50, 5 => 48, 6 => 30, 7 => 30,
                8 => 29.97, 9 => 29.97, 10 => 25, 11 => 24, 12 => 1000, 13 => 23.976,
                14 => scene.GlobalDouble("CustomFrameRate", 30) is > 0 and var f ? f : 30,
                15 => 96, 16 => 72, 17 => 59.94, 18 => 119.88,
                _ => 30,
            };
        }

        sealed class Channel
        {
            public long[] Times;
            public double[] Values;
            public bool[] Constant;

            public double Evaluate(long tick)
            {
                int n = Times.Length;
                if (n == 0) return 0;
                if (tick <= Times[0]) return Values[0];
                if (tick >= Times[n - 1]) return Values[n - 1];
                int lo = 0, hi = n - 1;
                while (hi - lo > 1)
                {
                    int mid = (lo + hi) >> 1;
                    if (Times[mid] <= tick) lo = mid; else hi = mid;
                }
                if (Constant[lo]) return Values[lo];
                double f = (double)(tick - Times[lo]) / (Times[hi] - Times[lo]);
                return Values[lo] + (Values[hi] - Values[lo]) * f;
            }
        }

        /// <summary>One animated vector property: per axis a curve, else the curve node's default.</summary>
        sealed class Vec3Track
        {
            public readonly Channel[] Axes = new Channel[3];
            public readonly double[] Defaults = new double[3];

            public DVec3 Evaluate(long tick)
            {
                double x = Axes[0]?.Evaluate(tick) ?? Defaults[0];
                double y = Axes[1]?.Evaluate(tick) ?? Defaults[1];
                double z = Axes[2]?.Evaluate(tick) ?? Defaults[2];
                return new DVec3(x, y, z);
            }
        }

        sealed class NodeTracks
        {
            public Vec3Track T, R, S;
        }

        static AnimationClip Import(ImportedModel model, ModelClipSettings cs)
        {
            var scene = model.Scene;
            FbxObject stack = null;
            foreach (var o in scene.ObjectList)
                if (o.Kind == "AnimationStack" && o.Name == cs.TakeName) { stack = o; break; }
            if (stack == null) return null;
            FbxObject layer = null;
            foreach (var c in stack.Children)
                if (c.Kind == "AnimationLayer") { layer = c; break; }

            // Curve node → the object property it drives.
            var targets = new Dictionary<FbxObject, (FbxObject Obj, string Prop)>();
            foreach (var o in scene.ObjectList)
                foreach (var (src, prop) in o.PropertyInputs)
                    if (src.Kind == "AnimationCurveNode") targets[src] = (o, prop);

            var nodeTracks = new Dictionary<FbxObject, NodeTracks>();
            var shapeTracks = new List<(FbxObject Channel, Channel Curve, double Default)>();
            foreach (var cn in layer?.Children ?? new List<FbxObject>())
            {
                if (cn.Kind != "AnimationCurveNode" || !targets.TryGetValue(cn, out var tgt)) continue;
                if (tgt.Obj.Kind == "Model")
                {
                    if (!nodeTracks.TryGetValue(tgt.Obj, out var nt)) nodeTracks[tgt.Obj] = nt = new NodeTracks();
                    var track = ReadVec3(cn);
                    switch (tgt.Prop)
                    {
                        case "Lcl Translation": nt.T = track; break;
                        case "Lcl Rotation": nt.R = track; break;
                        case "Lcl Scaling": nt.S = track; break;
                    }
                }
                else if (tgt.Obj.Kind == "Deformer" && tgt.Obj.SubClass == "BlendShapeChannel" && tgt.Prop == "DeformPercent")
                {
                    Channel curve = null;
                    foreach (var (src, _) in cn.PropertyInputs)
                        if (src.Kind == "AnimationCurve") { curve = ReadChannel(src); break; }
                    shapeTracks.Add((tgt.Obj, curve, cn.PropDouble("d|DeformPercent")));
                }
            }

            double fps = FrameRate(scene);
            double first = cs.FirstFrame, last = Math.Max(cs.LastFrame, cs.FirstFrame);
            var frames = new List<double>();
            for (double f = first; f < last - 1e-6; f += 1) frames.Add(f);
            frames.Add(last);
            float length = (float)((last - first) / fps);

            var clip = new AnimationClip
            {
                name = cs.Name,
                frameRate = (float)fps,
                length = length,
                isLooping = cs.LoopTime,
                wrapMode = cs.LoopTime ? WrapMode.Loop : WrapMode.Once,
            };

            var nodeBySource = new Dictionary<FbxObject, ModelNode>();
            foreach (var n in model.Nodes) if (n.Source != null) nodeBySource.TryAdd(n.Source, n);

            int count = frames.Count;
            var times = new float[count];
            var ticks = new long[count];
            for (int i = 0; i < count; i++)
            {
                times[i] = (float)((frames[i] - first) / fps);
                ticks[i] = (long)Math.Round(frames[i] / fps * TicksPerSecond);
            }

            foreach (var (obj, tr) in nodeTracks)
            {
                if (!nodeBySource.TryGetValue(obj, out var node) || node == model.Root) continue;
                string path = RelativePath(node);
                var pos = new Vector3[count];
                var rot = new Quaternion[count];
                var scl = new Vector3[count];
                var restT = V(obj.PropVector("Lcl Translation"));
                var restR = V(obj.PropVector("Lcl Rotation"));
                var restS = V(obj.PropVector("Lcl Scaling", 1, 1, 1));
                var prev = node.LocalRotation;
                for (int i = 0; i < count; i++)
                {
                    var t = tr.T?.Evaluate(ticks[i]) ?? restT;
                    var r = tr.R?.Evaluate(ticks[i]) ?? restR;
                    var s = tr.S?.Evaluate(ticks[i]) ?? restS;
                    var local = FbxModelImporter.LocalMatrix(obj, t, r, s);
                    if (node.TopLevel && !model.Settings.BakeAxisConversion) local = model.AxisConversion * local;
                    FbxModelImporter.ToUnity(local, model.UnitScale).Decompose(out var dt, out var dq, out var ds);
                    pos[i] = new Vector3((float)dt.X, (float)dt.Y, (float)dt.Z);
                    var q = new Quaternion((float)dq.x, (float)dq.y, (float)dq.z, (float)dq.w);
                    if (q.x * prev.x + q.y * prev.y + q.z * prev.z + q.w * prev.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    rot[i] = q;
                    prev = q;
                    scl[i] = new Vector3((float)ds.X, (float)ds.Y, (float)ds.Z);
                }
                if (tr.T != null)
                    for (int k = 0; k < 3; k++) Add(clip, path, 4, "m_LocalPosition." + Axis(k), times, i => Get(pos[i], k));
                if (tr.R != null)
                    for (int k = 0; k < 4; k++) Add(clip, path, 4, "m_LocalRotation." + Axis(k), times, i => GetQ(rot[i], k));
                if (tr.S != null)
                    for (int k = 0; k < 3; k++) Add(clip, path, 4, "m_LocalScale." + Axis(k), times, i => Get(scl[i], k));
            }

            foreach (var (channel, curve, def) in shapeTracks)
            {
                var meshNode = MeshNodeOf(channel, nodeBySource);
                if (meshNode == null) continue;
                Add(clip, RelativePath(meshNode), 137, "blendShape." + channel.Name, times,
                    i => (float)(curve?.Evaluate(ticks[i]) ?? def));
            }
            return clip;
        }

        static ModelNode MeshNodeOf(FbxObject channel, Dictionary<FbxObject, ModelNode> nodes)
        {
            // BlendShapeChannel → BlendShape deformer → Geometry → Model.
            foreach (var bs in channel.Parents)
                foreach (var geo in bs.Parents)
                    foreach (var model in geo.Parents)
                        if (model.Kind == "Model" && nodes.TryGetValue(model, out var n)) return n;
            return null;
        }

        static string RelativePath(ModelNode node)
            => node.Path.Length > ModelFileIds.RootPath.Length ? node.Path.Substring(ModelFileIds.RootPath.Length + 1) : "";

        static void Add(AnimationClip clip, string path, int classId, string attribute, float[] times, Func<int, float> value)
        {
            int n = times.Length;
            var curve = new AnimationCurve();
            for (int i = 0; i < n; i++)
            {
                float v = value(i);
                float inT = i > 0 && times[i] > times[i - 1] ? (v - value(i - 1)) / (times[i] - times[i - 1]) : 0f;
                float outT = i < n - 1 && times[i + 1] > times[i] ? (value(i + 1) - v) / (times[i + 1] - times[i]) : 0f;
                curve.AddKey(new Keyframe(times[i], v, inT, outT));
            }
            clip.Bindings.Add(new ClipBinding { Path = path, ClassId = classId, Attribute = attribute, Curve = curve });
        }

        static Vec3Track ReadVec3(FbxObject cn)
        {
            var t = new Vec3Track();
            string[] names = { "d|X", "d|Y", "d|Z" };
            for (int k = 0; k < 3; k++) t.Defaults[k] = cn.PropDouble(names[k]);
            foreach (var (src, prop) in cn.PropertyInputs)
            {
                if (src.Kind != "AnimationCurve") continue;
                int k = Array.IndexOf(names, prop);
                if (k >= 0) t.Axes[k] = ReadChannel(src);
            }
            return t;
        }

        static Channel ReadChannel(FbxObject curve)
        {
            var n = curve.Node;
            var times = n.LongArray("KeyTime");
            var values = n.DoubleArray("KeyValueFloat");
            int count = Math.Min(times.Length, values.Length);
            if (count != times.Length) Array.Resize(ref times, count);
            if (count != values.Length) Array.Resize(ref values, count);
            var constant = new bool[count];
            // Key attributes are run-length encoded: KeyAttrFlags[i] applies to KeyAttrRefCount[i] keys.
            var flags = n.IntArray("KeyAttrFlags");
            var refs = n.IntArray("KeyAttrRefCount");
            int key = 0;
            for (int a = 0; a < flags.Length && key < count; a++)
            {
                int run = a < refs.Length ? refs[a] : count - key;
                bool isConstant = (flags[a] & 0x02) != 0;
                for (int r = 0; r < run && key < count; r++) constant[key++] = isConstant;
            }
            return new Channel { Times = times, Values = values, Constant = constant };
        }

        static DVec3 V(double[] a) => new(a[0], a[1], a[2]);
        static string Axis(int k) => k switch { 0 => "x", 1 => "y", 2 => "z", _ => "w" };
        static float Get(Vector3 v, int k) => k == 0 ? v.x : k == 1 ? v.y : v.z;
        static float GetQ(Quaternion q, int k) => k == 0 ? q.x : k == 1 ? q.y : k == 2 ? q.z : q.w;
    }
}
