using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The navigator's seat on a hull: the Kite &amp; Tail crew role (Crew Vessel Lab round 3, signed off 2026-10-17;
    /// <c>Docs/Studios/CREW_VESSEL_LAB.md</c>, <c>KITE_NAVIGATOR.md</c> beside this file). One player flies the hull; the
    /// navigator flies a kite on a 2D board that is the hull's x-z plane, with the world projected onto it. The kite's head
    /// pops hostile prisms and rams rival hulls; its tail is a lasso, and a loop round motes of ONE element fires that
    /// element's effect, <c>base × growth^(n − 1)</c> for n motes: Space blasts (AOEExplosion), Mass lays a cluster of our
    /// prisms, Time hands the pilot boost, Charge powers the kite so its next ram steals that many petals (a scored
    /// Strike, <c>CombatHitDrain.TryAdmit</c>). Motes are the navigator's own and are drawn only on its board.
    ///
    /// <para><b>Seats.</b> The swap is the arena's gesture (<see cref="PilotSwapGesture"/>: D-pad left / right, keys 1 / 2).
    /// It moves the local human between the hull and the kite; whichever seat is empty is flown by an AI squad mate - the
    /// hull's own <c>AIPilot</c>, or <see cref="KiteNavigatorAI"/>, which drives the kite only through an aim point, as a
    /// thumb does.</para>
    ///
    /// <para><b>Local only.</b> Everything here runs on the machine of the hull's local human: the board, the motes and
    /// the AI are per-viewer, and the effects are this machine's. Networking the seat is a follow-up (KITE_NAVIGATOR.md).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KiteNavigator : MonoBehaviour
    {
        internal struct Mote
        {
            public Vector3 World;
            public float X, Z, Y, VX, VZ;
            public bool Has;
            public int Element;   // 0 Charge, 1 Mass, 2 Space, 3 Time (Element - 1)
        }

        internal struct PrismShadow
        {
            public Prism Prism;
            public float X, Z, Y;
            public Domains Domain;
            public bool Hostile;
        }

        internal struct RivalShadow
        {
            public IVesselStatus Status;
            public int Id;
            public float X, Z, Y, VX, VZ;
            public bool Has, Seen;
            public Domains Domain;
        }

        internal enum FxKind { Loop = 0, Fizzle = 1, Pop = 2, Space = 3, Mass = 4, Time = 5, Charge = 6, Steal = 7 }

        internal struct Fx
        {
            public FxKind Kind;
            public float X, Z, T0, MaxScale;
            public KiteVec[] Loop;
        }

        // AOEExplosion.prefab's authored timing, used only to draw the blast's ring on the board
        internal const float BlastDelay = 0.2f, BlastDuration = 3f;
        const float FxLife = 1.4f;
        const float PrismHalfWidth = 1f;          // the Sparrow's 2 u trail prism
        const float RamReach = 4f;                // a hull's shadow is this much wider than a point
        const int MaxPrismShadows = 900;

        static KiteNavigator _active;

        /// <summary>The navigator riding the local hull, or null.</summary>
        public static KiteNavigator Active => _active;

        KiteNavigatorConfigSO _cfg;
        IPlayer _player;
        IVessel _vessel;
        IVesselStatus _status;
        KiteSettings _s;
        readonly KiteNavigatorCore _kite = new();
        KiteNavigatorAI _ai;
        System.Random _rng;

        Vector3 _r = Vector3.right, _f = Vector3.forward, _u = Vector3.up;
        bool _snapped;

        internal readonly List<Mote> Motes = new();
        internal readonly List<PrismShadow> Prisms = new();
        internal readonly List<RivalShadow> Rivals = new();
        internal readonly List<Fx> Effects = new();
        readonly List<Prism> _query = new();
        readonly List<KiteMote> _aiMotes = new();
        readonly List<KiteTarget> _aiPrisms = new();
        readonly List<KiteTarget> _aiRivals = new();
        readonly List<KiteVec> _loop = new();
        readonly Dictionary<IVesselStatus, float> _lastRam = new();
        readonly Queue<(Vector3 pos, Quaternion rot)> _massQueue = new();

        float _charge, _boostEnergy;
        bool _boostAsserted;
        bool _humanNavigates, _grabbed, _down;
        float _swapReadyAt;
        KiteVec _aim;
        Vector2 _thumbScreen;
        GameObject _canvas;
        KiteBoardGraphic _graphic;

        internal KiteNavigatorConfigSO Config => _cfg;
        internal KiteSettings Settings => _s;
        internal KiteNavigatorCore Kite => _kite;
        internal Domains OwnDomain => _status != null ? _status.Domain : Domains.Jade;
        internal float Charge => _charge;
        internal float BoostEnergy => _boostEnergy;
        internal bool Down => _down;
        internal KiteVec Aim => _aim;
        internal Vector2 ThumbScreen => _thumbScreen;
        internal Vector3 BoardRight => _r;
        internal Vector3 BoardForward => _f;

        /// <summary>True while the local human flies the kite and the hull's AI flies the hull.</summary>
        public bool HumanNavigates => _humanNavigates;

        /// <summary>
        /// Seat a navigator on the local player's hull (the FrogletTools play-mode menu calls this). The human keeps the
        /// hull; the AI takes the kite until the swap gesture moves them. Returns null, with a warning, when there is no
        /// local hull to ride.
        /// </summary>
        public static KiteNavigator AttachToLocalVessel(KiteNavigatorConfigSO config)
        {
            if (!config || !config.GameData)
            {
                CSDebug.LogWarning("[KiteNavigator] The config or its GameData is not assigned; nothing to attach.");
                return null;
            }

            var player = config.GameData.LocalPlayer;
            var vessel = player?.Vessel;
            if (vessel == null || vessel.VesselStatus == null || !vessel.Transform)
            {
                CSDebug.LogWarning("[KiteNavigator] No local hull is flying yet; start a match first.");
                return null;
            }

            var go = vessel.Transform.gameObject;
            if (!go.TryGetComponent(out KiteNavigator kite)) kite = go.AddComponent<KiteNavigator>();
            kite.Bind(config, player);
            return kite;
        }

        /// <summary>Take the navigator off whichever hull it rides, handing the hull back to its human.</summary>
        public static void DetachActive()
        {
            if (_active) Destroy(_active);
        }

        void Bind(KiteNavigatorConfigSO config, IPlayer player)
        {
            _cfg = config;
            _player = player;
            _vessel = player.Vessel;
            _status = _vessel.VesselStatus;
            _s = config.ToSettings();
            int seed = System.Environment.TickCount;
            _ai = new KiteNavigatorAI(seed) { Level = config.AISkill };
            _rng = new System.Random(seed ^ 0x5f3759df);
            _kite.Reset(new KiteVec(0f, _s.Anchor * _s.BoardRadius * 0.5f));
            _ai.Reset(_kite.Position);
            _aim = _kite.Position;
            _snapped = false;
            Motes.Clear(); Effects.Clear(); _massQueue.Clear(); _lastRam.Clear();
            _charge = 0f; _boostEnergy = 0f;
            if (_active && _active != this) Destroy(_active);
            _active = this;
            EnsureCanvas();
        }

        bool ShouldRun =>
            _cfg && _status != null && _vessel != null && UnityLiveness.Alive(_vessel.Transform) && _status.IsLocalUser;

        void OnDisable()
        {
            if (_humanNavigates) SetHumanNavigates(false);
            ReleaseBoost();
            if (_canvas) _canvas.SetActive(false);
        }

        void OnEnable()
        {
            if (_canvas) _canvas.SetActive(true);
        }

        void OnDestroy()
        {
            if (_canvas) Destroy(_canvas);
            if (_active == this) _active = null;
        }

        void Update()
        {
            if (!ShouldRun) return;
            float dt = Time.deltaTime;

            PollSwap();
            if (dt <= 0f) return;

            UpdateBoard(dt);
            StepMotes(dt);
            ShadowPrisms();
            ShadowRivals(dt);

            if (_humanNavigates) ReadThumb();
            else StepAI(dt);

            var prev = _kite.Move(_s, _aim.X, _aim.Z, _down, dt);
            HeadContacts(prev);
            if (_kite.AdvanceTail(_s, _loop)) CloseLoop(_loop);

            BurnBoost(dt);
            LayMassQueue();
            PruneEffects();
            if (_graphic) _graphic.SetVerticesDirty();
        }

        // ------------------------------------------------------------------ seats

        void PollSwap()
        {
            int dir = PilotSwapGesture.DirectionThisFrame();
            if (dir == 0 || Time.unscaledTime < _swapReadyAt) return;
            _swapReadyAt = Time.unscaledTime + PilotSwap.CooldownSeconds;
            SetHumanNavigates(!_humanNavigates);
        }

        void SetHumanNavigates(bool navigate)
        {
            if (_humanNavigates == navigate) return;
            _humanNavigates = navigate;
            _grabbed = false;
            _down = false;

            // The hull's own AI flies while the human navigates; its flight input is paused so the pointer and the
            // sticks belong to the board - the order the lava lamp and the mode preview hand a hull to its AI in
            // (MenuCrystalClickHandler, ModePreviewSession). Coming back restores both, in the opposite order.
            if (navigate)
            {
                SetFlightInputPaused(true);
                _vessel.ToggleAIPilot(true);
            }
            else
            {
                _vessel.ToggleAIPilot(false);
                SetFlightInputPaused(false);
                _ai.Reset(_kite.Position);
            }
        }

        void SetFlightInputPaused(bool paused)
        {
            if (_player == null) return;
            if (_player.InputController) _player.InputController.SetPause(paused);
            else if (_player.InputStatus != null) _player.InputStatus.Paused = paused;
        }

        // ------------------------------------------------------------------ the board

        void UpdateBoard(float dt)
        {
            var t = _vessel.Transform;
            Vector3 hf = t.forward, hu = t.up;
            if (!_snapped || _cfg.Frame == KiteBoardFrame.Locked)
            {
                _f = hf; _u = hu; _r = t.right;
                _snapped = true;
                return;
            }

            if (_cfg.Frame == KiteBoardFrame.Eased)
            {
                float a = 1f - Mathf.Exp(-_cfg.EaseRate * dt);
                _f += (hf - _f) * a;
                _u += (hu - _u) * a;
                _f.Normalize();
                _r = Vector3.Cross(_u, _f).normalized;
                _u = Vector3.Cross(_f, _r);
                return;
            }

            var fh = new Vector3(hf.x, 0f, hf.z);
            if (fh.magnitude > 0.15f) _f = fh.normalized;
            _u = Vector3.up;
            _r = Vector3.Cross(_u, _f).normalized;
            _f = Vector3.Cross(_r, _u);
        }

        Vector3 HullPosition => _vessel.Transform.position;

        /// <summary>A world point on the board plane.</summary>
        internal Vector3 BoardToWorld(float x, float z) => HullPosition + _r * x + _f * z;

        void Shadow(Vector3 world, out float x, out float z, out float y)
        {
            var d = world - HullPosition;
            x = Vector3.Dot(d, _r); z = Vector3.Dot(d, _f); y = Vector3.Dot(d, _u);
        }

        bool Touchable(float y) => Mathf.Abs(y) <= _cfg.ReachY;

        // ------------------------------------------------------------------ motes (navigator only)

        void StepMotes(float dt)
        {
            float R = _s.BoardRadius, A = _s.Anchor * R;
            bool board = _cfg.MoteHome == KiteMoteHome.Board;
            for (int i = Motes.Count - 1; i >= 0; i--)
            {
                var m = Motes[i];
                Shadow(m.World, out float x, out float z, out float y);
                if (m.Has) { m.VX += ((x - m.X) / dt - m.VX) * 0.5f; m.VZ += ((z - m.Z) / dt - m.VZ) * 0.5f; }
                m.X = x; m.Z = z; m.Y = y; m.Has = true;
                if (board) { m.World = BoardToWorld(x, z); m.Y = 0f; }   // flow with the world, stay in the plane
                float dz = m.Z - A;
                if (Mathf.Sqrt(m.X * m.X + dz * dz) >= R * 1.9f || Mathf.Abs(m.Y) >= Mathf.Max(_cfg.ReachY * 5f, 160f))
                    Motes.RemoveAt(i);
                else Motes[i] = m;
            }

            // a same-element cluster ahead of the board, so it flows in from the top
            for (int guard = 0; Motes.Count < _cfg.MoteCount && guard < 6; guard++)
            {
                int e = _rng.Next(4), n = Mathf.Max(1, Mathf.RoundToInt(_cfg.MoteCluster * (0.5f + (float)_rng.NextDouble())));
                float cx = ((float)_rng.NextDouble() * 2f - 1f) * R * 0.8f, cz = A + R * (0.75f + (float)_rng.NextDouble() * 0.5f);
                float cy = board ? 0f : Gauss() * _cfg.ReachY * 0.9f;
                for (int i = 0; i < n && Motes.Count < _cfg.MoteCount + 6; i++)
                {
                    float x = cx + Gauss() * _cfg.MoteSpread, z = cz + Gauss() * _cfg.MoteSpread, y = board ? 0f : cy + Gauss() * 6f;
                    var world = BoardToWorld(x, z) + _u * y;
                    Motes.Add(new Mote { World = world, X = x, Z = z, Y = y, Has = true, Element = e });
                }
            }
        }

        float Gauss()
        {
            double u = System.Math.Max(1e-9, _rng.NextDouble()), v = _rng.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u)) * System.Math.Cos(2.0 * System.Math.PI * v));
        }

        // ------------------------------------------------------------------ prisms and rivals

        void ShadowPrisms()
        {
            Prisms.Clear();
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;

            float R = _s.BoardRadius;
            var centre = BoardToWorld(0f, _s.Anchor * R);
            _query.Clear();
            index.QuerySphere(centre, Mathf.Sqrt(R * R + _cfg.ReachY * _cfg.ReachY * 4f), _query);

            var own = OwnDomain;
            foreach (var p in _query)
            {
                if (!p || p.destroyed) continue;
                Shadow(p.transform.position, out float x, out float z, out float y);
                if (!KiteNavigatorCore.InDisc(_s, x, z)) continue;
                var domain = p.Domain;
                Prisms.Add(new PrismShadow { Prism = p, X = x, Z = z, Y = y, Domain = domain, Hostile = domain != own });
                if (Prisms.Count >= MaxPrismShadows) break;
            }
        }

        void ShadowRivals(float dt)
        {
            for (int i = 0; i < Rivals.Count; i++) { var r = Rivals[i]; r.Seen = false; Rivals[i] = r; }

            var players = _cfg.GameData.Players;
            var own = OwnDomain;
            for (int p = 0; p < players.Count; p++)
            {
                var player = players[p];
                if (player == null || player == _player) continue;
                var vessel = player.Vessel;
                var status = vessel?.VesselStatus;
                if (status == null || !UnityLiveness.Alive(vessel.Transform) || status.Domain == own) continue;

                Shadow(vessel.Transform.position, out float x, out float z, out float y);
                int i = IndexOfRival(status);
                var r = i >= 0 ? Rivals[i] : new RivalShadow { Status = status, Id = vessel.Transform.GetInstanceID() };
                if (r.Has) { r.VX += ((x - r.X) / dt - r.VX) * 0.5f; r.VZ += ((z - r.Z) / dt - r.VZ) * 0.5f; }
                r.X = x; r.Z = z; r.Y = y; r.Has = true; r.Seen = true; r.Domain = status.Domain;
                if (i >= 0) Rivals[i] = r; else Rivals.Add(r);
            }

            for (int i = Rivals.Count - 1; i >= 0; i--) if (!Rivals[i].Seen) Rivals.RemoveAt(i);
        }

        int IndexOfRival(IVesselStatus status)
        {
            for (int i = 0; i < Rivals.Count; i++) if (Rivals[i].Status == status) return i;
            return -1;
        }

        bool OffRamCooldown(IVesselStatus victim) =>
            !_lastRam.TryGetValue(victim, out float t) || Time.time - t >= _cfg.StealCooldown;

        // ------------------------------------------------------------------ who moves the aim

        void ReadThumb()
        {
            bool pressed = TryReadPointer(out var screen, out bool touch);
            if (!pressed)
            {
                _grabbed = false;
                _down = false;
                return;
            }

            if (!_grabbed)
            {
                // a press must START on the board (or just outside its rim) to take the kite
                KiteBoardGraphic.BoardScreen(_cfg, out var c, out float rpx);
                if (Vector2.Distance(screen, c) > rpx * (1f + _cfg.BoardTouchMargin)) return;
                _grabbed = true;
            }

            _thumbScreen = screen;
            if (touch || _cfg.OffsetMouse)
            {
                float dpi = Screen.dpi > 0f ? Screen.dpi : 160f;
                screen.y += _cfg.ThumbOffsetMillimetres / 25.4f * dpi;
            }
            _aim = KiteBoardGraphic.ScreenToBoard(_cfg, _s, screen);
            _down = true;
        }

        static bool TryReadPointer(out Vector2 screen, out bool touch)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            {
                screen = touchscreen.primaryTouch.position.ReadValue();
                touch = true;
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                screen = mouse.position.ReadValue();
                touch = false;
                return true;
            }

            screen = default;
            touch = false;
            return false;
        }

        void StepAI(float dt)
        {
            _aiMotes.Clear();
            foreach (var m in Motes)
                _aiMotes.Add(new KiteMote { X = m.X, Z = m.Z, VX = m.VX, VZ = m.VZ, Element = m.Element, Touchable = Touchable(m.Y) });

            // prisms are fixed in the world, so on the board they drift against the hull's own velocity
            var hullVelocity = _status.Course * _status.Speed;
            float pvx = -Vector3.Dot(hullVelocity, _r), pvz = -Vector3.Dot(hullVelocity, _f);
            _aiPrisms.Clear();
            foreach (var p in Prisms)
            {
                if (!p.Hostile || !Touchable(p.Y) || p.Prism.prismProperties is { IsSuperShielded: true }) continue;
                _aiPrisms.Add(new KiteTarget { Id = p.Prism.GetInstanceID(), X = p.X, Z = p.Z, VX = pvx, VZ = pvz });
            }

            _aiRivals.Clear();
            bool anyRival = false;
            foreach (var r in Rivals)
            {
                if (!KiteNavigatorCore.InDisc(_s, r.X, r.Z)) continue;
                anyRival = true;
                if (!Touchable(r.Y) || !OffRamCooldown(r.Status)) continue;
                _aiRivals.Add(new KiteTarget { Id = r.Id, X = r.X, Z = r.Z, VX = r.VX, VZ = r.VZ });
            }

            _ai.Step(_s, _cfg.Growth, _kite, _charge, _aiMotes, _aiPrisms, _aiRivals, anyRival, dt);
            _aim = _ai.Aim;
            _down = _ai.Down;
        }

        // ------------------------------------------------------------------ the head: pop and ram

        void HeadContacts(KiteVec prev)
        {
            var at = _kite.Position;
            float hr = _cfg.KiteRadius + PrismHalfWidth, h2 = hr * hr;
            var impact = (_r * _kite.Velocity.X + _f * _kite.Velocity.Z);
            var own = OwnDomain;
            string name = _status.PlayerName;

            foreach (var p in Prisms)
            {
                if (!p.Hostile || !Touchable(p.Y) || !p.Prism || p.Prism.destroyed) continue;
                if (KiteNavigatorCore.DistanceToSegmentSq(p.X, p.Z, prev, at) > h2) continue;
                p.Prism.Damage(impact, own, name);
                AddFx(FxKind.Pop, p.X, p.Z);
                Play(_cfg.PrismPoppedSfx, p.Prism.transform.position);
            }

            float rr = (_cfg.KiteRadius + RamReach) * (_cfg.KiteRadius + RamReach);
            for (int i = 0; i < Rivals.Count; i++)
            {
                var r = Rivals[i];
                if (!Touchable(r.Y)) continue;
                if (KiteNavigatorCore.DistanceToSegmentSq(r.X, r.Z, prev, at) <= rr) Ram(r, impact);
            }
        }

        /// <summary>
        /// A charged ram is a scored Strike on the rival (PvP is petals only): one gate for the score and the petals, then
        /// whole petals moved one at a time from the rival's fullest element, as many as the kite holds power for.
        /// </summary>
        void Ram(RivalShadow rival, Vector3 impact)
        {
            var victim = rival.Status;
            if (_charge < 1f || !OffRamCooldown(victim)) return;
            if (!ElementalTransfer.IsDecidedHere(_status)) return;
            if (!CombatHitDrain.TryAdmit(victim, _status, CombatHitClass.Strike, _cfg.StealCooldown,
                                         ElementalDebuffSources.VesselContact, out int supersededRank))
                return;
            _lastRam[victim] = Time.time;

            int want = Mathf.FloorToInt(_charge), got = 0;
            var rs = victim.ResourceSystem;
            while (want > 0 && rs != null)
            {
                var element = Element.None; int most = 0;
                for (var e = Element.Charge; e <= Element.Time; e++)
                {
                    int level = rs.GetLevel(e);
                    if (level >= 1 && level > most) { most = level; element = e; }
                }
                if (element == Element.None) break;
                int moved = ElementalTransfer.ApplyAuthoritative(ElementalTransferForm.Steal, victim, _status,
                                                                 ElementalTransfer.MaskOf(element), CombatHitDrain.NormalizedPerLevel,
                                                                 impact, ElementalDebuffSources.VesselContact);
                if (moved <= 0) break;
                got++; want--;
            }
            _charge = Mathf.Max(0f, _charge - got);

            _cfg.OnCombatHitLanded.Raise(new CombatHitStats
            {
                ShooterName = _status.PlayerName,
                VictimName = victim.PlayerName,
                HitClass = CombatHitClass.Strike,
                SupersededRank = supersededRank,
            });
            AddFx(FxKind.Steal, rival.X, rival.Z);
            Play(_cfg.StealSfx, BoardToWorld(rival.X, rival.Z));
        }

        // ------------------------------------------------------------------ the tail: loops and their effects

        void CloseLoop(List<KiteVec> poly)
        {
            var centre = KiteNavigatorCore.Centroid(poly);
            Effects.Add(new Fx { Kind = FxKind.Loop, X = centre.X, Z = centre.Z, T0 = Time.time, Loop = poly.ToArray() });

            int charge = 0, mass = 0, space = 0, time = 0, inside = 0;
            for (int i = Motes.Count - 1; i >= 0; i--)
            {
                var m = Motes[i];
                if (!Touchable(m.Y) || !KiteNavigatorCore.PointInPolygon(m.X, m.Z, poly)) continue;
                switch (m.Element) { case 0: charge++; break; case 1: mass++; break; case 2: space++; break; default: time++; break; }
                Motes.RemoveAt(i);
                inside++;
            }
            if (inside == 0) return;

            var at = BoardToWorld(centre.X, centre.Z);
            if (!KiteNavigatorCore.ResolveLoop(charge, mass, space, time, _cfg.MixedLoops, out int element, out int n))
            {
                AddFx(FxKind.Fizzle, centre.X, centre.Z);
                Play(_cfg.LoopFizzledSfx, at);
                return;
            }

            Play(_cfg.LoopClosedSfx, at);
            switch (element)
            {
                case 2: FireSpace(n, centre, at); break;
                case 1: FireMass(n, centre, at); break;
                case 3: FireTime(n, centre, at); break;
                default: FireCharge(n, centre, at); break;
            }
        }

        // SPACE: an AOEExplosion at the loop's centre, on the plane, sparing our own domain
        void FireSpace(int n, KiteVec c, Vector3 at)
        {
            float maxScale = KiteNavigatorCore.Grow(_cfg.SpaceMaxScale, n, _cfg.Growth, _cfg.SpaceMaxScaleCap);
            Effects.Add(new Fx { Kind = FxKind.Space, X = c.X, Z = c.Z, T0 = Time.time, MaxScale = maxScale });

            var prefabs = _cfg.SpaceExplosions;
            if (prefabs is not { Length: > 0 }) return;
            var impactor = _vessel.Transform.GetComponentInChildren<VesselImpactor>();
            var init = new AOEExplosion.InitializeStruct
            {
                OwnDomain = _status.Domain,
                Vessel = _vessel,
                AnnonymousExplosion = false,
                OverrideMaterial = _status.AOEExplosionMaterial,
                MaxScale = maxScale,
                SpawnPosition = at,
                SpawnRotation = Quaternion.LookRotation(_f, _u),
                AffectSelfOverride = false,
            };
            ExplosionHelper.CreateExplosion(prefabs, init, impactor ? impactor.DIContainer : null);
        }

        // MASS: a cluster of our domain's prisms at the loop's centre, laid over a few frames
        void FireMass(int n, KiteVec c, Vector3 at)
        {
            int count = Mathf.RoundToInt(KiteNavigatorCore.Grow(_cfg.MassPrisms, n, _cfg.Growth, _cfg.MassPrismCap));
            float rad = Mathf.Max(4f, 2.6f * Mathf.Pow(count, 1f / 3f) * (_cfg.MassPrismScale.z / 5f));
            for (int i = 0; i < count; i++)
            {
                var dir = RandomUnit();
                float d = rad * Mathf.Pow((float)_rng.NextDouble(), 1f / 3f);
                _massQueue.Enqueue((at + dir * d, Quaternion.LookRotation(RandomUnit(), RandomUnit())));
            }
            AddFx(FxKind.Mass, c.X, c.Z);
            Play(_cfg.MassClusterSfx, at);
        }

        Vector3 RandomUnit()
        {
            var v = new Vector3(Gauss(), Gauss(), Gauss());
            return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.up;
        }

        void LayMassQueue()
        {
            if (_massQueue.Count == 0) return;
            var spawner = _status.VesselPrismController;
            var channel = spawner ? spawner.PrismSpawnChannel : null;
            if (!channel) { _massQueue.Clear(); return; }

            var scale = _cfg.MassPrismScale;
            for (int i = 0; i < _cfg.MassPrismsPerFrame && _massQueue.Count > 0; i++)
            {
                var (pos, rot) = _massQueue.Dequeue();
                var ret = channel.RaiseEvent(new PrismEventData
                {
                    ownDomain = _status.Domain,
                    Rotation = rot,
                    SpawnPosition = pos,
                    Scale = scale,
                    PrismType = spawner.SpawnPrismType,
                });
                if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism)) continue;

                prism.TargetScale = scale;
                prism.ownerID = _status.PlayerName;
                prism.ChangeTeam(_status.Domain);
                prism.Initialize(_status.PlayerName);
                // a stated size: admit it past the pool's scale window AFTER Initialize (whose reset re-clamps it)
                prism.AdmitTargetScale(scale);
                prism.TargetScale = scale;
            }
        }

        // TIME: boost energy for the pilot, burned by the hull at once
        void FireTime(int n, KiteVec c, Vector3 at)
        {
            float s = KiteNavigatorCore.Grow(_cfg.TimeBoostSeconds, n, _cfg.Growth, _cfg.TimeBoostCap);
            _boostEnergy = Mathf.Min(_cfg.TimeBoostCap, _boostEnergy + s);
            AddFx(FxKind.Time, c.X, c.Z);
            Play(_cfg.TimeBoostSfx, at);
        }

        void BurnBoost(float dt)
        {
            if (_boostEnergy <= 0f) { ReleaseBoost(); return; }
            _boostEnergy = Mathf.Max(0f, _boostEnergy - dt);
            _status.IsBoosting = true;   // what a boost executor writes; the hull's own boostMultiplier applies
            _boostAsserted = true;
        }

        void ReleaseBoost()
        {
            if (!_boostAsserted) return;
            _boostAsserted = false;
            if (_status != null) _status.IsBoosting = false;
        }

        // CHARGE: kite power, spent by the next ram on a rival
        void FireCharge(int n, KiteVec c, Vector3 at)
        {
            float add = KiteNavigatorCore.Grow(_cfg.ChargePetals, n, _cfg.Growth, _cfg.ChargeCap);
            _charge = Mathf.Min(_cfg.ChargeCap, _charge + add);
            AddFx(FxKind.Charge, c.X, c.Z);
            Play(_cfg.ChargeSfx, at);
        }

        // ------------------------------------------------------------------ feedback

        void AddFx(FxKind kind, float x, float z)
        {
            if (Effects.Count < 64) Effects.Add(new Fx { Kind = kind, X = x, Z = z, T0 = Time.time });
        }

        void PruneEffects()
        {
            float now = Time.time;
            for (int i = Effects.Count - 1; i >= 0; i--)
            {
                var fx = Effects[i];
                float life = fx.Kind == FxKind.Space ? BlastDelay + BlastDuration + 0.6f : FxLife;
                if (now - fx.T0 > life) Effects.RemoveAt(i);
            }
        }

        static void Play(EventReference evt, Vector3 at)
        {
            if (evt.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(evt, at);
        }

        internal Color DomainColor(Domains domain)
        {
            var colorSet = _cfg.GameData.ThemeManagerData ? _cfg.GameData.ThemeManagerData.ColorSet : null;
            return colorSet ? colorSet.GetDomainSignalColor(domain) : Color.white;
        }

        void EnsureCanvas()
        {
            if (_canvas) return;
            _canvas = new GameObject("KiteNavigatorBoard", typeof(RectTransform), typeof(Canvas));
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;

            var board = new GameObject("Board", typeof(RectTransform), typeof(CanvasRenderer), typeof(KiteBoardGraphic));
            var rt = (RectTransform)board.transform;
            rt.SetParent(_canvas.transform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.pivot = Vector2.zero;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _graphic = board.GetComponent<KiteBoardGraphic>();
            _graphic.raycastTarget = false;
            _graphic.Bind(this);
        }
    }
}
