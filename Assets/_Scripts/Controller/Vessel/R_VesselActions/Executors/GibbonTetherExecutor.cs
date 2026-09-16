using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Gibbon's two arms: charge, fire, anchor, swing, reel, cut, drop.
    ///
    /// ONE component owns BOTH arms rather than one per side, because the interesting case is the
    /// two-line one and a per-arm component would have to reach across to its sibling to resolve
    /// it. The force it computes is handed to <see cref="GibbonVesselTransformer"/> through the
    /// base transformer's external-acceleration seam — ability logic lives here, momentum lives
    /// there, and the two meet at exactly one method.
    ///
    /// <b>The trigger cycle, which is one press for two jobs.</b> Press = drop whatever this arm
    /// is holding AND begin charging the next shot. Release = fire. So a pilot never has to think
    /// about letting go of a line; reaching for the next one IS letting go of this one, and the
    /// rhythm is a single alternating squeeze per arm.
    ///
    /// <b>Why the fired length is stored, not read at release.</b> The release EDGE is raised when
    /// the analog crosses back down through the deadzone, so the trigger reads ~0 by the time the
    /// release arrives — sampling it there would fire every shot at minimum length. The charge is
    /// therefore tracked every frame while held, and the last held value is both what gets drawn
    /// as the preview and what gets fired. The preview IS the contract.
    ///
    /// <b>Networking: none of its own, deliberately.</b> Press/release already round-trip through
    /// <c>R_VesselActionHandler</c>, so both edges run on every peer; the trigger depth is already
    /// an owner-written NetworkVariable on <c>InputStatus</c>, so every peer charges to the same
    /// length; and the base transformer is not owner-gated, so every peer runs the same swing from
    /// the same inputs. The one residual is that a peer can receive the release up to a tick
    /// before the owner's final depth, so its anchor can land slightly short — bounded by one
    /// tick of trigger travel, and it cannot desync the hull, whose motion replicates anyway.
    /// </summary>
    public sealed class GibbonTetherExecutor : ShipActionExecutorBase
    {
        public enum Arm { Left = 0, Right = 1 }

        [SerializeField] GibbonTetherConfigSO config;

        struct Line
        {
            public bool Charging;
            public float ChargeHeldFor;   // seconds, for devices with no analog trigger
            public bool Live;
            public float ChargedLength;   // what the preview is drawing, and what will fire
            public Vector3 Anchor;
            public float Rest;
            public float Tension01;
            public float NextCutTime;
        }

        readonly Line[] _lines = new Line[2];
        readonly LineRenderer[] _visuals = new LineRenderer[2];
        static Material _sharedLineMaterial;
        readonly List<Prism> _cutBuffer = new List<Prism>(64);

        IVesselStatus _status;

        public bool AnyTaut => _lines[0].Tension01 > 0f || _lines[1].Tension01 > 0f;
        public bool IsLive(Arm arm) => _lines[(int)arm].Live;
        public bool IsCharging(Arm arm) => _lines[(int)arm].Charging;
        public float Tension01(Arm arm) => _lines[(int)arm].Tension01;
        public float ChargedLength(Arm arm) => _lines[(int)arm].ChargedLength;
        public Vector3 Anchor(Arm arm) => _lines[(int)arm].Anchor;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _lines[0] = default;
            _lines[1] = default;
        }

        // ------------------------------------------------------------------ input edges

        /// <summary>Trigger PRESS. Drops this arm's line if it has one, then starts charging.</summary>
        public void BeginCharge(Arm arm)
        {
            int i = (int)arm;
            if (_lines[i].Live) Drop(arm);
            _lines[i].Charging = true;
            _lines[i].ChargeHeldFor = 0f;
            _lines[i].ChargedLength = config != null ? config.MinBeamLength : 45f;
        }

        /// <summary>Trigger RELEASE. Fires the beam at the length the preview was drawing.</summary>
        public void Fire(Arm arm)
        {
            int i = (int)arm;
            if (!_lines[i].Charging) return;
            _lines[i].Charging = false;
            if (config == null || _status == null) return;

            Vector3 muzzle = _status.Transform.position;
            Vector3 dir = LateralDirection(arm);
            float length = _lines[i].ChargedLength;
            Vector3 endpoint = muzzle + dir * length;

            // The firing sweep: everything hostile on the segment dies. This is the aimed half of
            // the weapon; the live line's cutting below is the incidental half.
            SweepAndCut(muzzle, endpoint);

            PlantAnchor(endpoint, dir);

            _lines[i].Anchor = endpoint;
            _lines[i].Rest = length;
            _lines[i].Live = true;
            _lines[i].Tension01 = 0f;
            _lines[i].NextCutTime = 0f;
        }

        /// <summary>Release a line without firing a new one — the drop half of a press, and what
        /// a teardown calls.</summary>
        public void Drop(Arm arm)
        {
            int i = (int)arm;
            _lines[i].Live = false;
            _lines[i].Tension01 = 0f;
            // The anchor prism is NOT removed. It is conserved mass in the pilot's own domain,
            // laid by an active force, and it stays in the world exactly as a trail prism does —
            // grazeable, steal-able, and available to anchor to again. Withering it on release
            // would be passive removal wearing a cleanup's costume.
        }

        /// <summary>Both arms, unconditionally. Called when the vessel stops being driven — the
        /// release edge never arrives for a vessel whose input is paused or that hands over to
        /// autopilot, so a held arm would otherwise stay held forever.</summary>
        public void DropAll()
        {
            _lines[0].Charging = false;
            _lines[1].Charging = false;
            Drop(Arm.Left);
            Drop(Arm.Right);
        }

        void OnDisable()
        {
            DropAll();
            for (int i = 0; i < 2; i++) if (_visuals[i]) _visuals[i].enabled = false;
        }

        void OnDestroy() => DestroyVisuals();

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (config == null || _status == null) return;
            TrackCharge(Arm.Left, LeftDepth());
            TrackCharge(Arm.Right, RightDepth());
            if (config.LiveLineCuts) CutWithLiveLines();
            UpdateVisuals();
        }

        void TrackCharge(Arm arm, float depth01)
        {
            int i = (int)arm;
            if (!_lines[i].Charging) return;
            _lines[i].ChargeHeldFor += Time.deltaTime;
            _lines[i].ChargedLength =
                TetherSolver.BeamLength(Depth01(depth01, _lines[i].ChargeHeldFor),
                                        config.MinBeamLength, config.MaxBeamLength);
        }

        /// <summary>
        /// The charge, 0-1 — analog where the device HAS an analog trigger, a timed wind-up where
        /// it does not.
        ///
        /// Only the gamepad reports real trigger travel; the keyboard and both mouse schemes write
        /// a BINARY 0/1 into the same fields and touch never writes them at all. Reading those raw
        /// would make every keyboard shot a maximum-length shot and every touch shot a minimum one
        /// — in both cases the analog charge, which is half the mechanic, simply would not exist.
        /// A hold-time ramp gives those devices the same verb (hold longer = reach further) at the
        /// cost of its immediacy, which is a real difference in feel and is recorded rather than
        /// hidden. Autopilot writes no trigger at all, so it takes a fixed mid-range shot.
        /// </summary>
        float Depth01(float rawAnalog, float heldSeconds)
        {
            if (_status.AutoPilotEnabled) return AutopilotDepth;
            bool hasAnalogTriggers =
                _status.InputStatus != null &&
                _status.InputStatus.ActiveInputDevice == InputDeviceType.Gamepad;
            if (hasAnalogTriggers) return Mathf.Clamp01(rawAnalog);
            return config.ChargeRampSeconds > 0f
                ? Mathf.Clamp01(heldSeconds / config.ChargeRampSeconds)
                : 1f;
        }

        /// <summary>
        /// One physics step for both lines: reel, then force. Called by
        /// <see cref="GibbonVesselTransformer"/> from inside the move step, so the tether is
        /// resolved in the same frame and the same order as thrust — never a frame behind it.
        /// </summary>
        public Vector3 Solve(Vector3 hullPosition, Vector3 velocity, float dt)
        {
            if (config == null || dt <= 0f) return Vector3.zero;

            Vector3 total = Vector3.zero;
            for (int i = 0; i < 2; i++)
            {
                if (!_lines[i].Live) { _lines[i].Tension01 = 0f; continue; }

                // Reel first: the winch sets the rest length this frame's force is measured against.
                float tangential =
                    TetherSolver.TangentialSpeed(hullPosition, velocity, _lines[i].Anchor);
                float floor = TetherSolver.RestLengthFloor(
                    config.HullRadius, config.AnchorRadius, config.Clearance,
                    tangential, config.MaxSwingRate);
                float step = TetherSolver.ReelStep(
                    velocity.magnitude, config.SpeedCap, config.ReelRate, dt);
                _lines[i].Rest = TetherSolver.ApplyReel(_lines[i].Rest, step, floor);

                var s = TetherSolver.Step(
                    hullPosition, velocity, _lines[i].Anchor, _lines[i].Rest,
                    config.Stiffness, config.RadialDamping,
                    config.MaxStretch, config.BreakStretch, dt);

                _lines[i].Tension01 = s.Tension01;
                total += s.DeltaV;

                // A snapped line drops itself. Speed is kept: the rope failed, the pilot did not.
                if (s.Break) Drop((Arm)i);
            }
            return total;
        }

        // ------------------------------------------------------------------ the anchor

        /// <summary>
        /// Plant the prism the beam anchors to.
        ///
        /// It is ORDINARY CONSERVED MASS in the pilot's own domain, laid through the same pooled
        /// path the Squirrel's boost ring uses — so it is grazeable by the food web, steal-able,
        /// rideable, and it counts for every mode that scores prisms. This vessel's trail is a
        /// CONSTELLATION: the arena it leaves behind is the record of where it swung.
        ///
        /// Anchors are deliberately filed under NO <c>Trail</c>, so the prismscape topology reads
        /// them as 0D singletons rather than a 1D ribbon. That is what they are: discrete points
        /// you swing from, not a line you ride. (They are still ordinary prisms, so skimming,
        /// grazing and stealing all work on them — only the RIDE needs a trail.) Note <c>Trail</c>
        /// is a plain [Serializable] class rather than a MonoBehaviour, so a serialized field for
        /// one would have written an inline copy rather than a reference to the vessel's own.
        ///
        /// <see cref="PrismType.Boost"/> is the right pool by its own description — "fast-growing,
        /// collider-live-on-spawn prisms: a surface a skimmer can boost off, usually flown past
        /// rather than hit". An anchor has to be real the instant the beam lands, which rules out
        /// any pool that defers its collider.
        /// </summary>
        void PlantAnchor(Vector3 position, Vector3 beamDirection)
        {
            var channel = _status.VesselPrismController != null
                ? _status.VesselPrismController.PrismSpawnChannel
                : null;
            if (channel == null) return;

            Quaternion rotation = Quaternion.LookRotation(beamDirection, _status.Transform.up);
            BoostRingBuilder.LayOne(
                channel, position, rotation, config.AnchorScale, PrismKind.Plain,
                _status.Domain, _status.PlayerName,
                $"{_status.PlayerName}::gibbon-anchor");
        }

        // ------------------------------------------------------------------ cutting

        void CutWithLiveLines()
        {
            for (int i = 0; i < 2; i++)
            {
                if (!_lines[i].Live || _lines[i].Tension01 <= 0f) continue;
                if (Time.time < _lines[i].NextCutTime) continue;
                _lines[i].NextCutTime = Time.time + Mathf.Max(0.01f, config.LiveCutInterval);
                SweepAndCut(_status.Transform.position, _lines[i].Anchor);
            }
        }

        /// <summary>
        /// Destroy hostile, cuttable mass along a segment.
        ///
        /// Own-domain mass is skipped, which is also what protects the pilot's own anchors and
        /// their own constellation — the rule is "hostile", not a special case per object.
        /// Super-shielded mass is untouchable by contract, so the line simply does not cut it.
        /// </summary>
        void SweepAndCut(Vector3 a, Vector3 b)
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null) return;

            index.QuerySegment(a, b, config.BeamCutRadius, _cutBuffer);
            if (_cutBuffer.Count == 0) return;

            Domains domain = _status.Domain;
            string playerName = _status.PlayerName;
            Vector3 along = (b - a).normalized;

            for (int i = 0; i < _cutBuffer.Count; i++)
            {
                var prism = _cutBuffer[i];
                if (!prism || prism.destroyed) continue;
                if (prism.Domain == domain) continue;                     // never cut your own
                if (prism.prismProperties is { IsSuperShielded: true }) continue;
                prism.Damage(along * config.BeamCutRadius, domain, playerName);
            }
        }

        // ------------------------------------------------------------------ visuals

        /// <summary>
        /// The line you can see, built at runtime so a vessel needs no authoring to have one.
        ///
        /// Deliberately minimal and deliberately NOT art: a single additive LineRenderer per arm,
        /// drawn hull-to-anchor while live and hull-to-preview-endpoint while charging. It exists
        /// because the mechanic is unreadable without it — the anchor prism and the swing are
        /// visible, but "where is my line and how loaded is it" is the thing a pilot is actually
        /// steering by. Width carries TENSION, so the line visibly thickens as it loads and thins
        /// as it goes slack, which is the one piece of state the physics has and the hull does not
        /// show. Replace wholesale when the real VFX lands.
        /// </summary>
        void UpdateVisuals()
        {
            for (int i = 0; i < 2; i++)
            {
                bool charging = _lines[i].Charging;
                bool live = _lines[i].Live;
                if (!charging && !live)
                {
                    if (_visuals[i]) _visuals[i].enabled = false;
                    continue;
                }

                var lr = EnsureVisual(i);
                Vector3 from = _status.Transform.position;
                Vector3 to = live
                    ? _lines[i].Anchor
                    : from + LateralDirection((Arm)i) * _lines[i].ChargedLength;

                lr.enabled = true;
                lr.SetPosition(0, from);
                lr.SetPosition(1, to);

                // A charging beam is a thin aiming line; a live one carries its load in its width.
                float w = live
                    ? Mathf.Lerp(0.6f, 3.2f, _lines[i].Tension01)
                    : 0.35f;
                lr.startWidth = w;
                lr.endWidth = w;
                lr.startColor = lr.endColor = LineColour(live, _lines[i].Tension01);
            }
        }

        LineRenderer EnsureVisual(int i)
        {
            if (_visuals[i]) return _visuals[i];
            var go = new GameObject($"GibbonLine{i}");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = SharedLineMaterial();
            _visuals[i] = lr;
            return lr;
        }

        /// <summary>One material for every Gibbon in the scene — a per-line material would leak
        /// an instance per arm per vessel.</summary>
        static Material SharedLineMaterial()
        {
            if (_sharedLineMaterial) return _sharedLineMaterial;
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Sprites/Default")
                     ?? Shader.Find("Unlit/Color");
            _sharedLineMaterial = new Material(sh) { name = "GibbonLine (runtime)" };
            return _sharedLineMaterial;
        }

        /// <summary>Cyan while it is only an aim, heating toward white as the line loads — the
        /// same "grey is idle, white is engaged" reading the HUD's petal ladder uses.</summary>
        static Color LineColour(bool live, float tension01)
            => live ? Color.Lerp(new Color(0.15f, 0.85f, 1f), Color.white, tension01)
                    : new Color(0.15f, 0.85f, 1f, 0.5f);

        void DestroyVisuals()
        {
            for (int i = 0; i < 2; i++)
                if (_visuals[i]) { Destroy(_visuals[i].gameObject); _visuals[i] = null; }
        }

        // ------------------------------------------------------------------ geometry / input

        /// <summary>
        /// Which way an arm fires: the HULL's own lateral axis, never a world one and never an
        /// aimed reticle. The whole aiming system of this vessel is that you fly the hull with two
        /// sticks and the beams go out its sides — so rolling ninety degrees turns the right
        /// trigger into an "up" beam, and a full 3D aim costs no extra input.
        /// </summary>
        Vector3 LateralDirection(Arm arm)
        {
            Transform t = _status.Transform;
            return arm == Arm.Left ? -t.right : t.right;
        }

        float LeftDepth() => _status.InputStatus?.LeftTriggerAnalog ?? 0f;
        float RightDepth() => _status.InputStatus?.RightTriggerAnalog ?? 0f;

        /// <summary>Autopilot writes no trigger at all, so an AI Gibbon takes a fixed mid-range
        /// shot rather than reading zero and firing every line at minimum.</summary>
        public float AutopilotDepth => 0.6f;
    }
}
