using System;
using System.Collections.Generic;
using CosmicShore.Content.Yaml;
using CosmicShore.Engine;

namespace CosmicShore.Content.AnimationImport
{
    /// <summary>
    /// Builds <see cref="AnimatorController"/> and <see cref="AnimationClip"/> objects from the
    /// project's .controller and .anim YAML. Covers what the game's controllers use: layers, nested
    /// state machines (flattened), states with a clip motion, state and Any State transitions with
    /// exit time / fixed or normalized duration / offset / parameter conditions, write-defaults and
    /// speed parameters; clips' float curves plus position, scale, euler and rotation curves split
    /// into per-axis bindings; blend-tree motions (1D, 2D simple/freeform directional, freeform
    /// cartesian, direct; nested) and "motion time" parameters. A clip that lives inside a model
    /// file comes through <c>loadClip</c> like any other (see <c>Models.FbxAnimationImporter</c>).
    /// </summary>
    public static class AnimatorImporter
    {
        public static AnimatorController LoadController(AssetFile file, Func<ObjRef, AnimationClip> loadClip)
        {
            UnityDocument root = null;
            foreach (var d in file.Documents)
                if (d.ClassId == 91) { root = d; break; }
            if (root == null) return null;

            var controller = new AnimatorController { name = root.Body.Str("m_Name") ?? "AnimatorController" };
            var parameters = new List<AnimatorControllerParameter>();
            foreach (var p in root.Body["m_AnimatorParameters"]?.Items ?? Array.Empty<YNode>())
                parameters.Add(new AnimatorControllerParameter
                {
                    name = p.Str("m_Name"),
                    type = (AnimatorControllerParameterType)p.Int("m_Type", 1),
                    defaultFloat = p.Float("m_DefaultFloat"),
                    defaultInt = p.Int("m_DefaultInt"),
                    defaultBool = p.Bool("m_DefaultBool"),
                });
            controller.Parameters = parameters.ToArray();

            var clips = new List<AnimationClip>();
            foreach (var l in root.Body["m_AnimatorLayers"]?.Items ?? Array.Empty<YNode>())
            {
                var layer = new AnimatorLayerData { Name = l.Str("m_Name") ?? "Base Layer", DefaultWeight = l.Float("m_DefaultWeight", 0f) };
                var smRef = ObjRef.From(l["m_StateMachine"]);
                var sm = file.Get(smRef.FileId);
                var stateIndex = new Dictionary<long, int>();
                var stateDocs = new List<UnityDocument>();
                if (sm != null) CollectStates(file, sm, stateDocs, new HashSet<long>());
                foreach (var sd in stateDocs)
                {
                    stateIndex[sd.FileId] = layer.States.Count;
                    string sname = sd.Body.Str("m_Name") ?? "";
                    var st = new AnimatorStateData
                    {
                        Name = sname,
                        NameHash = Animator.StringToHash(sname),
                        FullPathHash = Animator.StringToHash(layer.Name + "." + sname),
                        TagHash = Animator.StringToHash(sd.Body.Str("m_Tag") ?? ""),
                        Speed = sd.Body.Float("m_Speed", 1f),
                        WriteDefaults = sd.Body.Bool("m_WriteDefaultValues", true),
                        SpeedParameter = sd.Body.Bool("m_SpeedParameterActive") ? sd.Body.Str("m_SpeedParameter") : null,
                    };
                    if (sd.Body.Bool("m_TimeParameterActive") && sd.Body.Str("m_TimeParameter") is { Length: > 0 } tp) st.TimeParameter = tp;
                    var motion = ObjRef.From(sd.Body["m_Motion"]);
                    if (!motion.IsNull && !motion.IsLocal) st.Clip = loadClip(motion);
                    else if (!motion.IsNull && motion.IsLocal) st.Tree = ReadTree(file, motion.FileId, loadClip, clips, 0);
                    if (st.Clip != null) clips.Add(st.Clip);
                    layer.States.Add(st);
                }
                // Transitions after every state has an index.
                for (int i = 0; i < stateDocs.Count; i++)
                    foreach (var tr in stateDocs[i].Body["m_Transitions"]?.Items ?? Array.Empty<YNode>())
                        if (ReadTransition(file, ObjRef.From(tr).FileId, stateIndex) is { } t) layer.States[i].Transitions.Add(t);
                if (sm != null)
                {
                    foreach (var tr in sm.Body["m_AnyStateTransitions"]?.Items ?? Array.Empty<YNode>())
                        if (ReadTransition(file, ObjRef.From(tr).FileId, stateIndex) is { } t) layer.AnyStateTransitions.Add(t);
                    var def = ObjRef.From(sm.Body["m_DefaultState"]);
                    layer.DefaultState = stateIndex.TryGetValue(def.FileId, out var di) ? di : 0;
                }
                controller.Layers.Add(layer);
            }
            if (controller.Layers.Count > 0) controller.Layers[0].DefaultWeight = 1f;
            controller.animationClips = clips.ToArray();
            return controller;
        }

