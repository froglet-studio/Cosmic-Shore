using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What an Omega crew looks like in the world (<see cref="OmegaCrew"/>; <c>OMEGA_CREW.md</c> §5). The hull is the member
    /// the person sits in and wears the Stoat's model as a stand-in; the OTHER two members are drawn here as faceted
    /// bodies (an octahedron or a box in each member's proportions) at their places in the formation. The attacker's
    /// aim is a thin bar along its turret line, weapon fire a bar to the strike, the shield a ring of shards orbiting
    /// the formation while it is held, the ward a shell that fades. Everything is in the hull's domain colour on a LIT
    /// material (nothing in the world looks flat: LOCKED, <c>Docs/claude/SHADERS_AND_PERFORMANCE.md</c>), the colour set
    /// on the material itself (the Orca's way; a property block on the instanced template drew black, 2026-10-26), drawn
    /// with <c>Graphics.RenderMesh</c>, so the crew owns no GameObjects. The material ships through
    /// <c>Resources/OrcaCrewLit.mat</c> (URP/Lit, instancing on), the Orca's; a shader only <c>Shader.Find</c>s is stripped
    /// from a player build (vessel skill rule 55). Real member models are the next round (/vessel-model).
    /// </summary>
    public sealed class OmegaCrewView : System.IDisposable
    {
        const string LitTemplateResource = "OrcaCrewLit";

        readonly OmegaCrew _crew;
        readonly Material _mat;
        static Mesh _octa, _box;

        public OmegaCrewView(OmegaCrew crew)
        {
            _crew = crew;
            _mat = LitMaterial(DomainColor());
            _octa ??= Octahedron();
            _box ??= Box();
        }

        public void Dispose()
        {
            if (_mat) Object.Destroy(_mat);
        }

        static Material LitMaterial(Color color)
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
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 0.35f);
            }
            return m;
        }

        Color DomainColor() => _crew.Status != null ? ToyFactory.DomainAccentColor(_crew.Status.Domain) : Color.white;

        /// <summary>A member's body in its page proportions (right, up, forward u): a placeholder until each has a model.</summary>
        static (Mesh mesh, Vector3 scale) BodyShape(int member) => member switch
        {
            OmegaMath.Stoat => (_octa, new Vector3(4f, 4f, 9f)),        // plates
            OmegaMath.Thresher => (_octa, new Vector3(6f, 6f, 6f)),     // ball
            OmegaMath.Butterfly => (_box, new Vector3(14f, 1.5f, 6f)),  // wings
            OmegaMath.Rhino => (_octa, new Vector3(5f, 4f, 10f)),       // wedge
            OmegaMath.Grizzly => (_box, new Vector3(6f, 5f, 8f)),       // box
            OmegaMath.Scarab => (_box, new Vector3(8f, 2f, 8f)),        // disc
            OmegaMath.Squirrel => (_octa, new Vector3(3f, 3f, 10f)),    // cone
            OmegaMath.Sparrow => (_octa, new Vector3(8f, 2f, 8f)),      // fighter
            _ => (_box, new Vector3(12f, 1.5f, 8f)),                    // delta (Manta)
        };

        public void Draw()
        {
            var hull = _crew.Hull;
            if (!hull || !_mat) return;
            var rp = new RenderParams(_mat)
            {
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true,
                worldBounds = new Bounds(hull.position, Vector3.one * 100000f),
            };
            var def = _crew.Def;
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 6f);

            // the other two members at their places in the formation
            for (int i = 0; i < OmegaCrew.Seats; i++)
            {
                if (i == _crew.HullMember) continue;
                var (mesh, scale) = BodyShape(def.Members[i]);
                var b = _crew.BodyAt(i);
                if (b.Active) scale *= pulse;
                Graphics.RenderMesh(rp, mesh, 0, Matrix4x4.TRS(b.Pos, b.Rot, scale));
            }

            // the attacker's aim line (the person's turret, or the AI's)
            {
                var a = _crew.BodyAt(OmegaCrew.Attack);
                var dir = _crew.AimDirection;
                float len = _crew.HumanSeat == OmegaCrew.Attack ? 40f : 18f;
                Graphics.RenderMesh(rp, _box, 0, Matrix4x4.TRS(a.Pos + dir * (len * 0.5f + 8f), Quaternion.LookRotation(dir, a.Rot * Vector3.up), new Vector3(0.35f, 0.35f, len)));
            }

            // weapon fire: a bar from the shooter to the strike, fading
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

            // the shield: eight shards orbiting the formation at its radius while it is held
            if (_crew.ShieldUp)
            {
                var centre = _crew.FormationCentre;
                float r = _crew.ShieldRadius;
                for (int i = 0; i < 8; i++)
                {
                    float a = Time.time * 1.6f + i * Mathf.PI * 0.25f;
                    var local = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a * 0.5f) * r * 0.35f, Mathf.Sin(a) * r);
                    var p = centre + hull.rotation * local;
                    Graphics.RenderMesh(rp, _octa, 0, Matrix4x4.TRS(p, Quaternion.LookRotation(hull.rotation * Vector3.Cross(Vector3.up, local).normalized, hull.up), new Vector3(1.2f, 1.2f, 4f)));
                }
            }

            // the ward burst: a shell that swells and fades over 0.4 s
            if (_crew.WardFx(out var t))
            {
                float k = Mathf.Clamp01(t / 0.4f);
                float r = _crew.ShieldRadius * 1.5f * (1f - k * k);
                Graphics.RenderMesh(rp, _octa, 0, Matrix4x4.TRS(_crew.FormationCentre, hull.rotation, Vector3.one * r));
            }
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
