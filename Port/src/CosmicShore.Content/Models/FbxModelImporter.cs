using System;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Engine;
using Matrix4x4 = CosmicShore.Engine.Matrix4x4;

namespace CosmicShore.Content.Models
{
    /// <summary>
    /// Unity's ModelImporter, re-implemented for FBX: node hierarchy → GameObjects, Geometry
    /// → Mesh, in Unity's space. The conversion rules, each pinned against ground truth in
    /// the project (see Docs/VESSEL_CONSTRUCTION.md §4.3, §4.6.5, §4.7):
    ///
    ///  • AXIS. The file's GlobalSettings axis system (up/front/coord with signs) is rotated
    ///    onto FBX's Y-up/+Z-front/+X right-handed system. With <c>bakeAxisConversion: 0</c>
    ///    (every meta here) that rotation lands on the TOP-LEVEL nodes' transforms — the
    ///    famous "-90 on X" of a Z-up file's root — never on the vertices.
    ///  • HANDEDNESS. Right- to left-handed by mirroring X: positions/normals/translations
    ///    get x negated, rotations are conjugated by diag(-1,1,1) (quaternion (x,-y,-z,w)),
    ///    local SCALES are preserved, and triangle winding is reversed.
    ///  • UNITS. Positions and translations are scaled by UnitScaleFactor/100 (Unity's
    ///    "Convert Units": 1 FBX unit = 1 cm) × <c>globalScale</c>; scales are not — which is
    ///    why a Blender export's root keeps its Lcl Scaling 100 while landing at meter size.
    ///  • HIERARCHY. Every Model node is a GameObject; a file whose scene root has exactly one
    ///    child is COLLAPSED onto the prefab root (preserveHierarchy off), keeping that
    ///    node's transform. Siblings are sorted by name (sortHierarchyByName).
    ///  • MESHES are named after their node, split per unique (position, normal, uv, color)
    ///    with welding, one submesh per used material index, n-gons fan-triangulated
    ///    (ear-clipped when concave), and an n-gon with a zero-length edge DISCARDED as
    ///    Unity does ("... is self-intersecting and has been discarded").
    /// </summary>
    public static class FbxModelImporter
    {
        public static ImportedModel Import(string path, ModelImportSettings settings = null, string guid = null)
        {
            var scene = FbxScene.Load(path);
            return Import(scene, System.IO.Path.GetFileNameWithoutExtension(path), settings ?? new ModelImportSettings(), path, guid);
        }

        public static ImportedModel Import(FbxScene scene, string name, ModelImportSettings settings, string path = null, string guid = null)
        {
            var model = new ImportedModel { Name = name, Path = path, Guid = guid, Settings = settings, Scene = scene };

            double unitScaleFactor = scene.GlobalDouble("UnitScaleFactor", 1.0);
            model.FileScale = settings.UseFileScale ? unitScaleFactor / 100.0 : 0.01;
            model.UnitScale = model.FileScale * settings.GlobalScale;
            model.AxisConversion = AxisConversion(scene);

            BuildHierarchy(model);
            foreach (var node in model.Nodes) BuildNodeMesh(model, node);
            return model;
        }

        // ── Axis system ────────────────────────────────────────────────────

        static DVec3 Axis(int axis, int sign)
        {
            double s = sign < 0 ? -1 : 1;
            return axis switch { 0 => new DVec3(s, 0, 0), 2 => new DVec3(0, 0, s), _ => new DVec3(0, s, 0) };
        }

        /// <summary>Rotation taking the file's (coord, up, front) axes to (+X, +Y, +Z).</summary>
        public static DMat4 AxisConversion(FbxScene scene)
        {
            var up = Axis(scene.GlobalInt("UpAxis", 1), scene.GlobalInt("UpAxisSign", 1));
            var front = Axis(scene.GlobalInt("FrontAxis", 2), scene.GlobalInt("FrontAxisSign", 1));
            var coord = Axis(scene.GlobalInt("CoordAxis", 0), scene.GlobalInt("CoordAxisSign", 1));
            var m = DMat4.Identity;
            m.M00 = coord.X; m.M01 = coord.Y; m.M02 = coord.Z;
            m.M10 = up.X; m.M11 = up.Y; m.M12 = up.Z;
            m.M20 = front.X; m.M21 = front.Y; m.M22 = front.Z;
            return m;
        }

        // ── Node transforms ────────────────────────────────────────────────

