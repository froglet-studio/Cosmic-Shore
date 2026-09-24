using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Owns the Butterfly's <b>Fold</b> hold (tuning: <see cref="FoldActionSO"/>; design:
    /// <c>R_VesselActions/BUTTERFLY_FOLD.md</c>). Press → the vessel stops and a ghost appears;
    /// the sticks place the ghost; release → the vessel is there.
    ///
    /// <para><b>What runs where.</b> <c>R_VesselActionHandler</c> round-trips every press and
    /// release through the server, so <see cref="Engage"/> and <see cref="Release"/> already run on
    /// EVERY peer. Three things therefore happen everywhere — the stop, the closed-wing pose, and
    /// the hull's wither/bloom — because every one of them is something the OTHER pilots should be
    /// able to read: a Butterfly with its wings shut is about to leave. Only the GHOST is
    /// local-pilot-only. A screen is a thing one machine has, and the ghost is a preview of a
    /// decision nobody else is making.</para>
    ///
    /// <para><b>The pose is written once, by the owner, through
    /// <see cref="IVessel.SetPose"/></b> — which replicates. Every peer is running this executor,
    /// so each one writing its own idea of the destination would be four machines placing the same
    /// vessel from four different stick readings. The sticks are the OWNER's.</para>
    ///
    /// <para><b>Pitch and yaw die, roll does not, and neither is written here.</b> The stop is
    /// <c>IsTranslationRestricted</c>, and the Butterfly prefab authors
    /// <c>restrictedTurnMultiplier = 0</c>, so <c>VesselTransformer.TurnScalar</c> zeroes pitch and
    /// yaw for the duration and the sticks are free to aim. <c>Roll()</c> is deliberately NOT
    /// scaled by that multiplier and is left unsuppressed, so <c>YDiff</c> goes on rolling the
    /// vessel exactly as it always does — and because the camera reads the ROOT's rotation, rolling
    /// the vessel rolls the frame this executor builds its azimuth and elevation in. That is the
    /// whole "roll the world until the place you want is where your thumbs already are" mechanic,
    /// and it is bought with no new code.</para>
    ///
    /// <para><b>The CAMERA goes with the placement, not with the ship.</b> A pilot cannot choose
    /// a place they cannot see, and the reach is <c>MaxRadiusFraction</c> of the membrane — so a
    /// camera left behind the stopped vessel shows the destination as a few pixels of ghost, if it
    /// is on screen at all. <see cref="VesselPlacementView"/> frames the ghost instead, at the
    /// vessel's own follow distance and in the vessel's own ROLLED frame, which is what keeps the
    /// roll-the-world mechanic legible: the frame the sticks address is the frame you are looking
    /// through. Every peer calls it and only the local pilot's machine acts on it, so there is no
    /// camera gate in this file to get wrong.</para>
    ///
    /// <para>The anchor is held through the WITHER and released on the frame the pose is written —
    /// which is the frame the vessel arrives at the point the camera is already framing. So the
    /// teleport costs the camera no motion at all: it is already there, looking the right way, and
    /// the ship blooms in ahead of it.</para>
    ///
    /// <para><b>The trail is penned UP for the duration</b>, through
    /// <c>VesselPrismController.SetSpawnerPaused</c>. Not optional: <c>IsTranslationRestricted</c>
    /// deliberately does NOT write <c>VesselStatus.Speed</c> (so nothing downstream shifts), which
    /// means the spawn loop's <c>Speed &gt; 3</c> gate is still true while the vessel sits still —
    /// a Butterfly would pile its whole wake into one point. Pen-up is an independent axis from
    /// the spawner's own enable, so it cannot fight an ability that stopped the loop.</para>
    /// </summary>
    public sealed class FoldActionExecutor : ShipActionExecutorBase
    {
        [Header("Config")]
        [Tooltip("Wired directly rather than resolved from the binding maps: a direct reference " +
                 "makes a missing wire visible in the inspector instead of silently falling back " +
                 "to field initializers.")]
        [SerializeField] FoldActionSO config;

        [Header("Ghost")]
        [Tooltip("Material the destination ghost is drawn with. Leave empty and the ghost borrows " +
                 "the hull's own material — it still reads as a second copy of the ship, just an " +
                 "opaque one. Author a translucent domain-tinted material here for the real look.")]
        [SerializeField] Material ghostMaterial;

        [Tooltip("Seconds the ghost takes to bloom in and wither out. Continuity of existence " +
                 "applies to a preview as much as to conserved mass.")]
        [SerializeField, Min(0f)] float ghostBloomSeconds = 0.18f;

        IVesselStatus _status;
        FoldActionSO _activeSo;
        ButterflyHullBuilder _hull;
        ButterflyAnimation _animation;
        Transform _hullVisual;

        bool _held;
        float _heldSeconds;
        float _cooldownUntil = float.NegativeInfinity;

        // Placement state, owner-local (only the owner's sticks decide anything).
        Transform _ghost;
        readonly List<MeshRenderer> _ghostRenderers = new();
        Vector3 _ghostPosition;
        float _ghostBloom;

        // Departure/arrival, every peer.
        // Starts at 1 = fully present. A zero default would make TickArrival run on the vessel's
        // very first frame and bloom the hull in from nothing — an unexplained spawn animation,
        // and one that would fight anything else scaling the hull.
        float _witherPhase = 1f;       // 1 = fully present, 0 = gone
        bool  _departing;
        float _arriveTimer;
        bool  _stopped;                // did WE stop the vessel? only then may we un-stop it

        /// <summary>True while the pilot is holding the Fold. Maintained on every peer — the
        /// closed-wing pose and the stop are things every machine draws.</summary>
        public bool IsFolding => _held;

        /// <summary>Recharge for the HUD's ability card: 1 = just spent, 0 = ready. The fleet's
        /// clockwise depleting veil reads this, and it is the ONLY feedback a refused press gets,
        /// which is why it is a public surface rather than a private timer.</summary>
        public float CooldownRemaining01
        {
            get
            {
                if (_activeSo == null && config == null) return 0f;
                var so = _activeSo ? _activeSo : config;
                float total = so.ResolveCooldown(_status);
                if (total <= 0f) return 0f;
                float remaining = _cooldownUntil - Time.time;
                return remaining <= 0f ? 0f : Mathf.Clamp01(remaining / total);
            }
        }

        public bool IsReady => Time.time >= _cooldownUntil;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _hull = GetComponentInParent<ButterflyHullBuilder>(true);
            if (!_hull && shipStatus?.Vessel != null)
                _hull = shipStatus.Vessel.Transform.GetComponentInChildren<ButterflyHullBuilder>(true);
            _hullVisual = _hull ? _hull.transform : null;
            _animation = shipStatus?.Vessel != null
                ? shipStatus.Vessel.Transform.GetComponentInChildren<ButterflyAnimation>(true)
                : null;

            // A re-init hands this component to a different pilot (the vessel swap, the Cellular
            // Duel ownership swap). Whatever the last pilot was holding is not this one's.
            ReleaseInternal(commit: false);
            _cooldownUntil = float.NegativeInfinity;
        }

        void OnDisable()
        {
            // Unconditional and idempotent: a folding vessel that is despawned, pooled or
            // deactivated must let go of the stance, the pen and the ghost, however it went away.
            // The input-pause case is covered a layer up by R_VesselActionHandler.ReleaseHeldInputs.
            ReleaseInternal(commit: false);
        }

        void OnDestroy()
        {
            // The camera outlives this vessel — a placement left set would park the player camera
            // on a point in space for the rest of the match. Keyed, so a teardown arriving after
            // a swap cannot cancel the incoming hull's own placement.
            if (_status != null) VesselPlacementView.Clear(_status.Transform);
            DestroyGhost();
        }

        public void Engage(FoldActionSO so, IVesselStatus status)
        {
            if (!so) return;
            _activeSo = so;
            if (_status == null) _status = status;
            if (_status == null) return;

            // A refused press costs nothing and says so through the HUD's recharge veil, which is
            // already drawn on this card. Silence here would be a dead button.
            if (!IsReady || _held) return;

            _held = true;
            _heldSeconds = 0f;
            _ghostPosition = _status.Transform.position;
            _ghostBloom = 0f;

            // Seeded AT the vessel, so entering the placement view is a no-op snap rather than a
            // cut. The point then sweeps out under the sticks and the camera's ordinary smoothing
            // carries it.
            VesselPlacementView.Place(_status.Transform, _ghostPosition);

            SetStopped(true);
            _status.VesselPrismController?.SetSpawnerPaused(true);
            _animation?.SetFolded(true);
        }

        public void Release(FoldActionSO so, IVesselStatus status) => ReleaseInternal(commit: true);

        void ReleaseInternal(bool commit)
        {
            if (!_held)
            {
                // Still restore the stance and the pen: a teardown that only runs its restore on
                // the HELD path leaves a vessel frozen if anything cleared the flag first.
                if (_status != null)
                {
                    SetStopped(false);
                    _status.VesselPrismController?.SetSpawnerPaused(false);
                    VesselPlacementView.Clear(_status.Transform);
                }
                _animation?.SetFolded(false);
                DestroyGhost();
                return;
            }

            _held = false;
            var so = _activeSo ? _activeSo : config;

            // EVERY peer withers and blooms — a teleport is a disappearance followed by an
            // appearance, and the continuity law does not stop at the owner's screen. Only the
            // owner WRITES the pose (at the bottom of the wither, in TickDeparture); the others
            // run the same animation and adopt the replicated pose while it is playing. The
            // cooldown is tracked everywhere too, so a remote replica refuses a re-press for the
            // same reason the owner does.
            if (commit && so != null && _status != null && IsReady)
            {
                _cooldownUntil = Time.time + so.ResolveCooldown(_status);
                _departing = true;
                _arriveTimer = 0f;
            }
            else
            {
                Restore();
            }

            _animation?.SetFolded(false);
            DestroyGhost();
        }

        void Restore()
        {
            _departing = false;
            _witherPhase = 1f;
            if (_hullVisual) _hullVisual.localScale = Vector3.one;
            if (_status != null)
            {
                SetStopped(false);
                _status.VesselPrismController?.SetSpawnerPaused(false);
                VesselPlacementView.Clear(_status.Transform);
            }
        }

        void SetStopped(bool stopped)
        {
            // Only ever RELEASE a stance we took. Without this, every teardown path (OnDisable, a
            // re-init, a release that never engaged) writes false — which touches a
            // NetworkVariable during despawn, and would silently clear the stance for any future
            // ability on this hull that also uses it.
            if (!stopped && !_stopped) return;
            _stopped = stopped;

            if (_status?.Vessel is not VesselController controller) return;
            // Only the owner writes the replicated flag; every other peer adopts it through the
            // NetworkVariable callback. Writing it on all four peers would have each one racing
            // its own mirror against the replication.
            if (_status.IsNetworkOwner) controller.SetTranslationRestricted(stopped);
        }

        void Update()
        {
            if (_departing) { TickDeparture(); return; }
            if (!_held) { TickArrival(); return; }

            _heldSeconds += Time.deltaTime;
            if (_status == null || !_status.IsNetworkOwner) return;   // the sticks are the owner's

            var so = _activeSo ? _activeSo : config;
            if (so == null) return;

            Vector3 target = ResolveTarget(so);
            _ghostPosition = Vector3.MoveTowards(
                _ghostPosition, target, so.GhostTravelSpeed * Time.deltaTime);

            VesselPlacementView.Place(_status.Transform, _ghostPosition);

            EnsureGhost();
            _ghostBloom = Mathf.MoveTowards(
                _ghostBloom, 1f, Time.deltaTime / Mathf.Max(0.01f, ghostBloomSeconds));
            PoseGhost();
        }

        /// <summary>
        /// Where the ghost is being commanded to, this frame.
        ///
        /// INSIDE a membrane: a point in the cell's sphere, addressed in the VESSEL's own rolled
        /// frame — radius from <c>XDiff</c> (0 inward, 0.5 at rest, 1 outward), azimuth from
        /// <c>XSum</c>, elevation from <c>YSum</c>.
        ///
        /// OUTSIDE one: a reach along the heading, <c>hold × reachSpeed</c>, capped.
        ///
        /// <b>A cell with no membrane yet reads as open space</b>, deliberately:
        /// <c>Cell.MembraneRadius</c> returns 0 until the membrane has SPAWNED, and a radius of
        /// zero would collapse every placement onto the cell centre. That is the same trap the
        /// arena preview's framing already records; here the honest answer is that there is no
        /// sphere to place anything in yet, so the free-space branch is correct rather than a
        /// fallback.
        /// </summary>
        Vector3 ResolveTarget(FoldActionSO so)
        {
            Transform hull = _status.Transform;
            var cell = Cell.FindCellContaining(hull.position);
            float radius = cell ? cell.MembraneRadius : 0f;

            if (!cell || radius <= 0.01f)
            {
                float reach = Mathf.Min(_heldSeconds * so.FreeSpaceReachSpeed,
                                        so.ResolveFreeSpaceRange(_status));
                return hull.position + hull.forward * reach;
            }

            // InputStatus is a default interface member routing through Player, which is null
            // between a despawn and the next pair-init — read it defensively rather than NREing
            // inside a per-frame hold.
            var input = _status.Player != null ? _status.InputStatus : null;
            float xDiff = input != null ? Mathf.Clamp01(input.XDiff) : 0.5f;
            float xSum = input != null ? Mathf.Clamp(input.XSum, -1f, 1f) : 0f;
            float ySum = input != null ? Mathf.Clamp(input.YSum, -1f, 1f) : 0f;

            float azimuth = xSum * so.AzimuthDegrees * Mathf.Deg2Rad;
            float elevation = ySum * so.ElevationDegrees * Mathf.Deg2Rad;

            // The frame is the VESSEL's, which the pilot rolls with YDiff — see the class note.
            Vector3 forward = hull.forward, right = hull.right, up = hull.up;
            float ce = Mathf.Cos(elevation), se = Mathf.Sin(elevation);
            Vector3 direction = forward * (ce * Mathf.Cos(azimuth))
                              + right   * (ce * Mathf.Sin(azimuth))
                              + up      * se;

            return cell.transform.position
                   + direction.normalized * (xDiff * radius * so.MaxRadiusFraction);
        }

        // ---- departure / arrival ------------------------------------------------------------

        /// <summary>The hull withers at the origin, the pose is written at zero, and
        /// <see cref="TickArrival"/> blooms it back. Nothing may instantly disappear, and a
        /// teleport is a disappearance followed by an appearance — this is the platform's
        /// continuity law applied to a vessel rather than to a prism.
        ///
        /// Only the VISUAL is scaled. The vessel root carries the colliders, the skimmer and the
        /// trail spawner, and scaling those would shrink the ship's whole interaction with the
        /// world for a quarter of a second.</summary>
        void TickDeparture()
        {
            var so = _activeSo ? _activeSo : config;
            if (so == null) { Restore(); return; }

            // HELD through the wither. Releasing it here would swing the camera back to the
            // stationary hull for the length of the departure and then swing it out again when the
            // pose lands — the one cut this whole vantage exists to avoid.
            if (_status != null) VesselPlacementView.Place(_status.Transform, _ghostPosition);

            float depart = Mathf.Max(0.0001f, so.DepartSeconds);
            _witherPhase -= Time.deltaTime / depart;
            if (_witherPhase > 0f) { ApplyVisualScale(_witherPhase); return; }

            ApplyVisualScale(0f);
            _departing = false;
            _arriveTimer = 0f;

            // The POSE is the owner's alone: every peer is running this executor, and four
            // machines each writing their own idea of the destination would be four different
            // vessels. SetPose replicates, so the others receive it mid-bloom.
            if (_status != null && _status.IsNetworkOwner && _status.Vessel != null)
            {
                Transform hull = _status.Transform;
                _status.Vessel.SetPose(new Pose(_ghostPosition, hull.rotation));
            }

            // The stance and the pen come back the moment the pose is written — the vessel is
            // flying again, it is simply still blooming in. The camera is handed back on the SAME
            // frame, and hands back to a vessel that is now standing exactly where the camera was
            // already looking, so there is nothing to move.
            if (_status != null)
            {
                SetStopped(false);
                _status.VesselPrismController?.SetSpawnerPaused(false);
                VesselPlacementView.Clear(_status.Transform);
            }
        }

        void TickArrival()
        {
            if (_witherPhase >= 1f) return;
            var so = _activeSo ? _activeSo : config;
            float arrive = so != null ? Mathf.Max(0.0001f, so.ArriveSeconds) : 0.3f;
            _arriveTimer += Time.deltaTime;
            _witherPhase = Mathf.Clamp01(_arriveTimer / arrive);
            ApplyVisualScale(_witherPhase);
        }

        void ApplyVisualScale(float t)
        {
            if (_hullVisual) _hullVisual.localScale = Vector3.one * Mathf.Clamp01(t);
        }

        // ---- ghost --------------------------------------------------------------------------

        void EnsureGhost()
        {
            if (_ghost || _hull == null) return;

            var pieces = new List<ProceduralHullPiece>();
            _hull.BuildPreviewPieces(pieces);
            if (pieces.Count == 0) return;

            // Built through IProceduralHullSource rather than by cloning the live hull: that seam
            // exists precisely so a second copy of a generated ship can be made without waking the
            // builder, and a clone would bring its LateUpdate, its material watch and a second
            // element-morph target along with it.
            var root = new GameObject("ButterflyFoldGhost");
            _ghost = root.transform;

            Material material = ghostMaterial;
            if (!material)
            {
                var source = _hull.GetComponent<MeshRenderer>();
                if (source) material = source.sharedMaterial;
            }

            foreach (var piece in pieces)
            {
                var go = new GameObject(piece.Name);
                go.transform.SetParent(_ghost, false);
                go.transform.localPosition = piece.LocalPosition;

                var mesh = new Mesh { name = "FoldGhost_" + piece.Name };
                mesh.SetVertices(piece.Vertices);
                mesh.SetNormals(piece.Normals);
                mesh.SetUVs(0, piece.Uvs);
                mesh.subMeshCount = piece.Submeshes.Length;
                for (int i = 0; i < piece.Submeshes.Length; i++)
                    mesh.SetTriangles(piece.Submeshes[i], i);
                mesh.RecalculateBounds();

                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                var mats = new Material[piece.Submeshes.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                renderer.sharedMaterials = mats;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _ghostRenderers.Add(renderer);
            }
        }

        void PoseGhost()
        {
            if (!_ghost || _status == null) return;
            _ghost.SetPositionAndRotation(_ghostPosition, _status.Transform.rotation);
            _ghost.localScale = Vector3.one * _ghostBloom;
        }

        void DestroyGhost()
        {
            _ghostRenderers.Clear();
            if (!_ghost) return;
            var go = _ghost.gameObject;
            _ghost = null;
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
    }
}
