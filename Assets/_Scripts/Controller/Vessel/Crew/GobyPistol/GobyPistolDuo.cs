using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Goby &amp; Pistol crew vessel (VesselClassType.Goby). Garrett, 2026-10-22: "it should be a goby with its own kit
    /// that represents a goby and synergizes with the pistol and its kit" - the watchman goby and the near-blind pistol
    /// shrimp that share a burrow. The design: <c>.claude/skills/duo-vessel/references/designs/goby-and-pistol.md</c> §11.
    /// <list type="bullet">
    /// <item><b>The Goby</b> never cruises. It perches, aims, and DARTS like the Shrike slings itself: hold Dart to charge
    /// the distance, let go, and it flies there in <c>dartSeconds</c>, stopping on the first block on its line (Time 5: it
    /// passes through prisms). A dart into open space makes its own perch. Each dart lays the Pistol's ground. It LOCKS
    /// rival vessels (then fauna) in its nose's cone.</item>
    /// <item><b>The Pistol</b> rides the ground from perch to perch on a FLAT plane through the dart, downhill whichever
    /// way it goes (<see cref="PistolPlane"/>), Tiny Wings style. At each perch it rests and builds a DEN of prisms and
    /// shielded prisms (Mass 5: danger and super-shielded too), as many as the distance it travelled since its last den
    /// and Mass make; it may ride back to an old den to add to it. Mass beside its plane is pulled into it (Space; Space
    /// 5: shielded too). Its Snap fires a vortex ring; with the Goby locked on a rival that is on the Pistol's screen, the
    /// ring homes on it and takes petals (Charge). Time is its jump's cooldown.</item>
    /// </list>
    ///
    /// <para>One human flies one half and the seat's AI the other; the swap is the arena's gesture (D-pad left / right,
    /// keys 1 / 2, <see cref="PilotSwapGesture"/>) or a double tap on the small view. Each seat has its own controls: the
    /// Goby's Dart and Lock are the hull's own inputs (<see cref="CrewButtonActionSO"/>: Button3 / Button4, a pad's RT /
    /// LT), and on a phone each seat shows its own buttons (<see cref="GobyPistolButtons"/>).</para>
    ///
    /// <para>It lives on <c>Goby.prefab</c> and wakes only on the LOCAL pilot's hull while a human flies it (CrewSeat):
    /// the Pistol, the dens, the pull and the rings are simulated on that one machine until the crew is networked.</para>
    /// </summary>
    public sealed class GobyPistolDuo : MonoBehaviour, ICrewSeat, ICrewButtons, IHullParkAware
    {
        enum Half { Goby = 0, Pistol = 1 }

        [Tooltip("The crew's numbers. Empty: the config's defaults.")]
        [SerializeField] GobyPistolConfigSO config;

        [Tooltip("The AI's level on whichever half the human is not flying.")]
        [SerializeField] AIDifficulty aiLevel = AIDifficulty.Hard;

        [Tooltip("Draw the Pistol's view (full screen while you fly the Pistol, the small square while you fly the Goby).")]
        [SerializeField] bool showSideView = true;

        sealed class Den { public Vector3 Pos, Up; public readonly List<Prism> Prisms = new(); public int Placed; }
        sealed class RingRun { public VortexRing Ring; public readonly List<Prism> Prisms = new(); public IVesselStatus Target; }

        IVessel _goby; IVesselStatus _status; Transform _gt; IPlayer _seatPlayer;
        Half _human = Half.Goby;
        float _t, _swapReadyAt;
        bool _pinned, _penHeld;
        Vector3 _home;
        readonly List<Den> _perches = new();                 // perch k: where the Goby sat, and the den the Pistol builds there
        readonly List<PlaneSegment> _segments = new();       // segment k: the dart from perch k to perch k + 1
        readonly Trail _trail = new();

        // the Goby: its dart and its lock
        bool _dartHeld, _lockHeld, _prevLockHeld, _charging, _darting;
        float _chargeT, _dartReadyAt, _dartDist, _dartWent, _dartSpeed;
        Vector3 _dartDir, _dartUp;
        bool _dartPhase;
        Transform _lockTarget; IVesselStatus _lockVessel; bool _lockIsVessel;

        // the Goby's own AI (while the human flies the Pistol): it aims the hull and darts by the same calls the buttons make
        Vector3 _aiDir; float _aiDecideAt, _aiLandedAt; bool _aiHasDir;
        PistolAI.Mulberry32 _aiRng;

        // the Pistol
        PistolBodyMath.State _p; PistolBodyMath.Settings _body; VortexRing.Settings _vs;
        PistolPlane.Frame _f; PistolAI _ai;
        int _seg = -1, _dir = 1, _at, _travelDir = 1;
        bool _resting = true; float _restT, _waitT, _travelled; int _restQueue, _restSpawned;
        bool _touchGhost, _touchSnap, _backPressed;
        bool _mouseDown, _aiming; Vector2 _mouseFrom, _pointerAt; Vector2? _mouseAim, _padAim; int _fingerId = -1;
        float _scanAt;
        readonly List<RingRun> _rings = new();
        readonly HashSet<Prism> _held = new();
        readonly List<Prism> _q = new(), _q2 = new();

        // the views
        readonly CrewPictureInPicture _pip = new();
        readonly CrewSmallViewDoubleTap _pipTap = new();
        readonly GobyPistolButtons _buttons = new();
        CrewViewRender _render;
        GameObject _view, _bodyGo; Camera _cam; bool _camLive; Quaternion _camRot; Vector3 _camPos, _camWindow; float _camWidth;
        Mesh _torus; Material _ringMat, _lockMat, _bodyMat;

        GobyPistolConfigSO Config => config;

        /// <summary>Add the crew to the vessel you are flying (it then stands in for the Goby). False when no local
        /// vessel is flying. A hull that already carries it (the Goby) is left as it is.</summary>
        public static bool AttachToLocalVessel(GobyPistolConfigSO cfg)
        {
            var vt = VesselVisionShading.LocalVessel;
            if (!vt || !vt.TryGetComponent(out IVessel _)) return false;
            if (vt.TryGetComponent(out GobyPistolDuo _)) return true;
            var duo = vt.gameObject.AddComponent<GobyPistolDuo>();
            if (cfg) duo.config = cfg;
            return true;
        }

        /// <summary>The seat's subvessel on the card ("Goby and Pistol").</summary>
        public string SeatName => "Pistol";
        /// <summary>True while the local human flies the Pistol and the seat's AI flies the Goby.</summary>
        public bool HumanInSeat => _human == Half.Pistol;

        // ---- readouts for the HUD and the tests
        public int PerchCount => _perches.Count;
        public int DenPrisms { get { int n = 0; foreach (var d in _perches) n += d.Placed; return n; } }
        public bool PistolResting => _resting;
        public bool HasLock => _lockTarget;
        public bool DartReady => !_darting && _t >= _dartReadyAt;
        public bool HeadingBack => _travelDir < 0;

        // the local pilot's own hull, flown by a human: the crew seat rule every crew vessel shares
        bool ShouldRun(out IVessel vessel) => CrewSeat.ShouldWake(this, false, out vessel);

        // ================================ binding ================================

        void Bind(IVessel vessel)
        {
            _goby = vessel; _status = vessel.VesselStatus; _gt = transform;
            if (!config) config = ScriptableObject.CreateInstance<GobyPistolConfigSO>();
            _body = Config.Body; _vs = Config.Vortex;
            _t = 0f; _swapReadyAt = 0f; _human = Half.Goby;
            _perches.Clear(); _segments.Clear(); _rings.Clear(); _held.Clear();
            _darting = _charging = false; _dartReadyAt = 0f; ClearLock();
            _aiRng = new PistolAI.Mulberry32(20261022u); _ai = new PistolAI(31337u + 17u);
            _home = _gt.position; _aiHasDir = false; _aiLandedAt = 0f;
            Pin(true); HoldPen(true);
            // the Goby starts perched where it is, on a pad of its own
            AddPerch(_gt.position, _gt.up, ownPad: true);
            _p = new PistolBodyMath.State { launch = Mathf.RoundToInt(_body.pLaunchCharges) };
            _f = WaitingFrame();
            PistolBodyMath.PlaceAtStart(_p, _f, _body.pMinSpeed);
            _seg = -1; _at = 0; _travelDir = 1; _resting = true; _restT = 0f; _restQueue = _restSpawned = 0; _travelled = 0f; _waitT = 0f;
            BuildView();
        }

        void Unbind()
        {
            if (_goby == null) return;
            if (_human == Half.Pistol) SetHuman(Half.Goby);
            _buttons.Dispose();
            ReleaseAllRings();
            Pin(false); HoldPen(false);
            _render?.Dispose(); _render = null; _pip.Dispose();
            if (_view) Destroy(_view);
            if (_bodyGo) Destroy(_bodyGo);
            foreach (var o in new Object[] { _torus, _ringMat, _lockMat, _bodyMat }) if (o) Destroy(o);
            _perches.Clear(); _segments.Clear(); ClearLock();
            _goby = null; _status = null; _gt = null; _cam = null; _view = null; _bodyGo = null; _camLive = false;
        }

        void OnDisable() => Unbind();

        void OnDestroy() => Unbind();

        /// <summary>Translation pinned (the fleet's restricted stance): the Goby never cruises; it only aims and darts.</summary>
        void Pin(bool on)
        {
            if (_pinned == on || _status == null) return;
            _pinned = on;
            if (_status.Vessel is VesselController controller) controller.SetTranslationRestricted(on);
            else _status.IsTranslationRestricted = on;
        }

        /// <summary>The fleet spawner's pen is up: the Goby's only trail is the ground its darts lay.</summary>
        void HoldPen(bool on)
        {
            if (_penHeld == on || _status == null) return;
            var prisms = _status.VesselPrismController;
            if (!prisms) return;
            prisms.SetSpawnerPaused(on);
            _penHeld = on;
        }

        bool IsAuthority()
        {
            var player = _status?.Player;
            if (player != null && player.IsNetworkOwner) return true;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            return nm == null || !nm.IsListening;
        }

        // ================================ the step ================================

        void Update()
        {
            if (!ShouldRun(out var vessel)) { Unbind(); return; }
            if (_goby == null) Bind(vessel);
            float dt = Time.deltaTime; if (dt <= 0f) return;
            _t += dt;
            _body.pLaunchCd = Mathf.Max(0.1f, Config.JumpCooldown.EvaluateReplicated(_status));

            PollSwap();
            PaintButtons();

            if (_human == Half.Goby) HumanGoby(dt); else GobyAI(dt);
            StepDart(dt);
            HoldLock();

            StepPistol(dt);
            Undertow(dt);
            BodyHits();
            StepRings(dt);
        }

        // the level-5 upgrades, each read where it acts (named literally so the element ability table finds every gate)
        R_VesselElementalAbilityHandler Abilities => _status?.ElementalAbilityHandler;

        // ================================ the Goby: dart and perch ================================

        /// <summary>The Goby's own buttons from the hull's bindings (<see cref="CrewButtonActionSO"/>): 0 dart (hold to
        /// charge, let go to dart), 1 lock. The autopilot gate drops them while the seat's AI flies, so they come only from
        /// the person flying the Goby.</summary>
        public void SetButton(int button, bool down)
        {
            if (_goby == null || _human != Half.Goby) return;
            if (button == 0) _dartHeld = down;
            else if (button == 1) _lockHeld = down;
        }

        void HumanGoby(float dt)
        {
            if (_dartHeld && !_charging && DartReady) { _charging = true; _chargeT = 0f; }
            if (_charging)
            {
                _chargeT += dt;
                if (!_dartHeld) { _charging = false; FireDart(Mathf.Lerp(Config.DartMinRange, Config.DartRange, Mathf.Clamp01(_chargeT / Config.DartChargeSeconds))); }
            }
            if (_lockHeld && !_prevLockHeld) TryLock();
            _prevLockHeld = _lockHeld;
        }

        /// <summary>The charge the dart button holds, 0..1 (for the HUD).</summary>
        public float DartCharge01 => _charging ? Mathf.Clamp01(_chargeT / Config.DartChargeSeconds) : 0f;

        /// <summary>Dart along the nose, <paramref name="dist"/> u at most. It stops on the first block on its line (not the
        /// pair's own); with the Time upgrade it passes through prisms and stops at the full distance, on a block there if
        /// there is one. A dart that reaches nothing makes its own perch.</summary>
        public void FireDart(float dist)
        {
            if (_goby == null || !DartReady || dist < 20f) return;
            Vector3 o = _gt.position, f = _gt.forward;
            _dartPhase = (Abilities && Abilities.IsUpgradeActive(Element.Time));
            float go = dist;
            if (!_dartPhase && FirstOnLine(o, f, dist, out float along)) go = Mathf.Max(0f, along);
            if (go < 6f) return;
            _darting = true; _dartDir = f; _dartUp = _gt.up; _dartDist = go; _dartWent = 0f;
            _dartSpeed = go / Mathf.Max(0.05f, Config.DartSeconds);
            _aiHasDir = false;
        }

        /// <summary>The first block (not the pair's own) the hull's padded line meets within <paramref name="len"/>.</summary>
        bool FirstOnLine(Vector3 o, Vector3 f, float len, out float along)
        {
            along = float.MaxValue;
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return false;
            index.QuerySegment(o, o + f * len, Config.HullRadius + Config.QueryReach, _q);
            for (int i = 0; i < _q.Count; i++)
            {
                var prism = _q[i];
                if (!Perchable(prism)) continue;
                if (RayHits(prism, o, f, Config.HullRadius, len, out float t) && t < along) along = t;
            }
            _q.Clear();
            return along < float.MaxValue;
        }

        bool Perchable(Prism prism) => prism && !prism.destroyed && prism.prismProperties != null && prism.ownerID != _status.PlayerName;

        void StepDart(float dt)
        {
            if (!_darting) return;
            float move = Mathf.Min(_dartSpeed * dt, _dartDist - _dartWent);
            // without the Time upgrade, anything that moves into the line stops the dart there (the Shrike docks on the
            // first block it touches)
            if (!_dartPhase && move > 0f && FirstOnLine(_gt.position, _dartDir, move, out float hit)) { move = Mathf.Max(0f, hit); _dartDist = _dartWent + move; }
            if (IsAuthority()) _status.VesselTransformer.TranslateShip(_dartDir * move);
            _dartWent += move;
            if (_dartWent < _dartDist - 0.01f) return;
            _darting = false;
            _dartReadyAt = _t + Config.DartCooldown;
            _aiLandedAt = _t;
            Vector3 at = _gt.position;
            bool onBlock = NearBlock(at, Config.HullRadius + 4f);
            var prev = _perches[_perches.Count - 1];
            AddPerch(at, _dartUp, ownPad: !onBlock);
            var seg = new PlaneSegment(prev.Pos, at, (prev.Up + _dartUp).normalized);
            _segments.Add(seg);
            LayGround(seg);
        }

        bool NearBlock(Vector3 at, float r)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return false;
            index.QuerySphere(at, r + Config.QueryReach, _q2);
            bool near = false;
            for (int i = 0; i < _q2.Count && !near; i++)
                if (Perchable(_q2[i]) && BoxDistance(_q2[i], at) <= r) near = true;
            _q2.Clear();
            return near;
        }

        void AddPerch(Vector3 at, Vector3 up, bool ownPad)
        {
            var den = new Den { Pos = at, Up = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up };
            _perches.Add(den);
            if (!ownPad) return;
            // its own perch in open space: a small ring of blocks under the Goby
            var channel = _status?.VesselPrismController ? _status.VesselPrismController.PrismSpawnChannel : null;
            if (!channel) return;
            int n = Mathf.Max(1, Config.OwnPerchBlocks);
            Vector3 side = Vector3.Cross(den.Up, Mathf.Abs(Vector3.Dot(den.Up, Vector3.forward)) < 0.9f ? Vector3.forward : Vector3.right).normalized;
            float r = Config.PerchBlockSize * 1.6f;
            Vector3 under = at - den.Up * (Config.HullRadius + Config.PerchBlockSize * 0.6f);
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(360f * i / n, den.Up) * side;
                if (!SafeLookRotation.TryGet(dir, den.Up, out var rot, this, logError: false)) continue;
                BoostRingBuilder.LayOne(channel, under + dir * r, rot, Vector3.one * Config.PerchBlockSize, PrismKind.Plain,
                    _status.Domain, _status.PlayerName, _status.PlayerName);
            }
        }

        /// <summary>The dart's ground: a trail block every <c>trailBlockGap</c> along the line, on the hills the Pistol rides.</summary>
        void LayGround(in PlaneSegment seg)
        {
            var prisms = _status.VesselPrismController;
            var channel = prisms ? prisms.PrismSpawnChannel : null;
            if (!channel || seg.L < 1f) return;
            float gap = Mathf.Max(2f, Config.TrailBlockGap);
            var hills = Config.Hills;
            Vector3 scale = Config.TrailBlockScale;
            for (float s = gap * 0.5f; s < seg.L; s += gap)
            {
                Vector3 pos = seg.Ground(s, hills);
                Vector3 along = seg.S + seg.N * PistolPlane.HillSlope(s, seg.L, hills);
                if (!SafeLookRotation.TryGet(along, seg.N, out var rot, this, logError: false)) continue;
                var ret = channel.RaiseEvent(new PrismEventData
                {
                    ownDomain = _status.Domain, Rotation = rot, SpawnPosition = pos, Scale = scale, PrismType = prisms.SpawnPrismType,
                });
                if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism)) continue;
                prism.TargetScale = scale;
                prism.ownerID = _status.PlayerName;
                prism.ChangeTeam(_status.Domain);
                _trail.Add(prism);
                prism.prismProperties.Index = (ushort)Mathf.Min(_trail.TrailList.Count - 1, ushort.MaxValue);
                prism.Initialize(_status.PlayerName);
                prism.AdmitTargetScale(scale);
                prism.TargetScale = scale;
                prism.AssignTrail(_trail);   // AFTER Initialize: pool reuse clears trail membership
            }
        }

        // ================================ the Goby: lock ================================

        /// <summary>Lock the best target in the nose's cone: a rival vessel first, then fauna (the Shrike's order), the one
        /// nearest the aim within its class. Nothing in the cone clears the lock.</summary>
        void TryLock()
        {
            Vector3 pos = _gt.position, f = _gt.forward;
            float cone = Config.LockCone * Mathf.Deg2Rad, best = cone;
            Transform target = null; IVesselStatus vessel = null;
            var players = Config.GameData ? Config.GameData.Players : null;
            if (players != null)
                foreach (var player in players)
                {
                    var v = EnemyStatus(player);
                    if (v == null) continue;
                    float a = Angle(pos, f, v.Transform.position, Config.LockRange);
                    if (a < best) { best = a; target = v.Transform; vessel = v; }
                }
            if (!target)
            {
                var cells = Cell.ActiveCellsSnapshot;
                for (int c = 0; c < cells.Count; c++)
                {
                    var cell = cells[c];
                    if (!cell) continue;
                    var fauna = cell.LiveFauna;
                    for (int i = fauna.Count - 1; i >= 0; i--)
                    {
                        var creature = fauna[i];
                        if (!creature || creature.IsDying || !creature.isActiveAndEnabled) continue;
                        float a = Angle(pos, f, creature.transform.position, Config.LockRange);
                        if (a < best) { best = a; target = creature.transform; }
                    }
                }
            }
            _lockTarget = target; _lockVessel = vessel; _lockIsVessel = vessel != null;
        }

        void HoldLock()
        {
            if (!_lockTarget) { ClearLock(); return; }
            if (_lockIsVessel && (_lockVessel == null || !UnityLiveness.Alive(_lockVessel.Transform))) { ClearLock(); return; }
            if ((_lockTarget.position - _gt.position).sqrMagnitude > Config.LockRange * Config.LockKeepScale * Config.LockRange * Config.LockKeepScale) ClearLock();
        }

        void ClearLock() { _lockTarget = null; _lockVessel = null; _lockIsVessel = false; }

        IVesselStatus EnemyStatus(IPlayer player)
        {
            var victim = player?.Vessel?.VesselStatus;
            if (victim == null || ReferenceEquals(victim, _status)) return null;
            if (victim.Domain == _status.Domain || !UnityLiveness.Alive(victim.Transform)) return null;
            return victim;
        }

        static float Angle(Vector3 pos, Vector3 fwd, Vector3 at, float range)
        {
            Vector3 d = at - pos; float l = d.magnitude;
            if (l > range || l < 1f) return 9f;
            return Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, fwd) / l, -1f, 1f));
        }

        // ================================ the Goby's AI (while the human flies the Pistol) ================================

        /// <summary>The seat's Goby: locks the nearest rival in reach, picks a perch about <c>aiDartLength</c> away (toward a
        /// locked rival, within reach of home), turns the hull onto it and darts once the Pistol has caught up (or after
        /// <c>aiWaitForPistol</c>). It acts through the same calls the Goby's buttons make.</summary>
        void GobyAI(float dt)
        {
            if (_darting) return;
            var lvl = Difficulty();
            if (!_lockTarget || !_lockIsVessel) AiLockNearestRival();
            if (!_aiHasDir && _t >= _aiDecideAt) { _aiDir = AiChooseDirection(lvl); _aiHasDir = true; }
            if (!_aiHasDir) return;
            float maxDeg = 140f * dt;
            Quaternion.FromToRotation(_gt.forward, _aiDir).ToAngleAxis(out float deg, out var axis);
            if (deg > 0.05f && axis.sqrMagnitude > 1e-8f && IsAuthority()) _status.VesselTransformer.ApplyRotation(Mathf.Min(deg, maxDeg), axis);
            bool pistolHere = _resting && _at == _perches.Count - 1;
            if (deg < 4f && DartReady && (pistolHere || _t - _aiLandedAt > Config.AiWaitForPistol))
            {
                FireDart(Config.AiDartLength);
                _aiDecideAt = _t + lvl.Late * (0.5f + _aiRng.Next());
            }
        }

        void AiLockNearestRival()
        {
            var players = Config.GameData ? Config.GameData.Players : null;
            if (players == null) return;
            float best = Config.LockRange; IVesselStatus pick = null;
            foreach (var player in players)
            {
                var v = EnemyStatus(player);
                if (v == null) continue;
                float d = Vector3.Distance(v.Transform.position, _gt.position);
                if (d < best) { best = d; pick = v; }
            }
            if (pick != null) { _lockTarget = pick.Transform; _lockVessel = pick; _lockIsVessel = true; }
        }

        Vector3 AiChooseDirection(PistolAI.Difficulty lvl)
        {
            Vector3 pos = _gt.position, bestDir = _gt.forward; float bestScore = float.MinValue, L = Config.AiDartLength;
            for (int k = 0; k < 12; k++)
            {
                Vector3 dir;
                if (k == 0 && _lockTarget) dir = (_lockTarget.position - pos).normalized;
                else if (k == 1 && (pos - _home).magnitude > Config.AiHomeRadius * 0.5f) dir = (_home - pos).normalized;
                else dir = Quaternion.AngleAxis((_aiRng.Next() - 0.5f) * 140f, _gt.up) * Quaternion.AngleAxis((_aiRng.Next() - 0.5f) * 90f, _gt.right) * _gt.forward;
                float go = L; bool hit = FirstOnLine(pos, dir, L * 1.4f, out float along);
                if (hit) go = along;
                float score = -Mathf.Abs(go - L) * (hit ? 0.6f : 1f) - Mathf.Max(0f, (pos + dir * go - _home).magnitude - Config.AiHomeRadius) * 2f;
                if (go < 60f) score -= 1000f;
                if (k == 0 && _lockTarget) score += 120f;
                score += _aiRng.Next() * 30f;
                if (score > bestScore) { bestScore = score; bestDir = dir; }
            }
            // a misjudged perch: the aim off by a few degrees (Easy / Medium)
            if (_aiRng.Next() < lvl.Mis * 4f) bestDir = Quaternion.AngleAxis((_aiRng.Next() - 0.5f) * 20f, _gt.up) * bestDir;
            return bestDir.normalized;
        }

        PistolAI.Difficulty Difficulty() => aiLevel switch
        {
            AIDifficulty.Easy => PistolAI.Difficulty.Easy,
            AIDifficulty.Medium => PistolAI.Difficulty.Medium,
            _ => PistolAI.Difficulty.Hard,
        };

        // ================================ the Pistol: perch to perch ================================

        /// <summary>The frame the Pistol waits in before the first dart: straight ahead of the Goby's nose.</summary>
        PistolPlane.Frame WaitingFrame()
        {
            Vector3 a = _perches[_perches.Count - 1].Pos;
            return PistolPlane.For(new PlaneSegment(a, a + _gt.forward * 100f, _gt.up), 1, Config.SlopeDegrees, default);
        }

        void StepPistol(float dt)
        {
            if (_resting) { Rest(dt); return; }
            var inp = _human == Half.Pistol ? HumanPistol() : AIPistol();
            var ev = PistolBodyMath.Step(_p, _f, _t, dt, inp, _body);
            _travelled += ev.Travelled;
            if (ev.FireSnap) FireRing(ev.SnapAim);
            if (_p.x >= _f.EndX - Config.ArriveDistance) Arrive(_dir > 0 ? _seg + 1 : _seg);
        }

        /// <summary>The Pistol reaches perch <paramref name="perch"/>: it rests there and builds its den, as many prisms as
        /// the distance it travelled since its last den and the pair's Mass make (GobyDen).</summary>
        void Arrive(int perch)
        {
            _resting = true; _at = Mathf.Clamp(perch, 0, _perches.Count - 1); _restT = 0f; _waitT = 0f; _restSpawned = 0;
            var den = _perches[_at];
            _restQueue = GobyDen.PrismsFor(_travelled, Config.DenUnitsPerPrism, Config.DenMassScale.EvaluateReplicated(_status), den.Placed, Config.DenMax);
            _travelled = 0f;
            _p.x = _f.EndX; _p.y = PistolPlane.Ground(_f, _p.x); _p.wpos = den.Pos; _p.grounded = true;
        }

        void Rest(float dt)
        {
            _restT += dt;
            var den = _perches[_at];
            _p.wpos = den.Pos;
            // the den grows over the rest, one prism at a time
            int due = Config.RestSeconds > 0f ? Mathf.CeilToInt(_restQueue * Mathf.Clamp01(_restT / Config.RestSeconds)) : _restQueue;
            while (_restSpawned < due) { LayDenPrism(den); _restSpawned++; }
            if (_restT < Config.RestSeconds) return;

            // the Pistol's choice at a perch: on toward the Goby, or back toward the older dens (Back; the AI goes back after
            // waiting at the head)
            if (_human == Half.Pistol) { if (_backPressed) { _backPressed = false; _travelDir = -_travelDir; } }
            else if (_at == _perches.Count - 1 && _at > 0) { _waitT += dt; if (_waitT > Config.AiBackAfter) { _travelDir = -1; _waitT = 0f; } }
            else if (_travelDir < 0 && _restT > Config.RestSeconds) _travelDir = 1;   // the AI turns for the head at the den it went back to

            if (_travelDir < 0 && _at == 0) _travelDir = 1;
            if (_travelDir > 0 && _at < _segments.Count) Ride(_at, 1);
            else if (_travelDir < 0 && _at > 0) Ride(_at - 1, -1);
            // else: at the head perch, waiting for the Goby's next dart
        }

        void Ride(int seg, int dir)
        {
            _seg = seg; _dir = dir; _resting = false;
            _f = PistolPlane.For(_segments[seg], dir, Config.SlopeDegrees, Config.Hills);
            // from a standstill: the Pistol rested at the perch, so it sets off at the ride's least speed and gathers it on
            // the way down, never carrying its arrival speed into the first hump and flying off the perch (Garrett,
            // 2026-10-22: "it should not launch from the perch each time")
            PistolBodyMath.PlaceAtStart(_p, _f, _body.pMinSpeed);
        }

        void LayDenPrism(Den den)
        {
            if (den.Placed >= Config.DenMax) return;
            var channel = _status?.VesselPrismController ? _status.VesselPrismController.PrismSpawnChannel : null;
            if (!channel) return;
            float spacing = GobyDen.Spacing(Config.DenBlockSize, Config.DenClearance);
            Vector3 local = GobyDen.Local(den.Placed, Config.DenFirstRadius, spacing);
            Quaternion basis = Quaternion.FromToRotation(Vector3.up, den.Up);
            Vector3 outward = basis * local;
            var kind = GobyDen.KindFor(den.Placed, (Abilities && Abilities.IsUpgradeActive(Element.Mass)), Config.DenShieldedShare, Config.DenDangerShare, Config.DenSuperShare);
            den.Placed++;
            if (!SafeLookRotation.TryGet(outward, den.Up, out var rot, this, logError: false)) rot = basis;
            var prism = BoostRingBuilder.LayOne(channel, den.Pos + outward, rot, Vector3.one * Config.DenBlockSize, kind,
                _status.Domain, _status.PlayerName, _status.PlayerName);
            if (prism) den.Prisms.Add(prism);
        }

        PistolBodyMath.Input AIPistol()
        {
            if (_t >= _scanAt) { _scanAt = _t + 0.1f; ScanForAI(); }
            return _ai.Think(_p, _f, _t, Difficulty(), Config.AI, LockedInView());
        }

        void ScanForAI()
        {
            _ai.Vis.Clear();
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;
            index.QuerySphere(_p.wpos, Mathf.Max(Config.AI.pLaunchRange, 200f) + 60f, _q);   // the Snap's reach ahead (PistolAI)
            for (int i = 0; i < _q.Count; i++)
            {
                var prism = _q[i];
                if (!Breakable(prism)) continue;
                Vector3 fr = _f.ToFrame(prism.transform.position);
                if (Mathf.Abs(fr.z) > Config.BodyHalfDepth * 2f) continue;
                _ai.Vis.Add(new PistolAI.Seen { X = fr.x, Y = fr.y, D = fr.z });
            }
            _q.Clear();
        }

        /// <summary>The Goby's locked RIVAL VESSEL is on the Pistol's screen: a ring fired now homes on it.</summary>
        public bool LockedInView()
        {
            if (!_lockIsVessel || !_lockTarget) return false;
            if (_cam && _cam.enabled)
            {
                Vector3 v = _cam.WorldToViewportPoint(_lockTarget.position);
                return v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
            }
            Vector3 fr = _f.ToFrame(_lockTarget.position);
            return Mathf.Abs(fr.x - _p.x) <= Config.ViewWidth * 0.5f && Mathf.Abs(fr.y - _p.y) <= Config.ViewWidth * 0.35f;
        }

        // ---- the Pistol's buttons on a phone (GobyPistolButtons): 0 ghost (held), 1 snap, 2 back. Pressed on the seat
        // directly: the seat's AI flies the hull meanwhile, and an InputStatus press would be dropped by its gate.
        void SetPistolButton(int button, bool down)
        {
            if (_goby == null || _human != Half.Pistol) return;
            if (button == 0) _touchGhost = down;
            else if (button == 1) _touchSnap = down;
            else if (button == 2 && down) _backPressed = true;
        }

        // hold to dive (let go on an upslope and the hill throws it, Tiny Wings); G / B ghost; Q / E / X / Y Snap; B key or
        // LB to head back at a perch; on a phone a finger on the Pistol's view dives. Only with aimedJump on: drag (or RT +
        // right stick, or F) aims a jump and letting go flings it (Garrett, 2026-10-22: "launching intentionally was wierd")
        PistolBodyMath.Input HumanPistol()
        {
            var inp = new PistolBodyMath.Input();
            var kb = Keyboard.current; var pad = Gamepad.current;
            float dive = 0f;
            if (_cam && ReadPointer(out var mp, out bool began, out bool held))
            {
                if (began) { _mouseDown = true; _aiming = false; _mouseFrom = mp; }
                if (_mouseDown)
                {
                    if (Config.AimedJump && (mp - _mouseFrom).magnitude > 12f) _aiming = true;
                    if (_aiming && PlanePoint(mp, out var w)) _mouseAim = new Vector2(w.x - _p.x, w.y - _p.y);
                    if (!_aiming) dive = 1f;
                    if (!held)
                    {
                        if (_aiming && _mouseAim.HasValue) { inp.HasLaunch = true; inp.Launch = _mouseAim.Value; }
                        _mouseDown = _aiming = false; _mouseAim = null;
                    }
                }
            }
            if (pad != null)
            {
                if (Config.AimedJump && pad.rightTrigger.ReadValue() > 0.5f) _padAim = pad.rightStick.ReadValue() * _body.pLaunchRange;
                else if (_padAim.HasValue) { if (_padAim.Value.magnitude > 10f) { inp.HasLaunch = true; inp.Launch = _padAim.Value; } _padAim = null; }
                if (pad.buttonSouth.isPressed) dive = 1f;
                dive = Mathf.Max(dive, pad.leftTrigger.ReadValue());
                inp.Ghost |= pad.buttonEast.isPressed;
                inp.Snap |= pad.buttonWest.isPressed || pad.buttonNorth.isPressed;
                if (pad.leftShoulder.wasPressedThisFrame) _backPressed = true;
            }
            if (kb != null)
            {
                if (kb.spaceKey.isPressed || kb.sKey.isPressed || kb.downArrowKey.isPressed) dive = 1f;
                if (Config.AimedJump && kb.fKey.wasPressedThisFrame && !inp.HasLaunch) { inp.HasLaunch = true; inp.Launch = new Vector2(0.7f * _body.pLaunchRange, 0.45f * _body.pLaunchRange); }
                inp.Ghost |= kb.gKey.isPressed;
                inp.Snap |= kb.qKey.isPressed || kb.eKey.isPressed;
                if (kb.bKey.wasPressedThisFrame) _backPressed = true;
            }
            inp.Ghost |= _touchGhost;
            inp.Snap |= _touchSnap;
            inp.Dive = dive;
            if (inp.Snap) inp.SnapAim = _mouseAim ?? _padAim ?? new Vector2(1f, 0f);
            return inp;
        }

        // The Pistol's pointer: the mouse, or the ONE finger that started on the Pistol's view (by touch id, so a second
        // finger on the Ghost button never steals the drag). A press on the small view (the touch swap's) or on a seat
        // button (FlightTouchClaims) is never the Pistol's.
        bool ReadPointer(out Vector2 at, out bool began, out bool held)
        {
            at = _pointerAt; began = held = false;
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touches = touchscreen.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var t = touches[i];
                    int id = t.touchId.ReadValue();
                    if (_fingerId >= 0 ? id != _fingerId : !t.press.wasPressedThisFrame) continue;
                    var p = t.position.ReadValue();
                    if (_fingerId < 0)
                    {
                        if (!OnPistolView(p)) continue;
                        _fingerId = id; began = true;
                    }
                    at = _pointerAt = p;
                    held = t.press.isPressed;
                    if (!held) _fingerId = -1;
                    return true;
                }
                if (_fingerId >= 0) { _fingerId = -1; return true; }   // the finger is gone: a release where it last was
            }
            var mouse = Mouse.current;
            if (mouse == null) return false;
            at = _pointerAt = mouse.position.ReadValue();
            held = mouse.leftButton.isPressed;
            began = mouse.leftButton.wasPressedThisFrame && OnPistolView(at);
            return true;
        }

        bool OnPistolView(Vector2 screen) =>
            _cam.pixelRect.Contains(screen) && !_pip.SmallViewContains(_cam, HumanInSeat, screen) && !FlightTouchClaims.Claims(screen);

        /// <summary>A screen point on the Pistol's plane, in its frame (x, y).</summary>
        bool PlanePoint(Vector2 screen, out Vector2 frame)
        {
            frame = default;
            var ray = _cam.ScreenPointToRay(screen);
            float den = Vector3.Dot(ray.direction, _f.M);
            if (Mathf.Abs(den) < 1e-5f) return false;
            float s = Vector3.Dot(_f.O - ray.origin, _f.M) / den;
            if (s <= 0f) return false;
            Vector3 fr = _f.ToFrame(ray.origin + ray.direction * s);
            frame = new Vector2(fr.x, fr.y);
            return true;
        }

        // ================================ the undertow, the body, the rings ================================

        /// <summary>Space: mass either side of the Pistol's plane is pulled into it - never danger, super-shielded, a
        /// lifeform's body or the pair's own prisms; shielded ones only with the Space upgrade.</summary>
        void Undertow(float dt)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;
            float reach = Mathf.Max(0f, Config.PullReach.EvaluateReplicated(_status)), window = Config.PullWindow;
            if (reach <= 0.5f) return;
            bool shielded = (Abilities && Abilities.IsUpgradeActive(Element.Space));
            index.QuerySphere(_p.wpos, Mathf.Sqrt(window * window + reach * reach), _q);
            int moved = 0;
            for (int i = 0; i < _q.Count && moved < Config.PullMaxPerFrame; i++)
            {
                var prism = _q[i];
                if (!Pullable(prism, shielded)) continue;
                var tr = prism.transform;
                Vector3 fr = _f.ToFrame(tr.position);
                if (Mathf.Abs(fr.x - _p.x) > window || Mathf.Abs(fr.z) > reach || Mathf.Abs(fr.z) < 0.5f) continue;
                float step = Mathf.Min(Mathf.Abs(fr.z), Config.PullSpeed * dt);
                tr.position -= _f.M * (Mathf.Sign(fr.z) * step);
                prism.NotifyPositionChanged();
                moved++;
            }
            _q.Clear();
        }

        bool Pullable(Prism prism, bool shieldedToo)
        {
            // a creature's or a plant's own body (a HealthPrism) is the ecosystem's to move, never the undertow's
            if (!Breakable(prism) || prism is HealthPrism || _held.Contains(prism) || prism.IsGrowing) return false;
            var props = prism.prismProperties;
            if (props.IsDangerous || props.IsSuperShielded) return false;
            return shieldedToo || !props.IsShielded;
        }

        /// <summary>A prism the Pistol may break: alive, not the pair's own (its ground, its dens, its perches).</summary>
        bool Breakable(Prism prism) => prism && !prism.destroyed && prism.prismProperties != null && prism.ownerID != _status.PlayerName;

        void BodyHits()
        {
            if (_resting || _t < _p.ghostUntil) return;
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;
            index.QuerySphere(_p.wpos, Config.BodyR + Config.BodyHalfDepth + 6f, _q);
            Vector3 vel = PistolVelocity();
            for (int i = 0; i < _q.Count; i++)
            {
                var prism = _q[i];
                if (!Breakable(prism) || prism.prismProperties.IsSuperShielded) continue;
                if (!PistolBodyMath.Touches(_p, _f.ToFrame(prism.transform.position), Config.BodyHalfDepth, Config.BodyR)) continue;
                if (prism.DeflectsSilenced(_status.PlayerName, vel.magnitude)) continue;
                prism.Damage(vel, _status.Domain, _status.PlayerName);
                if (_p.grounded) _p.v *= 0.97f; else _p.vx *= 0.97f;
            }
            _q.Clear();
        }

        Vector3 PistolVelocity() => _p.grounded ? _f.R * _p.v : _f.R * _p.vx + _f.Up * _p.vy;

        void FireRing(Vector2 aim)
        {
            Vector3 n = aim.sqrMagnitude > 1f ? (_f.R * aim.x + _f.Up * aim.y).normalized : _f.R;
            var run = new RingRun { Ring = new VortexRing(_p.wpos, n, _vs.pVortexLife) };
            if (LockedInView()) run.Target = _lockVessel;   // the Goby's lock, on the Pistol's screen: the ring homes on it
            _rings.Add(run);
        }

        void StepRings(float dt)
        {
            var index = PrismSpatialIndex.Instance;
            var players = Config.GameData ? Config.GameData.Players : null;
            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                var run = _rings[i]; var ring = run.Ring;
                if (run.Target != null && UnityLiveness.Alive(run.Target.Transform)) ring.Steer(run.Target.Transform.position, Config.HomingTurnRate * Mathf.Deg2Rad * dt);
                bool alive = ring.Advance(dt, _vs);
                if (index && index.IsAvailable && !ring.Full(_vs))
                {
                    index.QuerySphere(ring.C, _vs.pVortexR + _vs.pVortexCapture, _q);
                    foreach (var prism in _q)
                    {
                        if (ring.Full(_vs)) break;
                        if (!Breakable(prism) || _held.Contains(prism) || prism.IsGrowing || prism.prismProperties.IsSuperShielded) continue;
                        if (ring.TryCapture(run.Prisms.Count, prism.transform.position, _vs)) { run.Prisms.Add(prism); _held.Add(prism); }
                    }
                    _q.Clear();
                }
                for (int j = 0; j < ring.Caps.Count; j++)
                {
                    var prism = run.Prisms[ring.Caps[j].Id]; if (!prism || prism.destroyed) continue;
                    var tr = prism.transform;
                    tr.position = ring.Flow(j, dt, _vs);
                    tr.Rotate(ring.N, 4f * dt * Mathf.Rad2Deg, Space.World);
                    prism.NotifyPositionChanged();
                }
                // a rival hull the ring passes through is a scored hit, and the ring collapses on it
                if (players != null)
                    foreach (var player in players)
                    {
                        var victim = EnemyStatus(player);
                        if (victim == null) continue;
                        float r = _vs.pVortexR + Config.RingHitPad;
                        if ((victim.Transform.position - ring.C).sqrMagnitude > r * r) continue;
                        ScoreRingHit(victim, ring.N * _vs.pVortexSpeed);
                        alive = false;
                        break;
                    }
                if (alive) continue;
                foreach (var prism in run.Prisms)
                {
                    _held.Remove(prism);
                    if (_vs.pVortexCollapse >= 0.5f && prism && !prism.destroyed && !prism.DeflectsSilenced(_status.PlayerName, 40f))
                        prism.Damage((prism.transform.position - ring.C).normalized * 40f, _status.Domain, _status.PlayerName);
                }
                _rings.RemoveAt(i);
            }
        }

        /// <summary>A ring on a rival hull under the LOCKED PvP rule (petals only): admitted through the one gate every
        /// scored hit passes, it takes <c>ringPetals</c> (Charge) from each of the victim's elements, and raises the score.</summary>
        void ScoreRingHit(IVesselStatus victim, Vector3 velocity)
        {
            if (!ElementalTransfer.IsDecidedHere(_status)) return;
            var hitClass = Config.RingHitClass;
            if (!CombatHitDrain.TryAdmit(victim, _status, hitClass, Config.HitCooldown, ElementalDebuffSources.Other, out int superseded)) return;
            float petals = Mathf.Max(0f, Config.RingPetals.EvaluateReplicated(_status));
            ElementalTransfer.ApplyAllAuthoritative(ElementalTransferForm.Eject, victim, _status, petals * CombatHitDrain.NormalizedPerLevel,
                velocity, ElementalDebuffSources.Other);
            if (Config.OnCombatHitLanded)
                Config.OnCombatHitLanded.Raise(new CombatHitStats
                {
                    ShooterName = _status.PlayerName, VictimName = victim.PlayerName, HitClass = hitClass, SupersededRank = superseded,
                });
        }

        void ReleaseAllRings()
        {
            foreach (var run in _rings) foreach (var prism in run.Prisms) _held.Remove(prism);
            _rings.Clear(); _held.Clear();
        }

        // ================================ who flies what (the arena's swap) ================================

        void PollSwap()
        {
            // the arena's gesture on the arena's cooldown, or on a phone a double tap on the small view (every crew vessel's
            // touch swap)
            bool tapped = _pipTap.Poll(showSideView ? _pip.SmallView(_cam, HumanInSeat) : null);
            if (tapped && Time.unscaledTime >= _swapReadyAt) _swapReadyAt = Time.unscaledTime + PilotSwap.CooldownSeconds;
            else if (!CrewSeat.PollSwap(ref _swapReadyAt)) return;
            SetHuman(_human == Half.Goby ? Half.Pistol : Half.Goby);
        }

        /// <inheritdoc cref="IHullParkAware.ReturnHumanToHull"/>
        public void ReturnHumanToHull()
        {
            if (_human != Half.Pistol) return;
            SetHuman(Half.Goby);
        }

        void SetHuman(Half half)
        {
            // to the Pistol: the player's flight input paused, the seat's own Goby AI aiming and darting the hull; back: the
            // input given back. The player is captured on the way out: it outlives the hull (a swap re-homes it).
            if (half == Half.Pistol && _human != Half.Pistol)
            {
                _seatPlayer = _status?.Player;
                CrewSeat.SetFlightInputPaused(_seatPlayer, true);
            }
            else if (half == Half.Goby && _human == Half.Pistol)
            {
                CrewSeat.SetFlightInputPaused(_seatPlayer, false);
                _seatPlayer = null;
            }
            _human = half;
            // a press carried across the swap is not a press
            _ai?.ReleaseAll(); if (_p != null) { _p.prevGhost = _p.prevVortex = false; }
            _mouseDown = _aiming = false; _mouseAim = _padAim = null; _fingerId = -1;
            _dartHeld = _lockHeld = _prevLockHeld = _charging = false;
            _touchGhost = _touchSnap = _backPressed = false;
            _aiHasDir = false;
        }

        // each seat's buttons on a phone: built once the hull's HUD row is, painted for the seat the person is in
        void PaintButtons()
        {
            if (!_buttons.Built)
            {
                var input = _status?.Player?.InputStatus;
                if (!_buttons.TryBuild(_gt, Config, input, SetPistolButton)) return;
            }
            _buttons.Paint(HumanInSeat, DartReady, HasLock, _t >= _p.ghostAt, _t >= _p.vortexAt, HeadingBack);
        }

        // ================================ the views ================================

        void BuildView()
        {
            _view = new GameObject("PistolView");   // at the scene root: the hull's scale and pose are not the view's
            _cam = _view.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.Skybox; _cam.orthographic = false; _cam.nearClipPlane = 0.5f; _cam.farClipPlane = 6000f;
            _torus = BuildRingMesh(VortexRing.Tube, 24, 6);
            // lit and flat-shaded volumes (nothing in the world looks flat): the ring 0x9fe8ff, the lock a warm ring, the
            // Pistol a small orange body
            _ringMat = Lit(new Color32(159, 232, 255, 255), new Color32(18, 58, 68, 255), 0.65f);
            _lockMat = Lit(new Color32(255, 92, 108, 255), new Color32(80, 16, 22, 255), 0.5f);
            _bodyMat = Lit(new Color32(255, 138, 61, 255), new Color32(90, 34, 5, 255), 0.45f);
            _bodyGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _bodyGo.name = "Pistol";
            if (_bodyGo.TryGetComponent(out Collider col)) Destroy(col);
            if (_bodyGo.TryGetComponent(out MeshRenderer mr)) mr.sharedMaterial = _bodyMat;
            _render = new CrewViewRender(_cam, RenderLook);
            _camLive = false;
        }

        void LateUpdate()
        {
            if (_goby == null) return;
            // the Pistol's body: a capsule lying along its travel (its long axis on the frame's x), smaller while ghosting
            if (_bodyGo)
            {
                float r = Config.BodyR;
                _bodyGo.transform.SetPositionAndRotation(_p.wpos, Quaternion.LookRotation(_f.M, _f.R));
                _bodyGo.transform.localScale = new Vector3(r * 2f, r * 1.6f, r * 2f) * (_t < _p.ghostUntil ? 0.6f : 1f);
            }
            if (!_cam) return;
            if (!showSideView) { _cam.enabled = false; _pip.Restore(); Draw3D(); return; }
            _pip.Layout(_cam, HumanInSeat, Config.PipSize, Config.ViewMargin);
            // the side camera, Tiny Wings' (Garrett, 2026-10-22): square to the plane and looking along +M from its back (so
            // the frame's x runs left to right), BESIDE the Pistol with an off-axis lens that looks ahead. It frames the
            // ground near the bottom with the Pistol a third of the way in, and zooms out as the Pistol speeds up and flies
            // high so the ground below never leaves the screen (PistolPlane.TinyWingsView); the camera pulls back with the
            // zoom, so the lens never changes. Zooming out is quick and back in slow, so a landing never snaps the view.
            float aspect = Mathf.Max(0.2f, _cam.pixelHeight > 0 ? (float)_cam.pixelWidth / _cam.pixelHeight : 1f);
            float speed = _p.grounded ? _p.v : Mathf.Sqrt(_p.vx * _p.vx + _p.vy * _p.vy);
            Vector2 centre = PistolPlane.TinyWingsView(_f, _p.x, _p.y, speed, Config.ViewWidth, Config.ViewMaxWidth, Config.ViewSpeedWiden,
                Config.ViewPistolAt, Config.ViewGroundedAt, Config.ViewHeadroom, aspect, out float wantW);
            _camWidth = !_camLive ? wantW : Mathf.Lerp(_camWidth, wantW, Mathf.Clamp01(Time.deltaTime * (wantW > _camWidth ? 3f : 0.8f)));
            float W = _camWidth, H = W / aspect, D = Mathf.Max(20f, Config.ViewDistance) * W / Mathf.Max(1f, Config.ViewWidth);
            Vector3 wantPos = _f.ToWorld(_p.x, centre.y) - _f.M * D, wantWindow = _f.ToWorld(centre.x, centre.y);
            var wantRot = Quaternion.LookRotation(_f.M, _f.Up);
            float k = Mathf.Clamp01(Time.deltaTime * 4f), kp = Mathf.Clamp01(Time.deltaTime * 6f);
            _camRot = _camLive ? Quaternion.Slerp(_camRot, wantRot, k) : wantRot;
            _camPos = _camLive ? Vector3.Lerp(_camPos, wantPos, kp) : wantPos;
            _camWindow = _camLive ? Vector3.Lerp(_camWindow, wantWindow, kp) : wantWindow;
            _camLive = true;
            Vector3 w = Quaternion.Inverse(_camRot) * (_camWindow - _camPos);
            float near = _cam.nearClipPlane;
            PistolPlane.OffAxis(new Vector2(w.x, w.y), Mathf.Max(5f, w.z), W, H, near, out float fl, out float fr, out float fb, out float ft);
            _cam.projectionMatrix = Matrix4x4.Frustum(fl, fr, fb, ft, near, _cam.farClipPlane);
            _cam.transform.SetPositionAndRotation(_camPos, _camRot);
            Draw3D();
        }

        void Draw3D()
        {
            foreach (var run in _rings)
                Draw(_torus, Matrix4x4.TRS(run.Ring.C, Quaternion.LookRotation(run.Ring.N, Mathf.Abs(run.Ring.N.y) < 0.9f ? Vector3.up : Vector3.right), Vector3.one * _vs.pVortexR), _ringMat);
            // the lock is a fact in the world, so it is drawn there: a lit ring round the locked target, facing the Goby
            if (_lockTarget)
            {
                Vector3 to = _gt.position - _lockTarget.position;
                if (to.sqrMagnitude > 1e-4f) Draw(_torus, Matrix4x4.TRS(_lockTarget.position, Quaternion.LookRotation(to.normalized), Vector3.one * 9f), _lockMat);
            }
        }

        static void Draw(Mesh mesh, Matrix4x4 m, Material material)
        {
            if (!mesh || !material) return;
            var rp = new RenderParams(material) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
            Graphics.RenderMesh(rp, mesh, 0, m);
        }

        // ---- how the Pistol's camera sees the game's prisms, for its render only (CrewViewRender, the crew vessels' one
        // look): their depth look measured from the Pistol's plane, and the occlusion corridor aimed from this lens at the
        // Pistol, so mass in front of it dithers instead of hiding it
        CrewViewRender.Look RenderLook()
        {
            var look = new CrewViewRender.Look { PlanePoint = _p.wpos, PlaneNormal = _f.M, FullSpreadAt = Config.DepthFullSpreadAt };
            var corridor = PrismOcclusionCorridor.Config;
            if (_gt && corridor && corridor.Enabled)
            {
                float r = Mathf.Clamp(Config.BodyR, corridor.MinVesselRadius, corridor.MaxVesselRadius);
                look.Hull = _gt;
                look.CorridorTarget = _p.wpos;
                look.OuterRadius = r * corridor.OuterRadiusScale;
                look.InnerRadius = r * corridor.InnerRadiusScale;
                look.CoreAlpha = corridor.CoreAlpha;
                look.NearRadius = corridor.NearRadius(r);
            }
            return look;
        }

        /// <summary>A lit material for a world effect (nothing in the world looks flat, LOCKED 2026-10-18).</summary>
        static Material Lit(Color32 color, Color32 emission, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (!shader) shader = Shader.Find("Standard");
            var m = new Material(shader) { color = color, enableInstancing = true };
            if (m.HasProperty(s_baseColor)) m.SetColor(s_baseColor, color);
            if (m.HasProperty(s_smoothness)) m.SetFloat(s_smoothness, smoothness);
            if (m.HasProperty(s_metallic)) m.SetFloat(s_metallic, 0.1f);
            if (m.HasProperty(s_emission)) { m.EnableKeyword("_EMISSION"); m.SetColor(s_emission, emission); }
            return m;
        }

        static readonly int s_baseColor = Shader.PropertyToID("_BaseColor"), s_smoothness = Shader.PropertyToID("_Smoothness"),
                            s_metallic = Shader.PropertyToID("_Metallic"), s_emission = Shader.PropertyToID("_EmissionColor");

        static Mesh BuildRingMesh(float tube, int seg, int side)
        {
            var v = new Vector3[VortexRing.FacetVertexCount(seg, side)]; var n = new Vector3[v.Length]; var tri = new int[v.Length];
            VortexRing.Facets(tube, seg, side, v, n);
            for (int k = 0; k < tri.Length; k++) tri[k] = k;
            var m = new Mesh { name = "VortexRing", vertices = v, normals = n, triangles = tri };
            m.RecalculateBounds();
            return m;
        }

        // ================================ geometry (ShrikeRules on the prism's box) ================================

        static Vector3 HalfExtents(Prism prism) => prism.transform.lossyScale * 0.5f;

        static float BoxDistance(Prism prism, Vector3 world)
        {
            var t = prism.transform;
            return ShrikeRules.BoxDistance(Quaternion.Inverse(t.rotation) * (world - t.position), HalfExtents(prism));
        }

        static bool RayHits(Prism prism, Vector3 origin, Vector3 dir, float pad, float maxT, out float entry)
        {
            var t = prism.transform;
            var inv = Quaternion.Inverse(t.rotation);
            return ShrikeRules.TryRayBox(inv * (origin - t.position), inv * dir, HalfExtents(prism), pad, maxT, out entry);
        }

        void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_goby == null) return;
            var box = new Rect(10f, 10f, 520f, 96f);
            GUI.Box(box, string.Empty);
            GUILayout.BeginArea(new Rect(box.x + 8f, box.y + 6f, box.width - 16f, box.height - 12f));
            GUILayout.Label($"GOBY & PISTOL   you fly: {(_human == Half.Goby ? "GOBY" : "PISTOL")}   AI: {aiLevel}   swap: D-pad left/right, 1/2 or double tap the small view");
            GUILayout.Label($"Goby    perches {_perches.Count}   dart {(DartReady ? "ready" : "...")}{(_charging ? $" charging {DartCharge01:0.00}" : "")}   lock {(_lockTarget ? (_lockIsVessel ? "vessel" : "fauna") : "none")}");
            GUILayout.Label($"Pistol  {(_resting ? $"resting at perch {_at}" : $"riding {_seg} {(_dir > 0 ? "on" : "back")}")}   jump {_p.launch}   den prisms {DenPrisms}   heading {(_travelDir > 0 ? "on" : "back")}   rings {_rings.Count}");
            GUILayout.Label(_human == Half.Pistol
                ? "Pistol: hold Space / mouse / finger / A to dive; drag (or RT + right stick) and let go to jump; G / B ghost; Q E / X Y Snap; B key / LB back"
                : "Goby: aim the hull; hold Q / RT to charge a dart, let go to dart; F / LT lock");
            GUILayout.EndArea();
#endif
        }
    }
}