        /// <summary>
        /// The FBX local transform: T·Roff·Rp·Rpre·R·Rpost⁻¹·Rp⁻¹·Soff·Sp·S·Sp⁻¹
        /// (FBX SDK's documented evaluation; pre/post rotations are XYZ-ordered).
        /// </summary>
        public static DMat4 LocalMatrix(FbxObject m)
        {
            var t = V(m.PropVector("Lcl Translation"));
            var r = V(m.PropVector("Lcl Rotation"));
            var s = V(m.PropVector("Lcl Scaling", 1, 1, 1));
            long order = m.PropLong("RotationOrder");
            var rOff = V(m.PropVector("RotationOffset"));
            var rPiv = V(m.PropVector("RotationPivot"));
            var sOff = V(m.PropVector("ScalingOffset"));
            var sPiv = V(m.PropVector("ScalingPivot"));
            var pre = V(m.PropVector("PreRotation"));
            var post = V(m.PropVector("PostRotation"));

            return DMat4.Translate(t) * DMat4.Translate(rOff) * DMat4.Translate(rPiv)
                   * DMat4.Euler(pre) * DMat4.Euler(r, order) * DMat4.Euler(post).Inverse()
                   * DMat4.Translate(-rPiv) * DMat4.Translate(sOff) * DMat4.Translate(sPiv)
                   * DMat4.Scale(s) * DMat4.Translate(-sPiv);
        }

        /// <summary>Geometric (pivot) transform of a node — baked into its mesh's vertices.</summary>
        public static DMat4 GeometricMatrix(FbxObject m)
            => DMat4.Translate(V(m.PropVector("GeometricTranslation")))
               * DMat4.Euler(V(m.PropVector("GeometricRotation")))
               * DMat4.Scale(V(m.PropVector("GeometricScaling", 1, 1, 1)));

        static DVec3 V(double[] a) => new(a[0], a[1], a[2]);

        /// <summary>Right-handed FBX-space affine → Unity space: conjugate by diag(-1,1,1), scale translation.</summary>
        public static DMat4 ToUnity(DMat4 a, double unitScale)
        {
            var r = a;
            // M·A·M with M = diag(-1,1,1): negate row 0 and column 0 of the linear part (M00 twice → unchanged).
            r.M01 = -a.M01; r.M02 = -a.M02; r.M10 = -a.M10; r.M20 = -a.M20;
            r.M03 = -a.M03 * unitScale; r.M13 = a.M13 * unitScale; r.M23 = a.M23 * unitScale;
            return r;
        }

        static void BuildHierarchy(ImportedModel model)
        {
            var scene = model.Scene;
            var s = model.Settings;
            var roots = new List<FbxObject>(scene.RootModels);
            if (s.SortHierarchyByName) roots.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            ModelNode root;
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (!s.PreserveHierarchy && roots.Count == 1)
            {
                root = MakeNode(model, roots[0], null, model.Name, ModelFileIds.RootPath, topLevel: true, counts);
            }
            else
            {
                root = new ModelNode { Name = model.Name, Path = ModelFileIds.RootPath };
                counts[root.Path] = 1;
                model.Nodes.Add(root);
                foreach (var r in roots) MakeNode(model, r, root, r.Name, ModelFileIds.RootPath + "/" + r.Name, topLevel: true, counts);
            }
            model.Root = root;
        }

        static ModelNode MakeNode(ImportedModel model, FbxObject src, ModelNode parent, string name, string path, bool topLevel, Dictionary<string, int> counts)
        {
            counts.TryGetValue(path, out int occ);
            counts[path] = occ + 1;
            var node = new ModelNode { Name = name, Path = path, PathOccurrence = occ, Parent = parent, Source = src };
            var local = LocalMatrix(src);
            if (topLevel && !model.Settings.BakeAxisConversion) local = model.AxisConversion * local;
            node.LocalMatrix = ToUnity(local, model.UnitScale);
            node.ModelMatrix = parent == null ? node.LocalMatrix : parent.ModelMatrix * node.LocalMatrix;
            node.LocalMatrix.Decompose(out var t, out var q, out var sc);
            node.LocalPosition = new Vector3((float)t.X, (float)t.Y, (float)t.Z);
            node.LocalRotation = new Quaternion((float)q.x, (float)q.y, (float)q.z, (float)q.w);
            node.LocalScale = new Vector3((float)sc.X, (float)sc.Y, (float)sc.Z);
            parent?.Children.Add(node);
            model.Nodes.Add(node);

            var kids = new List<FbxObject>();
            foreach (var c in src.Children) if (c.Kind == "Model") kids.Add(c);
            if (model.Settings.SortHierarchyByName) kids.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            foreach (var k in kids) MakeNode(model, k, node, k.Name, path + "/" + k.Name, topLevel: false, counts);
            return node;
        }

        // ── Meshes ─────────────────────────────────────────────────────────

        sealed class LayerElement
        {
            public string Mapping = "", Reference = "";
            public double[] Data = Array.Empty<double>();
            public int[] Index;
            public int Stride;

