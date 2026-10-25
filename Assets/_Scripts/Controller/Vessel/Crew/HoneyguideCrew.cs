using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Honeyguide and Ratel: two seats, one hull (Honeyguide Lab round 6, <c>Docs/Studios/HONEYGUIDE_LAB.md</c>;
    /// <c>HONEYGUIDE.md</c> beside this file). The <b>Honeyguide</b> flies the hull (the Sparrow's airframe) with two triggers:
    /// RT Peck and LT Flit, each taken over for a while by the upgrade cards (<c>HoneyguideCrew.Triggers.cs</c>). The
    /// <b>Ratel</b> plays the <b>Dynamo</b>, the hex puzzle in <see cref="DynamoCore"/>: a made pattern sends one of its prisms,
    /// drawn at random, to the battery, where its domain is a <b>slip charge</b>, and fires that tabletop card's own effect.
    /// The <b>Slip</b>: touch a 1-D trail of a domain the battery holds with the nose within <c>aimCone</c> of the prism's long
    /// axis, and the hull rides it to the end of the trail it points at (<c>HoneyguideCrew.Zip.cs</c>).
    ///
    /// <para><b>Seats.</b> The swap is the arena's gesture (<see cref="PilotSwapGesture"/>: D-pad left / right, the left / right arrow keys) or
    /// a double tap on the small board. Whichever seat is empty is played by an AI: the hull's own <c>AIPilot</c>, or the
    /// Ratel AI (<see cref="DynamoCore.AIPlan"/>, input-only, the lab's own planner).</para>
    ///
    /// <para><b>Two people.</b> The Dynamo, the AI and every effect run on the machine that flies the hull. A person on
    /// another machine can play the Ratel while someone else flies the Honeyguide (<see cref="CrewStations"/>: the swap
    /// gesture steps them into the free seat): their board is a copy, their moves travel to the hull's machine
    /// (<see cref="ICrewRelay"/>), which applies them to the real Dynamo and sends its state back - so the Ratel AI
    /// stands down, and nothing the board fires needs relaying.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class HoneyguideCrew : MonoBehaviour, IHullParkAware, ICrewStations, ICrewRelay
    {
        [Header("Config")]
        [Tooltip("Every number, card and word of the Honeyguide and Ratel (HoneyguideConfig.asset, written from the lab by " +
                 "Tools/Build/author_honeyguide_assets.py). The Honeyguide prefab wires it.")]
        [SerializeField] HoneyguideConfigSO config;

        static HoneyguideCrew _active;

        /// <summary>The crew riding the local hull, or null.</summary>
        public static HoneyguideCrew Active => _active;

        HoneyguideConfigSO _cfg;
        IPlayer _player;
        IVessel _vessel;
        IVesselStatus _status;
        DynamoSettings _s;
        DynamoCore _core;
        readonly DynamoCore.AIMemo _memo = new();
        System.Func<double> _aiDice;
        float _think;
        bool _humanEngineers, _weTookHull, _aiCrew;
        // _rider: the person on this machine plays the Ratel of a hull another machine flies (the board is a copy);
        // _remoteRatel: on the hull's machine, a person on another machine plays the Ratel
        bool _rider, _remoteRatel;
        float _stateSendAt;
        readonly List<int> _stateScratch = new();
        readonly int[] _inputScratch = new int[6];
        // the lowest preset seats the person on the Ratel at the start of this bind (CrewSingleView), still to be done
        bool _startSeatPending;
        float _swapReadyAt;
        HoneyguideDynamoView _view;
        VesselPrismController _laidBy;
        readonly List<DynamoEvent> _events = new();

        // match-clock stamps the view animates from (unscaled-free: Time.time)
        internal float SpinAt { get; private set; } = -99f;
        internal float PortsAt { get; private set; } = -99f;
        internal float AbilityAt { get; private set; } = -99f;
        internal readonly float[] RetiredAt = { -99f, -99f, -99f };

        /// <summary>The last pattern made, for the board's juice: when, which card, the cells it matched, and the prism it
        /// sent to the battery - the cell it left, its colour and the battery slot it landed in (-1 if none held it).</summary>
        internal sealed class MadePattern
        {
            public float At = -99f;
            public int Slot = -1, Pid = -1, Cell = -1, Colour, BatterySlot = -1;
            public readonly List<int> Cells = new();
        }

        internal readonly MadePattern LastMade = new();
        /// <summary>The line above the cards naming what the last pattern made does (madeLineTemplate), and whether that
        /// effect works on the Dynamo itself (drawn lime).</summary>
        internal string MadeLine { get; private set; } = "";
        internal bool MadeLineDynamo { get; private set; }

        internal HoneyguideConfigSO Config => _cfg;
        internal DynamoSettings Settings => _s;
        internal DynamoCore Core => _core;
        /// <summary>True while the local human plays the Ratel and the hull's AI flies.</summary>
        public bool HumanEngineers => _humanEngineers;
        /// <summary>True when both seats are AI (a hull flown by its AIPilot on this machine).</summary>
        public bool IsAICrew => _aiCrew;
        /// <summary>The battery the two seats share: slip charges, colours 1 Ruby, 2 Jade, 3 Gold, oldest first.</summary>
        public IReadOnlyList<int> Battery => _core != null ? _core.Battery : System.Array.Empty<int>();

        // ------------------------------------------------------------------ binding

        // The local pilot's own hull, flown by a human - or handed to the AI by this component's own swap - or an AI crew: a
        // hull that is not the local pilot's, flown by its AIPilot on THIS machine (the host owns every AI). The Wrasse's rule.
        // Two more (CrewStations): the person here rides this hull's Ratel seat (a rider: the board is a copy), or this is
        // their own hull, parked on its AI while they ride another - an AI crew, as on any hull an AI flies.
        bool ShouldRun(out IVessel vessel, out bool aiCrew, out bool rider)
        {
            vessel = null;
            aiCrew = rider = false;
            if (!config || !TryGetComponent(out vessel)) return false;
            var status = vessel.VesselStatus;
            if (CrewStations.LocalRiderSeat(this) == 1) { rider = true; return true; }
            aiCrew = true;
            if (CrewStations.IsParkedHere(vessel)) return config.AICrewEngineers;
            aiCrew = false;
            if (VesselVisionShading.LocalVessel == transform)
                return _weTookHull || status == null || !status.AutoPilotEnabled;
            aiCrew = true;
            return config.AICrewEngineers && status != null && status.AutoPilotEnabled
                   && status.Player != null && status.Player.IsNetworkOwner && !status.Player.IsLocalUser;
        }

        void Bind(IVessel vessel)
        {
            _cfg = config;
            _vessel = vessel;
            _status = vessel.VesselStatus;
            _player = _status?.Player;
            _s = _cfg.ToSettings();
            int seed = System.Environment.TickCount;
            _core = new DynamoCore(_s, seed);
            _core.Events.Clear();
            _aiDice = DynamoCore.Mulberry32(seed ^ 0x51ed27);
            _memo.Key = "";
            _think = 0f;
            _humanEngineers = _weTookHull = _remoteRatel = false;
            _startSeatPending = !_aiCrew && !_rider;
            _stateSendAt = 0f;
            ResetPilotSide();
            _execs = null;
            // a rider's copy flies nothing: the trail it would watch is the hull machine's
            _laidBy = _rider ? null : _status?.VesselPrismController;
            if (_laidBy) _laidBy.OnBlockSpawned += OnTrailPrismLaid;
            if (_rider) _humanEngineers = true;   // the rider plays the Ratel; the hull is not this machine's to hand over
            SpinAt = PortsAt = AbilityAt = -99f;
            for (int k = 0; k < RetiredAt.Length; k++) RetiredAt[k] = -99f;
            LastMade.At = -99f;
            MadeLine = "";
            MadeLineDynamo = false;
            // the view, the swap and the static handle belong to the local pilot's seat; an AI crew draws nothing
            if (_aiCrew) return;
            if (_active && _active != this) Destroy(_active);
            _active = this;
            _view = new HoneyguideDynamoView(this);
            var ab = _cfg.AbilityText(_core.Ability);   // the ability the Ratel starts with, for both seats
            if (ab != null) Toast(TemplateText.Fill(_cfg.AbilityToastTemplate, "name", ab.name, "text", ab.text));
        }

        void Unbind()
        {
            if (_vessel == null) return;
            if (_humanEngineers && !_rider) SetHumanEngineers(false);
            _humanEngineers = false;
            if (!_rider) EndPilotSide();
            if (_laidBy) _laidBy.OnBlockSpawned -= OnTrailPrismLaid;
            _laidBy = null;
            _view?.Dispose();
            _view = null;
            _vessel = null; _status = null; _player = null; _core = null;
            if (_active == this) _active = null;
        }

        void OnDisable() => Unbind();

        void OnDestroy() => Unbind();

        void Update()
        {
            if (!ShouldRun(out var vessel, out bool aiCrew, out bool rider)) { Unbind(); return; }
            if (_vessel != null && (aiCrew != _aiCrew || rider != _rider)) Unbind();   // the hull changed hands: seat it afresh
            if (_vessel == null) { _aiCrew = aiCrew; _rider = rider; Bind(vessel); }
            if (!UnityLiveness.Alive(_vessel.Transform)) return;

            if (!_aiCrew && !_rider)
            {
                PollSwap();
                ApplyStartSeat();
            }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_rider)
            {
                // the copy: the person's moves are applied here at once and sent on; the hull's machine answers with the
                // real board, which overwrites this one (OnCrewState). It advances so the board keeps turning between.
                if (_view != null) _view.ReadEngineerInput(_core, _s);
                _core.Advance(_s, dt);
                DrainEvents();
                _view?.Frame(dt);
                return;
            }

            // a person on another machine plays the Ratel: the AI stands down, their moves arrive by OnRiderInput
            _remoteRatel = CrewStations.HeldByRemoteHuman(this, 1);
            if (_remoteRatel && _humanEngineers) SetHumanEngineers(false);
            if (_humanEngineers && _view != null) _view.ReadEngineerInput(_core, _s);
            else if (!_remoteRatel) StepEngineerAI(dt);
            _core.Advance(_s, dt);
            DrainEvents();
            if (_remoteRatel && Time.time >= _stateSendAt) SendState();

            StepPilotSide(dt);
            _view?.Frame(dt);
        }

        // ------------------------------------------------------------------ seats

        // a double tap on the small board; the swap gesture (D-pad, the left / right arrow keys, a two-finger swipe) is the person's, and
        // CrewStations moves them here through TakeSeat, seats first, then hulls
        void PollSwap()
        {
            if (_view == null || !_view.SmallViewDoubleTapped() || Time.unscaledTime < _swapReadyAt) return;
            TakeSeat(_humanEngineers ? 0 : 1);
        }

        /// <inheritdoc cref="ICrewStations.SeatCount"/>: the Honeyguide (0) and the Ratel (1).
        public int SeatCount => 2;

        /// <inheritdoc cref="ICrewStations.LocalSeat"/>
        public int LocalSeat => _vessel != null && !_aiCrew && !_rider ? (_humanEngineers ? 1 : 0) : -1;

        /// <inheritdoc cref="ICrewStations.HostsRiders"/>: the Ratel seat (its board is relayed, <see cref="ICrewRelay"/>).
        public bool HostsRiders => true;

        /// <inheritdoc cref="ICrewStations.TakeSeat"/>
        public void TakeSeat(int seat)
        {
            bool engineer = seat == 1;
            if (LocalSeat < 0 || engineer == _humanEngineers) return;
            if (engineer && CrewStations.HeldByRemoteHuman(this, 1)) return;   // a person on another machine plays it
            _swapReadyAt = Time.unscaledTime + PilotSwap.CooldownSeconds;
            _startSeatPending = false;
            SetHumanEngineers(engineer);
        }

        // ------------------------------------------------------------------ the Ratel on another machine (ICrewRelay)

        /// <inheritdoc cref="ICrewRelay.OnRiderInput"/>: one board move, as <see cref="EngineerInput"/> sent it.
        public void OnRiderInput(int seat, int[] data)
        {
            if (seat != 1 || _core == null || _rider || data == null || data.Length < 6) return;
            if (!CrewStations.HeldByRemoteHuman(this, 1)) return;
            var inp = new DynamoInput
            {
                Kind = (DynamoInputKind)data[0], From = data[1], To = data[2], Cell = data[3], Colour = data[4], Slot = data[5],
            };
            if (_core.Apply(_s, inp)) DrainEvents();
            SendState();
        }

        /// <inheritdoc cref="ICrewRelay.OnCrewState"/>: the real board, over this machine's copy.
        public void OnCrewState(int[] data)
        {
            if (!_rider || _core == null) return;
            int ticks = _core.Ticks, portSet = _core.PortSet;
            var ability = _core.Ability;
            if (!_core.ReadState(data)) return;
            _core.Events.Clear();
            // the view's stamps follow what changed, as the hull machine's events would have moved them
            if (_core.Ticks != ticks) SpinAt = Time.time;
            if (_core.PortSet != portSet) PortsAt = Time.time;
            if (_core.Ability != ability) AbilityAt = Time.time;
        }

        // the hull's machine: the board, for the person playing it elsewhere (on every move, and a few times a second)
        void SendState()
        {
            _stateSendAt = Time.time + 0.2f;
            _core.WriteState(_stateScratch);
            CrewRelay.SendState(this, _stateScratch.ToArray());
        }

        // On the lowest preset the person starts on the Ratel (Garrett, 2026-10-22: "start the player in the wrasse and the
        // ratel ... so the ratel should run fine on very old phones"): its board is UI, and with no picture-in-picture the
        // world under it is not drawn at all (CrewSingleView). Taken once the hull is really the person's - flown by them,
        // not parked on its AI by a menu or the Vessel Changer, with live input - the Wrasse's rule (KiteNavigator.ApplySeatChoice).
        void ApplyStartSeat()
        {
            if (!_startSeatPending) return;
            if (_humanEngineers || !CrewSingleView.On) { _startSeatPending = false; return; }
            var input = _player?.InputStatus;
            if (_status == null || _status.AutoPilotEnabled || input == null || input.Paused || PauseSystem.Paused) return;
            _startSeatPending = false;
            SetHumanEngineers(true);
        }

        /// <inheritdoc cref="IHullParkAware.ReturnHumanToHull"/>
        public void ReturnHumanToHull()
        {
            if (!_humanEngineers) return;
            _weTookHull = false;   // the autopilot is the parker's now: SetHumanEngineers(false) must not stop it
            SetHumanEngineers(false);
        }

        void SetHumanEngineers(bool engineer)
        {
            if (_humanEngineers == engineer) return;
            _humanEngineers = engineer;
            _memo.Key = "";
            EndTriggers();   // a press held across the swap belongs to neither seat

            // The hull's own AI flies while the human plays the Ratel; its flight input is paused so the pointer and the
            // sticks belong to the board - the Wrasse's order. Coming back restores both, in the opposite order. Only an
            // autopilot this component started is stopped again.
            if (engineer)
            {
                SetFlightInputPaused(true);
                _weTookHull = !_status.AutoPilotEnabled;
                if (_weTookHull) _vessel.ToggleAIPilot(true);
            }
            else
            {
                if (_weTookHull && UnityLiveness.Alive(_vessel.Transform)) _vessel.ToggleAIPilot(false);
                _weTookHull = false;
                SetFlightInputPaused(false);
            }
            _view?.CancelDrag();
        }

        // the input is the PLAYER's, which outlives this hull (a swap re-homes it), so a destroyed one is skipped
        void SetFlightInputPaused(bool paused)
        {
            if (_player == null) return;
            if (UnityLiveness.Alive(_player.InputController)) _player.InputController.SetPause(paused);
            else if (_player.InputStatus != null) _player.InputStatus.Paused = paused;
        }

        // ------------------------------------------------------------------ the Ratel

        /// <summary>One Engineer input from the human's board (the view calls this). False when it was illegal. On a rider's
        /// copy it is applied here at once and sent to the hull's machine, whose answer is the board.</summary>
        internal bool EngineerInput(DynamoInput inp)
        {
            if (_core == null || !_humanEngineers || !_core.Apply(_s, inp)) return false;
            if (_rider)
            {
                _inputScratch[0] = (int)inp.Kind; _inputScratch[1] = inp.From; _inputScratch[2] = inp.To;
                _inputScratch[3] = inp.Cell; _inputScratch[4] = inp.Colour; _inputScratch[5] = inp.Slot;
                CrewRelay.SendInput(this, 1, (int[])_inputScratch.Clone());
            }
            return true;
        }

        void StepEngineerAI(float dt)
        {
            _think -= dt;
            if (_think > 0f) return;
            var skill = _cfg.EngineerSkill;
            _think = skill.think;
            var inp = _core.AIPlan(_s, new DynamoAISkill { Top = skill.top, Blunder = skill.blunder }, _aiDice, _memo);
            if (inp != null && inp.Value.Kind == DynamoInputKind.Activate) _memo.Key = "";
            if (inp != null) _core.Apply(_s, inp.Value);
        }

        // The core's events, in order. An effect can change the Dynamo (and so raise more events) while they are read, so
        // they are copied out and drained until none are left.
        void DrainEvents()
        {
            for (int guard = 0; guard < 8 && _core.Events.Count > 0; guard++)
            {
                _events.Clear();
                _events.AddRange(_core.Events);
                _core.Events.Clear();
                foreach (var e in _events) OnDynamoEvent(e);
            }
        }

        void OnDynamoEvent(DynamoEvent e)
        {
            switch (e.Kind)
            {
                case DynamoEventKind.Pick:   // the battery prism is taken: the pattern's effect happens now
                    if (!_aiCrew) RecordMade(e);
                    // a rider's copy fires nothing (the hull's machine does), but its line above the cards still names what
                    // the pattern does; the upgrade's level is the hull machine's to know
                    if (!_rider) ApplyEffect(e.Pid, e.Colour, 0);
                    else if (e.Pid >= 0 && e.Pid < _cfg.Patterns.Count && _cfg.Patterns[e.Pid] != null) SetMadeLine(_cfg.Patterns[e.Pid], "");
                    _lastMade = e.Pid;
                    Play(_cfg.PatternMadeSfx);
                    break;
                case DynamoEventKind.Tick:
                    SpinAt = Time.time;
                    Play(_cfg.SpinSfx);
                    break;
                case DynamoEventKind.Shuffle:
                    AbilityAt = Time.time;
                    Play(_cfg.RevealSfx);
                    var ab = _cfg.AbilityText(e.Ability);   // both seats read what the Ratel's new ability does
                    if (ab != null) Toast(TemplateText.Fill(_cfg.AbilityToastTemplate, "name", ab.name, "text", ab.text));
                    break;
                case DynamoEventKind.Ports:
                    PortsAt = Time.time;
                    break;
                case DynamoEventKind.Retire:
                    _lastRetired = e.Old;
                    if (e.Slot >= 0 && e.Slot < RetiredAt.Length) RetiredAt[e.Slot] = Time.time;
                    break;
            }
        }

        // Before the effect runs (it may burn, scatter or drain): the pattern's cells, found again with the taken prism put
        // back for the look, and the battery slot the charge went to - the last one, when the battery's newest is its colour.
        void RecordMade(DynamoEvent e)
        {
            var m = LastMade;
            m.At = Time.time;
            m.Slot = e.Slot;
            m.Pid = e.Pid;
            m.Cell = e.Cell;
            m.Colour = e.Colour;
            m.Cells.Clear();
            if (e.Cell >= 0 && e.Cell < DynamoCore.CellCount && _core.Cells[e.Cell] == 0 && e.Pid >= 0 && e.Pid < _s.Patterns.Count)
            {
                _core.Cells[e.Cell] = e.Colour;
                var cells = _core.Match(_s, e.Pid);
                _core.Cells[e.Cell] = 0;
                if (cells != null) m.Cells.AddRange(cells);
            }
            if (!m.Cells.Contains(e.Cell)) m.Cells.Add(e.Cell);
            var bat = _core.Battery;
            m.BatterySlot = bat.Count > 0 && bat[bat.Count - 1] == e.Colour ? bat.Count - 1 : -1;
        }

        // The line above the cards: what the pattern just made does, lime when it works on the Dynamo.
        void SetMadeLine(HoneyguidePattern pat, string level)
        {
            MadeLine = TemplateText.Fill(_cfg.MadeLineTemplate, "name", pat.name, "level", level, "text", pat.effectText);
            MadeLineDynamo = HoneyguideConfigSO.TouchesDynamo(pat.effect);
        }

        void Play(EventReference evt)
        {
            if (evt.IsNull || _aiCrew || _vessel == null) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(evt, _vessel.Transform.position);
        }
    }
}