        /// <summary>A BlendTree document (class 206) and, recursively, its child trees.</summary>
        static BlendTree ReadTree(AssetFile file, long fileId, Func<ObjRef, AnimationClip> loadClip, List<AnimationClip> clips, int depth)
        {
            var d = file.Get(fileId);
            if (d == null || d.ClassId != 206 || depth > 16) return null;
            var b = d.Body;
            var tree = new BlendTree
            {
                Name = b.Str("m_Name") ?? "BlendTree",
                Type = (BlendTreeType)b.Int("m_BlendType"),
                ParameterX = b.Str("m_BlendParameter") ?? "",
                ParameterY = b.Str("m_BlendParameterY") ?? "",
                NormalizedBlendValues = b.Bool("m_NormalizedBlendValues"),
            };
            foreach (var c in b["m_Childs"]?.Items ?? Array.Empty<YNode>())
            {
                var child = new BlendTreeChild
                {
                    Threshold = c.Float("m_Threshold"),
                    Position = new Vector2(c["m_Position"]?.Float("x") ?? 0f, c["m_Position"]?.Float("y") ?? 0f),
                    TimeScale = c.Float("m_TimeScale", 1f),
                    DirectParameter = c.Str("m_DirectBlendParameter") ?? "",
                };
                var m = ObjRef.From(c["m_Motion"]);
                if (!m.IsNull && !m.IsLocal) { child.Clip = loadClip(m); if (child.Clip != null) clips.Add(child.Clip); }
                else if (!m.IsNull) child.Tree = ReadTree(file, m.FileId, loadClip, clips, depth + 1);
                tree.Children.Add(child);
            }
            return tree;
        }

        static void CollectStates(AssetFile file, UnityDocument sm, List<UnityDocument> into, HashSet<long> seen)
        {
            if (!seen.Add(sm.FileId)) return;
            foreach (var cs in sm.Body["m_ChildStates"]?.Items ?? Array.Empty<YNode>())
                if (file.Get(ObjRef.From(cs["m_State"]).FileId) is { } st) into.Add(st);
            foreach (var csm in sm.Body["m_ChildStateMachines"]?.Items ?? Array.Empty<YNode>())
                if (file.Get(ObjRef.From(csm["m_StateMachine"]).FileId) is { } child) CollectStates(file, child, into, seen);
        }

        static AnimatorTransitionData ReadTransition(AssetFile file, long fileId, Dictionary<long, int> stateIndex)
        {
            var d = file.Get(fileId);
            if (d == null) return null;
            var b = d.Body;
            var t = new AnimatorTransitionData
            {
                HasExitTime = b.Bool("m_HasExitTime"),
                ExitTime = b.Float("m_ExitTime", 0.75f),
                Duration = b.Float("m_TransitionDuration", 0.25f),
                HasFixedDuration = b.Bool("m_HasFixedDuration", true),
                Offset = b.Float("m_TransitionOffset"),
                Mute = b.Bool("m_Mute"),
            };
            var dst = ObjRef.From(b["m_DstState"]);
            t.Destination = !dst.IsNull && stateIndex.TryGetValue(dst.FileId, out var di) ? di : -1;
            foreach (var c in b["m_Conditions"]?.Items ?? Array.Empty<YNode>())
                t.Conditions.Add(new AnimatorCondition
                {
                    Mode = (AnimatorConditionMode)c.Int("m_ConditionMode", 1),
                    Parameter = c.Str("m_ConditionEvent") ?? "",
                    Threshold = c.Float("m_EventTreshold"), // [sic] Unity's serialized name
                });
            return t;
        }

