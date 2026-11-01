using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Ark as a SOLID ship (Garrett's playtest, 2026-11-01: "it should only allow players to go through that tunnel. the
    /// engines should be solid and not possible to fly through"). Two halves, both of the modelled Ark
    /// (<see cref="ArkModel"/>, <c>ArkReverse.fbx</c>):
    ///
    /// <list type="bullet">
    /// <item><b>The volume.</b> A signed distance over the hull's envelope, ported from the model's own build
    ///   (<c>Tools/Build/vessel_model/specs/ark_reverse.py</c>: the deck and the shell fan, the umbo, the spine, the jaws,
    ///   the jets, the lance, the keel fin), with the hangar bay and the ring tunnel carved out of it - the one passage a
    ///   pilot can fly. Every frame a vessel inside the envelope is set back on its surface and its nose turned along it:
    ///   it slides along the hull, it does not pass through. Vessels only: creatures, rounds and blasts are untouched, so
    ///   every hull prism is still shot and eaten exactly as before (the food web and the escort's numbers do not move).</item>
    /// <item><b>The bulkhead.</b> The stern gape between the jets showed the whole mantle cavity (the brood, the pylons)
    ///   straight through the ship; a dark bulkhead now closes it just ahead of the jets, with rows of lit ports (quarters
    ///   and stores behind the wall). Built in the model's own materials, so it is lit and coloured as the hull.</item>
    /// </list>
    ///
    /// The distance is in the model's frame (the Blender frame the spec is written in: nose -Y, up +Z, left +X), carried
    /// to the Ark's local frame by Unity (x, y, z) = Blender (-x, z, -y), scaled by the model's scale.
    /// </summary>
    [DefaultExecutionOrder(-100)]   // after the vessels move (Update), before the cameras follow them (LateUpdate)
    public sealed class ArkHullSolid : MonoBehaviour
    {
        // ── The model's numbers (ark_reverse.py) ──────────────────────────────
        const float HY = -44f, R = 128f, THETA = 50f, Z0 = 4f, HDOME = 14f, PLATE_T = 1.8f, MANTLE = 18f;
        const float CORE_DROP = PLATE_T + MANTLE, CAVITY_MAX = -2f, DECK_T = 4.5f, RC = R + 2f, FLANK = 6f, CRANK = 6f;
        const float SHOULDER_RHO = 52f, DECK_NOSE = 50f, PROW_FALL = 0.08f, BULWARK_GAP = 1.8f;
        const float UMBO_RMAX = 49f, UMBO_INSET = 4f, UMBO_Z = 20f, UMBO_FALL = 0.26f;
        const float TIER2_FR = 0.46f, BRIDGE_FR = 0.225f, TIER_H0 = 3.5f, TIER_H1 = 7f;
        const float HANGAR_HW = 14f, HANGAR_Z0 = -23f, HANGAR_Z1 = -12f, HANGAR_BACK = -44f;
        const float JAW_IN = 21.2f, JAW_OUT = 33.5f, JAW_Z = -17.5f, JAW_HT = 2.25f, JAW_BACK = -58f, NOSE_Y = -98f;
        const float TUNNEL_R = 4.4f;
        const float ENG_RHO0 = 120f, ENG_RHO1 = 143f, ENG_Z = -5.9f, ENG_R = 6.4f;
        const float PAY_RHO0 = 100f, PAY_RHO1 = 140f, PAY_Z = 5.3f, PAY_R = 2.6f;
        const float KY0 = -42f, KY1 = 22f;
        const int NPLATE = 8;
        const float GROOVE_HALF_DEG = 2.6f;
        static readonly float[] Spine = { -88f, 17f, -24f, -86.6f, 19f, -25.6f, -56f, 19f, -25.6f, -55.2f, 20.6f, -26.6f,
                                          -53.2f, 20.6f, -26.6f, -52.4f, 17f, -24.4f, 20f, 17f, -24.4f, 20.8f, 18.4f, -25.4f,
                                          22.8f, 18.4f, -25.4f, 23.6f, 15f, -24f, 80f, 7f, -22f };
        static readonly Vector2[] Prow = { new(40f, HY - 12f), new(26f, HY - 36f), new(8f, HY - DECK_NOSE), new(0f, HY - DECK_NOSE) };
        static readonly (float y0, float y1)[] Pods = { (-40f, -20f), (-8f, 12f) };

        /// <summary>How far outside the surface a vessel's centre is held (its body still meets an edge-out blade).</summary>
        public const float VesselMargin = 1.2f;

        /// <summary>The bulkhead's radius from the hinge: just aft of the brood's last row, just ahead of the jets.</summary>
        public const float BulkheadRho = 117.2f;

        const float MaxPushPerFrame = 30f;
        const float GradientStep = 0.4f;
        const float ReachPadding = 30f;

        static Vector3[] s_tunnel;
        static float[] s_mids;

        Ark _ark;
        float _scale = 1f;

        /// <summary>Vessels set back on the hull since the Ark was built (a test, the HUD, a sim note).</summary>
        public int Contacts { get; private set; }

        public static ArkHullSolid Attach(Ark ark, Transform modelRoot, float modelScale, Material body, Material window)
        {
            var solid = ark.gameObject.AddComponent<ArkHullSolid>();
            solid._ark = ark;
            solid._scale = Mathf.Max(0.05f, modelScale);
            if (modelRoot && body) solid.BuildBulkhead(ark.transform, body, window);
            return solid;
        }

        // ── The volume ───────────────────────────────────────────────────────

        /// <summary>Signed distance (model units) from a point in the Ark's local frame at scale 1: negative inside the
        /// hull, positive outside, the hangar and the tunnel open.</summary>
        public static float Distance(Vector3 arkLocal)
        {
            var b = new Vector3(-arkLocal.x, -arkLocal.z, arkLocal.y);   // Unity -> Blender
            float solid = Mathf.Min(Mathf.Min(FanBody(b), SpineBody(b)), Mathf.Min(Jaws(b), Mathf.Min(Jets(b), Keel(b))));
            solid = Mathf.Min(solid, Lance(b));
            float carve = Mathf.Min(Hangar(b), Tunnel(b));
            return Mathf.Max(solid, -carve);
        }

        /// <summary>True when the point (Ark local, scale 1) is in the open passage (the hangar or the ring tunnel).</summary>
        public static bool InPassage(Vector3 arkLocal)
        {
            var b = new Vector3(-arkLocal.x, -arkLocal.z, arkLocal.y);
            return Mathf.Min(Hangar(b), Tunnel(b)) < 0f;
        }

        static float Smooth(float a, float c, float x)
        {
            float t = Mathf.Clamp01((x - a) / (c - a));
            return t * t * (3f - 2f * t);
        }

        static float Ang(float dx, float dy) => Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;

        static float Dome(float x, float y)
        {
            float dx = x, dy = y - HY;
            float rho = Mathf.Sqrt(dx * dx + dy * dy);
            float th = rho > 1e-6f ? Ang(dx, dy) : 0f;
            float u = Mathf.Min(rho / R, 1.15f);
            float rise = HDOME * (1f - Mathf.Pow(u, 2.1f)) *
                         (1f - 0.38f * Mathf.Min(1f, (th / THETA) * (th / THETA)) * Smooth(0f, 40f, rho));
            if (dy < 0f) rise *= Mathf.Exp(-(dy / 22f) * (dy / 22f));
            return Z0 + Mathf.Max(rise, -1f);
        }

        static Vector2 At(float phiDeg, float rho)
        {
            float p = phiDeg * Mathf.Deg2Rad;
            return new Vector2(rho * Mathf.Sin(p), HY + rho * Mathf.Cos(p));
        }

        static float RaySeg(float phi, Vector2 a, Vector2 c)
        {
            float p = phi * Mathf.Deg2Rad;
            float dx0 = Mathf.Sin(p), dy0 = Mathf.Cos(p);
            float ex = c.x - a.x, ey = c.y - a.y;
            float den = dx0 * ey - dy0 * ex;
            if (Mathf.Abs(den) < 1e-9f) return -1f;
            float ax = a.x, ay = a.y - HY;
            float t = (ax * ey - ay * ex) / den;
            float s = (ax * dy0 - ay * dx0) / den;
            return t > 0f && s >= -1e-6f && s <= 1f + 1e-6f ? t : -1f;
        }

        static float DeckR(float phi)
        {
            float a = Mathf.Abs(Mathf.Repeat(phi + 180f, 360f) - 180f);
            if (a <= THETA + FLANK) return RC;
            Vector2 p0 = At(THETA + FLANK, RC), p1 = At(THETA + FLANK + CRANK, SHOULDER_RHO);
            float t = RaySeg(a, p0, p1);
            if (t > 0f) return t;
            t = RaySeg(a, p1, Prow[0]);
            if (t > 0f) return t;
            for (int i = 0; i < Prow.Length - 1; i++)
            {
                t = RaySeg(a, Prow[i], Prow[i + 1]);
                if (t > 0f) return t;
            }
            return DECK_NOSE;
        }

        static float FloorZ(float x, float y)
        {
            float flat = CAVITY_MAX - PROW_FALL * Mathf.Max(0f, HY - y);
            if (y <= HY) return flat;
            float rho = Mathf.Sqrt(x * x + (y - HY) * (y - HY));
            float fan = Mathf.Min(Dome(x, y) - CORE_DROP, CAVITY_MAX);
            return flat + (fan - flat) * Smooth(50f, 54f, rho);
        }

        static float UmboR(float phi) => Mathf.Min(UMBO_RMAX, DeckR(phi) - UMBO_INSET);

        static float UmboTop(float x, float y, float rho, float phi)
        {
            float fr = rho / UmboR(phi);
            float baseZ = UMBO_Z - UMBO_FALL * Mathf.Max(0f, HY - y) - 0.07f * Mathf.Abs(x);
            return baseZ + (fr < BRIDGE_FR ? TIER_H1 : fr < TIER2_FR ? TIER_H0 : 0f);
        }

        /// <summary>The deck, the shell fan over the mantle cavity (closed: the bulkhead seals it), the flanks' bulwarks
        /// and the umbo, as one heightfield slab inside the deck's plan outline.</summary>
        static float FanBody(Vector3 b)
        {
            float dx = b.x, dy = b.y - HY;
            float rho = Mathf.Sqrt(dx * dx + dy * dy);
            float phi = rho > 1e-6f ? Ang(dx, dy) : 0f;
            float aphi = Mathf.Abs(phi);
            float outline = DeckR(phi);
            float bottom = FloorZ(b.x, b.y) - DECK_T;
            float top;
            float umboR = UmboR(phi);
            if (rho < umboR) top = UmboTop(b.x, b.y, rho, phi);
            else if (dy < 0f && aphi > 90f) top = FloorZ(b.x, b.y);   // the prow's rim ahead of the umbo
            else
            {
                float shell = Dome(b.x, b.y) + 1.5f;
                var edge = At(THETA, Mathf.Min(rho, R));
                float bulwark = Dome(edge.x, edge.y) - PLATE_T - BULWARK_GAP;
                top = Mathf.Lerp(shell, bulwark, Smooth(THETA - 0.9f, THETA + 1.5f, aphi));
            }
            return Mathf.Max(rho - outline, Mathf.Max(bottom - b.z, b.z - top));
        }

        static void SpineAt(float y, out float hw, out float zb)
        {
            int n = Spine.Length / 3;
            if (y <= Spine[0]) { hw = Spine[1]; zb = Spine[2]; return; }
            for (int i = 0; i < n - 1; i++)
            {
                float y0 = Spine[i * 3], y1 = Spine[i * 3 + 3];
                if (y < y0 || y > y1) continue;
                float t = (y - y0) / Mathf.Max(1e-4f, y1 - y0);
                hw = Mathf.Lerp(Spine[i * 3 + 1], Spine[i * 3 + 4], t);
                zb = Mathf.Lerp(Spine[i * 3 + 2], Spine[i * 3 + 5], t);
                return;
            }
            hw = Spine[(n - 1) * 3 + 1];
            zb = Spine[(n - 1) * 3 + 2];
        }

        /// <summary>The spine (the keel hull under the deck, prow to stern) and the side pods on its flanks.</summary>
        static float SpineBody(Vector3 b)
        {
            SpineAt(b.y, out float hw, out float zb);
            foreach (var (y0, y1) in Pods)
                if (b.y >= y0 && b.y <= y1) hw += 3.6f;
            float zt = FloorZ(0f, b.y) - DECK_T + 1f;
            float dy = Mathf.Max(Spine[0] - b.y, b.y - Spine[Spine.Length - 3]);
            return Mathf.Max(Mathf.Max(Mathf.Abs(b.x) - hw, dy), Mathf.Max(zb - b.z, b.z - zt));
        }

        static float Box(Vector3 p, Vector3 min, Vector3 max)
        {
            var c = (min + max) * 0.5f;
            var h = (max - min) * 0.5f;
            var q = new Vector3(Mathf.Abs(p.x - c.x) - h.x, Mathf.Abs(p.y - c.y) - h.y, Mathf.Abs(p.z - c.z) - h.z);
            var o = Vector3.Max(q, Vector3.zero);
            return o.magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
        }

        static float Capsule(Vector3 p, Vector3 a, Vector3 c, float r)
        {
            var pa = p - a;
            var ba = c - a;
            float h = Mathf.Clamp01(Vector3.Dot(pa, ba) / Mathf.Max(1e-6f, ba.sqrMagnitude));
            return (pa - ba * h).magnitude - r;
        }

        static float Jaws(Vector3 b)
        {
            var p = new Vector3(Mathf.Abs(b.x), b.y, b.z);
            return Box(p, new Vector3(JAW_IN, NOSE_Y, JAW_Z - JAW_HT), new Vector3(JAW_OUT, JAW_BACK, JAW_Z + JAW_HT));
        }

        static float[] Mids()
        {
            if (s_mids != null) return s_mids;
            s_mids = new float[NPLATE];
            for (int k = 0; k < NPLATE; k++)
            {
                float a = -THETA + k * (2f * THETA / NPLATE), c = -THETA + (k + 1) * (2f * THETA / NPLATE);
                s_mids[k] = 0.5f * (a + c);
            }
            return s_mids;
        }

        /// <summary>The eight jets in their gimbal rings, as the rings' envelope: solid, never a fly-through.</summary>
        static float Jets(Vector3 b)
        {
            float best = float.PositiveInfinity;
            var mids = Mids();
            for (int k = 0; k < mids.Length; k++)
            {
                var a = At(mids[k], ENG_RHO0);
                var c = At(mids[k], ENG_RHO1);
                best = Mathf.Min(best, Capsule(b, new Vector3(a.x, a.y, ENG_Z), new Vector3(c.x, c.y, ENG_Z), ENG_R));
            }
            return best;
        }

        static float Lance(Vector3 b)
        {
            var a = At(0f, PAY_RHO0);
            var c = At(0f, PAY_RHO1);
            return Capsule(b, new Vector3(a.x, a.y, PAY_Z), new Vector3(c.x, c.y, PAY_Z), PAY_R);
        }

        static float Keel(Vector3 b)
        {
            if (b.y < KY0 - 2f || b.y > KY1 + 2f) return Box(b, new Vector3(-1.5f, KY0, -36f), new Vector3(1.5f, KY1, -24f));
            SpineAt(b.y, out _, out float zb);
            return Box(b, new Vector3(-1.5f, KY0, zb - 11.2f), new Vector3(1.5f, KY1, zb));
        }

        /// <summary>The hangar bay, open out through its mouth past the jaws.</summary>
        static float Hangar(Vector3 b) =>
            Box(b, new Vector3(-HANGAR_HW, NOSE_Y - 20f, HANGAR_Z0), new Vector3(HANGAR_HW, HANGAR_BACK, HANGAR_Z1));

        /// <summary>The ring tunnel from the hangar's back wall, curving down out through the keel (the spec's spline).</summary>
        static float Tunnel(Vector3 b)
        {
            var pts = TunnelPoints();
            float best = float.PositiveInfinity;
            for (int i = 0; i < pts.Length - 1; i++)
                best = Mathf.Min(best, Capsule(b, pts[i], pts[i + 1], TUNNEL_R));
            return best;
        }

        /// <summary>The tunnel's centreline in the model's (Blender) frame, carried on past the keel so it opens.</summary>
        public static Vector3[] TunnelPoints()
        {
            if (s_tunnel != null) return s_tunnel;
            var ctrl = new[]
            {
                new Vector3(0f, HANGAR_BACK - 1f, -17.5f), new Vector3(0f, -14f, -17.5f), new Vector3(0f, 8f, -17.5f),
                new Vector3(0f, 26f, -19f), new Vector3(0f, 38f, -23f), new Vector3(0f, 48f, -30f),
            };
            var list = new List<Vector3>();
            for (int i = 0; i < ctrl.Length - 1; i++)
            {
                Vector3 p0 = ctrl[Mathf.Max(i - 1, 0)], p1 = ctrl[i], p2 = ctrl[i + 1], p3 = ctrl[Mathf.Min(i + 2, ctrl.Length - 1)];
                for (int s = 0; s < 6; s++)
                {
                    float t = s / 6f;
                    list.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (t * t) +
                                     (-p0 + 3f * p1 - 3f * p2 + p3) * (t * t * t)));
                }
            }
            var last = ctrl[^1];
            list.Add(last);
            var dir = (last - list[^2]).normalized;
            list.Add(last + dir * 14f);
            s_tunnel = list.ToArray();
            return s_tunnel;
        }

        /// <summary>The tunnel's centreline in the Ark's local frame (scale 1), for a camera or a test.</summary>
        public static Vector3 TunnelPointLocal(int i)
        {
            var p = TunnelPoints()[Mathf.Clamp(i, 0, TunnelPoints().Length - 1)];
            return new Vector3(-p.x, p.z, -p.y);
        }

        public static int TunnelPointCount => TunnelPoints().Length;

        // ── Holding vessels out ──────────────────────────────────────────────

        void LateUpdate()
        {
            if (!_ark || _ark.IsHullLost) return;
            var arkT = _ark.transform;
            float unit = _scale * Mathf.Max(1e-4f, arkT.lossyScale.x);   // model units -> world
            float reach = (Mathf.Max(R, -NOSE_Y) + ENG_RHO1 - R + ReachPadding) * unit;
            float reachSqr = reach * reach;
            var centre = arkT.position;
            var live = VesselStatus.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var vessel = live[i];
                if (!vessel || !vessel.isActiveAndEnabled) continue;
                var t = vessel.transform;
                if ((t.position - centre).sqrMagnitude > reachSqr) continue;
                if (!HasAuthority(vessel)) continue;
                HoldOut(vessel, t, arkT, unit);
            }
        }

        static bool HasAuthority(Component vessel)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return true;
            var no = vessel.GetComponentInParent<NetworkObject>();
            return !no || !no.IsSpawned || no.IsOwner;
        }

        void HoldOut(VesselStatus vessel, Transform t, Transform arkT, float unit)
        {
            var local = arkT.InverseTransformPoint(t.position) / _scale;
            float margin = VesselMargin / unit;   // the world margin, in model units
            float d = Distance(local);
            if (d >= margin) return;

            var n = Gradient(local);
            if (n.sqrMagnitude < 1e-8f) return;
            float push = Mathf.Min(margin - d, MaxPushPerFrame / unit);
            var worldN = arkT.TransformDirection(n).normalized;
            t.position += worldN * (push * unit);
            Contacts++;

            // The nose turns along the hull: the part of its heading that points into the surface is taken away.
            IVesselStatus status = vessel;
            var transformer = status.VesselTransformer;
            if (!transformer) return;
            float into = Vector3.Dot(t.forward, worldN);
            if (into >= 0f) return;
            var along = t.forward - worldN * into;
            if (along.sqrMagnitude < 1e-4f) along = Vector3.Cross(worldN, t.right);
            transformer.GentleSpinShip(along.normalized, t.up, 0.35f);
        }

        static Vector3 Gradient(Vector3 p)
        {
            float e = GradientStep;
            return new Vector3(
                Distance(p + new Vector3(e, 0f, 0f)) - Distance(p - new Vector3(e, 0f, 0f)),
                Distance(p + new Vector3(0f, e, 0f)) - Distance(p - new Vector3(0f, e, 0f)),
                Distance(p + new Vector3(0f, 0f, e)) - Distance(p - new Vector3(0f, 0f, e))).normalized;
        }

        // ── The bulkhead ─────────────────────────────────────────────────────

        void BuildBulkhead(Transform arkT, Material body, Material window)
        {
            var wall = new GameObject("ArkBulkhead");
            wall.transform.SetParent(arkT, false);
            wall.transform.localScale = Vector3.one * _scale;
            var mesh = BulkheadMesh();
            wall.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = wall.AddComponent<MeshRenderer>();
            r.sharedMaterials = window ? new[] { body, window } : new[] { body };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Vector3 ToUnity(float bx, float by, float bz) => new(-bx, bz, -by);

        /// <summary>
        /// The bulkhead across the stern gape: a curved wall at <see cref="BulkheadRho"/> from the deck to the shell's
        /// underside, notched down under each groove so the gills standing there keep their slots (the gills and the
        /// stern grille fill those notches), with a row of port slits in each bay. Submesh 0 the hull, 1 the ports.
        /// </summary>
        public static Mesh BulkheadMesh()
        {
            var verts = new List<Vector3>();
            var wallTris = new List<int>();
            var portTris = new List<int>();
            const int steps = 200;
            float rho = BulkheadRho;
            float grooveStep = 2f * THETA / NPLATE;
            for (int i = 0; i <= steps; i++)
            {
                float phi = -THETA + 2f * THETA * i / steps;
                var p = At(phi, rho);
                float bottom = FloorZ(p.x, p.y) - 0.6f;
                float top = Dome(p.x, p.y) - PLATE_T - 0.35f;
                // under a groove the gills' rails run down to dome - 8.7: the wall stops below them
                float k = (phi + THETA) / grooveStep;
                float toGroove = Mathf.Abs(k - Mathf.Round(k)) * grooveStep;
                bool interior = Mathf.Round(k) > 0 && Mathf.Round(k) < NPLATE;
                if (interior && toGroove < GROOVE_HALF_DEG) top = Dome(p.x, p.y) + 0.7f - 8.7f - 0.6f;
                verts.Add(ToUnity(p.x, p.y, bottom));
                verts.Add(ToUnity(p.x, p.y, top));
                if (i == 0) continue;
                int a = (i - 1) * 2, c = i * 2;
                wallTris.AddRange(new[] { a, a + 1, c, c, a + 1, c + 1 });
            }
            // the ports: two rows of slits per bay, set a hair aft of the wall's face
            for (int k = 0; k < NPLATE; k++)
            {
                float mid = -THETA + (k + 0.5f) * grooveStep;
                for (int s = -1; s <= 1; s += 2)
                {
                    float phi = mid + s * 2.2f;
                    var p = At(phi, rho + 0.15f);
                    float bottom = FloorZ(p.x, p.y), top = Dome(p.x, p.y) - PLATE_T;
                    for (int row = 0; row < 2; row++)
                    {
                        float z = Mathf.Lerp(bottom, top, row == 0 ? 0.32f : 0.52f);
                        AddPort(verts, portTris, phi, rho + 0.15f, z, 0.9f, 0.35f);
                    }
                }
            }
            // Face AFT (away from the hinge), where it is seen from: the hull's unlit shader shades by the facing of
            // the normal, and a wall seen from its back draws in its rim colour, bright.
            var n0 = Vector3.Cross(verts[1] - verts[0], verts[2] - verts[0]);
            var aft = new Vector3(verts[0].x, 0f, verts[0].z - 44f);
            if (Vector3.Dot(n0, aft) < 0f)
            {
                Flip(wallTris);
                Flip(portTris);
            }
            var mesh = new Mesh { name = "ArkBulkhead" };
            mesh.SetVertices(verts);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(wallTris, 0);
            mesh.SetTriangles(portTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Flip(List<int> tris)
        {
            for (int i = 0; i + 2 < tris.Count; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
        }

        static void AddPort(List<Vector3> verts, List<int> tris, float phi, float rho, float z, float halfDeg, float halfH)
        {
            var a = At(phi - halfDeg, rho);
            var c = At(phi + halfDeg, rho);
            int i0 = verts.Count;
            verts.Add(ToUnity(a.x, a.y, z - halfH));
            verts.Add(ToUnity(a.x, a.y, z + halfH));
            verts.Add(ToUnity(c.x, c.y, z - halfH));
            verts.Add(ToUnity(c.x, c.y, z + halfH));
            tris.AddRange(new[] { i0, i0 + 1, i0 + 2, i0 + 2, i0 + 1, i0 + 3 });
        }
    }
}