            public static LayerElement Read(FbxNode n, string dataName, string indexName, int stride)
            {
                if (n == null) return null;
                var e = new LayerElement
                {
                    Mapping = n.ChildString("MappingInformationType") ?? "",
                    Reference = n.ChildString("ReferenceInformationType") ?? "",
                    Data = n.DoubleArray(dataName),
                    Stride = stride,
                };
                if (indexName != null && n.Child(indexName) != null) e.Index = n.IntArray(indexName);
                return e;
            }

            /// <summary>Data element index for a polygon-vertex, or -1.</summary>
            public int Resolve(int polyVertex, int controlPoint, int polygon)
            {
                int i = Mapping switch
                {
                    "ByPolygonVertex" => polyVertex,
                    "ByVertice" or "ByVertex" => controlPoint,
                    "ByPolygon" => polygon,
                    "AllSame" => 0,
                    _ => polyVertex,
                };
                // A LayerElementMaterial carries no separate index array: its "Materials"
                // data IS the per-polygon material index, even under IndexToDirect.
                if ((Reference is "IndexToDirect" or "Index") && Index != null)
                {
                    if (Index == null || i < 0 || i >= Index.Length) return -1;
                    i = Index[i];
                }
                return i >= 0 && (i + 1) * Stride <= Data.Length ? i : -1;
            }
        }

        static IEnumerable<FbxNode> LayerNodes(FbxNode geometry, string name)
        {
            var list = new List<FbxNode>(geometry.ChildrenNamed(name));
            list.Sort((a, b) => a.Long(0).CompareTo(b.Long(0)));
            return list;
        }

        static void BuildNodeMesh(ImportedModel model, ModelNode node)
        {
            if (node.Source == null) return;
            FbxObject geometry = null;
            foreach (var c in node.Source.Children)
                if (c.Kind == "Geometry" && c.SubClass == "Mesh") { geometry = c; break; }
            if (geometry == null) return;

            foreach (var c in node.Source.Children)
                if (c.Kind == "Material") node.Materials.Add(c.Name);

            int occurrence = 0;
            foreach (var m in model.Meshes) if (m.Name == node.Source.Name) occurrence++;
            var imported = new ImportedMesh
            {
                Name = node.Source.Name, Occurrence = occurrence, Node = node, Geometry = geometry,
                FileId = ModelFileIds.Mesh(node.Source.Name, occurrence),
            };
            imported.Mesh = BuildMesh(model, node, geometry, imported);
            node.Mesh = imported;
            model.Meshes.Add(imported);
            model.MeshById[imported.FileId] = imported;
        }

        struct VertexKey : IEquatable<VertexKey>
        {
            public int ControlPoint;
            public Vector3 N;
            public Vector2 Uv0, Uv1;
            public Color C;
            public int Corner; // -1 when welding

            public bool Equals(VertexKey o) => ControlPoint == o.ControlPoint && N.Equals(o.N) && Uv0.Equals(o.Uv0) && Uv1.Equals(o.Uv1)
                                               && C.Equals(o.C) && Corner == o.Corner;
            public override bool Equals(object obj) => obj is VertexKey o && Equals(o);
            public override int GetHashCode() => HashCode.Combine(ControlPoint, N, Uv0, Uv1, C, Corner);
        }

