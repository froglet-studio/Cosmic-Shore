using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One end of a Butterfly's <b>fold gate</b> pair — a standing portal the Fold leaves behind
    /// (design: <c>R_VesselActions/BUTTERFLY_FOLD.md</c> §6). Every fold opens TWO: one where the
    /// vessel left and one where it arrived. Thread either one and you are at the other.
    ///
    /// <para><b>It is a SWITCH, and it is a DOMAIN switch.</b> A ring you thread is the platform's
    /// one word for "this activates something", so the gate is the ordinary
    /// <see cref="ToyFactory.AddSwitchRing"/> ring drawn at its own trigger radius — the ring IS
    /// the volume, never an advertisement for a bigger one. It wears the placer's DOMAIN because
    /// the colour says who may use it, the second wearer outside the toybox after
    /// <see cref="ScarabSwitch"/> and for the same reason: it BELONGS to a domain rather than
    /// handing you one. Nothing about a gate changes a pilot's domain, so the two readings of a
    /// domain-coloured ring never share a screen.</para>
    ///
    /// <para><b>The Butterfly is the only hull that can place one, and every hull of its domain can
    /// use one.</b> That is the point: the fleet's slowest ship cannot out-fly anybody, and what it
    /// can do instead is leave a shortcut standing that its whole team keeps. A gate is therefore
    /// the Butterfly's contribution to a TEAM rather than to its own lap.</para>
    ///
    /// <para><b>Nothing removes a gate but the pilot who placed it.</b> A pair stands until that
    /// Butterfly folds again and the new pair replaces it — an ACTIVE, explicit player act, the
    /// same class of removal as a cell swap or a Scarab standing one switch too many. There is no
    /// lifespan, no decay and no idle culler here, and there must never be one: that is the timed
    /// culler the platform rejects, wearing a portal's costume.</para>
    ///
    /// <para><b>Who detects, who moves.</b> Each machine tests only the vessels it OWNS, and the
    /// owner writes its own pose — which replicates. So a transit needs no new networking, cannot
    /// double-fire across peers, and cannot be decided for you by somebody else's frame. The
    /// GEOMETRY agrees across machines for free, because neither end is placed from a stick
    /// reading: the origin gate is laid at a vessel that has been stopped and replicated for the
    /// whole hold, and the destination gate at the vessel's replicated pose once it has arrived.</para>
    ///
    /// <para><b>A transit is a TELEPORT and every watcher already knows it</b>, because
    /// <c>IVessel.SetPose</c> bumps <c>VesselTransformer.TeleportCount</c>. So a gate cannot thread
    /// a race ring (<c>GateRaceController</c> declines any step containing a teleport) — the rule
    /// Waystation needed for the Fold covers the gates with nothing added.</para>
    ///
    /// <para><b>A transit is SEAMLESS: nothing on screen says the pilot jumped.</b> Four pieces,
    /// each of which removes one of the ways a teleport shows (<c>BUTTERFLY_FOLD.md</c>
    /// § "Seamless transit"):</para>
    /// <list type="number">
    /// <item><b>The pose is carried through, not re-laid</b> (<see cref="FoldGateGeometry.Through"/>):
    /// the pilot comes out exactly as far past the far plane as they were past the near one, so
    /// the jump is a change of frame with no lurch in it.</item>
    /// <item><b>The mouth is a WINDOW</b>: a gate the viewer's domain may thread shows the far
    /// side through its ring (<see cref="FoldGatePortalView"/>), rendered from exactly where the
    /// camera is about to be — so the pilot flies INTO the place they see.</item>
    /// <item><b>The camera follows the ship through the mouth</b> rather than cutting to it
    /// (<c>CustomCameraController.CarryThroughPortal</c>): it keeps framing the ship through the
    /// window until the camera itself reaches the plane, then takes the same map.</item>
    /// <item><b>Ribbons are cut AT the mouths</b> (<see cref="TeleportContinuity"/>): the tail and
    /// jets end at the near ring and start again at the far one, instead of drawing one straight
    /// streak across the arena between them.</item>
    /// </list>
    /// <para>The vessel is still NOT withered on a transit — that judgement stands and is now
    /// stronger: the rings are the continuity and the window is what makes them read as one
    /// place, so there is no disappearance to cover.</para>
    /// </summary>
    public class FoldGate : MonoBehaviour
    {
        /// <summary>
        /// Every standing gate, oldest first — the <see cref="ScarabSwitch.Live"/> shape, and for
        /// the same reason: an AI, a HUD marker or a mode wanting "the nearest gate of my domain"
        /// must not be running <c>FindObjectsByType</c> to get it. A gate joins on
        /// <see cref="Build"/> rather than Awake, so no reader can ever see one before it knows its
        /// domain, and leaves ahead of its own destruction.
        /// </summary>
        public static readonly List<FoldGate> Live = new();

        Domains _domain;
        string _placerName = string.Empty;
        Vector3 _axis = Vector3.forward;
        float _radius = 1f;
        float _exitClearance;
        float _bloomSeconds = 0.35f;

        FoldGate _partner;
        GameObject _ring;
        MeshRenderer _window;
        MaterialPropertyBlock _windowBlock;
        float _windowBlend;
        IReadOnlyList<IPlayer> _players;

        bool _retiring;
        float _bloom;

        readonly Dictionary<IVessel, Vector3> _lastPos = new();
        readonly HashSet<IVessel> _armed = new();
        readonly List<IVessel> _scratchDead = new();

        /// <summary>The pilot who folded this pair into being.</summary>
        public string PlacerName => _placerName;

        /// <summary>The domain that may thread it — and the colour its ring is painted in.</summary>
        public Domains PlacerDomain => _domain;

        /// <summary>Mouth radius in world units. The ring is drawn at exactly this.</summary>
        public float RingRadius => _radius;

        /// <summary>The mouth's centre in world space.</summary>
        public Vector3 Centre => transform.position;

        /// <summary>The mouth's axis (the normal of its plane). Both ends of a pair share it.</summary>
        public Vector3 Axis => _axis;

        /// <summary>True once <see cref="Retire"/> has run — a closing gate is never a portal.</summary>
        public bool IsRetiring => _retiring;

        /// <summary>
        /// The window surface inside the ring, or null if the portal material could not be
        /// resolved. Shown and hidden only by <see cref="FoldGatePortalView"/>.
        /// </summary>
        public MeshRenderer Window => _window;

        /// <summary>How opaque the window currently is, 0 (a plain ring) .. 1 (the far side).</summary>
        public float WindowBlend => _windowBlend;

        /// <summary>Furthest a camera may be for this gate to show the far side.</summary>
        public float WindowRange { get; private set; } = 2500f;

        /// <summary>Seconds the far side takes to fade into the ring.</summary>
        public float WindowFadeSeconds { get; private set; } = 0.3f;

        /// <summary>Far-side render resolution as a fraction of the gameplay camera's.</summary>
        public float WindowRenderScale { get; private set; } = 0.75f;

        /// <summary>The other end. Null until <see cref="Pair"/> runs, and a gate with no partner
        /// is inert rather than broken: the origin gate stands alone for the length of the fold's
        /// wither and arrival, and threading it in that window must do nothing rather than send a
        /// pilot to a destination that does not exist yet.</summary>
        public FoldGate Partner => _partner;

        /// <summary>Lay the ring. Call immediately after AddComponent, as ScarabSwitch does.</summary>
        public void Build(IVesselStatus placer, IReadOnlyList<IPlayer> players,
                          Vector3 centre, Vector3 axis, float radius,
                          float exitClearance, float bloomSeconds,
                          ThemeManagerDataContainerSO theme,
                          float windowRange, float windowFadeSeconds, float windowRenderScale)
        {
            WindowRange = Mathf.Max(0f, windowRange);
            WindowFadeSeconds = Mathf.Max(0.01f, windowFadeSeconds);
            WindowRenderScale = Mathf.Clamp(windowRenderScale, 0.25f, 1f);

            _domain = placer != null ? placer.Domain : Domains.Blue;
            _placerName = placer != null ? placer.PlayerName : string.Empty;
            _players = players;
            _radius = Mathf.Max(1f, radius);
            _exitClearance = Mathf.Max(0f, exitClearance);
            _bloomSeconds = Mathf.Max(0.01f, bloomSeconds);

            transform.position = centre;
            _axis = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.forward;

            // A ring faces along its axis; the antipode guard is why this is SafeLookRotation and
            // not LookRotation, which is undefined when up is parallel to forward and invents a
            // pose rather than saying so.
            if (SafeLookRotation.TryGet(_axis, out var rot, this)) transform.rotation = rot;

            _ring = ToyFactory.AddSwitchRing(transform, _radius, theme,
                                             ToySwitchSignal.Domain, _domain);
            BuildWindow();
            _bloom = 0f;
            ApplyBloom();

            if (!Live.Contains(this)) Live.Add(this);
        }

        /// <summary>Join the two ends. Symmetric, so one call wires both.</summary>
        public static void Pair(FoldGate a, FoldGate b)
        {
            if (!a || !b || a == b) return;
            a._partner = b;
            b._partner = a;

            // The camera carry and the window both treat the pair as one frame displaced by a
            // TRANSLATION (FoldGateGeometry.Through). A fold lays both ends from one heading, so
            // this holds by construction; say so loudly if a future caller breaks it.
            if (Vector3.Dot(a._axis, b._axis) < 0.9999f)
                CSDebug.LogWarning($"[FoldGate] {a._placerName}'s pair was laid on two different " +
                                   "axes - the window and the camera carry assume one shared " +
                                   "axis and will read the far side at the wrong angle.");
        }

        // A scene unload destroys gates without retiring them; a stale entry would outlive the
        // scene and be handed to the next match's readers.
        void OnDestroy() => Live.Remove(this);

        /// <summary>
        /// Wither this gate away over <paramref name="seconds"/> and destroy it. The ONLY removal
        /// path, and it is always caused by its placer folding again — never by a clock. It leaves
        /// the roster immediately so nothing is steered at a gate that is already closing, and it
        /// unpairs its partner so a half-retired pair can never send a pilot into a gate that is
        /// mid-wither.
        /// </summary>
        public void Retire(float seconds)
        {
            if (_retiring) return;
            _retiring = true;
            Live.Remove(this);
            SetWindow(false, 0f);
            if (_partner && _partner._partner == this) _partner._partner = null;
            _partner = null;
            RetireAsync(Mathf.Max(0.05f, seconds), this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid RetireAsync(float seconds, CancellationToken ct)
        {
            float from = _bloom;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                _bloom = Mathf.Lerp(from, 0f, Mathf.Clamp01(t / seconds));
                ApplyBloom();
                // Sequencing only, never thread marshaling (Docs/THREADING.md).
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            CSDebug.LogVerbose(CSLogChannel.ButterflyFold,
                $"[FoldGate] {_placerName}'s gate closed — replaced by a new fold.");
            Destroy(gameObject);
        }

        void Update()
        {
            // Settles toward 1 from BOTH directions - the bloom rises into it and a flare's
            // overshoot falls back to it. One clause, so a flare can never leave a ring stuck
            // oversized the way a rise-only settle would.
            if (!_retiring && !Mathf.Approximately(_bloom, 1f))
            {
                _bloom = Mathf.MoveTowards(_bloom, 1f, Time.deltaTime / _bloomSeconds);
                ApplyBloom();
            }

            if (_retiring || _partner == null || _players == null) return;

            for (int i = 0; i < _players.Count; i++)
            {
                var vessel = _players[i]?.Vessel;
                if (vessel == null) continue;

                // Only the machine that OWNS a vessel decides that vessel moved: the pose write
                // replicates, so a peer acting too would be two machines teleporting one ship.
                if (!vessel.IsNetworkOwner) continue;

                var status = vessel.VesselStatus;
                if (status?.Player == null || status.Domain != _domain) continue;

                Vector3 cur = vessel.Transform.position;
                bool first = !_lastPos.TryGetValue(vessel, out var prev);
                _lastPos[vessel] = cur;

                // ARMING. A vessel may only be taken by a gate it has been clear of. Without
                // this the very first thing every fold does is teleport the pilot back: the
                // destination gate is laid AROUND them, so flying out of their own arrival ring
                // crosses its plane and sends them home. It is a geometric latch rather than a
                // timer - "you got clear of this gate" is exactly the fact that matters, it has
                // no number of its own, and a system whose whole rule is that nothing runs on a
                // clock should not gate its own detector on one.
                if (!_armed.Contains(vessel))
                {
                    if (!InNearZone(cur)) _armed.Add(vessel);
                    continue;
                }
                if (first) continue;               // two samples are needed to test a crossing
                if (!CrossedMouth(prev, cur, out _)) continue;

                Transit(vessel, status, cur);
                return;                            // one transit per gate per frame
            }

            // A despawned vessel leaves its samples behind, and a gate outlives several of them.
            if (_lastPos.Count > _players.Count) PruneDead();
        }

        void PruneDead()
        {
            _scratchDead.Clear();
            foreach (var key in _lastPos.Keys)
            {
                bool alive = false;
                for (int i = 0; i < _players.Count && !alive; i++)
                    alive = _players[i] != null && ReferenceEquals(_players[i].Vessel, key);
                if (!alive) _scratchDead.Add(key);
            }
            for (int i = 0; i < _scratchDead.Count; i++)
            {
                _lastPos.Remove(_scratchDead[i]);
                _armed.Remove(_scratchDead[i]);
            }
        }

        bool InNearZone(Vector3 p) => FoldGateGeometry.InNearZone(
            p, transform.position, _axis, _radius, _exitClearance);

        bool CrossedMouth(Vector3 prev, Vector3 cur, out Vector3 hit) =>
            FoldGateGeometry.CrossedMouth(prev, cur, transform.position, _axis, _radius, out hit);

        /// <summary>
        /// Put the pilot through. The pilot's position relative to this mouth becomes the same
        /// position relative to the partner's (<see cref="FoldGateGeometry.Through"/>), so:
        ///
        /// <list type="bullet">
        /// <item><b>Where in the mouth you entered is where you leave</b> — threading near the rim
        /// comes out near the rim.</item>
        /// <item><b>The side you were heading for is the side you come out on</b>, and exactly as
        /// far past it as your last step took you — so momentum reads through the gate and there
        /// is no lurch on the frame of the jump.</item>
        /// </list>
        ///
        /// <para>The pilot's ROTATION and SPEED are untouched. A gate moves you; it does not fly
        /// you.</para>
        ///
        /// <para>Both ends are then locked out for this vessel and re-seeded at the exit. The
        /// re-seed is the load-bearing half: without it the partner's very next sample is a segment
        /// running from the pilot's old position to the exit, which crosses its plane and would
        /// bounce them straight back. That is a detector debounce, not a lifespan — nothing is
        /// removed by it.</para>
        /// </summary>
        void Transit(IVessel vessel, IVesselStatus status, Vector3 cur)
        {
            var partner = _partner;
            if (!partner) return;

            Vector3 exit = FoldGateGeometry.Through(cur, transform.position, _axis,
                                                    partner.transform.position, partner._axis);

            // The pose write reaches VesselTransformer.SetPose on every peer, and that is where
            // the rest of the seamlessness happens (TeleportContinuity): the ribbons are cut at
            // the two mouths and any camera following this ship is carried through. Doing it
            // THERE rather than here is what makes it true on every machine - a spectator, and a
            // peer watching a teammate, see the same transit the pilot does.
            vessel.SetPose(new Pose(exit, vessel.Transform.rotation));

            // Both ends are re-seeded and DISARMED. The re-seed alone is not enough: the far
            // gate deposits the pilot just past its own plane, so it has to treat them as
            // somebody standing in its mouth - which is what disarming says - until they have
            // flown clear of it.
            //
            // Re-seeded at where the vessel ACTUALLY is after the write, not at `exit`: on every
            // current route the owner's pose lands synchronously, but if it ever did not, seeding
            // the far position while the hull still sat on the near side would hand the far gate
            // a segment running back across the world, and a later landing would read as a
            // crossing. Both ends are disarmed either way, so a deferred landing is harmless.
            Vector3 now = vessel.Transform.position;
            _lastPos[vessel] = now;
            partner._lastPos[vessel] = now;
            _armed.Remove(vessel);
            partner._armed.Remove(vessel);

            Flare();
            partner.Flare();

            CSDebug.LogVerbose(CSLogChannel.ButterflyFold,
                $"[FoldGate] {status.PlayerName} threaded {_placerName}'s gate " +
                $"({Vector3.Distance(transform.position, partner.transform.position):F0}u).");
        }

        // ---- transit resolution, asked on EVERY peer -----------------------------------------

        /// <summary>
        /// Was the jump <paramref name="from"/> → <paramref name="to"/> a transit through a
        /// standing gate pair? Asked by <see cref="TeleportContinuity"/> from inside every pose
        /// write, on every machine, because only the OWNER runs <see cref="Update"/>'s detector
        /// and everyone else only sees the pose arrive.
        ///
        /// <para><b>Tolerant on purpose.</b> On the owner <paramref name="from"/> is the exact
        /// sample the detector used, so the map lands on <paramref name="to"/> to float precision.
        /// On a peer <paramref name="from"/> is the replica's pose when the write arrives, which
        /// interpolation may have left a little short of — or already a little past — the mouth,
        /// so the test asks only that the jump is the pair's translation to within a mouth
        /// diameter. Two gates of one pair are at least <c>MinGateSeparation</c> apart, so no
        /// ordinary teleport lands within that tolerance by accident.</para>
        ///
        /// <para>The mouths returned are points ON the two planes, at the lateral offset the
        /// vessel went through at — the near one is where a ribbon should end, the far one where
        /// the next should begin.</para>
        /// </summary>
        public static bool TryResolveTransit(Vector3 from, Vector3 to, out FoldGate near,
                                             out Vector3 departMouth, out Vector3 arriveMouth)
        {
            near = null;
            departMouth = arriveMouth = default;
            float bestErr = float.MaxValue;

            for (int i = 0; i < Live.Count; i++)
            {
                var g = Live[i];
                if (!g || g._retiring) continue;
                var p = g._partner;
                if (!p || p._retiring) continue;

                Vector3 c = g.transform.position;
                float depth = FoldGateGeometry.NearZoneDepth(g._radius, g._exitClearance);
                if (Mathf.Abs(FoldGateGeometry.Axial(from, c, g._axis)) > depth * 3f) continue;
                if (FoldGateGeometry.Lateral(from, c, g._axis) > g._radius * 1.5f) continue;

                Vector3 mapped = FoldGateGeometry.Through(from, c, g._axis, p.transform.position, p._axis);
                float err = (mapped - to).sqrMagnitude;
                float tolerance = g._radius * 2f;
                if (err > tolerance * tolerance || err >= bestErr) continue;

                bestErr = err;
                near = g;
                departMouth = FoldGateGeometry.OnPlane(from, c, g._axis);
                arriveMouth = FoldGateGeometry.Through(departMouth, c, g._axis,
                                                       p.transform.position, p._axis);
            }
            return near != null;
        }

        // ---- the window --------------------------------------------------------------------

        const string WindowMaterialResourcePath = "FoldGatePortal";
        static readonly int WindowBlendId = Shader.PropertyToID("_PortalBlend");
        static Material s_windowMaterial;
        static Mesh s_windowMesh;
        static bool s_warnedNoWindowMaterial;

        /// <summary>
        /// Lay the window surface inside the ring. A child of the RING, so it blooms, withers and
        /// flares with it for free, and it sits exactly on the mouth's plane - which is the plane
        /// the far-side render is clipped at, so the picture and the surface agree.
        ///
        /// <para>It starts hidden. Only <see cref="FoldGatePortalView"/> shows one, because there
        /// is one far-side render and it belongs to one gate at a time.</para>
        /// </summary>
        void BuildWindow()
        {
            if (!_ring) return;
            if (s_windowMaterial == null)
            {
                s_windowMaterial = Resources.Load<Material>(WindowMaterialResourcePath);
                if (s_windowMaterial == null)
                {
                    if (!s_warnedNoWindowMaterial)
                    {
                        s_warnedNoWindowMaterial = true;
                        CSDebug.LogWarning($"[FoldGate] No Resources/{WindowMaterialResourcePath} " +
                                           "material - fold gates will be plain rings with no " +
                                           "view of the far side.");
                    }
                    return;
                }
            }
            if (s_windowMesh == null) s_windowMesh = BuildWindowMesh();

            var go = new GameObject("PortalWindow");
            go.transform.SetParent(_ring.transform, false);
            // The ring mesh's tube centre is at 0.5 in its own units and its inner edge at 0.46;
            // 0.48 tucks the window's rim under the tube so there is never a gap to see through.
            go.transform.localScale = Vector3.one * 0.96f;

            go.AddComponent<MeshFilter>().sharedMesh = s_windowMesh;
            _window = go.AddComponent<MeshRenderer>();
            _window.sharedMaterial = s_windowMaterial;
            _window.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _window.receiveShadows = false;
            _window.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _window.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _window.forceRenderingOff = true;
            _windowBlock = new MaterialPropertyBlock();
            _windowBlend = 0f;
        }

        /// <summary>
        /// Show or hide the window at <paramref name="blend"/> opacity. Called by
        /// <see cref="FoldGatePortalView"/> only.
        /// </summary>
        public void SetWindow(bool shown, float blend)
        {
            if (!_window) return;
            _windowBlend = shown ? Mathf.Clamp01(blend) : 0f;
            bool render = shown && _windowBlend > 0.001f && !_retiring;
            _window.forceRenderingOff = !render;
            if (!render) return;
            _windowBlock.SetFloat(WindowBlendId, _windowBlend);
            _window.SetPropertyBlock(_windowBlock);
        }

        /// <summary>A flat unit disc (radius 0.5) in the ring's XY plane, facing its +Z.</summary>
        static Mesh BuildWindowMesh()
        {
            const int segments = 64;
            var verts = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var v = new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f);
                verts[i + 1] = v;
                uvs[i + 1] = new Vector2(v.x + 0.5f, v.y + 0.5f);
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = (i + 1) % segments + 1;
            }
            var mesh = new Mesh { name = "FoldGateWindow", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A visible acknowledgement at BOTH ends — the ring punches and settles. It is
        /// what says a transit happened to everyone except the pilot, who was already looking
        /// somewhere else by then.</summary>
        void Flare()
        {
            if (!_ring || _retiring) return;
            _bloom = 1.25f;
            ApplyBloom();
        }

        void ApplyBloom()
        {
            if (!_ring) return;
            _ring.transform.localScale = Vector3.one * (_radius * 2f * Mathf.Max(0f, _bloom));
        }
    }
}