        public static AnimationClip LoadClip(AssetFile file)
        {
            UnityDocument doc = null;
            foreach (var d in file.Documents)
                if (d.ClassId == 74) { doc = d; break; }
            if (doc == null) return null;
            var b = doc.Body;
            var clip = new AnimationClip { name = b.Str("m_Name") ?? "Clip", frameRate = b.Float("m_SampleRate", 60f) };
            float maxKey = 0f;

            foreach (var c in b["m_FloatCurves"]?.Items ?? Array.Empty<YNode>())
            {
                var curve = ReadCurve(c["curve"], null, ref maxKey);
                clip.Bindings.Add(new ClipBinding
                {
                    Path = c.Str("path") ?? "",
                    ClassId = c.Int("classID"),
                    Attribute = c.Str("attribute") ?? "",
                    ScriptGuid = ObjRef.From(c["script"]) is { IsNull: false } s ? s.Guid : null,
                    Curve = curve,
                });
            }
            AddVector(clip, b["m_PositionCurves"], "m_LocalPosition", 3, ref maxKey);
            AddVector(clip, b["m_ScaleCurves"], "m_LocalScale", 3, ref maxKey);
            AddVector(clip, b["m_EulerCurves"], "localEulerAnglesRaw", 3, ref maxKey);
            AddVector(clip, b["m_RotationCurves"], "m_LocalRotation", 4, ref maxKey);

            var settings = b["m_AnimationClipSettings"];
            float start = settings?.Float("m_StartTime") ?? 0f, stop = settings?.Float("m_StopTime") ?? 0f;
            clip.length = stop > start ? stop - start : maxKey;
            clip.isLooping = settings?.Bool("m_LoopTime") ?? false;
            clip.wrapMode = clip.isLooping ? WrapMode.Loop : WrapMode.Once;
            return clip;
        }

        static readonly string[] s_axes = { "x", "y", "z", "w" };

        static void AddVector(AnimationClip clip, YNode curves, string attribute, int dims, ref float maxKey)
        {
            foreach (var c in curves?.Items ?? Array.Empty<YNode>())
            {
                string path = c.Str("path") ?? "";
                for (int k = 0; k < dims; k++)
                    clip.Bindings.Add(new ClipBinding
                    {
                        Path = path,
                        ClassId = 4,
                        Attribute = attribute + "." + s_axes[k],
                        Curve = ReadCurve(c["curve"], s_axes[k], ref maxKey),
                    });
            }
        }

        /// <summary>A serialized curve; <paramref name="axis"/> picks one component of vector keys.</summary>
        static AnimationCurve ReadCurve(YNode n, string axis, ref float maxKey)
        {
            var curve = new AnimationCurve();
            foreach (var k in n?["m_Curve"]?.Items ?? Array.Empty<YNode>())
            {
                float time = k.Float("time");
                float value = axis == null ? k.Float("value") : (k["value"]?.Float(axis) ?? 0f);
                float inT = axis == null ? Slope(k, "inSlope") : Slope(k["inSlope"], axis);
                float outT = axis == null ? Slope(k, "outSlope") : Slope(k["outSlope"], axis);
                curve.AddKey(new Keyframe(time, value, inT, outT));
                if (time > maxKey) maxKey = time;
            }
            return curve;
        }

        static float Slope(YNode n, string key)
        {
            var s = n?[key]?.Scalar;
            if (s == null) return 0f;
            if (s == "Infinity") return float.PositiveInfinity;
            if (s == "-Infinity") return float.NegativeInfinity;
            return YScalar.TryFloat(s, out var f) ? f : 0f;
        }
    }
}