        static Mesh BuildMesh(ImportedModel model, ModelNode node, FbxObject geometry, ImportedMesh info)
        {
            var s = model.Settings;
            var g = geometry.Node;
            double[] cps = g.DoubleArray("Vertices");
            int[] pvi = g.IntArray("PolygonVertexIndex");
            int cpCount = cps.Length / 3;

            var geo = GeometricMatrix(node.Source);
            var geoN = geo.NormalMatrix();
            double us = model.UnitScale;

            // Control points in Unity mesh space.
            var positions = new Vector3[cpCount];
            var posD = new DVec3[cpCount];
            for (int i = 0; i < cpCount; i++)
            {
                var p = geo.Point(new DVec3(cps[i * 3], cps[i * 3 + 1], cps[i * 3 + 2]));
                posD[i] = new DVec3(-p.X * us, p.Y * us, p.Z * us);
                positions[i] = new Vector3((float)posD[i].X, (float)posD[i].Y, (float)posD[i].Z);
            }

            // Polygons.
            var polyStart = new List<int>();
            var polyCount = new List<int>();
            {
                int start = 0;
                for (int i = 0; i < pvi.Length; i++)
                    if (pvi[i] < 0) { polyStart.Add(start); polyCount.Add(i - start + 1); start = i + 1; }
            }
            int CP(int corner) { int v = pvi[corner]; return v < 0 ? ~v : v; }
            info.SourcePolygons = polyStart.Count;

            // Layers.
            LayerElement normals = null, colors = null, materials = null;
            var uvs = new List<LayerElement>();
            foreach (var n in LayerNodes(g, "LayerElementNormal")) { normals = LayerElement.Read(n, "Normals", "NormalsIndex", 3); break; }
            foreach (var n in LayerNodes(g, "LayerElementColor")) { colors = LayerElement.Read(n, "Colors", "ColorIndex", 4); break; }
            foreach (var n in LayerNodes(g, "LayerElementMaterial")) { materials = LayerElement.Read(n, "Materials", null, 1); break; }
            foreach (var n in LayerNodes(g, "LayerElementUV")) uvs.Add(LayerElement.Read(n, "UV", "UVIndex", 2));
            if (s.SwapUVChannels && uvs.Count >= 2) (uvs[0], uvs[1]) = (uvs[1], uvs[0]);
            bool importNormals = s.NormalImportMode == 0 && normals != null;

            // Deformers.
            FbxObject skin = null;
            var blendChannels = new List<FbxObject>();
            foreach (var c in geometry.Children)
            {
                if (c.Kind != "Deformer") continue;
                if (c.SubClass == "Skin") skin ??= c;
                else if (c.SubClass == "BlendShape" && s.ImportBlendShapes)
                    foreach (var ch in c.Children)
                        if (ch.Kind == "Deformer" && ch.SubClass == "BlendShapeChannel") blendChannels.Add(ch);
            }
            bool deformed = skin != null || blendChannels.Count > 0;
            node.Skinned = deformed;

            // Keep polygons; discard what Unity discards.
            var kept = new List<int>();
            for (int p = 0; p < polyStart.Count; p++)
            {
                int n = polyCount[p];
                if (n < 3) { info.DiscardedPolygons++; continue; }
                if (n >= 4)
                {
                    bool zeroEdge = false;
                    for (int k = 0; k < n && !zeroEdge; k++)
                    {
                        var a = posD[CP(polyStart[p] + k)];
                        var b = posD[CP(polyStart[p] + (k + 1) % n)];
                        if (a.X == b.X && a.Y == b.Y && a.Z == b.Z) zeroEdge = true;
                    }
                    if (zeroEdge) { info.DiscardedPolygons++; continue; }
                }
                kept.Add(p);
            }
            model.DiscardedPolygons += info.DiscardedPolygons;

            // Per-corner normals (imported, or calculated with the smoothing angle).
            Vector3[] cornerNormals = importNormals
                ? ImportCornerNormals(normals, pvi, polyStart, polyCount, kept, CP, geoN)
                : CalculateCornerNormals(posD, pvi, polyStart, polyCount, kept, CP, s.NormalSmoothAngle);

            // Weld corners into Unity vertices.
            var map = new Dictionary<VertexKey, int>();
            var vPos = new List<Vector3>();
            var vNrm = new List<Vector3>();
            var vUv0 = new List<Vector2>();
            var vUv1 = new List<Vector2>();
            var vCol = new List<Color>();
            var vCp = new List<int>();
            var cornerVertex = new Dictionary<int, int>();
            bool hasUv0 = uvs.Count > 0, hasUv1 = uvs.Count > 1, hasColor = colors != null;
            foreach (int p in kept)
            {
                for (int k = 0; k < polyCount[p]; k++)
                {
                    int corner = polyStart[p] + k;
                    int cp = CP(corner);
                    var key = new VertexKey { ControlPoint = s.WeldVertices ? (deformed ? cp : -1) : cp, Corner = s.WeldVertices ? -1 : corner };
                    key.N = cornerNormals[corner];
                    if (hasUv0) key.Uv0 = ReadUv(uvs[0], corner, cp, p);
                    if (hasUv1) key.Uv1 = ReadUv(uvs[1], corner, cp, p);
                    if (hasColor) key.C = ReadColor(colors, corner, cp, p);
                    if (!deformed && s.WeldVertices)
                    {
                        // Weld by position rather than control-point identity (two control points at one position merge).
                        key.ControlPoint = PositionIdentity(positions, cp);
                    }
                    if (!map.TryGetValue(key, out int vi))
                    {
                        vi = vPos.Count;
                        map[key] = vi;
                        vPos.Add(positions[cp]);
                        vNrm.Add(key.N);
                        vUv0.Add(key.Uv0);
                        vUv1.Add(key.Uv1);
                        vCol.Add(key.C);
                        vCp.Add(cp);
                    }
                    cornerVertex[corner] = vi;
                }
            }

            // Triangulate into submeshes by material index.
            var bySubmesh = new SortedDictionary<int, List<int>>();
            foreach (int p in kept)
            {
                int mat = 0;
                if (materials != null)
                {
                    int mi = materials.Resolve(polyStart[p], CP(polyStart[p]), p);
                    if (mi >= 0) mat = (int)materials.Data[mi];
                }
                if (!bySubmesh.TryGetValue(mat, out var tris)) bySubmesh[mat] = tris = new List<int>();
                Triangulate(polyStart[p], polyCount[p], CP, posD, cornerVertex, tris);
            }

            var mesh = new Mesh { name = node.Source.Name, isReadable = s.IsReadable };
            mesh.SetVertices(vPos);
            mesh.SetNormals(vNrm);
            if (hasUv0) mesh.SetUVs(0, vUv0);
            if (hasUv1) mesh.SetUVs(1, vUv1);
            if (hasColor) mesh.SetColors(vCol);
            if (vPos.Count > 65535 || s.IndexFormat == 2) mesh.indexFormat = Engine.Rendering.IndexFormat.UInt32;
            mesh.subMeshCount = Math.Max(1, bySubmesh.Count);
            int sub = 0;
            var materialNames = new List<string>();
            foreach (var kv in bySubmesh)
            {
                mesh.SetTriangles(kv.Value, sub++);
                materialNames.Add(kv.Key >= 0 && kv.Key < node.Materials.Count ? node.Materials[kv.Key] : null);
            }
            if (bySubmesh.Count > 0)
            {
                node.Materials.Clear();
                node.Materials.AddRange(materialNames);
            }
            mesh.RecalculateBounds();
            if (hasUv0 && s.TangentImportMode != 2) mesh.SetTangents(CalculateTangents(vPos, vNrm, vUv0, bySubmesh.Values));

            if (skin != null) ApplySkin(model, node, skin, mesh, vCp);
            if (blendChannels.Count > 0) ApplyBlendShapes(model, node, geo, blendChannels, mesh, vCp, vNrm, bySubmesh.Values, cpCount);
            return mesh;
        }

