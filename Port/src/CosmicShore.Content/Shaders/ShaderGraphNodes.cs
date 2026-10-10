using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace CosmicShore.Content.Shaders
{
    // The node emitters: one case per Shader Graph node type the project uses. Each follows the
    // HLSL the package generates for that node (com.unity.shadergraph 17.3, Editor/Data/Nodes).
    public sealed partial class ShaderGraphCompiler
    {
        /// <summary>Every node type the compiler emits code for.</summary>
        public static IReadOnlyCollection<string> SupportedNodeTypes => s_supported;

        /// <summary>Node types that carry no code of their own (handled by the walk itself).</summary>
        public static IReadOnlyCollection<string> StructuralNodeTypes { get; } = new[] { "BlockNode", "SubGraphOutputNode", "PreviewNode", "RedirectNodeData" };

        /// <summary>Supported node types whose output only approximates Unity's (and why).</summary>
        public static IReadOnlyDictionary<string, string> ApproximateNodeTypes { get; } = new Dictionary<string, string>
        {
            ["SceneColorNode"] = "no opaque-texture copy: returns black",
            ["SceneDepthNode"] = "no depth-texture copy: returns the far plane",
            ["ObjectNode"] = "world bounds are the object's origin",
            ["CameraNode"] = "orthographic width/height read the camera's ortho params only",
        };

        static readonly HashSet<string> s_supported = new(StringComparer.Ordinal)
        {
            "PropertyNode", "BlockNode", "MultiplyNode", "AddNode", "SampleTexture2DNode", "CustomFunctionNode", "BranchNode",
            "SubtractNode", "BlendNode", "TimeNode", "PositionNode", "NormalVectorNode", "ColorNode", "DivideNode",
            "TilingAndOffsetNode", "UVNode", "DotProductNode", "SubGraphNode", "RedirectNodeData", "SplitNode",
            "ComparisonNode", "FresnelNode", "ClampNode", "RotateAboutAxisNode", "OneMinusNode", "Vector1Node",
            "SubGraphOutputNode", "AbsoluteNode", "Vector2Node", "Vector3Node", "Vector4Node", "ReplaceColorNode", "CombineNode",
            "Texture2DPropertiesNode", "LerpNode", "VertexColorNode", "DistanceNode", "NoiseNode", "SimpleNoiseNode", "ViewVectorNode",
            "TangentVectorNode", "NormalizeNode", "Texture2DAssetNode", "IsFrontFaceNode", "RotateNode", "PowerNode",
            "ObjectNode", "VoronoiNode", "SmoothstepNode", "OrNode", "StepNode", "LengthNode", "RandomRangeNode",
            "BitangentVectorNode", "SquareRootNode", "SineNode", "GradientNoiseNode", "ScreenPositionNode", "CameraNode",
            "SpherizeNode", "InvertColorsNode", "NegateNode", "TransformNode", "MaximumNode", "ViewDirectionNode",
            "PreviewNode", "AndNode", "ColorspaceConversionNode", "CrossProductNode", "IntegerNode", "SceneColorNode",
            "NormalFromHeightNode", "TwirlNode", "MinimumNode", "FractionNode", "AllNode", "AnyNode", "NotNode", "SceneDepthNode",
            "TransformationMatrixNode", "DDXYNode", "DDXNode", "DDYNode", "NoiseSineWaveNode", "GatherTexture2DNode", "SphereMaskNode",
            "ArcsineNode", "ArccosineNode", "ArctangentNode", "Arctangent2Node", "PolarCoordinatesNode", "CosineNode", "TangentNode",
            "ModuloNode", "HueNode", "InverseLerpNode", "BooleanNode", "SliderNode", "GradientNode", "SampleGradient",
            "RemapNode", "TriangleWaveNode", "SquareWaveNode", "SawtoothWaveNode", "RoundNode", "FloorNode", "CeilingNode",
            "TruncateNode", "SignNode", "SaturateNode", "ExponentialNode", "LogNode", "ReciprocalNode", "ReciprocalSquareRootNode",
            "VertexIDNode", "SwizzleNode", "MatrixConstructionNode", "ConstantNode", "ReflectionNode", "ProjectionNode",
            "RejectionNode", "ChannelMaskNode", "NormalStrengthNode", "NormalBlendNode", "NormalUnpackNode", "ContrastNode",
            "SaturationNode", "PosterizeNode", "DitherNode", "RectangleNode", "EllipseNode", "CheckerboardNode",
        };

        internal sealed partial class Stage
        {
            void EmitNode(Scope sc, SgNode n, Dictionary<int, V> outs)
            {
                switch (n.Type)
                {
                    // ── Structure ──
                    case "PropertyNode": EmitProperty(sc, n, outs); return;
                    case "SubGraphNode": EmitSubGraph(sc, n, outs); return;
                    case "CustomFunctionNode": EmitCustomFunction(sc, n, outs); return;
                    case "RedirectNodeData":
                    case "PreviewNode":
                    {
                        var k = Dynamic(sc, n, 0);
                        outs[1] = In(sc, n, 0, k);
                        return;
                    }

                    // ── Math: basic ──
                    case "AddNode": Binary(sc, n, outs, (a, b) => $"{a} + {b}"); return;
                    case "SubtractNode": Binary(sc, n, outs, (a, b) => $"{a} - {b}"); return;
                    case "DivideNode": Binary(sc, n, outs, (a, b) => $"{a} / {b}"); return;
                    case "PowerNode": Binary(sc, n, outs, (a, b) => $"pow({a}, {b})"); return;
                    case "MaximumNode": Binary(sc, n, outs, (a, b) => $"max({a}, {b})"); return;
                    case "MinimumNode": Binary(sc, n, outs, (a, b) => $"min({a}, {b})"); return;
                    case "ModuloNode": Binary(sc, n, outs, (a, b) => $"sg_fmod({a}, {b})"); Fmod(); return;
                    case "StepNode": Binary(sc, n, outs, (a, b) => $"step({a}, {b})"); return;
                    case "MultiplyNode": Multiply(sc, n, outs); return;
                    case "SquareRootNode": Unary(sc, n, outs, a => $"sqrt({a})"); return;
                    case "AbsoluteNode": Unary(sc, n, outs, a => $"abs({a})"); return;
                    case "NegateNode": Unary(sc, n, outs, a => $"-({a})"); return;
                    case "OneMinusNode": Unary(sc, n, outs, a => $"1.0 - {a}"); return;
                    case "NormalizeNode": Unary(sc, n, outs, a => $"normalize({a})"); return;
                    case "FractionNode": Unary(sc, n, outs, a => $"fract({a})"); return;
                    case "FloorNode": Unary(sc, n, outs, a => $"floor({a})"); return;
                    case "CeilingNode": Unary(sc, n, outs, a => $"ceil({a})"); return;
                    case "RoundNode": Unary(sc, n, outs, a => $"round({a})"); return;
                    case "TruncateNode": Unary(sc, n, outs, a => $"trunc({a})"); return;
                    case "SignNode": Unary(sc, n, outs, a => $"sign({a})"); return;
                    case "SaturateNode": Unary(sc, n, outs, a => $"clamp({a}, 0.0, 1.0)"); return;
                    case "SineNode": Unary(sc, n, outs, a => $"sin({a})"); return;
                    case "CosineNode": Unary(sc, n, outs, a => $"cos({a})"); return;
                    case "TangentNode": Unary(sc, n, outs, a => $"tan({a})"); return;
                    case "ArcsineNode": Unary(sc, n, outs, a => $"asin({a})"); return;
                    case "ArccosineNode": Unary(sc, n, outs, a => $"acos({a})"); return;
                    case "ArctangentNode": Unary(sc, n, outs, a => $"atan({a})"); return;
                    case "Arctangent2Node": Binary(sc, n, outs, (a, b) => $"atan({a}, {b})"); return;
                    case "ReciprocalNode": Unary(sc, n, outs, a => $"1.0 / {a}"); return;
                    case "ReciprocalSquareRootNode": Unary(sc, n, outs, a => $"inversesqrt({a})"); return;
                    case "ExponentialNode": Unary(sc, n, outs, a => n.Int("m_ExponentialBase") == 1 ? $"exp2({a})" : $"exp({a})"); return;
                    case "LogNode": Unary(sc, n, outs, a => n.Int("m_LogBase") switch { 1 => $"log2({a})", 2 => $"(log2({a}) * 0.30102999566)", _ => $"log({a})" }); return;
                    case "TriangleWaveNode": Unary(sc, n, outs, a => $"2.0 * abs(2.0 * ({a} - floor(0.5 + {a}))) - 1.0"); return;
                    case "SquareWaveNode": Unary(sc, n, outs, a => $"1.0 - 2.0 * round(fract({a}))"); return;
                    case "SawtoothWaveNode": Unary(sc, n, outs, a => $"2.0 * ({a} - floor(0.5 + {a}))"); return;
                    case "DDXNode": Unary(sc, n, outs, a => Fragment ? $"dFdx({a})" : $"(0.0 * {a})"); return;
                    case "DDYNode": Unary(sc, n, outs, a => Fragment ? $"(-dFdy({a}))" : $"(0.0 * {a})"); return;
                    case "DDXYNode": Unary(sc, n, outs, a => Fragment ? $"(abs(dFdx({a})) + abs(dFdy({a})))" : $"(0.0 * {a})"); return;

                    case "ClampNode":
                    {
                        var k = Dynamic(sc, n, 0, 1, 2);
                        outs[3] = Let(k, $"clamp({In(sc, n, 0, k).E}, {In(sc, n, 1, k).E}, {In(sc, n, 2, k).E})");
                        return;
                    }
                    case "LerpNode":
                    {
                        var k = Dynamic(sc, n, 0, 1, 2);
                        outs[3] = Let(k, $"mix({In(sc, n, 0, k).E}, {In(sc, n, 1, k).E}, {In(sc, n, 2, k).E})");
                        return;
                    }
                    case "InverseLerpNode":
                    {
                        var k = Dynamic(sc, n, 0, 1, 2);
                        string a = In(sc, n, 0, k).E, b = In(sc, n, 1, k).E, t = In(sc, n, 2, k).E;
                        outs[3] = Let(k, $"({t} - {a}) / ({b} - {a})");
                        return;
                    }
                    case "SmoothstepNode":
                    {
                        var k = Dynamic(sc, n, 0, 1, 2);
                        outs[3] = Let(k, $"smoothstep({In(sc, n, 0, k).E}, {In(sc, n, 1, k).E}, {In(sc, n, 2, k).E})");
                        return;
                    }
                    case "RemapNode":
                    {
                        var k = Dynamic(sc, n, 0);
                        var x = In(sc, n, 0, k).E;
                        var im = In(sc, n, 1, K.Vec2).E;
                        var om = In(sc, n, 2, K.Vec2).E;
                        outs[3] = Let(k, $"{om}.x + ({x} - {im}.x) * ({om}.y - {om}.x) / ({im}.y - {im}.x)");
                        return;
                    }
                    case "RandomRangeNode":
                    {
                        var seed = In(sc, n, 0, K.Vec2).E;
                        outs[3] = Let(K.Float, $"mix({In(sc, n, 1, K.Float).E}, {In(sc, n, 2, K.Float).E}, fract(sin(dot({seed}, vec2(12.9898, 78.233))) * 43758.5453))");
                        return;
                    }
                    case "NoiseSineWaveNode":
                    {
                        var k = Dynamic(sc, n, 0);
                        var x = In(sc, n, 0, k);
                        var mm = In(sc, n, 1, K.Vec2).E;
                        var si = Let(k, $"sin({x.E})");
                        var so = Let(k, $"sin({x.E} + 1.0)");
                        var r = Let(k, $"fract(sin(({si.E} - {so.E}) * (12.9898 + 78.233)) * 43758.5453)");
                        outs[2] = Let(k, $"{si.E} + mix({mm}.x, {mm}.y, {r.E})");
                        return;
                    }

                    // ── Math: vector ──
                    case "DotProductNode":
                    {
                        var k = Dynamic(sc, n, 0, 1);
                        outs[2] = Let(K.Float, $"dot({In(sc, n, 0, k).E}, {In(sc, n, 1, k).E})");
                        return;
                    }
                    case "CrossProductNode": outs[2] = Let(K.Vec3, $"cross({In(sc, n, 0, K.Vec3).E}, {In(sc, n, 1, K.Vec3).E})"); return;
                    case "DistanceNode":
                    {
                        var k = Dynamic(sc, n, 0, 1);
                        outs[2] = Let(K.Float, $"distance({In(sc, n, 0, k).E}, {In(sc, n, 1, k).E})");
                        return;
                    }
                    case "LengthNode":
                    {
                        var k = Dynamic(sc, n, 0);
                        outs[1] = Let(K.Float, $"length({In(sc, n, 0, k).E})");
                        return;
                    }
                    case "ReflectionNode":
                    {
                        var k = Dynamic(sc, n, 0, 1);
                        outs[2] = Let(k, $"reflect({In(sc, n, 0, k).E}, {In(sc, n, 1, k).E})");
                        return;
                    }
                    case "ProjectionNode":
                    {
                        var k = Dynamic(sc, n, 0, 1);
                        string a = In(sc, n, 0, k).E, b = In(sc, n, 1, k).E;
                        outs[2] = Let(k, $"{b} * dot({a}, {b}) / dot({b}, {b})");
                        return;
                    }
                    case "RejectionNode":
                    {
                        var k = Dynamic(sc, n, 0, 1);
                        string a = In(sc, n, 0, k).E, b = In(sc, n, 1, k).E;
                        outs[2] = Let(k, $"{a} - ({b} * dot({a}, {b}) / dot({b}, {b}))");
                        return;
                    }
                    case "RotateAboutAxisNode":
                    {
                        var p = In(sc, n, 0, K.Vec3).E;
                        var axis = Let(K.Vec3, $"normalize({In(sc, n, 1, K.Vec3).E})");
                        var rot = In(sc, n, 2, K.Float).E;
                        if (n.Int("m_Unit") == 1) rot = $"radians({rot})";
                        var r = Let(K.Float, rot);
                        var s = Let(K.Float, $"sin({r.E})");
                        var c = Let(K.Float, $"cos({r.E})");
                        var pv = Let(K.Vec3, p);
                        outs[3] = Let(K.Vec3, $"{pv.E} * {c.E} + cross({axis.E}, {pv.E}) * {s.E} + {axis.E} * dot({axis.E}, {pv.E}) * (1.0 - {c.E})");
                        return;
                    }
                    case "SphereMaskNode":
                    {
                        var k = Dynamic(sc, n, 0, 1);
                        string co = In(sc, n, 0, k).E, ce = In(sc, n, 1, k).E, r = In(sc, n, 2, K.Float).E, h = In(sc, n, 3, K.Float).E;
                        outs[4] = Let(k, $"{Glsl(k)}(1.0 - clamp((distance({co}, {ce}) - {r}) / (1.0 - {h}), 0.0, 1.0))");
                        return;
                    }
                    case "TransformNode": Transform(sc, n, outs); return;
                    case "TransformationMatrixNode": outs[0] = new V(MatrixFor(n), K.Mat4); return;

                    // ── Logic ──
                    case "BranchNode":
                    {
                        var k = Dynamic(sc, n, 1, 2);
                        outs[3] = Let(k, $"({In(sc, n, 0, K.Bool).E} ? {In(sc, n, 1, k).E} : {In(sc, n, 2, k).E})");
                        return;
                    }
                    case "ComparisonNode":
                    {
                        var op = n.Int("m_ComparisonType") switch { 0 => "==", 1 => "!=", 2 => "<", 3 => "<=", 4 => ">", _ => ">=" };
                        outs[2] = Let(K.Bool, $"({In(sc, n, 0, K.Float).E} {op} {In(sc, n, 1, K.Float).E})");
                        return;
                    }
                    case "AndNode": outs[2] = Let(K.Bool, $"({In(sc, n, 0, K.Bool).E} && {In(sc, n, 1, K.Bool).E})"); return;
                    case "OrNode": outs[2] = Let(K.Bool, $"({In(sc, n, 0, K.Bool).E} || {In(sc, n, 1, K.Bool).E})"); return;
                    case "NotNode": outs[1] = Let(K.Bool, $"(!{In(sc, n, 0, K.Bool).E})"); return;
                    case "AllNode":
                    case "AnyNode":
                    {
                        var k = Dynamic(sc, n, 0);
                        var x = In(sc, n, 0, k).E;
                        var f = n.Type == "AllNode" ? "all" : "any";
                        outs[1] = Let(K.Bool, k == K.Float ? $"({x} != 0.0)" : $"{f}(notEqual({x}, {Glsl(k)}(0.0)))");
                        return;
                    }

                    // ── Input: basic ──
                    case "Vector1Node": outs[0] = In(sc, n, 1, K.Float); return;
                    case "Vector2Node": outs[0] = Let(K.Vec2, $"vec2({In(sc, n, 1, K.Float).E}, {In(sc, n, 2, K.Float).E})"); return;
                    case "Vector3Node": outs[0] = Let(K.Vec3, $"vec3({In(sc, n, 1, K.Float).E}, {In(sc, n, 2, K.Float).E}, {In(sc, n, 3, K.Float).E})"); return;
                    case "Vector4Node": outs[0] = Let(K.Vec4, $"vec4({In(sc, n, 1, K.Float).E}, {In(sc, n, 2, K.Float).E}, {In(sc, n, 3, K.Float).E}, {In(sc, n, 4, K.Float).E})"); return;
                    case "IntegerNode": outs[0] = new V(Lit(n.Int("m_Value")), K.Float); return;
                    case "BooleanNode": outs[0] = new V(n.Bool("m_Value") ? "true" : "false", K.Bool); return;
                    case "SliderNode": outs[0] = new V(Lit(n.Json.TryGetProperty("m_Value", out var sv) ? ShaderGraphAsset.F(sv, "x") : 0f), K.Float); return;
                    case "ConstantNode":
                        outs[0] = new V(n.Int("m_constant") switch { 0 => "3.14159265359", 1 => "6.28318530718", 2 => "0.618034", 3 => "2.718282", _ => "1.414214" }, K.Float);
                        return;
                    case "ColorNode":
                    {
                        var c = n.Json.GetProperty("m_Color").GetProperty("color");
                        float r = ShaderGraphAsset.F(c, "r"), g = ShaderGraphAsset.F(c, "g"), b = ShaderGraphAsset.F(c, "b"), a = ShaderGraphAsset.F(c, "a");
                        int mode = ShaderGraphAsset.Int(n.Json.GetProperty("m_Color"), "mode");
                        int ver = n.Int("m_SGVersion");
                        // Version 0 and the default (sRGB) picker are linearised; the HDR picker is already linear.
                        bool srgb = ver == 0 || mode == 0;
                        outs[0] = new V(srgb ? Vec(Lin(r), Lin(g), Lin(b), a) : Vec(r, g, b, a), K.Vec4);
                        return;
                    }
                    case "TimeNode":
                        outs[0] = new V("sg_Time.x", K.Float);
                        outs[1] = new V("sg_Time.y", K.Float);
                        outs[2] = new V("sg_Time.z", K.Float);
                        outs[3] = new V("sg_Time.w", K.Float);
                        outs[4] = new V("sg_Time.w", K.Float);
                        return;
                    case "MatrixConstructionNode":
                    {
                        var r = Enumerable.Range(0, 4).Select(i => In(sc, n, i, K.Vec4).E).ToArray();
                        // Rows (Unity's default), so the GLSL column-major constructor takes the transpose.
                        var rows = n.Int("m_Axis") == 0;
                        var m = rows ? $"transpose(mat4({r[0]}, {r[1]}, {r[2]}, {r[3]}))" : $"mat4({r[0]}, {r[1]}, {r[2]}, {r[3]})";
                        foreach (var o in n.Outputs) outs[o.Id] = Let(K.Mat4, m);
                        return;
                    }

                    // ── Input: geometry ──
                    case "PositionNode": outs[0] = Position(n.Int("m_Space")); return;
                    case "NormalVectorNode": outs[0] = NormalVec(n.Int("m_Space")); return;
                    case "TangentVectorNode": outs[0] = TangentVec(n.Int("m_Space")); return;
                    case "BitangentVectorNode": outs[0] = BitangentVec(n.Int("m_Space")); return;
                    case "ViewDirectionNode": outs[0] = ViewDirection(n.Int("m_Space"), normalized: n.Int("m_SGVersion") >= 1); return;
                    case "ViewVectorNode": outs[0] = ViewDirection(n.Int("m_Space"), normalized: false); return;
                    case "UVNode": outs[0] = new V("sg_Uv" + Math.Clamp(n.Int("m_OutputChannel"), 0, 3), K.Vec4); return;
                    case "VertexColorNode": outs[0] = new V("sg_Color", K.Vec4); return;
                    case "VertexIDNode": outs[0] = new V("sg_VertexID", K.Float); return;
                    case "ScreenPositionNode": outs[0] = ScreenPosition(n.Int("m_ScreenSpaceType")); return;
                    case "IsFrontFaceNode": outs[0] = new V("sg_FrontFace", K.Bool); return;
                    case "ObjectNode":
                        Unit.Approx("ObjectNode: " + ApproximateNodeTypes["ObjectNode"]);
                        outs[0] = new V("sg_ObjectToWorld[3].xyz", K.Vec3);
                        outs[1] = Let(K.Vec3, "vec3(length(sg_ObjectToWorld[0].xyz), length(sg_ObjectToWorld[1].xyz), length(sg_ObjectToWorld[2].xyz))");
                        outs[2] = new V("sg_ObjectToWorld[3].xyz", K.Vec3);
                        outs[3] = new V("sg_ObjectToWorld[3].xyz", K.Vec3);
                        outs[4] = outs[1];
                        return;
                    case "CameraNode":
                        outs[0] = new V("sg_CamPos", K.Vec3);
                        outs[1] = new V("sg_CamDir", K.Vec3);
                        outs[2] = new V("sg_OrthoParams.w", K.Float);
                        outs[3] = new V("sg_ProjectionParams.y", K.Float);
                        outs[4] = new V("sg_ProjectionParams.z", K.Float);
                        outs[5] = new V("sg_ProjectionParams.x", K.Float);
                        outs[6] = new V("sg_OrthoParams.x", K.Float);
                        outs[7] = new V("sg_OrthoParams.y", K.Float);
                        return;
                    case "SceneColorNode":
                        Unit.Approx("SceneColorNode: " + ApproximateNodeTypes["SceneColorNode"]);
                        outs[1] = new V("vec3(0.0)", K.Vec3);
                        return;
                    case "SceneDepthNode":
                        Unit.Approx("SceneDepthNode: " + ApproximateNodeTypes["SceneDepthNode"]);
                        outs[1] = new V(n.Int("m_DepthSamplingMode") == 0 ? "1.0" : "sg_ProjectionParams.z", K.Float);
                        return;

                    // ── Channel ──
                    case "SplitNode":
                    {
                        var k = Dynamic(sc, n, 0);
                        var x = In(sc, n, 0, k);
                        int d = Dim(k);
                        var xv = IsSimple(x.E) ? x : Let(k, x.E);
                        for (int i = 0; i < 4; i++)
                            outs[i + 1] = new V(i < d ? (d == 1 ? xv.E : $"{xv.E}.{"xyzw"[i]}") : "0.0", K.Float);
                        return;
                    }
                    case "CombineNode":
                    {
                        string r = In(sc, n, 0, K.Float).E, g = In(sc, n, 1, K.Float).E, b = In(sc, n, 2, K.Float).E, a = In(sc, n, 3, K.Float).E;
                        var rgba = Let(K.Vec4, $"vec4({r}, {g}, {b}, {a})");
                        outs[4] = rgba;
                        outs[5] = new V($"{rgba.E}.xyz", K.Vec3);
                        outs[6] = new V($"{rgba.E}.xy", K.Vec2);
                        return;
                    }
                    case "SwizzleNode":
                    {
                        var x = In(sc, n, 0, K.Vec4);
                        var mask = (n.Str("_maskInput") ?? n.Str("m_MaskInput") ?? "xyzw").ToLowerInvariant().Replace('r', 'x').Replace('g', 'y').Replace('b', 'z').Replace('a', 'w');
                        if (mask.Length is < 1 or > 4 || mask.Any(c => "xyzw".IndexOf(c) < 0)) throw new SgException($"{n}: swizzle mask '{mask}'");
                        outs[1] = Let(VecK(mask.Length), $"{x.E}.{mask}");
                        return;
                    }
                    case "ChannelMaskNode":
                    {
                        var k = Dynamic(sc, n, 0);
                        var x = In(sc, n, 0, k).E;
                        int ch = n.Int("m_Channel", 15);
                        var m = Enumerable.Range(0, Dim(k)).Select(i => (ch & (1 << i)) != 0 ? "1.0" : "0.0");
                        outs[1] = Let(k, Dim(k) == 1 ? $"{x} * {m.First()}" : $"{x} * {Glsl(k)}({string.Join(", ", m)})");
                        return;
                    }

                    // ── UV ──
                    case "TilingAndOffsetNode": outs[3] = Let(K.Vec2, $"{In(sc, n, 0, K.Vec2).E} * {In(sc, n, 1, K.Vec2).E} + {In(sc, n, 2, K.Vec2).E}"); return;
                    case "RotateNode":
                    {
                        var uv = Let(K.Vec2, $"{In(sc, n, 0, K.Vec2).E} - {In(sc, n, 1, K.Vec2).E}");
                        var rot = In(sc, n, 2, K.Float).E;
                        if (n.Int("m_Unit") == 1) rot = $"({rot} * (3.1415926 / 180.0))";
                        var s = Let(K.Float, $"sin({rot})");
                        var c = Let(K.Float, $"cos({rot})");
                        outs[3] = Let(K.Vec2, $"vec2(dot({uv.E}, vec2({c.E}, {s.E})), dot({uv.E}, vec2(-{s.E}, {c.E}))) + {In(sc, n, 1, K.Vec2).E}");
                        return;
                    }
                    case "TwirlNode":
                    {
                        var center = In(sc, n, 1, K.Vec2).E;
                        var d = Let(K.Vec2, $"{In(sc, n, 0, K.Vec2).E} - {center}");
                        var a = Let(K.Float, $"{In(sc, n, 2, K.Float).E} * length({d.E})");
                        var off = In(sc, n, 3, K.Vec2).E;
                        outs[4] = Let(K.Vec2, $"vec2(cos({a.E}) * {d.E}.x - sin({a.E}) * {d.E}.y, sin({a.E}) * {d.E}.x + cos({a.E}) * {d.E}.y) + {center} + {off}");
                        return;
                    }
                    case "SpherizeNode":
                    {
                        var uv = Let(K.Vec2, In(sc, n, 0, K.Vec2).E);
                        var d = Let(K.Vec2, $"{uv.E} - {In(sc, n, 1, K.Vec2).E}");
                        var d2 = Let(K.Float, $"dot({d.E}, {d.E})");
                        outs[4] = Let(K.Vec2, $"{uv.E} + {d.E} * ({d2.E} * {d2.E} * {In(sc, n, 2, K.Vec2).E}) + {In(sc, n, 3, K.Vec2).E}");
                        return;
                    }
                    case "PolarCoordinatesNode":
                    {
                        var d = Let(K.Vec2, $"{In(sc, n, 0, K.Vec2).E} - {In(sc, n, 1, K.Vec2).E}");
                        outs[4] = Let(K.Vec2, $"vec2(length({d.E}) * 2.0 * {In(sc, n, 2, K.Float).E}, atan({d.E}.x, {d.E}.y) * (1.0 / 6.28) * {In(sc, n, 3, K.Float).E})");
                        return;
                    }

                    // ── Texture ──
                    case "SampleTexture2DNode": SampleTexture(sc, n, outs); return;
                    case "Texture2DAssetNode":
                    {
                        string guid = n.Json.TryGetProperty("m_Texture", out var t) ? ShaderGraphAsset.NestedGuid(ShaderGraphAsset.Str(t, "m_SerializedTexture")) : null;
                        outs[0] = new V(Unit.AssetTexture(guid), K.Tex);
                        return;
                    }
                    case "Texture2DPropertiesNode":
                    {
                        var t = In(sc, n, 1, K.Tex).E;
                        var size = Let(K.Vec2, $"vec2(textureSize({t}, 0))");
                        outs[0] = new V($"{size.E}.x", K.Float);
                        outs[2] = new V($"{size.E}.y", K.Float);
                        outs[3] = new V($"(1.0 / {size.E}.x)", K.Float);
                        outs[4] = new V($"(1.0 / {size.E}.y)", K.Float);
                        return;
                    }
                    case "GatherTexture2DNode":
                    {
                        Gather();
                        var t = In(sc, n, 1, K.Tex).E;
                        var g = Let(K.Vec4, $"sg_gather({t}, {In(sc, n, 2, K.Vec2).E}, {In(sc, n, 4, K.Vec2).E})");
                        outs[0] = g;
                        outs[5] = new V($"{g.E}.x", K.Float);
                        outs[6] = new V($"{g.E}.y", K.Float);
                        outs[7] = new V($"{g.E}.z", K.Float);
                        outs[8] = new V($"{g.E}.w", K.Float);
                        return;
                    }

                    // ── Procedural ──
                    case "NoiseNode":
                    case "SimpleNoiseNode":
                    {
                        bool legacy = n.Int("m_SGVersion") < 1 || n.Int("m_HashType") == 1;
                        Noise();
                        outs[2] = Let(K.Float, $"sg_simpleNoise({In(sc, n, 0, K.Vec2).E}, {In(sc, n, 1, K.Float).E}, {(legacy ? "true" : "false")})");
                        return;
                    }
                    case "GradientNoiseNode":
                    {
                        // Version 0 was LegacyMod; version 1 stores 0 Deterministic, 1 LegacyMod.
                        bool legacyMod = n.Int("m_SGVersion") < 1 || n.Int("m_HashType") == 1;
                        Noise();
                        outs[2] = Let(K.Float, $"sg_gradientNoise({In(sc, n, 0, K.Vec2).E}, {In(sc, n, 1, K.Float).E}, {(legacyMod ? "true" : "false")})");
                        return;
                    }
                    case "VoronoiNode":
                    {
                        bool legacy = n.Int("m_SGVersion") < 1 || n.Int("m_HashType") == 1;
                        Noise();
                        var cells = Decl(K.Float);
                        var d = Decl(K.Float);
                        Line($"sg_voronoi({In(sc, n, 0, K.Vec2).E}, {In(sc, n, 1, K.Float).E}, {In(sc, n, 2, K.Float).E}, {(legacy ? "true" : "false")}, {d}, {cells});");
                        outs[3] = new V(d, K.Float);
                        outs[4] = new V(cells, K.Float);
                        return;
                    }
                    case "GradientNode": outs[0] = new V("", K.Gradient, GradientKeys.FromNode(n.Json)); return;
                    case "SampleGradient": SampleGradient(sc, n, outs); return;
                    case "RectangleNode":
                    {
                        var uv = In(sc, n, 0, K.Vec2).E;
                        string w = In(sc, n, 1, K.Float).E, h = In(sc, n, 2, K.Float).E;
                        var d = Let(K.Vec2, Fragment
                            ? $"(abs({uv} * 2.0 - 1.0) - vec2({w}, {h})) / fwidth({uv})"
                            : $"(abs({uv} * 2.0 - 1.0) - vec2({w}, {h})) * 1e5");
                        var dv = Let(K.Vec2, $"clamp(1.0 - {d.E}, 0.0, 1.0)");
                        outs[3] = Let(K.Float, $"min({dv.E}.x, {dv.E}.y)");
                        return;
                    }
                    case "EllipseNode":
                    {
                        var uv = In(sc, n, 0, K.Vec2).E;
                        string w = In(sc, n, 1, K.Float).E, h = In(sc, n, 2, K.Float).E;
                        var d = Let(K.Float, $"length(({uv} * 2.0 - 1.0) / vec2({w}, {h}))");
                        outs[4] = Let(K.Float, Fragment ? $"clamp((1.0 - {d.E}) / fwidth({d.E}), 0.0, 1.0)" : $"step({d.E}, 1.0)");
                        return;
                    }
                    case "CheckerboardNode":
                    {
                        var uv = Let(K.Vec2, $"({In(sc, n, 0, K.Vec2).E} + 0.5) * {In(sc, n, 3, K.Vec2).E}");
                        var ca = In(sc, n, 1, K.Vec3).E;
                        var cb = In(sc, n, 2, K.Vec3).E;
                        var c = Let(K.Float, $"step(0.0, sin({uv.E}.x * 3.14159265) * sin({uv.E}.y * 3.14159265))");
                        outs[4] = Let(K.Vec3, $"mix({cb}, {ca}, {c.E})");
                        return;
                    }

                    // ── Artistic ──
                    case "BlendNode": Blend(sc, n, outs); return;
                    case "InvertColorsNode":
                    {
                        var k = Dynamic(sc, n, 0);
                        var x = In(sc, n, 0, k).E;
                        var mask = new[] { n.Bool("m_RedChannel"), n.Bool("m_GreenChannel"), n.Bool("m_BlueChannel"), n.Bool("m_AlphaChannel") }
                            .Take(Dim(k)).Select(b => b ? "1.0" : "0.0").ToArray();
                        var m = Dim(k) == 1 ? mask[0] : $"{Glsl(k)}({string.Join(", ", mask)})";
                        outs[1] = Let(k, $"abs({m} - {x})");
                        return;
                    }
                    case "ReplaceColorNode":
                    {
                        var x = Let(K.Vec3, In(sc, n, 0, K.Vec3).E);
                        var dist = Let(K.Float, $"distance({In(sc, n, 1, K.Vec3).E}, {x.E})");
                        outs[4] = Let(K.Vec3, $"mix({In(sc, n, 2, K.Vec3).E}, {x.E}, clamp(({dist.E} - {In(sc, n, 3, K.Float).E}) / max({In(sc, n, 5, K.Float).E}, 1e-5), 0.0, 1.0))");
                        return;
                    }
                    case "HueNode":
                    {
                        Color();
                        var off = In(sc, n, 1, K.Float).E;
                        if (n.Int("m_HueMode") == 0) off = $"({off} / 360.0)";
                        outs[2] = Let(K.Vec3, $"sg_hue({In(sc, n, 0, K.Vec3).E}, {off})");
                        return;
                    }
                    case "ColorspaceConversionNode":
                    {
                        Color();
                        var conv = n.Json.GetProperty("m_Conversion");
                        int from = ShaderGraphAsset.Int(conv, "from"), to = ShaderGraphAsset.Int(conv, "to");
                        outs[1] = Let(K.Vec3, $"sg_colorspace({In(sc, n, 0, K.Vec3).E}, {from}, {to})");
                        return;
                    }
                    case "ContrastNode":
                    {
                        var x = Let(K.Vec3, In(sc, n, 0, K.Vec3).E);
                        var c = In(sc, n, 1, K.Float).E;
                        var mid = "pow(0.5, 2.2)";
                        outs[2] = Let(K.Vec3, $"({x.E} - {mid}) * {c} + {mid}");
                        return;
                    }
                    case "SaturationNode":
                    {
                        var x = Let(K.Vec3, In(sc, n, 0, K.Vec3).E);
                        var luma = Let(K.Float, $"dot({x.E}, vec3(0.2126729, 0.7151522, 0.0721750))");
                        outs[2] = Let(K.Vec3, $"vec3({luma.E}) + {In(sc, n, 1, K.Float).E} * ({x.E} - vec3({luma.E}))");
                        return;
                    }
                    case "PosterizeNode":
                    {
                        var k = Dynamic(sc, n, 0, 1);
                        var steps = In(sc, n, 1, k).E;
                        outs[2] = Let(k, $"floor({In(sc, n, 0, k).E} / (1.0 / {steps})) * (1.0 / {steps})");
                        return;
                    }
                    case "DitherNode":
                    {
                        Dither();
                        var k = Dynamic(sc, n, 0);
                        outs[2] = Let(k, $"{In(sc, n, 0, k).E} - {Glsl(k)}(sg_dither({ScreenPosition(0).E}))");
                        return;
                    }
                    case "NormalFromHeightNode":
                    {
                        var h = In(sc, n, 0, K.Float).E;
                        var strength = In(sc, n, 2, K.Float).E;
                        bool tangentOut = n.Int("m_OutputSpace") == 0;
                        if (!Fragment) { outs[1] = new V(tangentOut ? "vec3(0.0, 0.0, 1.0)" : "normalize(sg_NrmWS)", K.Vec3); return; }
                        NormalHeight();
                        outs[1] = Let(K.Vec3, $"sg_normalFromHeight({h}, {strength}, {(tangentOut ? "true" : "false")})");
                        return;
                    }
                    case "NormalStrengthNode":
                    {
                        var x = Let(K.Vec3, In(sc, n, 0, K.Vec3).E);
                        var s = In(sc, n, 1, K.Float).E;
                        outs[2] = Let(K.Vec3, $"vec3({x.E}.xy * {s}, mix(1.0, {x.E}.z, clamp({s}, 0.0, 1.0)))");
                        return;
                    }
                    case "NormalBlendNode":
                    {
                        var a = Let(K.Vec3, In(sc, n, 0, K.Vec3).E);
                        var b = Let(K.Vec3, In(sc, n, 1, K.Vec3).E);
                        outs[2] = Let(K.Vec3, n.Int("m_BlendMode") == 0
                            ? $"normalize(vec3({a.E}.xy + {b.E}.xy, {a.E}.z * {b.E}.z))"
                            : $"normalize(({a.E} + vec3(0.0, 0.0, 1.0)) * dot({a.E} + vec3(0.0, 0.0, 1.0), {b.E} * vec3(-1.0, -1.0, 1.0)) / ({a.E}.z + 1.0) - {b.E} * vec3(-1.0, -1.0, 1.0))");
                        return;
                    }
                    case "NormalUnpackNode":
                    {
                        var x = In(sc, n, 0, K.Vec4).E;
                        outs[1] = Let(K.Vec3, $"normalize({x}.xyz * 2.0 - 1.0)");
                        return;
                    }
                    case "FresnelNode":
                    {
                        var nrm = In(sc, n, 0, K.Vec3).E;
                        var view = In(sc, n, 1, K.Vec3).E;
                        outs[3] = Let(K.Float, $"pow(1.0 - clamp(dot(normalize({nrm}), normalize({view})), 0.0, 1.0), {In(sc, n, 2, K.Float).E})");
                        return;
                    }

                    default:
                        throw new SgException($"unsupported node {n.Type}");
                }
            }

            static float Lin(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

            void Unary(Scope sc, SgNode n, Dictionary<int, V> outs, Func<string, string> f)
            {
                var k = Dynamic(sc, n, 0);
                outs[1] = Let(k, f(In(sc, n, 0, k).E));
            }

            void Binary(Scope sc, SgNode n, Dictionary<int, V> outs, Func<string, string, string> f)
            {
                var k = Dynamic(sc, n, 0, 1);
                outs[2] = Let(k, f(In(sc, n, 0, k).E, In(sc, n, 1, k).E));
            }

            /// <summary>Multiply takes matrices as well as vectors (HLSL mul = GLSL operator* on Unity-ordered matrices).</summary>
            void Multiply(Scope sc, SgNode n, Dictionary<int, V> outs)
            {
                var ra = Raw(sc, n, 0);
                var rb = Raw(sc, n, 1);
                bool ma = ra?.K == K.Mat4, mb = rb?.K == K.Mat4;
                if (ma || mb)
                {
                    if (ma && mb) { outs[2] = Let(K.Mat4, $"{ra.Value.E} * {rb.Value.E}"); return; }
                    var mat = ma ? ra.Value : rb.Value;
                    var other = ma ? rb : ra;
                    var vk = other is { } o && o.K != K.Mat4 ? (o.K == K.Bool ? K.Float : o.K) : K.Vec4;
                    var v4 = other is { } ov ? Cast(ov, K.Vec4).E : "vec4(0.0)";
                    var r = ma ? $"({mat.E} * {v4})" : $"({v4} * {mat.E})";
                    outs[2] = Let(vk, Cast(new V(r, K.Vec4), vk).E);
                    return;
                }
                var k = Dynamic(sc, n, 0, 1);
                outs[2] = Let(k, $"{In(sc, n, 0, k).E} * {In(sc, n, 1, k).E}");
            }

            void SampleTexture(Scope sc, SgNode n, Dictionary<int, V> outs)
            {
                var tex = In(sc, n, 1, K.Tex);
                var uv = In(sc, n, 2, K.Vec2).E;
                var call = Fragment ? $"texture({tex.E}, {uv})" : $"textureLod({tex.E}, {uv}, 0.0)";
                var rgba = Let(K.Vec4, call);
                if (n.Int("m_TextureType") == 1)
                    rgba = Let(K.Vec4, $"vec4(normalize({rgba.E}.xyz * 2.0 - 1.0), 1.0)");
                outs[0] = rgba;
                outs[4] = new V($"{rgba.E}.x", K.Float);
                outs[5] = new V($"{rgba.E}.y", K.Float);
                outs[6] = new V($"{rgba.E}.z", K.Float);
                outs[7] = new V($"{rgba.E}.w", K.Float);
            }

            void SampleGradient(Scope sc, SgNode n, Dictionary<int, V> outs)
            {
                var gin = n.Inputs.FirstOrDefault(s => s.Type is "GradientInput" or "Gradient");
                var tin = n.Inputs.FirstOrDefault(s => s.Type == "Vector1");
                var outSlot = n.Outputs.First();
                var gv = gin == null ? new V("", K.Gradient, GradientKeys.FromSlot(default)) : (Raw(sc, n, gin.Id) ?? In(sc, n, gin.Id, K.Gradient));
                var g = gv.Data as GradientKeys ?? throw new SgException($"{n}: gradient input has no keys");
                var time = Let(K.Float, tin == null ? "0.0" : In(sc, n, tin.Id, K.Float).E);
                var c = Let(K.Vec3, Vec(g.Colors[0].R, g.Colors[0].G, g.Colors[0].B));
                float fixedMode = g.Mode == 1 ? 1f : 0f;
                for (int i = 1; i < g.Colors.Count; i++)
                {
                    var (r, gg, b, t) = g.Colors[i];
                    float t0 = g.Colors[i - 1].T;
                    float last = i <= g.Colors.Count - 1 ? 1f : 0f;
                    Line($"{{ float p = clamp(({time.E} - {Lit(t0)}) / ({Lit(t - t0)}), 0.0, 1.0) * {Lit(last)}; {c.E} = mix({c.E}, {Vec(r, gg, b)}, mix(p, step(0.01, p), {Lit(fixedMode)})); }}");
                }
                Color();
                Line($"{c.E} = sg_colorspace({c.E}, 0, 1);");
                var a = Let(K.Float, Lit(g.Alphas[0].A));
                for (int i = 1; i < g.Alphas.Count; i++)
                {
                    var (al, t) = g.Alphas[i];
                    float t0 = g.Alphas[i - 1].T;
                    Line($"{{ float p = clamp(({time.E} - {Lit(t0)}) / ({Lit(t - t0)}), 0.0, 1.0); {a.E} = mix({a.E}, {Lit(al)}, mix(p, step(0.01, p), {Lit(fixedMode)})); }}");
                }
                outs[outSlot.Id] = Let(K.Vec4, $"vec4({c.E}, {a.E})");
            }

            void Blend(Scope sc, SgNode n, Dictionary<int, V> outs)
            {
                var k = Dynamic(sc, n, 0, 1);
                var b = Let(k, In(sc, n, 0, k).E);
                var l = Let(k, In(sc, n, 1, k).E);
                var op = In(sc, n, 3, K.Float).E;
                string B = b.E, L = l.E, T = Glsl(k);
                string Res(string e) => Let(k, e).E;
                string r;
                switch (n.Int("m_BlendMode"))
                {
                    case 0: r = $"1.0 - (1.0 - {L}) / ({B} + 0.000000000001)"; break;
                    case 1: r = $"min({L}, {B})"; break;
                    case 2: r = $"abs({L} - {B})"; break;
                    case 3: r = $"{B} / (1.0 - clamp({L}, 0.000001, 0.999999))"; break;
                    case 4: r = $"{B} / ({L} + 0.000000000001)"; break;
                    case 5: r = $"{L} + {B} - (2.0 * {L} * {B})"; break;
                    case 6:
                    {
                        var r1 = Res($"1.0 - 2.0 * (1.0 - {B}) * (1.0 - {L})");
                        var r2 = Res($"2.0 * {B} * {L}");
                        var z = Res($"step({L}, {T}(0.5))");
                        r = $"{r2} * {z} + (1.0 - {z}) * {r1}";
                        break;
                    }
                    case 7: r = $"step(1.0 - {B}, {L})"; break;
                    case 8: r = $"max({L}, {B})"; break;
                    case 9: r = $"{B} + {L} - 1.0"; break;
                    case 10: r = $"{B} + {L}"; break;
                    case 11: r = $"mix(max({B} + (2.0 * {L}) - 1.0, {T}(0.0)), min({B} + 2.0 * ({L} - 0.5), {T}(1.0)), step({T}(0.5), {L}))"; break;
                    case 12: r = $"{L} + 2.0 * {B} - 1.0"; break;
                    case 13: r = $"{B} * {L}"; break;
                    case 14: r = $"1.0 - abs(1.0 - {L} - {B})"; break;
                    case 15:
                    {
                        var r1 = Res($"1.0 - 2.0 * (1.0 - {B}) * (1.0 - {L})");
                        var r2 = Res($"2.0 * {B} * {L}");
                        var z = Res($"step({B}, {T}(0.5))");
                        r = $"{r2} * {z} + (1.0 - {z}) * {r1}";
                        break;
                    }
                    case 16:
                    {
                        var check = Res($"step({T}(0.5), {L})");
                        var r1 = Res($"{check} * max(2.0 * ({B} - 0.5), {L})");
                        r = $"{r1} + (1.0 - {check}) * min(2.0 * {B}, {L})";
                        break;
                    }
                    case 17: r = $"1.0 - (1.0 - {L}) * (1.0 - {B})"; break;
                    case 18:
                    {
                        var r1 = Res($"2.0 * {B} * {L} + {B} * {B} * (1.0 - 2.0 * {L})");
                        var r2 = Res($"sqrt({B}) * (2.0 * {L} - 1.0) + 2.0 * {B} * (1.0 - {L})");
                        var z = Res($"step({T}(0.5), {L})");
                        r = $"{r2} * {z} + (1.0 - {z}) * {r1}";
                        break;
                    }
                    case 19: r = $"{B} - {L}"; break;
                    case 20:
                    {
                        var bc = Res($"clamp({B}, 0.000001, 0.999999)");
                        var r1 = Res($"1.0 - (1.0 - {L}) / (2.0 * {bc})");
                        var r2 = Res($"{L} / (2.0 * (1.0 - {bc}))");
                        var z = Res($"step({T}(0.5), {bc})");
                        r = $"{r2} * {z} + (1.0 - {z}) * {r1}";
                        break;
                    }
                    default: r = L; break; // Overwrite
                }
                outs[2] = Let(k, $"mix({B}, {r}, {op})");
            }

            // ── Spaces ──

            internal V Position(int space) => space switch
            {
                0 => new V("sg_PosOS", K.Vec3),
                1 => new V("(sg_View * vec4(sg_PosWS, 1.0)).xyz", K.Vec3),
                3 => new V("(transpose(sg_TBN()) * sg_PosWS)", K.Vec3),
                _ => new V("sg_PosWS", K.Vec3),
            };

            internal V NormalVec(int space) => space switch
            {
                0 => new V("normalize(sg_NrmOS)", K.Vec3),
                1 => new V("normalize(mat3(sg_View) * sg_NrmWS)", K.Vec3),
                3 => new V("vec3(0.0, 0.0, 1.0)", K.Vec3),
                _ => new V("normalize(sg_NrmWS)", K.Vec3),
            };

            internal V TangentVec(int space) => space switch
            {
                0 => new V("normalize(sg_TanOS.xyz)", K.Vec3),
                1 => new V("normalize(mat3(sg_View) * sg_TanWS.xyz)", K.Vec3),
                3 => new V("vec3(1.0, 0.0, 0.0)", K.Vec3),
                _ => new V("normalize(sg_TanWS.xyz)", K.Vec3),
            };

            internal V BitangentVec(int space) => space switch
            {
                0 => new V("(cross(sg_NrmOS, sg_TanOS.xyz) * sg_TanOS.w)", K.Vec3),
                1 => new V("(mat3(sg_View) * (cross(sg_NrmWS, sg_TanWS.xyz) * sg_TanWS.w))", K.Vec3),
                3 => new V("vec3(0.0, 1.0, 0.0)", K.Vec3),
                _ => new V("(cross(normalize(sg_NrmWS), normalize(sg_TanWS.xyz)) * sg_TanWS.w)", K.Vec3),
            };

            internal V ViewDirection(int space, bool normalized)
            {
                string ws = "(sg_CamPos - sg_PosWS)";
                string v = space switch
                {
                    0 => normalized ? $"(mat3(sg_WorldToObject) * {ws})" : "((sg_WorldToObject * vec4(sg_CamPos, 1.0)).xyz - sg_PosOS)",
                    1 => $"(mat3(sg_View) * {ws})",
                    3 => $"(transpose(sg_TBN()) * {ws})",
                    _ => ws,
                };
                return new V(normalized ? $"normalize({v})" : v, K.Vec3);
            }

            internal V Uv(int channel) => new V($"sg_Uv{Math.Clamp(channel, 0, 3)}.xy", K.Vec2);

            internal V ScreenPosition(int type) => type switch
            {
                1 => new V("sg_ScreenPosRaw", K.Vec4),
                2 => new V("vec4(sg_ScreenUV * 2.0 - 1.0, 0.0, 0.0)", K.Vec4),
                3 => new V("fract(vec4((sg_ScreenUV.x * 2.0 - 1.0) * sg_ScreenParams.x / sg_ScreenParams.y, sg_ScreenUV.y * 2.0 - 1.0, 0.0, 0.0))", K.Vec4),
                4 => new V("vec4(sg_ScreenUV * sg_ScreenParams.xy, 0.0, 0.0)", K.Vec4),
                _ => new V("vec4(sg_ScreenUV, 0.0, 0.0)", K.Vec4),
            };

            /// <summary>Transform node: position/direction/normal between object, view, world, tangent and absolute world.</summary>
            void Transform(Scope sc, SgNode n, Dictionary<int, V> outs)
            {
                var conv = n.Json.GetProperty("m_Conversion");
                int from = ShaderGraphAsset.Int(conv, "from"), to = ShaderGraphAsset.Int(conv, "to");
                int type = n.Int("m_ConversionType");
                bool norm = n.Bool("m_Normalize", true) && type != 0;
                if (from == 4) from = 2;
                if (to == 4) to = 2;
                var x = Let(K.Vec3, In(sc, n, 0, K.Vec3).E);
                string toWorld = from switch
                {
                    0 => type == 0 ? $"(sg_ObjectToWorld * vec4({x.E}, 1.0)).xyz" : type == 2 ? $"(transpose(mat3(sg_WorldToObject)) * {x.E})" : $"(mat3(sg_ObjectToWorld) * {x.E})",
                    1 => type == 0 ? $"(sg_InvView * vec4({x.E}, 1.0)).xyz" : $"(mat3(sg_InvView) * {x.E})",
                    3 => $"(sg_TBN() * {x.E})",
                    5 => throw new SgException($"{n}: transform from screen space"),
                    _ => x.E,
                };
                var w = Let(K.Vec3, toWorld);
                string r = to switch
                {
                    0 => type == 0 ? $"(sg_WorldToObject * vec4({w.E}, 1.0)).xyz" : type == 2 ? $"(transpose(mat3(sg_ObjectToWorld)) * {w.E})" : $"(mat3(sg_WorldToObject) * {w.E})",
                    1 => type == 0 ? $"(sg_View * vec4({w.E}, 1.0)).xyz" : $"(mat3(sg_View) * {w.E})",
                    3 => $"(transpose(sg_TBN()) * {w.E})",
                    5 => $"((sg_ViewProj * vec4({w.E}, 1.0)).xyz / max(abs((sg_ViewProj * vec4({w.E}, 1.0)).w), 1e-6))",
                    _ => w.E,
                };
                outs[1] = Let(K.Vec3, norm ? $"normalize({r})" : r);
            }

            static string MatrixFor(SgNode n)
            {
                int t = n.Int("m_MatrixType", -1);
                if (t < 0)
                {
                    // Legacy UnityMatrixType: Model, InverseModel, View, InverseView, Projection, InverseProjection, ViewProjection, InverseViewProjection.
                    return n.Int("m_matrix") switch
                    {
                        0 => "sg_ObjectToWorld", 1 => "sg_WorldToObject", 2 => "sg_View", 3 => "sg_InvView",
                        4 => "sg_Proj", 5 => "inverse(sg_Proj)", 6 => "sg_ViewProj", _ => "inverse(sg_ViewProj)",
                    };
                }
                return t switch
                {
                    0 => "(sg_View * sg_ObjectToWorld)",
                    1 => "sg_View",
                    2 => "sg_Proj",
                    3 => "sg_ViewProj",
                    4 => "transpose(sg_View * sg_ObjectToWorld)",
                    5 => "transpose(inverse(sg_View * sg_ObjectToWorld))",
                    6 => "sg_ObjectToWorld",
                    _ => "sg_WorldToObject",
                };
            }

            // ── Shared helper functions (each emitted once per program) ──

            void Fmod() => Unit.Helper("fmod", @"float sg_fmod(float a, float b) { return a - b * trunc(a / b); }
vec2 sg_fmod(vec2 a, vec2 b) { return a - b * trunc(a / b); }
vec3 sg_fmod(vec3 a, vec3 b) { return a - b * trunc(a / b); }
vec4 sg_fmod(vec4 a, vec4 b) { return a - b * trunc(a / b); }");

            void Noise() => Unit.Helper("noise", @"float sg_hashTchou21(vec2 i) {
  uvec2 v = uvec2(ivec2(round(i)));
  v.y ^= 1103515245u; v.x += v.y; v.x *= v.y; v.x ^= v.x >> 5u; v.x *= 0x27d4eb2du;
  return float(v.x >> 8u) * (1.0 / float(0x00ffffff));
}
vec2 sg_hashTchou22(vec2 i) {
  uvec2 v = uvec2(ivec2(round(i)));
  v.y ^= 1103515245u; v.x += v.y; v.x *= v.y; v.x ^= v.x >> 5u; v.x *= 0x27d4eb2du; v.y ^= (v.x << 3u);
  return vec2(v >> 8u) * (1.0 / float(0x00ffffff));
}
float sg_hashLegacySine21(vec2 i) { return fract(sin(dot(i, vec2(12.9898, 78.233))) * 43758.5453); }
vec2 sg_hashLegacySine22(vec2 i) { return fract(sin(vec2(i.x * 15.27 + i.y * 99.41, i.x * 47.63 + i.y * 89.98))); }
float sg_hashLegacyMod21(vec2 i) {
  i = i - 289.0 * trunc(i / 289.0);
  float x = (34.0 * i.x + 1.0) * i.x; x = x - 289.0 * trunc(x / 289.0) + i.y;
  x = (34.0 * x + 1.0) * x; x = x - 289.0 * trunc(x / 289.0);
  return fract(x / 41.0) * 2.0 - 1.0;
}
float sg_valueNoise(vec2 uv, bool legacy) {
  vec2 i = floor(uv); vec2 f = fract(uv); f = f * f * (3.0 - 2.0 * f);
  vec2 c0 = i, c1 = i + vec2(1.0, 0.0), c2 = i + vec2(0.0, 1.0), c3 = i + vec2(1.0, 1.0);
  float r0 = legacy ? sg_hashLegacySine21(c0) : sg_hashTchou21(c0);
  float r1 = legacy ? sg_hashLegacySine21(c1) : sg_hashTchou21(c1);
  float r2 = legacy ? sg_hashLegacySine21(c2) : sg_hashTchou21(c2);
  float r3 = legacy ? sg_hashLegacySine21(c3) : sg_hashTchou21(c3);
  return mix(mix(r0, r1, f.x), mix(r2, r3, f.x), f.y);
}
float sg_simpleNoise(vec2 uv, float scale, bool legacy) {
  float o = 0.0;
  for (int octave = 0; octave < 3; octave++) {
    float freq = pow(2.0, float(octave)); float amp = pow(0.5, float(3 - octave));
    o += sg_valueNoise(uv * (scale / freq), legacy) * amp;
  }
  return o;
}
vec2 sg_gradientDir(vec2 p, bool legacyMod) {
  float x = legacyMod ? sg_hashLegacyMod21(p) : sg_hashTchou21(p);
  return normalize(vec2(x - floor(x + 0.5), abs(x) - 0.5));
}
float sg_gradientNoise(vec2 uv, float scale, bool legacyMod) {
  vec2 p = uv * scale; vec2 ip = floor(p); vec2 fp = fract(p);
  float d00 = dot(sg_gradientDir(ip, legacyMod), fp);
  float d01 = dot(sg_gradientDir(ip + vec2(0.0, 1.0), legacyMod), fp - vec2(0.0, 1.0));
  float d10 = dot(sg_gradientDir(ip + vec2(1.0, 0.0), legacyMod), fp - vec2(1.0, 0.0));
  float d11 = dot(sg_gradientDir(ip + vec2(1.0, 1.0), legacyMod), fp - vec2(1.0, 1.0));
  fp = fp * fp * fp * (fp * (fp * 6.0 - 15.0) + 10.0);
  return mix(mix(d00, d01, fp.y), mix(d10, d11, fp.y), fp.x) + 0.5;
}
void sg_voronoi(vec2 uv, float angleOffset, float cellDensity, bool legacy, out float o, out float cells) {
  vec2 g = floor(uv * cellDensity); vec2 f = fract(uv * cellDensity);
  vec3 res = vec3(8.0, 0.0, 0.0);
  o = 8.0; cells = 0.0;
  for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) {
    vec2 lattice = vec2(x, y);
    vec2 h = legacy ? sg_hashLegacySine22(lattice + g) : sg_hashTchou22(lattice + g);
    vec2 offset = vec2(sin(h.y * angleOffset), cos(h.x * angleOffset)) * 0.5 + 0.5;
    float d = distance(lattice + offset, f);
    if (d < res.x) { res = vec3(d, offset.x, offset.y); o = res.x; cells = res.y; }
  }
}");

            void Color() => Unit.Helper("color", @"vec3 sg_rgb2hsv(vec3 c) {
  vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
  vec4 P = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
  vec4 Q = mix(vec4(P.xyw, c.r), vec4(c.r, P.yzx), step(P.x, c.r));
  float D = Q.x - min(Q.w, Q.y); float E = 1e-10;
  float V = (D == 0.0) ? Q.x : (Q.x + E);
  return vec3(abs(Q.z + (Q.w - Q.y) / (6.0 * D + E)), D / (Q.x + E), V);
}
vec3 sg_hsv2rgb(vec3 c) {
  vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
  vec3 P = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
  return c.z * mix(K.xxx, clamp(P - K.xxx, 0.0, 1.0), c.y);
}
vec3 sg_srgb2linear(vec3 c) {
  vec3 lo = c / 12.92; vec3 hi = pow(max(abs((c + 0.055) / 1.055), 1.192092896e-07), vec3(2.4));
  return mix(hi, lo, vec3(lessThanEqual(c, vec3(0.04045))));
}
vec3 sg_linear2srgb(vec3 c) {
  vec3 lo = c * 12.92; vec3 hi = (pow(max(abs(c), 1.192092896e-07), vec3(1.0 / 2.4)) * 1.055) - 0.055;
  return mix(hi, lo, vec3(lessThanEqual(c, vec3(0.0031308))));
}
// Colorspace enum: 0 RGB (sRGB), 1 Linear, 2 HSV.
vec3 sg_colorspace(vec3 c, int from, int to) {
  if (from == to) return c;
  vec3 lin = from == 0 ? sg_srgb2linear(c) : from == 1 ? c : sg_srgb2linear(sg_hsv2rgb(c));
  if (from == 2 && to == 0) return sg_hsv2rgb(c);
  if (from == 0 && to == 2) return sg_rgb2hsv(c);
  if (from == 1 && to == 2) return sg_rgb2hsv(sg_linear2srgb(c));
  return to == 1 ? lin : sg_linear2srgb(lin);
}
vec3 sg_hue(vec3 c, float offset) {
  vec3 hsv = sg_rgb2hsv(c);
  float hue = hsv.x + offset;
  hsv.x = (hue < 0.0) ? hue + 1.0 : (hue > 1.0) ? hue - 1.0 : hue;
  return sg_hsv2rgb(hsv);
}");

            void Gather() => Unit.Helper("gather", @"// textureGather is GL 4.0 / ES 3.1: the 2x2 footprint by texelFetch, in HLSL Gather order.
vec4 sg_gather(sampler2D t, vec2 uv, vec2 offset) {
  ivec2 size = textureSize(t, 0);
  vec2 p = uv * vec2(size) - 0.5;
  ivec2 b = ivec2(floor(p)) + ivec2(offset);
  ivec2 m = size - 1;
  float x0y1 = texelFetch(t, clamp(b + ivec2(0, 1), ivec2(0), m), 0).r;
  float x1y1 = texelFetch(t, clamp(b + ivec2(1, 1), ivec2(0), m), 0).r;
  float x1y0 = texelFetch(t, clamp(b + ivec2(1, 0), ivec2(0), m), 0).r;
  float x0y0 = texelFetch(t, clamp(b, ivec2(0), m), 0).r;
  return vec4(x0y1, x1y1, x1y0, x0y0);
}");

            void Dither() => Unit.Helper("dither", @"float sg_dither(vec4 screen) {
  vec2 uv = screen.xy * sg_ScreenParams.xy;
  const float m[16] = float[16](1.0/17.0, 9.0/17.0, 3.0/17.0, 11.0/17.0, 13.0/17.0, 5.0/17.0, 15.0/17.0, 7.0/17.0,
                                4.0/17.0, 12.0/17.0, 2.0/17.0, 10.0/17.0, 16.0/17.0, 8.0/17.0, 14.0/17.0, 6.0/17.0);
  int i = (int(uv.x) % 4) * 4 + int(uv.y) % 4;
  return m[i];
}");

            void NormalHeight() => Unit.Helper("normalheight", @"#ifdef SG_FRAGMENT
vec3 sg_normalFromHeight(float h, float strength, bool tangentOut) {
  mat3 tbn = sg_TBN();
  vec3 dx = dFdx(sg_PosWS); vec3 dy = dFdy(sg_PosWS);
  vec3 crossX = cross(tbn[2], dx); vec3 crossY = cross(dy, tbn[2]);
  float d = dot(dx, crossY);
  float sgn = d < 0.0 ? -1.0 : 1.0;
  float surface = sgn / max(0.000000000000001192093, abs(d));
  vec3 surfGrad = surface * (dFdx(h) * crossY + dFdy(h) * crossX);
  vec3 o = normalize(tbn[2] - strength * surfGrad);
  return tangentOut ? transpose(tbn) * o : o;
}
#endif");
        }
    }
}
