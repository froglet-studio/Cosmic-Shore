using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using SVector3 = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tandava, redesigned as SHIVA'S CELL (Assets/_Scripts/Controller/Arcade/TANDAVA.md §0; the lab is
    /// Docs/Studios/ShivaLab). A voyage location: the squad goes into a barren cell, planets wheeling round it, to break
    /// a humongous Shiva in its middle (<see cref="ShivaFigure"/>). He sits inside three shells of danger prisms laid on
    /// every peer, and cannot be touched. Each form lights a circuit of switch rings (<see cref="RaceGateRing"/>, the Time
    /// games' ring) spiralling in to the lowest standing shell; any pilot threading the lit ring moves the squad on, and
    /// the last ring cracks the shell. He claps: a wave sweeps out at <see cref="ShivaDirectorSettings.WaveSpeed"/> that
    /// wipes every prism the squad laid, rips every element off a vessel it passes inside
    /// <see cref="ShivaDirectorSettings.ClapReach"/> (scattered as crystals), and withers the cell's flora; behind it the
    /// cell grows again (the form's own colony flora and crystals, its own colours) and the next form's swarm comes out of
    /// his hands - many-headed serpents that hold the rings ahead and lunge (the Tandava swarm, driven here as its
    /// <see cref="ISwarmDirector"/>), sharks, piranha and clawfish. NATARAJA's rings dance; the DEVOURING MOUTH opens on a
    /// black hole (<see cref="BlackHoleRegistry"/>). A drum runs per form: out, his dance ends the cell. The third shell
    /// broken, he comes apart in light.
    ///
    /// The SERVER runs the director (<see cref="ShivaDirectorCore"/>, pure C#, Tools/Build/swarm_core_harness mode
    /// `shiva`) and each serpent's mind (<see cref="ShivaSerpentBrain"/>), and replicates the seed (every peer replays the
    /// circuits from it), the form, the state, the lit ring, the clocks, the shells, his yaw and mouth, and every
    /// serpent's goal / pose / anchor. Everything that is a local object on each peer - the rings, the shells, the figure,
    /// the wave's effects, the flora, the crystals, the fauna, the black hole - each peer builds from those. Pilots are
    /// tested by the peer that simulates them (the GateRaceController pattern) and reported to the server.
    /// </summary>
    public class ShivaController : MultiplayerDomainGamesController, ISwarmDirector, IGoalSource
    {
        [Header("Shiva")]
        [Tooltip("Drag ShivaSettings.asset - the director, the forms, the swarm, the shield, the figure, the text.")]
        [SerializeField] ShivaSettingsSO settings;
        [Tooltip("Drag ShivaScoringRule.asset - the outcome-based rule.")]
        [SerializeField] ShivaScoringRuleSO rule;

        /// <summary>The live match, if any (the HUD arrow reads it).</summary>
        public static ShivaController Current { get; private set; }

        // the replicated state (server writes)
        readonly NetworkVariable<int> _seed = new(1);
        readonly NetworkVariable<int> _form = new(0);
        readonly NetworkVariable<int> _state = new((int)ShivaState.Play);
        readonly NetworkVariable<int> _ring = new(0);
        readonly NetworkVariable<int> _shells = new(0b111);
        readonly NetworkVariable<int> _outcome = new((int)ShivaOutcome.Running);
        readonly NetworkVariable<bool> _racing = new(false);
        readonly NetworkVariable<float> _clock = new(0f);
        readonly NetworkVariable<float> _formTime = new(0f);
        readonly NetworkVariable<float> _yaw = new(0f);
        readonly NetworkVariable<float> _mouth = new(0f);
        readonly NetworkVariable<float> _clapK = new(0f);
        readonly NetworkList<Vector3> _serpentGoal = new();
        readonly NetworkList<Vector3> _serpentAnchor = new();
        readonly NetworkList<int> _serpentPose = new();

        ShivaDirectorCore _core;                                   // server only
        readonly List<ShivaSerpentBrain> _brains = new();         // server: one per serpent slot
        // every peer
        ShivaFigure _figure;
        GameObject _root;
        RaceGateRing[] _rings;
        int _ringsForm = -1;
        ShivaRing[] _circuit;
        readonly List<Prism>[] _shellPrisms = { new(), new(), new() };
        bool _built, _finalResultsSent, _cellPrepared, _tinted;
        int _revision, _appliedShells = 0b111;
        float _danceT, _waveStartedAt = -1f, _lastWaveR, _flashUntil, _turnStartedAt = -1f;
        Vector3 _flashAt;
        readonly List<Prism> _waveVictims = new();
        readonly HashSet<VesselStatus> _waveStripped = new();
        readonly Dictionary<IPlayer, Vector3> _lastPilotPos = new();
        readonly Dictionary<SwarmFauna, int> _serpentSlot = new();
        readonly List<SwarmFauna> _serpents = new();
        readonly Queue<(Vector3 at, Vector3 heading, int plan)> _pendingSerpents = new();
        BlackHole _hole;
        readonly List<Vector3> _sample = new();
        readonly List<Vector3> _pilotPos = new(), _pilotVel = new();
        readonly Dictionary<IPlayer, Vector3> _pilotLast = new();

        protected override bool UseGolfRules => false;
        protected override bool UseSceneReloadForReplay => true;   // the flora, the fauna and the shells do not reset in place
        protected override bool HasEndGame => false;               // end-game runs through OnTurnEndedCustom -> SyncFinalScores_ClientRpc

        public ShivaOutcome Outcome => (ShivaOutcome)_outcome.Value;
        public ShivaState State => (ShivaState)_state.Value;
        public int Form => _form.Value;
        public int LitRing => _ring.Value;
        /// <summary>The lit ring's world position, for the HUD arrow (null between circuits).</summary>
        public Transform LitRingTransform => _rings != null && _ring.Value < _rings.Length && _rings[_ring.Value] ? _rings[_ring.Value].transform : null;
        string FormName(int k) => settings && k >= 0 && k < settings.Forms.Count ? settings.Forms[k].DisplayName : "";

        // ───────────────────────────────────────────────────────────── lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Current = this;
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            rule.ResetOutcome();
            _form.OnValueChanged += OnFormChanged;
            _state.OnValueChanged += OnStateChanged;
            _outcome.OnValueChanged += OnOutcomeChanged;
            gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
            if (IsServer) _seed.Value = (int)((uint)System.Environment.TickCount ^ (uint)GetInstanceID()) & 0x7fffffff;
            if (settings && settings.SwarmConfig) SwarmFauna.SetDirector(settings.SwarmConfig, this);
            else CSDebug.LogError("[Shiva] ShivaController has no settings / swarm config - re-run Tools/Build/author_shiva_assets.py.");
            GoalStack.Source = this;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) DisarmAutopilots();
            _form.OnValueChanged -= OnFormChanged;
            _state.OnValueChanged -= OnStateChanged;
            _outcome.OnValueChanged -= OnOutcomeChanged;
            gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
            if (settings && settings.SwarmConfig) SwarmFauna.ClearDirector(settings.SwarmConfig, this);
            if (_hole) _hole.BeginDespawn(0.5f);
            if (_root) Destroy(_root);
            if (ReferenceEquals(GoalStack.Source, this)) GoalStack.Source = null;
            if (Current == this) Current = null;
            base.OnNetworkDespawn();
        }

        bool TryGetCell(out Cell cell)
        {
            cell = null;
            var cells = Cell.ActiveCellsSnapshot;
            for (int i = 0; i < cells.Count && !cell; i++) cell = cells[i];
            return cell;
        }

        Vector3 Origin => TryGetCell(out var cell) ? cell.transform.position : Vector3.zero;

        // ───────────────────────────────────────────────────────────── every frame, every peer

        void Update()
        {
            if (!settings) return;
            if (!_cellPrepared && TryGetCell(out var cell) && cell.CytoplasmVisual) { CellVisualTint.For(cell).Prepare(); _cellPrepared = true; }
            if (!_built && TryGetCell(out _)) BuildWorld();
            if (!_built) return;

            if (IsServer && _racing.Value && _core != null && _core.Outcome == ShivaOutcome.Running) StepServer(Time.deltaTime);

            _danceT += Time.deltaTime;
            float formTime = IsServer && _core != null ? _core.FormTime : _formTime.Value;
            _figure.Draw(_form.Value, _clapK.Value, _danceT, _mouth.Value, _yaw.Value, Time.time);
            UpdateShells();
            UpdateRings(formTime);
            UpdateWave();
            UpdateMouth();
            if (gameData != null && gameData.IsTurnRunning && State == ShivaState.Play) DetectRingCrossings();
            if (Time.time < _flashUntil)
                PrismLit.PublishLight(GetInstanceID(), LitVolume.Sphere(_flashAt, settings.FlashRadius), settings.FlashStrength, Domains.Gold);
        }

        /// <summary>Every peer, once: the figure, the shield, the first cell (flora, crystals, colours).</summary>
        void BuildWorld()
        {
            _built = true;
            _root = new GameObject("Shiva");
            _root.transform.position = Origin;
            _figure = _root.AddComponent<ShivaFigure>();
            _figure.Initialize(settings, Origin);
            LayShells();
            GrowCell(0);
        }

        // ───────────────────────────────────────────────────────────── the hunt starts

        protected override void OnCountdownTimerEnded()
        {
            if (IsServer) ArmAutopilots();
            base.OnCountdownTimerEnded();
        }

        void HandleTurnStarted()
        {
            _turnStartedAt = Time.time;
            if (!IsServer) return;
            var squadAt = S(SquadCentroid() - Origin);
            _seed.Value = (int)ShivaDirectorCore.SeedFacing((uint)_seed.Value & 0x3fffffffu, Mathf.Atan2(squadAt.Z, squadAt.X));
            _core = new ShivaDirectorCore(settings.Director, (uint)_seed.Value, squadAt);
            _racing.Value = true;
            Mirror();
            Narrate(GameToastSituation.TandavaMatchStart, settings.StartLine);
            Release_ClientRpc(0);
        }

        Vector3 SquadCentroid()
        {
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (var p in gameData.Players) { var t = p?.Vessel?.Transform; if (t) { sum += t.position; n++; } }
            return n > 0 ? sum / n : Origin + Vector3.forward * 1000f;
        }

        // ───────────────────────────────────────────────────────────── the server's director

        void StepServer(float dt)
        {
            _core.Step(dt, S(SquadCentroid() - Origin));
            SensePilots(dt);
            for (int q = 0; q < _core.Events.Count; q++) Apply(_core.Events[q]);
            _core.Events.Clear();
            Mirror();
        }

        void Mirror()
        {
            _form.Value = _core.Form;
            _state.Value = (int)_core.State;
            _ring.Value = _core.RingIndex;
            _clock.Value = _core.Clock;
            _formTime.Value = _core.FormTime;
            _yaw.Value = _core.Yaw;
            _mouth.Value = _core.MouthOpen;
            _clapK.Value = _core.ClapK;
            int mask = 0; for (int k = 0; k < 3; k++) if (_core.ShellUp[k]) mask |= 1 << k;
            _shells.Value = mask;
        }

        void Apply(ShivaEvent e)
        {
            switch (e.Kind)
            {
                case ShivaEventKind.ShellCracked:
                    Narrate(GameToastSituation.TandavaHaloBroken, TemplateText.Fill(settings.ShellLine, "left", (2 - e.A).ToString()));
                    break;
                case ShivaEventKind.Clap:
                    Narrate(GameToastSituation.TandavaRising, settings.ClapLine);
                    break;
                case ShivaEventKind.Wave:
                    Wave_ClientRpc(e.A);
                    break;
                case ShivaEventKind.Regrow:
                    Regrow_ClientRpc(e.A);
                    break;
                case ShivaEventKind.Play:
                    if (e.A < settings.Forms.Count && !string.IsNullOrEmpty(settings.Forms[e.A].BeginLine))
                        Narrate(GameToastSituation.TandavaFormTaken, settings.Forms[e.A].BeginLine);
                    Release_ClientRpc(e.A);
                    break;
                case ShivaEventKind.Won:
                case ShivaEventKind.Lost:
                    rule.Publish(_core.Outcome, _core.Form + 1, FormName(_core.Form));
                    _outcome.Value = (int)_core.Outcome;   // the turn monitor sees the rule's outcome and ends the turn
                    break;
            }
        }

        void SensePilots(float dt)
        {
            _pilotPos.Clear(); _pilotVel.Clear();
            foreach (var p in gameData.Players)
            {
                var t = p?.Vessel?.Transform;
                if (!t) continue;
                Vector3 at = t.position;
                Vector3 vel = dt > 1e-3f && _pilotLast.TryGetValue(p, out var last) ? (at - last) / dt : Vector3.zero;
                if (vel.sqrMagnitude > settings.MaxPlausibleSpeed * settings.MaxPlausibleSpeed) vel = Vector3.zero;
                _pilotLast[p] = at;
                _pilotPos.Add(at - Origin); _pilotVel.Add(vel);
            }
        }

        // ───────────────────────────────────────────────────────────── the shield

        /// <summary>Every peer: three shells of danger prisms, a Fibonacci lattice each, laid at runtime with the vessels'
        /// own prism channel (the AstroLeagueArena precedent). A cage you see him through.</summary>
        void LayShells()
        {
            if (!settings.PrismChannel) { CSDebug.LogError("[Shiva] ShivaSettings has no PrismChannel - the shield is not laid."); return; }
            var origin = Origin;
            for (int k = 0; k < 3; k++)
            {
                float R = settings.Director.Shell(k);
                int n = Mathf.RoundToInt(4f * Mathf.PI * R * R / (settings.ShellSpacing * settings.ShellSpacing));
                for (int i = 0; i < n; i++)
                {
                    float y = 1f - 2f * (i + 0.5f) / n, rr = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y)), th = i * 2.39996f;
                    var d = new Vector3(rr * Mathf.Cos(th), y, rr * Mathf.Sin(th));
                    BoostRingBuilder.LayOne(settings.PrismChannel, origin + d * R, Quaternion.LookRotation(d), settings.ShellPrismScale,
                                            PrismKind.Danger, settings.ShellDomain, null, "Shiva", null, _shellPrisms[k]);
                }
            }
        }

        /// <summary>Every peer: a shell that has cracked comes down, outward, as debris.</summary>
        void UpdateShells()
        {
            int mask = _shells.Value;
            if (mask == _appliedShells) return;
            var origin = Origin;
            for (int k = 0; k < 3; k++)
            {
                bool was = (_appliedShells & (1 << k)) != 0, now = (mask & (1 << k)) != 0;
                if (!was || now) continue;
                foreach (var p in _shellPrisms[k])
                {
                    if (!p || p.destroyed) continue;
                    p.DeactivateShields();
                    p.Damage((p.transform.position - origin).normalized * 30f, Domains.Blue, "Shiva", devastate: true);
                }
                _shellPrisms[k].Clear();
                Flash(origin);
            }
            _appliedShells = mask;
        }

        // ───────────────────────────────────────────────────────────── the circuit

        void UpdateRings(float formTime)
        {
            var state = State;
            bool show = _racing.Value && state == ShivaState.Play && Outcome == ShivaOutcome.Running;
            if (!show) { RetireRings(); return; }
            if (_rings == null || _ringsForm != _form.Value) BuildRings(_form.Value);
            var origin = Origin;
            int lit = _ring.Value;
            for (int i = 0; i < _rings.Length; i++)
            {
                var r = _rings[i];
                if (!r) continue;
                if (i < lit) { r.Retire(0.6f); _rings[i] = null; continue; }
                if (_form.Value == 1)
                {
                    var c = ShivaDirectorCore.DanceAt(settings.Director, _circuit[i], formTime);
                    _circuit[i].Centre = c;
                }
                r.transform.position = origin + V(_circuit[i].Centre);
                r.SetIsNextForLocalPilot(i == lit);
            }
            if (_form.Value == 1)
            {
                ShivaDirectorCore.OrientRings(_circuit);
                for (int i = 0; i < _rings.Length; i++) if (_rings[i]) _rings[i].SetAxis(V(_circuit[i].Normal));
            }
        }

        void BuildRings(int form)
        {
            RetireRings();
            _circuit = ShivaDirectorCore.RingsForForm(settings.Director, (uint)_seed.Value, form);
            _ringsForm = form;
            _rings = new RaceGateRing[_circuit.Length];
            var theme = gameData ? gameData.ThemeManagerData : null;
            for (int i = 0; i < _circuit.Length; i++)
            {
                var go = new GameObject($"Shiva Ring {form + 1}.{i + 1:00}");
                go.transform.SetParent(_root.transform, true);
                var ring = go.AddComponent<RaceGateRing>();
                ring.Build(i, new RaceGate(Origin + V(_circuit[i].Centre), V(_circuit[i].Normal), settings.Director.Mouth), theme, 0.6f);
                _rings[i] = ring;
            }
            _lastPilotPos.Clear();
        }

        void RetireRings()
        {
            if (_rings == null) return;
            foreach (var r in _rings) if (r) r.Retire(0.6f);
            _rings = null;
            _ringsForm = -1;
        }

        /// <summary>Every peer tests only the vessels it simulates (the host owns the AIs) as swept segments against the
        /// lit ring, in the ring's own frame (a dancing ring moves while the vessel does), and reports to the server.</summary>
        void DetectRingCrossings()
        {
            if (_rings == null) return;
            int lit = _ring.Value;
            if (lit >= _rings.Length || !_rings[lit]) return;
            var ring = _rings[lit];
            float maxStep = settings.MaxPlausibleSpeed * Time.deltaTime * 2f + 5f;
            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsNetworkOwner) continue;
                var t = p.Vessel?.Transform;
                if (!t) { _lastPilotPos.Remove(p); continue; }
                Vector3 cur = t.position - ring.transform.position;   // ring-relative: the dance does not sweep past a vessel
                if (!_lastPilotPos.TryGetValue(p, out var prev)) { _lastPilotPos[p] = cur; continue; }
                _lastPilotPos[p] = cur;
                if ((cur - prev).sqrMagnitude > maxStep * maxStep) continue;
                if (!ring.CrossedMouth(ring.transform.position + prev, ring.transform.position + cur)) continue;
                if (IsServer) RingThreadedServer(lit, p);
                else ReportRing_ServerRpc(lit);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        void ReportRing_ServerRpc(int ring, ServerRpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            IPlayer pilot = null;
            foreach (var p in gameData.Players)
                if (p is Player np && np && np.OwnerClientId == sender && !np.IsInitializedAsAI) { pilot = p; break; }
            var t = pilot?.Vessel?.Transform;
            if (!t || _core == null || ring != _core.RingIndex || ring >= _core.Rings.Length) return;
            float reach = settings.Director.Mouth + 200f;
            if ((t.position - (Origin + V(_core.Rings[ring].Centre))).sqrMagnitude > reach * reach) return;   // plausibility
            RingThreadedServer(ring, pilot);
        }

        void RingThreadedServer(int ring, IPlayer pilot)
        {
            if (_core == null || ring != _core.RingIndex) return;
            if (!_core.ThreadLitRing(0)) return;
            if (pilot?.RoundStats != null) pilot.RoundStats.SwitchesThreaded++;
            for (int q = 0; q < _core.Events.Count; q++) Apply(_core.Events[q]);
            _core.Events.Clear();
            Mirror();
        }

        // ───────────────────────────────────────────────────────────── the clap: the wave, the strip, the collapse

        [ClientRpc]
        void Wave_ClientRpc(int form)
        {
            _waveStartedAt = Time.time;
            _lastWaveR = 0f;
            _waveStripped.Clear();
            // the squad's prisms the wave will take: every player-owned prism standing now (never the environment's,
            // never a super-shielded one, never a creature's body)
            _waveVictims.Clear();
            var all = new List<Prism>();
            PrismSpatialIndex.EnsureInstance().CopyLivePrisms(all);
            foreach (var p in all)
            {
                if (!p || p.destroyed || p.IsEnvironmentOwned || p is HealthPrism) continue;
                if (p.prismProperties is { IsSuperShielded: true }) continue;
                _waveVictims.Add(p);
            }
            var theme = gameData ? gameData.ThemeManagerData : null;
            var origin = Origin;
            TandavaGoldBurst.Burst(theme, origin, null, settings.WaveShards, settings.Director.WaveSpeed, settings.BurstScale);
            Flash(origin);
        }

        /// <summary>Every peer: as this peer's wave front passes them, the squad's prisms go, the vessels this peer
        /// simulates inside the reach lose every element (scattered as crystals), and the flora withers.</summary>
        void UpdateWave()
        {
            if (_waveStartedAt < 0f) return;
            var s = settings.Director;
            float r = s.WaveStartRadius + s.WaveSpeed * (Time.time - _waveStartedAt);
            float r0 = _lastWaveR;
            _lastWaveR = r;
            var origin = Origin;
            for (int i = _waveVictims.Count - 1; i >= 0; i--)
            {
                var p = _waveVictims[i];
                if (!p || p.destroyed) { _waveVictims.RemoveAt(i); continue; }
                var off = p.transform.position - origin;
                if (off.magnitude > r) continue;
                p.Damage(off.normalized * 20f, Domains.Blue, "Shiva", devastate: true);
                _waveVictims.RemoveAt(i);
            }
            foreach (var v in VesselStatus.Live)
            {
                if (!v || !v.IsNetworkOwner || _waveStripped.Contains(v)) continue;
                var off = v.ShipTransform.position - origin;
                float d = off.magnitude;
                if (d > r) continue;
                _waveStripped.Add(v);
                if (d < s.ClapReach) Strip(v, off.normalized);
            }
            var flora = FloraHeartRegistry.Live;
            for (int i = flora.Count - 1; i >= 0; i--)
            {
                var f = flora[i];
                if (!f || !f.HeartTransform) continue;
                float d = (f.HeartTransform.position - origin).magnitude;
                if (d >= r0 && d < r) f.KillReplicated();
            }
            if (r > s.CellRadius + 40f) { _waveStartedAt = -1f; _waveVictims.Clear(); }
        }

        void Strip(VesselStatus v, Vector3 outward)
        {
            var res = v.ResourceSystem;
            if (res == null) return;
            foreach (var e in new[] { Element.Charge, Element.Mass, Element.Space, Element.Time })
            {
                int petals = res.AccrueElementalLoss(e, res.TakeableLevel(e), ElementalDebuffSources.Other);
                if (petals > 0) ElementalCrystalEjector.Eject(v, e, petals, outward * settings.StripSpeed);
            }
        }

        // ───────────────────────────────────────────────────────────── the new cell, the swarm

        [ClientRpc]
        void Regrow_ClientRpc(int form) => GrowCell(form);

        /// <summary>Every peer: the form's flora (colony seeds that grow themselves), its crystals, its colours.</summary>
        void GrowCell(int form)
        {
            if (!settings || form < 0 || form >= settings.Forms.Count || !TryGetCell(out var cell)) return;
            var spec = settings.Forms[form];
            if (spec.Flora != null)
                foreach (var f in spec.Flora)
                    for (int i = 0; f.Config && i < f.Count; i++) CellLifeSpawnerBase.PlantFlora(cell, f.Config, null);
            ScatterCrystals(form, spec.Crystals);
            CellVisualTint.For(cell).TransitionTo(spec.Palette, settings.TransitionSeconds, settings.TransitionFlash);
            _tinted = true;
            _revision++;
            GoalStack.RefreshAll();
        }

        /// <summary>The form's crystals, at the same seeded points on every peer: through the band between the standing
        /// shell and the membrane.</summary>
        void ScatterCrystals(int form, int count)
        {
            var s = settings.Director;
            var rnd = new ShivaRandom((uint)_seed.Value * 31u + (uint)form * 7919u + 1u);
            float inner = s.Shell(Mathf.Min(2, form)) + 120f, outer = s.CellRadius - 140f;
            var origin = Origin;
            var elements = new[] { Element.Charge, Element.Mass, Element.Space, Element.Time };
            for (int i = 0; i < count; i++)
            {
                float rr = inner + rnd.Next() * (outer - inner), th = rnd.Next() * 6.283f, y = (rnd.Next() - 0.5f) * 1000f;
                var at = origin + new Vector3(rr * Mathf.Cos(th), y, rr * Mathf.Sin(th));
                ElementalCrystalEjector.ShedOntoSphere(at, 0.5f, at + Vector3.up * 0.5f, 0f, elements[(int)(rnd.Next() * 4f) % 4], 1, 0f, "");
            }
        }

        /// <summary>Every peer: the form's swarm comes out of his hands - the serpents (this controller directs them), the
        /// sharks, piranha and clawfish (their own minds).</summary>
        [ClientRpc]
        void Release_ClientRpc(int form)
        {
            if (!settings || form < 0 || form >= settings.Forms.Count || !TryGetCell(out var cell)) return;
            var spec = settings.Forms[form];
            var j = ShivaDirectorCore.Figure(form, 0f, 0f, 0f, _yaw.Value);
            var hands = new[] { V(j.HandUL), V(j.HandUR), V(j.HandLL), V(j.HandLR) };
            float clear = settings.Director.Shell(Mathf.Min(2, form)) + 70f;
            var origin = Origin;
            Vector3 Out(int k) => origin + hands[k % 4].normalized * clear;
            if (settings.SerpentSpecies && settings.SerpentSpecies.FaunaPrefab)
                for (int k = 0; k < spec.SerpentCount; k++)
                {
                    var at = Out(k);
                    _pendingSerpents.Enqueue((at, (at - origin).normalized, spec.SerpentPlanIndex));
                    CellLifeSpawnerBase.SpawnFaunaWithDomain(cell, settings.SerpentSpecies.FaunaPrefab, at, Domains.Blue, at, settings.SerpentSpecies);
                }
            var goal = LitRingTransform ? LitRingTransform.position : Out(0);
            if (spec.Fauna != null)
                foreach (var f in spec.Fauna)
                    for (int k = 0; f.Config && k < f.Count; k++) CellLifeSpawnerBase.SpawnFaunaBanded(cell, f.Config, Domains.Blue, goal, Out(k + 1));
            if (IsServer) Narrate(GameToastSituation.TandavaLunge, settings.SwarmLine);
        }

        // ───────────────────────────────────────────────────────────── ISwarmDirector: the serpents (every peer)

        bool ISwarmDirector.TryGetSeed(SwarmFauna swarm, out Vector3 position, out Vector3 heading)
        {
            if (!_serpentSlot.ContainsKey(swarm))
            {
                var (at, h, plan) = _pendingSerpents.Count > 0 ? _pendingSerpents.Dequeue() : (Origin + Vector3.forward * 900f, Vector3.forward, settings.Forms[0].SerpentPlanIndex);
                _serpentSlot[swarm] = _serpents.Count;
                _serpents.Add(swarm);
                _seedPlan[swarm] = plan;
                if (IsServer)
                {
                    _brains.Add(new ShivaSerpentBrain(_serpents.Count * 1.7f, 2f + _serpents.Count));
                    _serpentGoal.Add(at); _serpentAnchor.Add(at); _serpentPose.Add((int)ShivaSerpentPose.Guard);
                }
                position = at; heading = h;
                return true;
            }
            position = swarm.transform.position; heading = Vector3.forward;
            return true;
        }

        readonly Dictionary<SwarmFauna, int> _seedPlan = new();

        bool ISwarmDirector.TryGetSeedForm(SwarmFauna swarm, out int form)
        {
            form = _seedPlan.TryGetValue(swarm, out var p) ? p : settings.Forms[0].SerpentPlanIndex;
            return true;
        }

        bool ISwarmDirector.TryGetGoal(SwarmFauna swarm, out Vector3 goal)
        {
            int slot = _serpentSlot.TryGetValue(swarm, out var s) ? s : -1;
            goal = slot >= 0 && slot < _serpentGoal.Count ? _serpentGoal[slot] : swarm.transform.position;
            return true;
        }

        /// <summary>Every peer, every published tick: the server decides each serpent's goal and pose (its brain), every
        /// peer's body takes them; a client's body is nudged toward the server's.</summary>
        void ISwarmDirector.OnTickPublished(SwarmFauna swarm)
        {
            if (!_serpentSlot.TryGetValue(swarm, out int slot) || !settings) return;
            int planOf = _seedPlan.TryGetValue(swarm, out var p) ? p : settings.Forms[0].SerpentPlanIndex;
            if (IsServer && slot < _brains.Count && _core != null)
            {
                var ss = new ShivaSerpentSettings { LungeRange = settings.LungeRange, LungeCooldown = settings.LungeCooldown, RearSeconds = settings.RearSeconds,
                    StrikeSeconds = settings.StrikeSeconds, GuardOrbit = settings.GuardOrbit, Mouth = settings.Director.Mouth };
                var ps = new SVector3[_pilotPos.Count]; var pv = new SVector3[_pilotVel.Count];
                for (int i = 0; i < ps.Length; i++) { ps[i] = S(_pilotPos[i]); pv[i] = S(_pilotVel[i]); }
                var brain = _brains[slot];
                brain.Step(Time.deltaTime > 0f ? Mathf.Max(Time.deltaTime, 0.05f) : 0.05f, ss, S(swarm.AnchorWorld - Origin), _core.Rings, _core.RingIndex, slot, _serpents.Count,
                           ps, pv, _core.State == ShivaState.Play);
                _serpentGoal[slot] = Origin + V(brain.Goal);
                _serpentPose[slot] = (int)brain.Pose;
                _serpentAnchor[slot] = swarm.AnchorWorld;
            }
            if (slot >= _serpentPose.Count) return;
            var pose = (ShivaSerpentPose)_serpentPose[slot];
            var spec = SpecForPlan(planOf);
            int want = pose == ShivaSerpentPose.Rear ? spec.RearPlanIndex : pose == ShivaSerpentPose.Strike ? spec.StrikePlanIndex : spec.SerpentPlanIndex;
            if (swarm.FormIndex != want && want >= 0) swarm.RequestPose(want);
            bool strike = pose == ShivaSerpentPose.Strike;
            swarm.SetLevers(strike ? settings.StrikeCruise : settings.GuardCruise, strike ? settings.StrikeTurn : settings.GuardTurn, false);
            swarm.Goal = _serpentGoal[slot];
            if (!IsServer)
            {
                Vector3 gap = _serpentAnchor[slot] - swarm.AnchorWorld;
                float d = gap.magnitude;
                if (d > settings.NudgeThreshold && _serpentAnchor[slot] != Vector3.zero)
                    swarm.TryNudge(gap * (Mathf.Min(settings.MaxNudge, d * settings.NudgeFraction) / d));
            }
        }

        ShivaFormSpec SpecForPlan(int plan)
        {
            foreach (var f in settings.Forms) if (f.SerpentPlanIndex == plan) return f;
            return settings.Forms[0];
        }

        void ISwarmDirector.OnFormCommitted(SwarmFauna swarm, int fromForm, int toForm) { }

        // ───────────────────────────────────────────────────────────── the mouth, the end

        /// <summary>Every peer: the third form's black hole sits in his mouth, as strong as the jaw is open.</summary>
        void UpdateMouth()
        {
            float open = _mouth.Value;
            bool want = _form.Value == 2 && open > 0.05f && Outcome == ShivaOutcome.Running && settings.BlackHoleStrength > 0f;
            var j = ShivaDirectorCore.Figure(_form.Value, 0f, _danceT, open, _yaw.Value);
            Vector3 at = Origin + V(j.Mouth);
            if (want && !_hole && BlackHoleRegistry.CanSpawn(1)) _hole = BlackHoleRegistry.Spawn(at, settings.BlackHoleStrength * open);
            if (!_hole) return;
            if (!want) { _hole.BeginDespawn(1.5f); _hole = null; return; }
            _hole.transform.position = at;
            _hole.SetStrength(settings.BlackHoleStrength * open);
        }

        void OnFormChanged(int previous, int current) { _revision++; GoalStack.RefreshAll(); }
        void OnStateChanged(int previous, int current) => GoalStack.RefreshAll();

        void OnOutcomeChanged(int previous, int current)
        {
            var o = (ShivaOutcome)current;
            if (o == ShivaOutcome.Won && _figure)
            {
                _figure.SamplePositions(_sample, settings.WinShards);
                _figure.Shatter();
                var theme = gameData ? gameData.ThemeManagerData : null;
                TandavaGoldBurst.Burst(theme, Origin, _sample, settings.WinShards, settings.BurstSpeed, settings.BurstScale);
                _flashAt = Origin; _flashUntil = Time.time + settings.FlashSeconds;
                foreach (var list in _shellPrisms) list.Clear();
            }
            if (o != ShivaOutcome.Running) RestoreCell();
            GoalStack.RefreshAll();
        }

        void Flash(Vector3 at) { _flashAt = at; _flashUntil = Time.time + 1.2f; }

        void RestoreCell()
        {
            if (!_tinted || !settings || !TryGetCell(out var cell)) return;
            CellVisualTint.For(cell).Restore(settings.RestoreSeconds);
            _tinted = false;
        }

        void Narrate(GameToastSituation situation, string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            Narrate_ClientRpc((int)situation, line);
        }

        [ClientRpc]
        void Narrate_ClientRpc(int situation, FixedString512Bytes line) => GameToastAPI.Post((GameToastSituation)situation, Domains.Blue, line.ToString());

        // ───────────────────────────────────────────────────────────── the autopilots

        /// <summary>
        /// Every AI pilot flies the circuit (the lab's approach, Docs/Studios/ShivaLab/sim.js ai): onto the lit ring's axis
        /// a few turn radii out, then straight through it. Steering is <see cref="AIPilot.SetExternalTargetProvider"/> and
        /// nothing else, so it cannot leak into another mode.
        /// </summary>
        void ArmAutopilots()
        {
            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;
                var me = p;
                pilot.SetExternalTargetProvider(() =>
                {
                    var self = me.Vessel?.Transform;
                    var ring = LitRingTransform;
                    if (!self || !ring || _rings == null) return Origin;
                    var rg = _rings[_ring.Value];
                    Vector3 rel = self.position - ring.position, n = rg.Axis;
                    float side = Mathf.Sign(Vector3.Dot(rel, n)); if (side == 0f) side = 1f;
                    float onAxis = Vector3.Dot(rel.normalized, n * side), gate = 160f;
                    return onAxis > 0.8f || rel.magnitude < settings.Director.Mouth ? ring.position - n * (side * gate) : ring.position + n * (side * Mathf.Min(gate, rel.magnitude));
                });
            }
        }

        void DisarmAutopilots()
        {
            var players = gameData != null ? gameData.Players : null;
            if (players == null) return;
            foreach (var p in players) if (p != null && p.IsInitializedAsAI) p.Vessel?.VesselStatus?.AIPilot?.ClearExternalTargetProvider();
        }

        // ───────────────────────────────────────────────────────────── IGoalSource: the top-left goal stack

        int IGoalSource.Revision => _revision;

        bool IGoalSource.TryGetGoals(List<GoalEntry> goals)
        {
            if (!settings || settings.Forms.Count == 0) return false;
            int form = Mathf.Clamp(_form.Value, 0, settings.Forms.Count - 1);
            goals.Add(GoalEntry.Text(null, TemplateText.Fill(settings.FormLabel, "form", (form + 1).ToString(), "forms", ShivaDirectorCore.Forms.ToString()), FormName(form)));
            if (State == ShivaState.Play)
            {
                int n = settings.Director.RingCount(form);
                goals.Add(GoalEntry.Count(null, settings.RingsLabel, Mathf.Min(_ring.Value, n), n));
                goals.Add(GoalEntry.Text(null, settings.DrumLabel, Clock(_clock.Value)));
            }
            else if (Outcome == ShivaOutcome.Running) goals.Add(GoalEntry.Text(null, settings.RegrowLabel, ""));
            int up = 0; for (int k = 0; k < 3; k++) if ((_shells.Value & (1 << k)) != 0) up++;
            goals.Add(GoalEntry.Count(null, settings.ShellsLabel, up, 3));
            return true;
        }

        static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        // ───────────────────────────────────────────────────────────── game end (the Tandava / Brood Rush pattern)

        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;
            DisarmAutopilots();
            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0) return;
            var winner = rule.ResolveWinner(gameData);
            rule.AssignScores(gameData, winner, 0f);
            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            string winnerName = winner == Domains.Blue ? "" :
                gameData.RoundStatsList.Where(s => s.Domain == winner).OrderByDescending(s => s.Score).Select(s => s.Name).FirstOrDefault() ?? "";
            _finalResultsSent = true;
            bool won = Outcome == ShivaOutcome.Won;
            Narrate(won ? GameToastSituation.TandavaBroken : GameToastSituation.TandavaCompleted, won ? settings.WonLine : settings.LostLine);
            var stats = gameData.RoundStatsList;
            int count = stats.Count;
            var names = new FixedString64Bytes[count]; var scores = new float[count]; var domains = new int[count]; var rings = new int[count];
            for (int i = 0; i < count; i++) { names[i] = new FixedString64Bytes(stats[i].Name); scores[i] = stats[i].Score; domains[i] = (int)stats[i].Domain; rings[i] = stats[i].SwitchesThreaded; }
            SyncFinalScores_ClientRpc(names, scores, domains, rings, new FixedString64Bytes(winnerName), (int)winner, _outcome.Value, _form.Value);
        }

        protected override void SetupNewRound()
        {
            if (_finalResultsSent) return;
            base.SetupNewRound();
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(FixedString64Bytes[] names, float[] scores, int[] domains, int[] rings,
            FixedString64Bytes winnerName, int winnerDomain, int outcome, int formEnded)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null) { CSDebug.LogError($"[Shiva] Client could not match RoundStats for '{sName}'."); continue; }
                stat.Score = scores[i]; stat.Domain = (Domains)domains[i]; stat.SwitchesThreaded = rings[i];
            }
            rule.Publish((ShivaOutcome)outcome, formEnded + 1, FormName(formEnded));
            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;
            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            if (IsServer) DisarmAutopilots();
            _finalResultsSent = false;
            rule.ResetOutcome();
            foreach (var s in gameData.RoundStatsList) { s.SwitchesThreaded = 0; s.Score = 0f; }
            gameData.InvokeTurnStarted();
        }

        static SVector3 S(Vector3 v) => new(v.x, v.y, v.z);
        static Vector3 V(SVector3 v) => new(v.X, v.Y, v.Z);
    }
}