        // Two control points at the bit-identical Unity position are the same vertex for welding.
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Vector3[], Dictionary<Vector3, int>> s_positionIds = new();

        static int PositionIdentity(Vector3[] positions, int cp)
        {
            var table = s_positionIds.GetValue(positions, arr =>
            {
                var d = new Dictionary<Vector3, int>();
                for (int i = 0; i < arr.Length; i++) d.TryAdd(arr[i], i);
                return d;
            });
            return table[positions[cp]];
        }

        static Vector2 ReadUv(LayerElement e, int corner, int cp, int poly)
        {
            int i = e.Resolve(corner, cp, poly);
            return i < 0 ? default : new Vector2((float)e.Data[i * 2], (float)e.Data[i * 2 + 1]);
        }

        static Color ReadColor(LayerElement e, int corner, int cp, int poly)
        {
            int i = e.Resolve(corner, cp, poly);
            return i < 0 ? Color.white : new Color((float)e.Data[i * 4], (float)e.Data[i * 4 + 1], (float)e.Data[i * 4 + 2], (float)e.Data[i * 4 + 3]);
        }

        static Vector3 ToUnityNormal(DVec3 n)
        {
            var u = new DVec3(-n.X, n.Y, n.Z).Normalized;
            return new Vector3((float)u.X, (float)u.Y, (float)u.Z);
        }

        static Vector3[] ImportCornerNormals(LayerElement normals, int[] pvi, List<int> polyStart, List<int> polyCount, List<int> kept, Func<int, int> cpOf, DMat4 geoN)
        {
            var result = new Vector3[pvi.Length];
            foreach (int p in kept)
                for (int k = 0; k < polyCount[p]; k++)
                {
                    int corner = polyStart[p] + k;
                    int i = normals.Resolve(corner, cpOf(corner), p);
                    if (i < 0) continue;
                    var n = geoN.Vector(new DVec3(normals.Data[i * 3], normals.Data[i * 3 + 1], normals.Data[i * 3 + 2]));
                    result[corner] = ToUnityNormal(n);
                }
            return result;
        }

        static DVec3 PolygonNormal(int start, int count, Func<int, int> cpOf, DVec3[] pos)
        {
            // Newell's method (in Unity space — already mirrored, so it follows the reversed winding).
            double x = 0, y = 0, z = 0;
            for (int k = 0; k < count; k++)
            {
                var a = pos[cpOf(start + k)];
                var b = pos[cpOf(start + (k + 1) % count)];
                x += (a.Y - b.Y) * (a.Z + b.Z);
                y += (a.Z - b.Z) * (a.X + b.X);
                z += (a.X - b.X) * (a.Y + b.Y);
            }
            return new DVec3(x, y, z);
        }

