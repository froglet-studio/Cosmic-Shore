using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What an Omega crew looks like in the world (<see cref="OmegaCrew"/>; <c>OMEGA_CREW.md</c> §5). The hull is the member
    /// the person is seated in and wears the Stoat's model as a stand-in; the OTHER two members are drawn here as faceted
    /// bodies (an octahedron or a box in each member's proportions, the page's body shapes), flying beside the hull apart,
    /// docked on their slots round it combined with struts between them. Weapon fire is a lit bar from the hull to where
    /// it struck; the Arms seat's aim is a thin bar along the turret line; the finisher a cone that fades. Everything is in
    /// the hull's domain colour on a LIT material (nothing in the world looks flat: LOCKED, <c>Docs/claude/SHADERS_AND_PERFORMANCE.md</c>)
    /// and drawn with <c>Graphics.RenderMesh</c>, so the crew owns no GameObjects. The material ships through
    /// <c>Resources/OrcaCrewLit.mat</c> (URP/Lit, instancing on), the Orca's; a shader only <c>Shader.Find</c>s is stripped
    /// from a player build (vessel skill rule 55). Real member models are the next round (/vessel-model).
    /// </summary>
    public sealed class OmegaCrewView : System.IDisposable
    {
        const string LitTemplateResource = "OrcaCrewLit";

        readonly OmegaCrew _crew;
        readonly Material _mat;
        readonly MaterialPropertyBlock _props = new();
        static Mesh _octa, _box;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), Color1 = Shader.PropertyToID("_Color");

        public OmegaCrewView(OmegaCrew crew)
        {
            _crew = crew;
            _mat = LitMaterial();
            _octa ??= Octahedron();
            _box ??= Box();
        }

        public void Dispose()
        {
            if (_mat) Object.Destroy(_mat);
        }

        static Material LitMaterial()
        {
            var template = Resources.Load<Material>(LitTemplateResource);
            Material m;
            if (template && template.shader && template.shader.isSupported) m = new Material(template);
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader)
                {
                    CSDebug.LogError("[OmegaCrewView] Resources/OrcaCrewLit.mat is missing and URP/Lit was not found: the crew's members will not draw.");
                    shader = Shader.Find("Standard");
                }
                m = new Material(shader);
            }
            m.enableInstancing = true;
            return m;
        }

        Color DomainColor() => _crew.Status != null ? ToyFactory.DomainAccentColor(_crew.Status.Domain) : Color.white;

        /// <summary>A member's body in its page proportions (right, up, forward u): a placeholder until each has a model.</summary>
        static (Mesh mesh, Vector3 scale) BodyShape(int member) => member switch
        {
            OmegaMath.Stoat => (_octa, new Vector3(4f, 4f, 9f)),        // plates
            OmegaMath.Thresher => (_octa, new Vector3(6f, 6f, 6f)),     // ball
            OmegaMath.Butterfly => (_box, new Vector3(14f, 1f, 6f)),    // wings
            OmegaMath.Rhino => (_octa, new Vector3(5f, 4f, 10f)),       // wedge
            OmegaMath.Grizzly => (_box, new Vector3(6f, 5f, 8f)),       // box
            OmegaMath.Scarab => (_box, new Vector3(8f, 1.5f, 8f)),      // disc
            OmegaMath.Squirrel => (_octa, new Vector3(3f, 3f, 10f)),    // cone
            OmegaMath.Sparrow => (_octa, new Vector3(8f, 2f, 8f)),      // fighter
            _ => (_box, new Vector3(12f, 1f, 8f)),                      // delta (Manta)
        };

        public void Draw()
        {
            var hull = _crew.Hull;
            if (!hull || !_mat) return;
            var colour = DomainColor();
            _props.Clear();
            _props.SetColor(BaseColor, colour);
            _props.SetColor(Color1, colour);
            var rp = new RenderParams(_mat)
            {
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true,
                worldBounds = new Bounds(hull.position, Vector3.one * 100000f),
                matProps = _props,
            };
            var def = _crew.Def;
            bool omega = _crew.Form == OmegaMath.Form.Omega;
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 6f);

            // the other two members: beside the hull, docking, or docked
            for (int i = 0; i < OmegaCrew.Seats; i++)
            {
                if (i == _crew.HullMember) continue;
                var (mesh, scale) = BodyShape(def.Members[i]);
                Vector3 pos; Quaternion rot;
                if (omega)
                {
                    pos = OmegaMath.SlotWorld(hull.position, hull.rotation, SlotIndex(i));
                    rot = hull.rotation;
                    scale *= 1.25f;
                    // a strut from the hull to the docked member
                    var mid = (hull.position + pos) * 0.5f;
                    var span = pos - hull.position;
                    if (span.sqrMagnitude > 1e-4f)
                        Graphics.RenderMesh(rp, _box, 0, Matrix4x4.TRS(mid, Quaternion.LookRotation(span, hull.up), new Vector3(1.2f, 1.2f, span.magnitude)));
                }
                else
                {
                    var b = _crew.BodyAt(i);
                    pos = b.Pos; rot = b.Rot;
                    if (b.Active) scale *= pulse;
                }
                Graphics.RenderMesh(rp, mesh, 0, Matrix4x4.TRS(pos, rot, scale));
            }

            // the Arms seat's aim line, combined
            if (omega && _crew.HumanSeat == OmegaMath.Arms)
            {
                var dir = _crew.AimDirection;
                float len = 40f;
                Graphics.RenderMesh(rp, _box, 0, Matrix4x4.TRS(hull.position + dir * (len * 0.5f + 8f), Quaternion.LookRotation(dir, hull.up), new Vector3(0.35f, 0.35f, len)));
            }

            // weapon fire: a bar from the hull to the strike, fading
            var shots = _crew.Shots;
            for (int i = 0; i < shots.Count; i++)
            {
                var s = shots[i];
                var span = s.B - s.A;
                if (span.sqrMagnitude < 1e-4f) continue;
                float k = s.Life > 0f ? Mathf.Clamp01(s.T / s.Life) : 0f;
                float w = (s.Wide ? 3f : 0.8f) * (0.4f + 0.6f * k);
                Graphics.RenderMesh(rp, _box, 0, Matrix4x4.TRS((s.A + s.B) * 0.5f, Quaternion.LookRotation(span, hull.up), new Vector3(w, w, span.magnitude)));
            }

            // the finisher: a cone of the finisher's reach, swelling and fading over its 0.6 s
            if (_crew.FinisherFx(out var fpos, out var frot, out var reach, out var t))
            {
                float k = Mathf.Clamp01(t / 0.6f);
                float grow = 1f - k * k;
                float baseR = (reach * 0.45f + 10f) * grow;
                Graphics.RenderMesh(rp, _octa, 0, Matrix4x4.TRS(fpos + frot * Vector3.forward * (reach * 0.5f * grow), frot, new Vector3(baseR, baseR, reach * 0.5f * grow)));
            }
        }

        int SlotIndex(int member)
        {
            if (member == _crew.HullMember) return 0;
            int k = 1;
            for (int i = 0; i < OmegaCrew.Seats; i++)
            {
                if (i == _crew.HullMember) continue;
                if (i == member) return k;
                k++;
            }
            return 2;
        }

        static Mesh Octahedron()
        {
            var p = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            int[][] faces =
            {
                new[] { 4, 0, 2 }, new[] { 4, 2, 1 }, new[] { 4, 1, 3 }, new[] { 4, 3, 0 },
                new[] { 5, 2, 0 }, new[] { 5, 1, 2 }, new[] { 5, 3, 1 }, new[] { 5, 0, 3 },
            };
            return Faceted("OmegaCrewOctahedron", p, faces);
        }

        static Mesh Box()
        {
            var p = new Vector3[8];
            for (int i = 0; i < 8; i++) p[i] = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
            int[][] faces =
            {
                new[] { 0, 2, 3, 1 }, new[] { 4, 5, 7, 6 }, new[] { 0, 1, 5, 4 },
                new[] { 2, 6, 7, 3 }, new[] { 0, 4, 6, 2 }, new[] { 1, 3, 7, 5 },
            };
            return Faceted("OmegaCrewBox", p, faces);
        }

        // Each face gets its own vertices, so its normal is its own: lit facets, never a smooth blob (the Orca's helper).
        static Mesh Faceted(string name, Vector3[] p, int[][] faces)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            foreach (var f in faces)
            {
                int start = verts.Count;
                foreach (int i in f) verts.Add(p[i]);
                for (int k = 1; k + 1 < f.Length; k++)
                {
                    var a = verts[start]; var b = verts[start + k]; var c = verts[start + k + 1];
                    bool outward = Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) > 0f;
                    tris.Add(start); tris.Add(outward ? start + k : start + k + 1); tris.Add(outward ? start + k + 1 : start + k);
                }
            }
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