        static Vector3[] CalculateCornerNormals(DVec3[] pos, int[] pvi, List<int> polyStart, List<int> polyCount, List<int> kept, Func<int, int> cpOf, float smoothAngle)
        {
            var result = new Vector3[pvi.Length];
            var faceN = new Dictionary<int, DVec3>();
            var incident = new Dictionary<DVec3Key, List<int>>();
            foreach (int p in kept)
            {
                // pos is Unity space (mirrored): the FBX-order Newell normal points INWARD there, so negate.
                var n = -PolygonNormal(polyStart[p], polyCount[p], cpOf, pos);
                faceN[p] = n;
                for (int k = 0; k < polyCount[p]; k++)
                {
                    var key = new DVec3Key(pos[cpOf(polyStart[p] + k)]);
                    if (!incident.TryGetValue(key, out var l)) incident[key] = l = new List<int>();
                    if (!l.Contains(p)) l.Add(p);
                }
            }
            double cosLimit = Math.Cos(smoothAngle * Math.PI / 180.0);
            foreach (int p in kept)
            {
                var fn = faceN[p].Normalized;
                for (int k = 0; k < polyCount[p]; k++)
                {
                    int corner = polyStart[p] + k;
                    var sum = new DVec3(0, 0, 0);
                    foreach (int q in incident[new DVec3Key(pos[cpOf(corner)])])
                    {
                        var qn = faceN[q];
                        if (q == p || DVec3.Dot(qn.Normalized, fn) >= cosLimit) sum += qn;
                    }
                    var u = sum.Normalized;
                    result[corner] = new Vector3((float)u.X, (float)u.Y, (float)u.Z);
                }
            }
            return result;
        }

        readonly struct DVec3Key : IEquatable<DVec3Key>
        {
            readonly double _x, _y, _z;
            public DVec3Key(DVec3 v) { _x = v.X; _y = v.Y; _z = v.Z; }
            public bool Equals(DVec3Key o) => _x == o._x && _y == o._y && _z == o._z;
            public override bool Equals(object obj) => obj is DVec3Key o && Equals(o);
            public override int GetHashCode() => HashCode.Combine(_x, _y, _z);
        }

        /// <summary>
        /// Emit a polygon's triangles in Unity winding (FBX corner order reversed by the X
        /// mirror). Convex polygons fan from corner 0; concave ones are ear-clipped in their
        /// best-fit plane.
        /// </summary>
        static void Triangulate(int start, int count, Func<int, int> cpOf, DVec3[] pos, Dictionary<int, int> cornerVertex, List<int> tris)
        {
            void Emit(int a, int b, int c)
            {
                tris.Add(cornerVertex[start + a]);
                tris.Add(cornerVertex[start + c]);
                tris.Add(cornerVertex[start + b]);
            }
            if (count == 3) { Emit(0, 1, 2); return; }

            // Work in FBX orientation (un-mirror x) so "convex" means CCW about the Newell normal.
            var pts = new DVec3[count];
            for (int k = 0; k < count; k++) { var p = pos[cpOf(start + k)]; pts[k] = new DVec3(-p.X, p.Y, p.Z); }
            var normal = new DVec3(0, 0, 0);
            for (int k = 0; k < count; k++)
            {
                var a = pts[k]; var b = pts[(k + 1) % count];
                normal += new DVec3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
            }
            bool convex = true;
            for (int k = 0; k < count && convex; k++)
            {
                var a = pts[(k + count - 1) % count]; var b = pts[k]; var c = pts[(k + 1) % count];
                if (DVec3.Dot(DVec3.Cross(b - a, c - b), normal) < -1e-12 * Math.Max(1, normal.Length)) convex = false;
            }
            if (convex || normal.Length < 1e-20)
            {
                for (int k = 1; k + 1 < count; k++) Emit(0, k, k + 1);
                return;
            }

            // Ear clipping.
            var idx = new List<int>();
            for (int k = 0; k < count; k++) idx.Add(k);
            int guard = 0;
            while (idx.Count > 3 && guard++ < count * count)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    var a = pts[ia]; var b = pts[ib]; var c = pts[ic];
                    if (DVec3.Dot(DVec3.Cross(b - a, c - b), normal) <= 0) continue; // reflex
                    bool contains = false;
                    foreach (int j in idx)
                    {
                        if (j == ia || j == ib || j == ic) continue;
                        if (InTriangle(pts[j], a, b, c, normal)) { contains = true; break; }
                    }
                    if (contains) continue;
                    Emit(ia, ib, ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            for (int k = 1; k + 1 < idx.Count; k++) Emit(idx[0], idx[k], idx[k + 1]);
        }

        static bool InTriangle(DVec3 p, DVec3 a, DVec3 b, DVec3 c, DVec3 n)
            => DVec3.Dot(DVec3.Cross(b - a, p - a), n) >= 0 && DVec3.Dot(DVec3.Cross(c - b, p - b), n) >= 0 && DVec3.Dot(DVec3.Cross(a - c, p - c), n) >= 0;

        static Vector4[] CalculateTangents(List<Vector3> pos, List<Vector3> nrm, List<Vector2> uv, IEnumerable<List<int>> submeshes)
        {
            int n = pos.Count;
            var tan = new DVec3[n];
            var bit = new DVec3[n];
            foreach (var tris in submeshes)
                for (int i = 0; i + 2 < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    DVec3 p0 = D(pos[a]), p1 = D(pos[b]), p2 = D(pos[c]);
                    double s1 = uv[b].x - uv[a].x, t1 = uv[b].y - uv[a].y, s2 = uv[c].x - uv[a].x, t2 = uv[c].y - uv[a].y;
                    double det = s1 * t2 - s2 * t1;
                    if (Math.Abs(det) < 1e-20) continue;
                    double r = 1 / det;
                    var e1 = p1 - p0; var e2 = p2 - p0;
                    var sdir = (e1 * t2 - e2 * t1) * r;
                    var tdir = (e2 * s1 - e1 * s2) * r;
                    tan[a] += sdir; tan[b] += sdir; tan[c] += sdir;
                    bit[a] += tdir; bit[b] += tdir; bit[c] += tdir;
                }
            var result = new Vector4[n];
            for (int i = 0; i < n; i++)
            {
                var nn = D(nrm[i]);
                var t = (tan[i] - nn * DVec3.Dot(nn, tan[i])).Normalized;
                if (t.Length < 0.5)
                {
                    // Degenerate UVs: any vector perpendicular to the normal.
                    var other = Math.Abs(nn.X) < 0.9 ? new DVec3(1, 0, 0) : new DVec3(0, 1, 0);
                    t = DVec3.Cross(nn, other).Normalized;
                }
                double w = DVec3.Dot(DVec3.Cross(nn, t), bit[i]) < 0 ? -1 : 1;
                result[i] = new Vector4((float)t.X, (float)t.Y, (float)t.Z, (float)w);
            }
            return result;
        }

        static DVec3 D(Vector3 v) => new(v.x, v.y, v.z);

        // ── Skin ───────────────────────────────────────────────────────────

        static void ApplySkin(ImportedModel model, ModelNode node, FbxObject skin, Mesh mesh, List<int> vertexControlPoint)
        {
            var nodeBySource = new Dictionary<FbxObject, ModelNode>();
            foreach (var n in model.Nodes) if (n.Source != null) nodeBySource[n.Source] = n;

            var influences = new Dictionary<int, List<(int bone, double w)>>();
            var bindposes = new List<Matrix4x4>();
            foreach (var cluster in skin.Children)
            {
                if (cluster.Kind != "Deformer" || cluster.SubClass != "Cluster") continue;
                ModelNode bone = null;
                foreach (var c in cluster.Children) if (c.Kind == "Model" && nodeBySource.TryGetValue(c, out bone)) break;
                if (bone == null) continue;
                int boneIndex = node.Bones.Count;
                node.Bones.Add(bone);

                var transform = DMat4.FromArray(cluster.Node.DoubleArray("Transform"));
                var link = DMat4.FromArray(cluster.Node.DoubleArray("TransformLink"));
                var bind = ToUnity(link.Inverse() * transform, model.UnitScale);
                bindposes.Add(ToEngine(bind));

                var idx = cluster.Node.IntArray("Indexes");
                var w = cluster.Node.DoubleArray("Weights");
                for (int i = 0; i < idx.Length && i < w.Length; i++)
                {
                    if (!influences.TryGetValue(idx[i], out var l)) influences[idx[i]] = l = new List<(int, double)>();
                    l.Add((boneIndex, w[i]));
                }
            }
            if (node.Bones.Count == 0) return;

            // Root bone: the topmost bone (whose parent is not itself a bone).
            var boneSet = new HashSet<ModelNode>(node.Bones);
            foreach (var b in node.Bones)
                if (b.Parent == null || !boneSet.Contains(b.Parent)) { node.RootBone = b; break; }

            int maxBones = Math.Clamp(model.Settings.MaxBonesPerVertex, 1, 4);
            double minW = model.Settings.MinBoneWeight;
            var weights = new BoneWeight[vertexControlPoint.Count];
            for (int v = 0; v < weights.Length; v++)
            {
                if (!influences.TryGetValue(vertexControlPoint[v], out var list)) continue;
                list.Sort((a, b) => b.w.CompareTo(a.w));
                int n = 0;
                double sum = 0;
                for (; n < list.Count && n < maxBones && list[n].w >= minW; n++) sum += list[n].w;
                if (sum <= 0) continue;
                var bw = new BoneWeight();
                for (int k = 0; k < n; k++)
                {
                    float wk = (float)(list[k].w / sum);
                    switch (k)
                    {
                        case 0: bw.boneIndex0 = list[k].bone; bw.weight0 = wk; break;
                        case 1: bw.boneIndex1 = list[k].bone; bw.weight1 = wk; break;
                        case 2: bw.boneIndex2 = list[k].bone; bw.weight2 = wk; break;
                        default: bw.boneIndex3 = list[k].bone; bw.weight3 = wk; break;
                    }
                }
                weights[v] = bw;
            }
            mesh.boneWeights = weights;
            mesh.bindposes = bindposes.ToArray();
        }

        public static Matrix4x4 ToEngine(DMat4 m) => new()
        {
            m00 = (float)m.M00, m01 = (float)m.M01, m02 = (float)m.M02, m03 = (float)m.M03,
            m10 = (float)m.M10, m11 = (float)m.M11, m12 = (float)m.M12, m13 = (float)m.M13,
            m20 = (float)m.M20, m21 = (float)m.M21, m22 = (float)m.M22, m23 = (float)m.M23,
            m30 = 0, m31 = 0, m32 = 0, m33 = 1,
        };

        // ── Blend shapes ───────────────────────────────────────────────────

        static void ApplyBlendShapes(ImportedModel model, ModelNode node, DMat4 geo, List<FbxObject> channels, Mesh mesh,
                                     List<int> vertexControlPoint, List<Vector3> baseNormals, IEnumerable<List<int>> submeshes, int cpCount)
        {
            double us = model.UnitScale;
            var basePos = mesh.vertices;
            var allTris = new List<int>();
            foreach (var t in submeshes) allTris.AddRange(t);
            var baseFace = VertexFaceNormals(basePos, allTris);

            foreach (var channel in channels)
            {
                var shapes = new List<FbxObject>();
                foreach (var c in channel.Children) if (c.Kind == "Geometry" && c.SubClass == "Shape") shapes.Add(c);
                if (shapes.Count == 0) continue;
                var fullWeights = channel.Node.DoubleArray("FullWeights");
                for (int si = 0; si < shapes.Count; si++)
                {
                    var shape = shapes[si].Node;
                    var idx = shape.IntArray("Indexes");
                    var dv = shape.DoubleArray("Vertices");
                    var cpDelta = new Dictionary<int, DVec3>();
                    for (int i = 0; i < idx.Length && i * 3 + 2 < dv.Length; i++)
                    {
                        var d = geo.Vector(new DVec3(dv[i * 3], dv[i * 3 + 1], dv[i * 3 + 2]));
                        cpDelta[idx[i]] = new DVec3(-d.X * us, d.Y * us, d.Z * us);
                    }
                    var deltas = new Vector3[basePos.Length];
                    var moved = new Vector3[basePos.Length];
                    for (int v = 0; v < deltas.Length; v++)
                    {
                        if (cpDelta.TryGetValue(vertexControlPoint[v], out var d)) deltas[v] = new Vector3((float)d.X, (float)d.Y, (float)d.Z);
                        moved[v] = basePos[v] + deltas[v];
                    }

                    Vector3[] normalDeltas = null;
                    if (model.Settings.BlendShapeNormalImportMode != 2)
                    {
                        // Unity recalculates shape normals: rotate each vertex normal by how its
                        // incident faces turned under the shape (hard edges survive).
                        var movedFace = VertexFaceNormals(moved, allTris);
                        normalDeltas = new Vector3[basePos.Length];
                        for (int v = 0; v < normalDeltas.Length; v++)
                        {
                            if (!cpDelta.ContainsKey(vertexControlPoint[v])) continue;
                            var rot = Quaternion.FromToRotation(baseFace[v], movedFace[v]);
                            normalDeltas[v] = rot * baseNormals[v] - baseNormals[v];
                        }
                    }
                    float weight = si < fullWeights.Length ? (float)fullWeights[si] : 100f * (si + 1) / shapes.Count;
                    mesh.AddBlendShapeFrame(channel.Name, weight, deltas, normalDeltas, null);
                }
            }
        }

        static Vector3[] VertexFaceNormals(Vector3[] pos, List<int> tris)
        {
            var acc = new Vector3[pos.Length];
            for (int i = 0; i + 2 < tris.Count; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                var n = Vector3.Cross(pos[b] - pos[a], pos[c] - pos[a]);
                acc[a] += n; acc[b] += n; acc[c] += n;
            }
            for (int i = 0; i < acc.Length; i++) acc[i] = acc[i].sqrMagnitude > 0 ? acc[i].normalized : Vector3.up;
            return acc;
        }
    }
}
